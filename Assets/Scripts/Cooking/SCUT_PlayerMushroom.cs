using UnityEngine;

public class SCUT_PlayerMushroom : MonoBehaviour
{
    public int count = 0;
    public KeyCode pickupKey = KeyCode.Space;
    
    private bool isGameStarted = false; // 游戏是否正式开始
    private bool isNearSpecialMushroom = false; // 是否在特殊蘑菇附近
    private GameObject currentSpecialMushroom; // 当前触碰到的特殊蘑菇

    // 临时 UI Canvas
    private GameObject tempUICanvas;

    public void StartGame()
    {
        isGameStarted = true;
        count = 0; 
        Debug.Log("【游戏正式开始】采蘑菇脚本激活。");
    }

    void Start()
    {
        // 游戏测试期间直接激活
        StartGame();
        CreateTempUI();
    }

    void Update()
    {
        if (!isGameStarted) return;

        // 【核心逻辑】：如果玩家在特殊蘑菇旁边，并且按下了确认键（空格）
        if (isNearSpecialMushroom && currentSpecialMushroom != null)
        {
            if (Input.GetKeyDown(pickupKey))
            {
                // 关闭临时提示 UI
                if (tempUICanvas != null) tempUICanvas.SetActive(false);

                // 锁住状态，防止单帧重复触发
                isNearSpecialMushroom = false; 

                // 找到场景中的风精灵，命令它出场，并将特殊蘑菇传给它
                SCUT_FlowerDryadController dryad = FindObjectOfType<SCUT_FlowerDryadController>();
                if (dryad != null)
                {
                    dryad.TriggerSpecialMushroomEvent(currentSpecialMushroom);
                }
                else
                {
                    Debug.LogError("场景中未找到 SCUT_FlowerDryadController 脚本！请检查精灵物体上是否挂载！");
                }
            }
            return; // 处于特殊蘑菇交互时，不执行后面的普通采摘计数
        }

        // 普通采蘑菇计数（去掉了 >=5 的限制，你想怎么采就怎么采）
        if (Input.GetKeyDown(pickupKey))
        {
            count++;
            Debug.Log("普通Mushroom Picked! Count: " + count);
        }
    }

    // 触发检测：检测特殊蘑菇
    private void OnTriggerEnter(Collider other)
    {
        // 确保你的特殊蘑菇物体名字叫 "SpecialMushroom"
        if (other.gameObject.name == "SpecialMushroom")
        {
            isNearSpecialMushroom = true;
            currentSpecialMushroom = other.gameObject;

            // 显示临时 UI 提示
            if (tempUICanvas != null) tempUICanvas.SetActive(true);
            Debug.Log("【检测】踩到了特殊蘑菇！按下 [空格] 确认是否进入失重室。");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.name == "SpecialMushroom")
        {
            isNearSpecialMushroom = false;
            currentSpecialMushroom = null;

            // 玩家离开，关闭提示
            if (tempUICanvas != null) tempUICanvas.SetActive(false);
        }
    }

    // 动态生成临时UI，不需要手动去建UI物体，代码自动生成在屏幕中央
    private void CreateTempUI()
    {
        tempUICanvas = new GameObject("Temp_GravityUI", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
        tempUICanvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

        GameObject textGo = new GameObject("Text");
        textGo.transform.SetParent(tempUICanvas.transform);
        
        UnityEngine.UI.Text text = textGo.AddComponent<UnityEngine.UI.Text>();
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.text = "采到了特殊的蘑菇！\n是否要进入失重室游戏？\n【按下空格键确认】";
        text.fontSize = 35;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.yellow;

        RectTransform rect = textGo.GetComponent<RectTransform>();
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(600, 200);

        tempUICanvas.SetActive(false); // 默认隐藏
    }
}