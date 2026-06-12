using UnityEngine;
using System.Collections;
using UnityEngine.UI; // 如果你后续需要接入 UI 文本显示分数，可以解开此命名空间

// 🔥【核心修复】：类名已完美修改为 SCUT_BCIFocusTrainingManager，彻底消除报错！
public class SCUT_BCIFocusTrainingManager : MonoBehaviour
{
    [Header("[BCI Status Log]")]
    public bool isTrainingActive = false;

    [Header("[Simulation Config]")]
    [Tooltip("是否开启键盘模拟脑电数据（方便在没有佩戴脑电设备时测试）")]
    public bool useDebugSimulation = true;
    
    [Tooltip("模拟脑电波上下波动的速度")]
    public float simulationSpeed = 2.0f;

    private SCUT_FlowerDryadController dryadController;

    void Start()
    {
        // 自动抓取场景中的风精灵控制器
        dryadController = FindObjectOfType<SCUT_FlowerDryadController>();
        if (dryadController == null)
        {
            Debug.LogError("【BCI管理器】未在场景中找到 SCUT_FlowerDryadController 脚本！");
        }
    }

    void Update()
    {
        if (!isTrainingActive) return;

        // 如果开启了模拟数据，这里会根据时间自动生成 0~100 留白波动的专注度分数
        if (useDebugSimulation)
        {
            // 使用 Mathf.PingPong 模拟一个在 40 到 95 之间来回波动的脑电专注度
            float simulatedScore = 40f + Mathf.PingPong(Time.time * simulationSpeed * 10f, 55f);
            
            // 实时将模拟数据同步给风精灵
            SendFocusScoreToDryad(simulatedScore);
        }
    }

    /// <summary>
    /// 当风精灵和蘑菇运镜全部到位后，会通过协程正式调用此方法激活 BCI 阶段
    /// </summary>
    public void StartFocusTrainingPhase()
    {
        isTrainingActive = true;
        Debug.Log("脑机接口（BCI）专注度训练正式开始！请集中注意力...");
    }

    /// <summary>
    /// 【核心接口】对外开放。如果你后续接入了真正的商用脑电帽（如 Emotiv、MindLink 或者是 NeuroSky），
    /// 请在你的脑电 SDK 接收回调里调用这个方法，把实时分数传进来。
    /// </summary>
    /// <param name="score">0 到 100 之间的脑电专注度数值</param>
    public void ReceiveRealBCIScore(float score)
    {
        if (!isTrainingActive) return;
        
        // 如果接入了真数据，自动关闭模拟
        useDebugSimulation = false; 
        
        SendFocusScoreToDryad(score);
    }

    private void SendFocusScoreToDryad(float score)
    {
        if (dryadController != null)
        {
            dryadController.focusScore = score;
            // 打印当前的专注度数据，方便你在 Console 窗口观察蘑菇什么时候触发通关
            Debug.Log($"【BCI 实时脑电同步】当前专注度: {score:F1} / 目标: {dryadController.targetFocus}");
        }
    }

    /// <summary>
    /// 训练成功结束（由风精灵通关后触发调用，或者由本脚本自主控制）
    /// </summary>
    public void StopFocusTrainingPhase()
    {
        isTrainingActive = false;
        if (dryadController != null)
        {
            dryadController.focusScore = 0f;
        }
        Debug.Log("【BCI 训练结束】数据流已切断。");
    }
}