using UnityEngine;

public class SCUT_PlayerMushroom : MonoBehaviour
{
    public int count = 0;
    public KeyCode pickupKey = KeyCode.Space;
    
    [Header("👉 把你刚才新建的 Canvas 拖到这里")]
    public GameObject weightlessUIPanel; 

    private bool isGameStarted = false; // 游戏是否正式开始
    private bool isNearSpecialMushroom = false; // 是否在特殊蘑菇附近
    private GameObject currentSpecialMushroom; // 当前触碰到的特殊蘑菇

    public void StartGame()
    {
        isGameStarted = true;
        count = 0; 
        if (weightlessUIPanel != null) weightlessUIPanel.SetActive(false);
        Debug.Log("【游戏正式开始】采蘑菇脚本激活。");
    }

    void Start()
    {
        StartGame();
    }

    void Update()
    {
        if (!isGameStarted) return;

        // 【核心交互】：如果玩家在特殊蘑菇旁边，并且按下了确认键（空格）
        if (isNearSpecialMushroom && currentSpecialMushroom != null)
        {
            if (Input.GetKeyDown(pickupKey))
            {
                // 1. 立刻关闭提示 UI
                if (weightlessUIPanel != null) weightlessUIPanel.SetActive(false);

                // 2. 锁住状态，防止单帧多次重复触发
                isNearSpecialMushroom = false; 

                // 3. 通知风精灵执行后续全部剧情（飞过来、刮风、拔高蘑菇）
                SCUT_FlowerDryadController dryad = FindObjectOfType<SCUT_FlowerDryadController>();
                if (dryad != null)
                {
                    dryad.TriggerSpecialMushroomEvent(currentSpecialMushroom);
                }
                else
                {
                    Debug.LogError("场景中未找到 SCUT_FlowerDryadController 脚本！");
                }
            }
            return; // 拦截，不触发普通采摘
        }

        // 普通采蘑菇计数
        if (Input.GetKeyDown(pickupKey))
        {
            count++;
            Debug.Log("普通Mushroom Picked! Count: " + count);
        }
    }

    // 玩家走入特殊蘑菇的绿色 Collider 圈圈
    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.name == "SpecialMushroom")
        {
            isNearSpecialMushroom = true;
            currentSpecialMushroom = other.gameObject;

            // 显示提示 UI
            if (weightlessUIPanel != null) weightlessUIPanel.SetActive(true);
            Debug.Log("【检测成功】靠近了特殊蘑菇，UI已弹窗。");
        }
    }

    // 玩家离开特殊蘑菇的圈圈
    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.name == "SpecialMushroom")
        {
            isNearSpecialMushroom = false;
            currentSpecialMushroom = null;

            // 玩家离开，自动关闭 UI 提示
            if (weightlessUIPanel != null) weightlessUIPanel.SetActive(false);
        }
    }
}