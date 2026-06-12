using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.UI;

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
    [SerializeField] private string childId = "child_001";
    [SerializeField] private string wechatCloudEnvId = "cloud1-d9gz2tmfub107d0ff";
    [SerializeField] private string targetCollectionName = "main_game_logs";
    [SerializeField] private string uploadEndpoint = "https://cloud1-d9gz2tmfub107d0ff-1430429849.ap-shanghai.app.tcloudbase.com/submitGameLog";
    [SerializeField] private string uploadBearerToken = string.Empty;
    [SerializeField] private float requestTimeoutSeconds = 15f;
    [SerializeField] private bool prettyPrintPayload = true;
    [SerializeField] private bool logPayloadWhenUploadSkipped = true;

    [Header("Debug Testing")]
    [SerializeField] private bool enableQuickUploadTestShortcut = true;
    [SerializeField] private KeyCode quickUploadTestShortcut = KeyCode.F8;
    [SerializeField] private bool quickUploadTestRequiresShift = true;
    [SerializeField] private bool preferLiveSessionSnapshotForQuickUpload = true;
    [SerializeField] private bool enableQuickUploadDebugPanel = true;

    public static MiniProgramGameDataManager Instance { get; private set; }

    public bool HasActiveSession => sessionActive;
    public string LastUploadStatus => lastUploadStatus;
    public UploadEnvelope LastCompletedPayload => lastCompletedPayload;
    public string LastPayloadJson => lastPayloadJson;

    private readonly List<int> focusSamples = new List<int>();
    private readonly List<float> distractDurations = new List<float>();
    private readonly HashSet<string> completedMilestones = new HashSet<string>();
    private readonly Dictionary<string, int> successfulActionsById = new Dictionary<string, int>();

    private HybridBciPlatformBridge bridge;
    private HybridBciGameplayInput gameplayInput;
    private Coroutine uploadCoroutine;
    private Canvas debugCanvas;
    private Text debugStatusText;
    private Text debugHintText;
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
    private UploadEnvelope lastCompletedPayload;

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
        HandleQuickUploadTestShortcut();
        RefreshDebugUi();
    }

    public void ConfigureSessionDefaults(string gameModule, string recipeName, string configuredChildId = null)
    {
        if (!string.IsNullOrWhiteSpace(gameModule))
        {
            defaultGameModule = gameModule.Trim();
        }

        if (!string.IsNullOrWhiteSpace(recipeName))
        {
            defaultRecipeName = recipeName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(configuredChildId))
        {
            childId = configuredChildId.Trim();
        }
    }

    public void ConfigureUploadTarget(
        string envId,
        string collectionName,
        string endpoint,
        string bearerToken = null,
        bool? enableAutoUpload = null)
    {
        if (!string.IsNullOrWhiteSpace(envId))
        {
            wechatCloudEnvId = envId.Trim();
        }

        if (!string.IsNullOrWhiteSpace(collectionName))
        {
            targetCollectionName = collectionName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            uploadEndpoint = endpoint.Trim();
        }

        if (bearerToken != null)
        {
            uploadBearerToken = bearerToken.Trim();
        }

        if (enableAutoUpload.HasValue)
        {
            autoUploadOnSessionComplete = enableAutoUpload.Value;
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

        lastCompletedPayload = BuildUploadEnvelope();
        lastPayloadJson = JsonUtility.ToJson(lastCompletedPayload, prettyPrintPayload);
        Debug.Log(
            $"MiniProgram session complete. focusSamples={focusSamples.Count}, avgAttention={lastCompletedPayload.payload.avgAttention}, score={lastCompletedPayload.payload.score}\n{lastPayloadJson}",
            this);

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

    [ContextMenu("Mini Program/Upload Quick Test Payload")]
    public void UploadQuickTestPayload()
    {
        if (!Application.isPlaying)
        {
            lastUploadStatus = "Quick upload skipped: enter Play mode first";
            Debug.LogWarning("MiniProgram quick upload skipped because the game is not running.", this);
            return;
        }

        var payload = BuildQuickUploadEnvelope(out var payloadSource);
        if (payload == null)
        {
            lastUploadStatus = "Quick upload skipped: payload unavailable";
            return;
        }

        lastCompletedPayload = payload;
        lastPayloadJson = JsonUtility.ToJson(payload, prettyPrintPayload);

        if (string.IsNullOrWhiteSpace(uploadEndpoint))
        {
            lastUploadStatus = "Upload skipped: endpoint not configured";
            Debug.LogWarning(
                $"MiniProgram quick upload skipped.\nReason: endpoint not configured\nPayload source: {payloadSource}\nPayload:\n{lastPayloadJson}",
                this);
            return;
        }

        Debug.Log(
            $"MiniProgram quick upload triggered.\nPayload source: {payloadSource}\nEndpoint: {uploadEndpoint}\nPayload:\n{lastPayloadJson}",
            this);
        UploadPayload(payload);
    }

    [ContextMenu("Mini Program/Retry Last Upload")]
    public void RetryLastUploadFromContextMenu()
    {
        RetryLastUpload();
    }

    [ContextMenu("Mini Program/Log Last Payload Json")]
    public void LogLastPayloadJson()
    {
        if (string.IsNullOrWhiteSpace(lastPayloadJson))
        {
            Debug.Log("MiniProgram payload log requested, but there is no payload yet.", this);
            return;
        }

        Debug.Log($"MiniProgram last payload json:\n{lastPayloadJson}", this);
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

    private void ResolveGameplayInput()
    {
        if (gameplayInput != null)
        {
            return;
        }

        gameplayInput = HybridBciGameplayInput.Instance;
        if (gameplayInput == null)
        {
            gameplayInput = FindObjectOfType<HybridBciGameplayInput>();
        }
    }

    private void HandleQuickUploadTestShortcut()
    {
        if (!enableQuickUploadTestShortcut || !Input.GetKeyDown(quickUploadTestShortcut))
        {
            return;
        }

        if (quickUploadTestRequiresShift &&
            !Input.GetKey(KeyCode.LeftShift) &&
            !Input.GetKey(KeyCode.RightShift))
        {
            return;
        }

        UploadQuickTestPayload();
    }

    private void RefreshDebugUi()
    {
        if (!enableQuickUploadDebugPanel)
        {
            if (debugCanvas != null)
            {
                debugCanvas.gameObject.SetActive(false);
            }

            return;
        }

        EnsureDebugUi();
        if (debugCanvas == null)
        {
            return;
        }

        if (!debugCanvas.gameObject.activeSelf)
        {
            debugCanvas.gameObject.SetActive(true);
        }

        if (debugStatusText != null)
        {
            debugStatusText.text =
                $"Status: {lastUploadStatus}\n" +
                $"ChildId: {childId}\n" +
                $"Collection: {targetCollectionName}";
            debugStatusText.color = ResolveStatusColor();
        }

        if (debugHintText != null)
        {
            debugHintText.text =
                $"Shortcut: {FormatQuickUploadShortcut()}\n" +
                "Press Esc to unlock mouse";
        }
    }

    private void EnsureDebugUi()
    {
        if (debugCanvas != null)
        {
            EnsureEventSystem();
            return;
        }

        EnsureEventSystem();

        var canvasObject = new GameObject("MiniProgram Upload Debug UI");
        canvasObject.transform.SetParent(transform, false);

        debugCanvas = canvasObject.AddComponent<Canvas>();
        debugCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        debugCanvas.sortingOrder = 5000;

        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;

        canvasObject.AddComponent<GraphicRaycaster>();

        var panel = CreateUiObject("Panel", canvasObject.transform);
        var panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 1f);
        panelRect.anchorMax = new Vector2(1f, 1f);
        panelRect.pivot = new Vector2(1f, 1f);
        panelRect.anchoredPosition = new Vector2(-20f, -20f);
        panelRect.sizeDelta = new Vector2(360f, 180f);

        var panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0.08f, 0.12f, 0.16f, 0.88f);

        CreateLabel(
            panel.transform,
            "Title",
            "Mini Program Upload",
            new Vector2(18f, -14f),
            new Vector2(240f, 30f),
            22,
            FontStyle.Bold,
            Color.white);

        debugStatusText = CreateLabel(
            panel.transform,
            "Status",
            string.Empty,
            new Vector2(18f, -48f),
            new Vector2(324f, 66f),
            16,
            FontStyle.Normal,
            Color.white);

        CreateButton(
            panel.transform,
            "UploadButton",
            "Quick Upload",
            new Vector2(18f, -118f),
            new Vector2(150f, 38f),
            new Color(0.2f, 0.62f, 0.35f, 0.96f),
            UploadQuickTestPayload);

        CreateButton(
            panel.transform,
            "RetryButton",
            "Retry Last",
            new Vector2(186f, -118f),
            new Vector2(150f, 38f),
            new Color(0.21f, 0.42f, 0.72f, 0.96f),
            RetryLastUpload);

        debugHintText = CreateLabel(
            panel.transform,
            "Hint",
            string.Empty,
            new Vector2(18f, -160f),
            new Vector2(324f, 30f),
            14,
            FontStyle.Normal,
            new Color(0.78f, 0.87f, 0.97f, 1f));
    }

    private void EnsureEventSystem()
    {
        var eventSystem = FindObjectOfType<EventSystem>();
        if (eventSystem != null)
        {
            return;
        }

        var eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<StandaloneInputModule>();
    }

    private GameObject CreateUiObject(string name, Transform parent)
    {
        var target = new GameObject(name);
        target.transform.SetParent(parent, false);
        return target;
    }

    private Text CreateLabel(
        Transform parent,
        string name,
        string value,
        Vector2 anchoredPosition,
        Vector2 size,
        int fontSize,
        FontStyle fontStyle,
        Color color)
    {
        var textObject = CreateUiObject(name, parent);
        var rect = textObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        var text = textObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = TextAnchor.UpperLeft;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.color = color;
        text.text = value;

        return text;
    }

    private void CreateButton(
        Transform parent,
        string name,
        string label,
        Vector2 anchoredPosition,
        Vector2 size,
        Color backgroundColor,
        UnityEngine.Events.UnityAction onClick)
    {
        var buttonObject = CreateUiObject(name, parent);
        var rect = buttonObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        var image = buttonObject.AddComponent<Image>();
        image.color = backgroundColor;

        var button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.normalColor = backgroundColor;
        colors.highlightedColor = backgroundColor * 1.08f;
        colors.pressedColor = backgroundColor * 0.92f;
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.75f);
        button.colors = colors;
        button.onClick.AddListener(onClick);

        var labelText = CreateLabel(
            buttonObject.transform,
            "Label",
            label,
            new Vector2(size.x * 0.5f, -8f),
            new Vector2(size.x - 12f, size.y - 8f),
            17,
            FontStyle.Bold,
            Color.white);
        labelText.alignment = TextAnchor.UpperCenter;

        var labelRect = labelText.rectTransform;
        labelRect.anchorMin = new Vector2(0.5f, 1f);
        labelRect.anchorMax = new Vector2(0.5f, 1f);
        labelRect.pivot = new Vector2(0.5f, 1f);
    }

    private Color ResolveStatusColor()
    {
        if (string.IsNullOrWhiteSpace(lastUploadStatus))
        {
            return Color.white;
        }

        if (lastUploadStatus.IndexOf("succeeded", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return new Color(0.55f, 0.96f, 0.62f, 1f);
        }

        if (lastUploadStatus.IndexOf("failed", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return new Color(1f, 0.58f, 0.58f, 1f);
        }

        if (lastUploadStatus.IndexOf("uploading", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return new Color(1f, 0.85f, 0.42f, 1f);
        }

        if (lastUploadStatus.IndexOf("skipped", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return new Color(0.93f, 0.79f, 0.44f, 1f);
        }

        return Color.white;
    }

    private string FormatQuickUploadShortcut()
    {
        return quickUploadTestRequiresShift
            ? $"Shift + {quickUploadTestShortcut}"
            : quickUploadTestShortcut.ToString();
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
        ResolveGameplayInput();

        var focus = -1;
        if (gameplayInput != null && gameplayInput.HasLiveConnection)
        {
            focus = Mathf.RoundToInt(gameplayInput.SmoothedAttention);
        }
        else if (bridge != null && bridge.IsConnected)
        {
            focus = bridge.AttentionValue;
        }

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

    private UploadEnvelope BuildUploadEnvelope()
    {
        return new UploadEnvelope
        {
            envId = wechatCloudEnvId,
            collectionName = targetCollectionName,
            payload = BuildGameLogPayload()
        };
    }

    private UploadEnvelope BuildQuickUploadEnvelope(out string payloadSource)
    {
        if (preferLiveSessionSnapshotForQuickUpload && HasRecordedSessionData())
        {
            payloadSource = sessionActive
                ? "current session snapshot"
                : "last recorded session snapshot";
            return BuildUploadEnvelope();
        }

        payloadSource = "representative test payload";
        return BuildRepresentativeTestEnvelope();
    }

    private bool HasRecordedSessionData()
    {
        return focusSamples.Count > 0 ||
               distractDurations.Count > 0 ||
               completedMilestones.Count > 0 ||
               totalTrackedActionAttempts > 0 ||
               successfulActionCount > 0 ||
               invalidActionCount > 0 ||
               harvestedMushroomCount > 0 ||
               fishCaughtCount > 0 ||
               fishEscapedCount > 0;
    }

    private UploadEnvelope BuildRepresentativeTestEnvelope()
    {
        var payload = new GameLogPayload
        {
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            childId = childId,
            durationMinutes = 3.5f,
            game_module = ResolveGameModuleName(),
            recipeName = ResolveRecipeName(),
            score = 2180,
            avgAttention = 76,
            distractCount = 1,
            eegMetrics = new EegMetrics
            {
                meanFocus = 76,
                peakFocus = 92,
                valFocus = 48,
                durationRatio60 = 84,
                durationRatio80 = 46,
                avgDistractDuration = 5.4f,
                focusCv = 0.182f
            },
            dimensionMetrics = new DimensionMetrics
            {
                sustained = 82,
                selective = 85,
                executive = 83,
                impulse = 81
            }
        };

        return new UploadEnvelope
        {
            envId = wechatCloudEnvId,
            collectionName = targetCollectionName,
            payload = payload
        };
    }

    private GameLogPayload BuildGameLogPayload()
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
            childId = childId,
            durationMinutes = (float)Math.Round(Math.Max(0f, Time.unscaledTime - sessionStartTime) / 60f, 2),
            game_module = ResolveGameModuleName(),
            recipeName = ResolveRecipeName(),
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

    private string ResolveGameModuleName()
    {
        return string.IsNullOrWhiteSpace(activeGameModule) ? defaultGameModule : activeGameModule;
    }

    private string ResolveRecipeName()
    {
        return string.IsNullOrWhiteSpace(activeRecipeName) ? defaultRecipeName : activeRecipeName;
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

    private void UploadPayload(UploadEnvelope payload)
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

    private IEnumerator UploadRoutine(UploadEnvelope payload)
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
                Debug.LogWarning(
                    $"MiniProgram upload failed.\nEndpoint: {uploadEndpoint}\nStatus: {lastUploadStatus}\nBody:\n{lastPayloadJson}\nResponse:\n{request.downloadHandler.text}",
                    this);
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
        public string childId;
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
    public class UploadEnvelope
    {
        public string envId;
        public string collectionName;
        public GameLogPayload payload;
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
