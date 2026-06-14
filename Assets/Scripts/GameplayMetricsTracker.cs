// ============================================================
// GameplayMetricsTracker.cs
// 由 MushroomSoupSceneBootstrap 自动创建，无需手动挂载
// 自动追踪游戏全过程的专注力指标，在烹饪完成时自动提交到云端
//
// 依赖：GameLogSender.cs, HybridBciGameplayInput.cs
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameplayMetricsTracker : MonoBehaviour
{
    [Header("引用")]
    public GameLogSender logSender;
    public MushroomSoupGame soupGame;

    [Header("走神检测")]
    [Range(0, 100)] public int distractThreshold = 40;
    public float distractCooldown = 3f;

    [Header("调试")]
    public bool showDebugGui = true;

    // ── 运行时指标 ──
    private float sessionStartTime;
    private bool isTracking;
    private bool gameCompleted;
    private List<float> attentionSamples = new List<float>();
    private float lastAttentionValue;
    private float lastDistractTime = -999f;
    private int totalDistractCount;
    private float peakAttention;

    // ── EEG 相关 ──
    private float eegPeak;
    private float eegValley = float.MaxValue;
    private float eegBelow60Time;
    private float eegBelow80Time;
    private float totalSampleTime;

    // ── 游戏状态 ──
    private string currentModule;
    private string currentRecipe;

    private HybridBciGameplayInput gameplayInput;

    // ==================== 生命周期 ====================

    private void Awake()
    {
        if (logSender == null)
            logSender = FindObjectOfType<GameLogSender>();
        if (soupGame == null)
            soupGame = FindObjectOfType<MushroomSoupGame>();

        if (logSender == null)
        {
            var go = new GameObject("GameLogSender");
            logSender = go.AddComponent<GameLogSender>();
            DontDestroyOnLoad(go);
        }
    }

    private void Start()
    {
        // 延迟查找：Awake 时 soupGame 可能尚未初始化
        if (soupGame == null)
            soupGame = FindObjectOfType<MushroomSoupGame>();

        gameplayInput = HybridBciGameplayInput.Instance;
        if (gameplayInput == null)
            gameplayInput = FindObjectOfType<HybridBciGameplayInput>();

        DetectModule();
        BeginSession();
    }

    private void Update()
    {
        if (!isTracking || gameCompleted) return;

        float attention = GetCurrentAttention();
        attentionSamples.Add(attention);
        lastAttentionValue = attention;

        if (attention > peakAttention) peakAttention = attention;

        if (attention < distractThreshold && Time.time - lastDistractTime > distractCooldown)
        {
            totalDistractCount++;
            lastDistractTime = Time.time;
        }

        // EEG 采样
        totalSampleTime += Time.deltaTime;
        if (attention > eegPeak) eegPeak = attention;
        if (attention < eegValley) eegValley = attention;
        if (attention < 60f) eegBelow60Time += Time.deltaTime;
        if (attention < 80f) eegBelow80Time += Time.deltaTime;

        CheckGameCompletion();
    }

    // ==================== 公开方法 ====================

    /// <summary>手动提交当前会话（游戏完成时也会自动调用）</summary>
    public void SubmitCurrentSession()
    {
        if (!isTracking) return;

        // 如果没有收集到任何采样数据，跳过提交（防止全 0 数据入库）
        if (attentionSamples.Count == 0)
        {
            Debug.LogWarning("[MetricsTracker] ⚠️ 无采样数据，跳过提交（场景可能未正常运行）");
            isTracking = false;
            gameCompleted = true;
            return;
        }

        isTracking = false;
        gameCompleted = true;

        float totalMin = (Time.time - sessionStartTime) / 60f;
        float avgAttention = attentionSamples.Count > 0
            ? attentionSamples.Sum() / attentionSamples.Count
            : 0f;

        var log = new GameLogData
        {
            game_module     = currentModule,
            durationMinutes = Mathf.RoundToInt(Mathf.Max(1, totalMin)),
            recipeName      = GetRecipeName(),
            avgAttention    = Mathf.RoundToInt(avgAttention),
            peakFocus       = Mathf.RoundToInt(peakAttention),
            distractCount   = totalDistractCount,
            timestamp       = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            clientVersion   = Application.version,
            deviceInfo      = SystemInfo.deviceModel,
            dimensionMetrics = CalculateDimensions(avgAttention, totalMin),
            eegMetrics       = CalculateEegMetrics(avgAttention)
        };

        Debug.Log($"[MetricsTracker] 📊 提交:\n" +
                  $"  模块={log.game_module} 菜={log.recipeName} 时长={log.durationMinutes}min\n" +
                  $"  均专注={log.avgAttention} 峰值={log.peakFocus} 走神={log.distractCount}\n" +
                  $"  持续={log.dimensionMetrics.sustained} 选择={log.dimensionMetrics.selective} " +
                  $"  执行={log.dimensionMetrics.executive} 冲动={log.dimensionMetrics.impulse}");

        logSender.SubmitLog(log);
    }

    // ==================== 内部逻辑 ====================

    private void BeginSession()
    {
        sessionStartTime = Time.time;
        isTracking = true;
        gameCompleted = false;
        attentionSamples.Clear();
        totalDistractCount = 0;
        peakAttention = 0f;
        eegPeak = 0f;
        eegValley = float.MaxValue;
        eegBelow60Time = 0f;
        eegBelow80Time = 0f;
        totalSampleTime = 0f;
        Debug.Log($"[MetricsTracker] ▶ 开始追踪 | 模块={currentModule}");
    }

    private void DetectModule()
    {
        var sceneName = SceneManager.GetActiveScene().name.ToLower();
        if (sceneName.Contains("camp") || sceneName.Contains("yingdi"))
            currentModule = "camp";
        else if (sceneName.Contains("room") || sceneName.Contains("fangjian"))
            currentModule = "room";
        else if (sceneName.Contains("kitchen") || sceneName.Contains("chufang"))
            currentModule = "kitchen";
        else
            currentModule = "kitchen";
    }

    private float GetCurrentAttention()
    {
        // 优先 BCI 设备
        if (gameplayInput != null && gameplayInput.HasLiveConnection)
            return gameplayInput.SmoothedAttention;

        // 键盘模拟
        if (Input.GetKey(KeyCode.Space))
            return 75f + UnityEngine.Random.Range(-5f, 5f);

        return 45f + Mathf.Sin(Time.time * 0.5f) * 10f;
    }

    private void CheckGameCompletion()
    {
        if (gameCompleted) return;

        // 至少收集 2 秒样本才允许提交，防止空数据入库
        if (attentionSamples.Count < 60)
            return;

        // 自动模式：30s 无游戏引用时自动提交（方便测试）
        if (soupGame == null)
        {
            if (Time.time - sessionStartTime > 30f)
                SubmitCurrentSession();
            return;
        }

        if (IsGameCompleted())
        {
            Debug.Log("[MetricsTracker] 🎉 检测到烹饪完成");
            SubmitCurrentSession();
        }
    }

    private bool IsGameCompleted()
    {
        if (soupGame == null) return false;
        var field = typeof(MushroomSoupGame).GetField("dishPhase",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field == null) return false;
        var value = field.GetValue(soupGame);
        // DishPhase.Completed == 3
        return value != null && (int)value == 3;
    }

    private DimensionMetrics CalculateDimensions(float avg, float totalMin)
    {
        int sustained = Mathf.RoundToInt(Mathf.Clamp(avg, 0, 100));

        // 选择注意力：基于走神频率
        float distractPerMin = totalMin > 0 ? totalDistractCount / totalMin : 0;
        int selective = Mathf.RoundToInt(Mathf.Clamp(100f - distractPerMin * 33.3f, 0, 100));

        int executive = Mathf.RoundToInt(Mathf.Clamp(avg * 0.7f + 30f, 0, 100));
        int impulse = Mathf.RoundToInt(Mathf.Clamp(100f - distractPerMin * 33.3f, 0, 100));

        return new DimensionMetrics
        {
            sustained = sustained,
            selective = selective,
            executive = executive,
            impulse   = impulse
        };
    }

    private EegMetrics CalculateEegMetrics(float avg)
    {
        float valley = eegValley == float.MaxValue ? 0f : eegValley;

        float variance = 0f;
        if (attentionSamples.Count > 1)
        {
            foreach (var v in attentionSamples)
                variance += (v - avg) * (v - avg);
            variance /= attentionSamples.Count;
        }
        float cv = avg > 0 ? Mathf.Sqrt(variance) / avg : 0f;

        return new EegMetrics
        {
            meanFocus           = avg,
            peakFocus           = eegPeak,
            valFocus            = valley,
            durationRatio60     = totalSampleTime > 0 ? (1f - eegBelow60Time / totalSampleTime) * 100f : 0f,
            durationRatio80     = totalSampleTime > 0 ? (1f - eegBelow80Time / totalSampleTime) * 100f : 0f,
            avgDistractDuration = 2.5f,
            focusCv             = cv
        };
    }

    private string GetRecipeName()
    {
        var sceneName = SceneManager.GetActiveScene().name.ToLower();
        if (sceneName.Contains("mushroom") || sceneName.Contains("soup")) return "森林蘑菇汤";
        if (sceneName.Contains("fish")) return "香煎河鱼";
        return "蘑菇汤+煎鱼套餐";
    }

    // ==================== 调试 GUI ====================

    private void OnGUI()
    {
        if (!showDebugGui || !isTracking) return;

        GUILayout.BeginArea(new Rect(10, 10, 280, 170));
        GUI.Box(new Rect(0, 0, 280, 170), "");
        GUILayout.Label($"📊 MetricsTracker | {currentModule}");
        GUILayout.Label($"  注意力: {lastAttentionValue:F0}  峰值: {peakAttention:F0}");
        GUILayout.Label($"  走神: {totalDistractCount}  采样: {attentionSamples.Count}");
        GUILayout.Label($"  已追踪: {(Time.time - sessionStartTime):F0}s");
        GUILayout.EndArea();
    }
}

// ==================== 扩展方法 ====================

internal static class ListExtensions
{
    public static float Sum(this List<float> list)
    {
        float total = 0f;
        foreach (var v in list) total += v;
        return total;
    }
}
