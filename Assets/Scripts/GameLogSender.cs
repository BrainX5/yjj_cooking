// ============================================================
// GameLogSender.cs
// Unity 端 — HTTP 调用微信云函数 submitGameLog，提交训练日志
//
// 使用方法：
//   1. 将此脚本挂到场景中任意 GameObject 上
//   2. 在 Inspector 中确认 ApiUrl（默认已填）
//   3. 填写 UserOpenid（用户标识）
//   4. 游戏结束时调用 SubmitLog()
// ============================================================

using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class GameLogSender : MonoBehaviour
{
    [Header("后端配置")]
    [Tooltip("云函数 HTTP 地址")]
    public string apiUrl = "https://cloud1-d9gz2tmfub107d0ff.service.tcloudbase.com/submitGameLog";

    [Header("用户标识")]
    [Tooltip("微信用户的 openid，需由小程序登录后传给 Unity")]
    public string userOpenid = "";

    // ==================== 公开方法：提交训练日志 ====================

    /// <summary>提交一局训练日志到云端</summary>
    public void SubmitLog(GameLogData logData)
    {
        logData.openid = userOpenid;
        StartCoroutine(PostCoroutine(logData));
    }

    // ==================== HTTP 请求 ====================

    private IEnumerator PostCoroutine(GameLogData data)
    {
        string json = JsonUtility.ToJson(data);
        Debug.Log($"[GameLogSender] 📤 发送 JSON:\n{json}");

        using (UnityWebRequest req = new UnityWebRequest(apiUrl, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            req.uploadHandler   = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                var resp = JsonUtility.FromJson<ApiResponse>(req.downloadHandler.text);
                if (resp.code == 0)
                {
                    Debug.Log($"[GameLogSender] ✅ 提交成功 _id={resp.data._id}");
                    OnSubmitSuccess?.Invoke(resp);
                }
                else
                {
                    Debug.LogError($"[GameLogSender] ❌ 服务端错误 code={resp.code} msg={resp.msg}");
                    OnSubmitFailed?.Invoke(resp.msg);
                }
            }
            else
            {
                Debug.LogError($"[GameLogSender] ❌ 网络错误: {req.error}");
                OnSubmitFailed?.Invoke(req.error);
            }
        }
    }

    // ==================== 事件回调 ====================

    public event Action<ApiResponse> OnSubmitSuccess;
    public event Action<string>       OnSubmitFailed;
}

// ==================== 数据模型 ====================

/// <summary>游戏训练日志 — 对应云数据库 main_game_logs 集合</summary>
[Serializable]
public class GameLogData
{
    // ── 身份标识（由 GameLogSender 自动注入）──
    public string openid;

    // ── 基础字段 ──
    public long   timestamp;
    public string game_module;
    public int    durationMinutes;
    public string recipeName;

    // ── 专注力核心 ──
    public int avgAttention;
    public int peakFocus;
    public int distractCount;

    // ── ADHD 四维 ──
    public DimensionMetrics dimensionMetrics;

    // ── EEG 脑电 ──
    public EegMetrics eegMetrics;

    // ── 元数据 ──
    public string clientVersion;
    public string deviceInfo;
}

[Serializable]
public class DimensionMetrics
{
    public int sustained;
    public int selective;
    public int executive;
    public int impulse;
}

[Serializable]
public class EegMetrics
{
    public float meanFocus;
    public float peakFocus;
    public float valFocus;
    public float durationRatio60;
    public float durationRatio80;
    public float avgDistractDuration;
    public float focusCv;
}

/// <summary>服务端响应</summary>
[Serializable]
public class ApiResponse
{
    public int    code;
    public string msg;
    public ApiResponseData data;
}

[Serializable]
public class ApiResponseData
{
    public string _id;
    public long   timestamp;
}
