using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-90)]
public class MiniProgramGameDataManager : MonoBehaviour
{
    [Header("Session Defaults")]
    [SerializeField] private string defaultGameModule = "room";
    [SerializeField] private string defaultRecipeName = "失重备菜室";
    [SerializeField] private int expectedMilestoneCount = 8;

    [Header("Attention Sampling")]
    [SerializeField] private float sampleIntervalSeconds = 1f;
    [SerializeField] private int distractAttentionThreshold = 30;
    [SerializeField] private float distractDurationSeconds = 5f;

    [Header("Upload")]
    [SerializeField] private bool autoUploadOnSessionComplete = true;
    [SerializeField] private string uploadEndpoint = string.Empty;
    [SerializeField] private string uploadBearerToken = string.Empty;
    [SerializeField] private float requestTimeoutSeconds = 15f;
    [SerializeField] private bool prettyPrintPayload = true;
    [SerializeField] private bool logPayloadWhenUploadSkipped = true;

    public static MiniProgramGameDataManager Instance { get; private set; }

    public bool HasActiveSession => sessionActive;
    public string LastUploadStatus => lastUploadStatus;
    public GameLogPayload LastCompletedPayload => lastCompletedPayload;
    public string LastPayloadJson => lastPayloadJson;

    private readonly List<int> focusSamples = new List<int>();
    private readonly List<float> distractDurations = new List<float>();
    private readonly HashSet<string> completedMilestones = new HashSet<string>();
    private readonly Dictionary<string, int> successfulActionsById = new Dictionary<string, int>();

    private HybridBciPlatformBridge bridge;
    private Coroutine uploadCoroutine;
    private float sessionStartTime;
    private float sampleTimer;
    private float lowAttentionDuration;
    private string activeGameModule = string.Empty;
    private string activeRecipeName = string.Empty;
    private string lastPayloadJson = string.Empty;
    private string lastUploadStatus = "Idle";
    private bool sessionActive;
    private bool distractEventCounted;
    private bool completedSuccessfully;
    private int totalTrackedActionAttempts;
    private int successfulActionCount;
    private int invalidActionCount;
    private int harvestedMushroomCount;
    private int fishCaughtCount;
    private int fishEscapedCount;
    private GameLogPayload lastCompletedPayload;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        ResolveBridge();
        SampleAttentionIfNeeded();
    }

    public void ConfigureSessionDefaults(string gameModule, string recipeName)
    {
        if (!string.IsNullOrWhiteSpace(gameModule))
        {
            defaultGameModule = gameModule.Trim();
        }

        if (!string.IsNullOrWhiteSpace(recipeName))
        {
            defaultRecipeName = recipeName.Trim();
        }
    }

    public void BeginSession(string gameModule = null, string recipeName = null)
    {
        ResetSessionState();
        sessionActive = true;
        sessionStartTime = Time.unscaledTime;
        activeGameModule = string.IsNullOrWhiteSpace(gameModule) ? defaultGameModule : gameModule.Trim();
        activeRecipeName = string.IsNullOrWhiteSpace(recipeName) ? defaultRecipeName : recipeName.Trim();
        lastUploadStatus = "Session running";
    }

    public void CancelSession()
    {
        ResetSessionState();
        lastUploadStatus = "Session cancelled";
    }

    public void RecordHarvestedMushroom()
    {
        if (!sessionActive)
        {
            return;
        }

        harvestedMushroomCount++;
        RecordMilestone("harvest_mushroom");
    }

    public void RecordActionOutcome(string actionId, bool success)
    {
        if (!sessionActive || string.IsNullOrWhiteSpace(actionId))
        {
            return;
        }

        totalTrackedActionAttempts++;
        if (success)
        {
            successfulActionCount++;
            if (!successfulActionsById.ContainsKey(actionId))
            {
                successfulActionsById[actionId] = 0;
            }

            successfulActionsById[actionId]++;
            return;
        }

        invalidActionCount++;
    }

    public void RecordFishCaught()
    {
        if (!sessionActive)
        {
            return;
        }

        fishCaughtCount++;
        RecordMilestone("fish_caught");
    }

    public void RecordFishEscaped()
    {
        if (!sessionActive)
        {
            return;
        }

        fishEscapedCount++;
    }

    public void RecordMilestone(string milestoneId)
    {
        if (!sessionActive || string.IsNullOrWhiteSpace(milestoneId))
        {
            return;
        }

        completedMilestones.Add(milestoneId);
    }

    public void CompleteSession(bool wasSuccessful)
    {
        if (!sessionActive)
        {
            return;
        }

        completedSuccessfully = wasSuccessful;
        FinalizeDistractEventIfNeeded();
        sessionActive = false;

        lastCompletedPayload = BuildPayload();
        lastPayloadJson = JsonUtility.ToJson(lastCompletedPayload, prettyPrintPayload);

        if (!autoUploadOnSessionComplete || string.IsNullOrWhiteSpace(uploadEndpoint))
        {
            lastUploadStatus = string.IsNullOrWhiteSpace(uploadEndpoint)
                ? "Upload skipped: endpoint not configured"
                : "Upload skipped: auto upload disabled";

            if (logPayloadWhenUploadSkipped)
            {
                Debug.Log($"MiniProgram game log ready:\n{lastPayloadJson}", this);
            }

            return;
        }

        UploadPayload(lastCompletedPayload);
    }

    public void RetryLastUpload()
    {
        if (lastCompletedPayload == null)
        {
            lastUploadStatus = "Retry skipped: no payload";
            return;
        }

        if (string.IsNullOrWhiteSpace(uploadEndpoint))
        {
            lastUploadStatus = "Retry skipped: endpoint not configured";
            return;
        }

        UploadPayload(lastCompletedPayload);
    }

    private void ResolveBridge()
    {
        if (bridge != null)
        {
            return;
        }

        bridge = HybridBciPlatformBridge.Instance;
        if (bridge == null)
        {
            bridge = FindObjectOfType<HybridBciPlatformBridge>();
        }
    }

    private void SampleAttentionIfNeeded()
    {
        if (!sessionActive)
        {
            return;
        }

        sampleTimer += Time.unscaledDeltaTime;
        var effectiveInterval = Mathf.Max(0.2f, sampleIntervalSeconds);
        while (sampleTimer >= effectiveInterval)
        {
            sampleTimer -= effectiveInterval;
            SampleAttention();
        }
    }

    private void SampleAttention()
    {
        var focus = bridge != null && bridge.IsConnected ? bridge.AttentionValue : -1;
        if (focus < 0)
        {
            return;
        }

        focus = Mathf.Clamp(focus, 0, 100);
        focusSamples.Add(focus);

        if (focus < distractAttentionThreshold)
        {
            lowAttentionDuration += Mathf.Max(0.2f, sampleIntervalSeconds);
            if (!distractEventCounted && lowAttentionDuration >= distractDurationSeconds)
            {
                distractEventCounted = true;
            }

            return;
        }

        FinalizeDistractEventIfNeeded();
    }

    private void FinalizeDistractEventIfNeeded()
    {
        if (distractEventCounted && lowAttentionDuration >= distractDurationSeconds)
        {
            distractDurations.Add(lowAttentionDuration);
        }

        lowAttentionDuration = 0f;
        distractEventCounted = false;
    }

    private GameLogPayload BuildPayload()
    {
        var meanFocus = ComputeAverageFocus();
        var peakFocus = ComputePeakFocus();
        var minFocus = ComputeMinFocus();
        var durationRatio60 = ComputeDurationRatio(60);
        var durationRatio80 = ComputeDurationRatio(80);
        var distractCount = distractDurations.Count;
        var avgDistractDuration = ComputeAverageDistractDuration();
        var focusCv = ComputeFocusCv(meanFocus);
        var actionAccuracy = ComputeActionAccuracy();
        var milestoneCompletion = ComputeMilestoneCompletion();

        var payload = new GameLogPayload
        {
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            durationMinutes = (float)Math.Round(Math.Max(0f, Time.unscaledTime - sessionStartTime) / 60f, 2),
            game_module = string.IsNullOrWhiteSpace(activeGameModule) ? defaultGameModule : activeGameModule,
            recipeName = string.IsNullOrWhiteSpace(activeRecipeName) ? defaultRecipeName : activeRecipeName,
            score = ComputeScore(meanFocus, distractCount),
            avgAttention = meanFocus,
            distractCount = distractCount,
            eegMetrics = new EegMetrics
            {
                meanFocus = meanFocus,
                peakFocus = peakFocus,
                valFocus = minFocus,
                durationRatio60 = durationRatio60,
                durationRatio80 = durationRatio80,
                avgDistractDuration = (float)Math.Round(avgDistractDuration, 2),
                focusCv = (float)Math.Round(focusCv, 3)
            },
            dimensionMetrics = new DimensionMetrics()
        };

        payload.dimensionMetrics.sustained = ComputeSustainedDimension(
            payload.avgAttention,
            payload.eegMetrics.durationRatio60,
            payload.eegMetrics.durationRatio80,
            distractCount);
        payload.dimensionMetrics.selective = ComputeSelectiveDimension(
            actionAccuracy,
            payload.eegMetrics.durationRatio80,
            milestoneCompletion,
            distractCount);
        payload.dimensionMetrics.executive = ComputeExecutiveDimension(
            milestoneCompletion,
            actionAccuracy,
            payload.avgAttention,
            payload.eegMetrics.durationRatio60);
        payload.dimensionMetrics.impulse = ComputeImpulseDimension(
            actionAccuracy,
            distractCount,
            avgDistractDuration);

        return payload;
    }

    private int ComputeAverageFocus()
    {
        if (focusSamples.Count == 0)
        {
            return 0;
        }

        var total = 0f;
        for (var i = 0; i < focusSamples.Count; i++)
        {
            total += focusSamples[i];
        }

        return Mathf.RoundToInt(total / focusSamples.Count);
    }

    private int ComputePeakFocus()
    {
        if (focusSamples.Count == 0)
        {
            return 0;
        }

        var peak = 0;
        for (var i = 0; i < focusSamples.Count; i++)
        {
            if (focusSamples[i] > peak)
            {
                peak = focusSamples[i];
            }
        }

        return peak;
    }

    private int ComputeMinFocus()
    {
        if (focusSamples.Count == 0)
        {
            return 0;
        }

        var min = 100;
        for (var i = 0; i < focusSamples.Count; i++)
        {
            if (focusSamples[i] < min)
            {
                min = focusSamples[i];
            }
        }

        return min;
    }

    private int ComputeDurationRatio(int threshold)
    {
        if (focusSamples.Count == 0)
        {
            return 0;
        }

        var count = 0;
        for (var i = 0; i < focusSamples.Count; i++)
        {
            if (focusSamples[i] >= threshold)
            {
                count++;
            }
        }

        return Mathf.RoundToInt(count * 100f / focusSamples.Count);
    }

    private float ComputeAverageDistractDuration()
    {
        if (distractDurations.Count == 0)
        {
            return 0f;
        }

        var total = 0f;
        for (var i = 0; i < distractDurations.Count; i++)
        {
            total += distractDurations[i];
        }

        return total / distractDurations.Count;
    }

    private float ComputeFocusCv(int meanFocus)
    {
        if (focusSamples.Count <= 1 || meanFocus <= 0)
        {
            return 0f;
        }

        var mean = meanFocus;
        var variance = 0f;
        for (var i = 0; i < focusSamples.Count; i++)
        {
            var delta = focusSamples[i] - mean;
            variance += delta * delta;
        }

        variance /= focusSamples.Count;
        var standardDeviation = Mathf.Sqrt(variance);
        return standardDeviation / mean;
    }

    private float ComputeActionAccuracy()
    {
        if (totalTrackedActionAttempts <= 0)
        {
            return completedSuccessfully ? 1f : 0f;
        }

        return Mathf.Clamp01(successfulActionCount / (float)totalTrackedActionAttempts);
    }

    private float ComputeMilestoneCompletion()
    {
        var safeMilestoneCount = Mathf.Max(1, expectedMilestoneCount);
        return Mathf.Clamp01(completedMilestones.Count / (float)safeMilestoneCount);
    }

    private int ComputeScore(int meanFocus, int distractCount)
    {
        var score = 0f;
        score += completedSuccessfully ? 1500f : 650f * ComputeMilestoneCompletion();
        score += completedMilestones.Contains("soup_complete") ? 280f : 0f;
        score += completedMilestones.Contains("fish_caught") ? 220f : 0f;
        score += completedMilestones.Contains("dish_complete") ? 420f : 0f;
        score += harvestedMushroomCount * 80f;
        score += successfulActionCount * 90f;
        score += meanFocus * 4f;
        score -= distractCount * 80f;
        score -= fishEscapedCount * 45f;
        score -= invalidActionCount * 25f;
        return Mathf.Max(0, Mathf.RoundToInt(score));
    }

    private int ComputeSustainedDimension(int avgAttention, int durationRatio60, int durationRatio80, int distractCount)
    {
        var distractScore = Mathf.Clamp01(1f - distractCount / 6f) * 100f;
        return ClampMetric(
            avgAttention * 0.35f +
            durationRatio60 * 0.35f +
            durationRatio80 * 0.15f +
            distractScore * 0.15f);
    }

    private int ComputeSelectiveDimension(float actionAccuracy, int durationRatio80, float milestoneCompletion, int distractCount)
    {
        var distractScore = Mathf.Clamp01(1f - distractCount / 6f) * 100f;
        return ClampMetric(
            actionAccuracy * 100f * 0.5f +
            durationRatio80 * 0.2f +
            milestoneCompletion * 100f * 0.15f +
            distractScore * 0.15f);
    }

    private int ComputeExecutiveDimension(float milestoneCompletion, float actionAccuracy, int avgAttention, int durationRatio60)
    {
        return ClampMetric(
            milestoneCompletion * 100f * 0.45f +
            actionAccuracy * 100f * 0.25f +
            avgAttention * 0.15f +
            durationRatio60 * 0.15f);
    }

    private int ComputeImpulseDimension(float actionAccuracy, int distractCount, float avgDistractDuration)
    {
        var distractScore = Mathf.Clamp01(1f - distractCount / 8f) * 100f;
        var distractDurationScore = Mathf.Clamp01(1f - avgDistractDuration / 12f) * 100f;
        return ClampMetric(
            actionAccuracy * 100f * 0.55f +
            distractScore * 0.25f +
            distractDurationScore * 0.2f);
    }

    private int ClampMetric(float value)
    {
        return Mathf.Clamp(Mathf.RoundToInt(value), 0, 100);
    }

    private void UploadPayload(GameLogPayload payload)
    {
        if (payload == null || string.IsNullOrWhiteSpace(uploadEndpoint))
        {
            return;
        }

        if (uploadCoroutine != null)
        {
            StopCoroutine(uploadCoroutine);
        }

        uploadCoroutine = StartCoroutine(UploadRoutine(payload));
    }

    private IEnumerator UploadRoutine(GameLogPayload payload)
    {
        lastUploadStatus = "Uploading";
        lastPayloadJson = JsonUtility.ToJson(payload, prettyPrintPayload);

        using (var request = new UnityWebRequest(uploadEndpoint, UnityWebRequest.kHttpVerbPOST))
        {
            var bodyRaw = System.Text.Encoding.UTF8.GetBytes(lastPayloadJson);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = Mathf.CeilToInt(Mathf.Max(1f, requestTimeoutSeconds));
            request.SetRequestHeader("Content-Type", "application/json");

            if (!string.IsNullOrWhiteSpace(uploadBearerToken))
            {
                request.SetRequestHeader("Authorization", $"Bearer {uploadBearerToken.Trim()}");
            }

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                lastUploadStatus = $"Upload succeeded ({request.responseCode})";
                Debug.Log($"MiniProgram game log uploaded: {request.downloadHandler.text}", this);
            }
            else
            {
                lastUploadStatus = $"Upload failed ({request.responseCode}): {request.error}";
                Debug.LogWarning($"MiniProgram upload failed. Body:\n{lastPayloadJson}\nResponse:\n{request.downloadHandler.text}", this);
            }
        }

        uploadCoroutine = null;
    }

    private void ResetSessionState()
    {
        sessionActive = false;
        sampleTimer = 0f;
        lowAttentionDuration = 0f;
        distractEventCounted = false;
        completedSuccessfully = false;
        totalTrackedActionAttempts = 0;
        successfulActionCount = 0;
        invalidActionCount = 0;
        harvestedMushroomCount = 0;
        fishCaughtCount = 0;
        fishEscapedCount = 0;
        activeGameModule = string.Empty;
        activeRecipeName = string.Empty;
        focusSamples.Clear();
        distractDurations.Clear();
        completedMilestones.Clear();
        successfulActionsById.Clear();
    }

    [Serializable]
    public class GameLogPayload
    {
        public long timestamp;
        public float durationMinutes;
        public string game_module;
        public string recipeName;
        public int score;
        public int avgAttention;
        public int distractCount;
        public EegMetrics eegMetrics;
        public DimensionMetrics dimensionMetrics;
    }

    [Serializable]
    public class EegMetrics
    {
        public int meanFocus;
        public int peakFocus;
        public int valFocus;
        public int durationRatio60;
        public int durationRatio80;
        public float avgDistractDuration;
        public float focusCv;
    }

    [Serializable]
    public class DimensionMetrics
    {
        public int sustained;
        public int selective;
        public int executive;
        public int impulse;
    }
}
