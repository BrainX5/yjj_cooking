using UnityEngine;

public class SCUT_PlayerMushroom : MonoBehaviour
{
    public int count = 0;
    public KeyCode pickupKey = KeyCode.Space;
    
    [Header("👉 把你刚才新建的 Canvas 拖到这里")]
    public GameObject weightlessUIPanel; 

    private bool isGameStarted = false; 
    private bool isNearSpecialMushroom = false; 
    private GameObject currentSpecialMushroom; 

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

        if (isNearSpecialMushroom && currentSpecialMushroom != null)
        {
            if (Input.GetKeyDown(pickupKey))
            {
                if (weightlessUIPanel != null) weightlessUIPanel.SetActive(false);
                isNearSpecialMushroom = false; 

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
            return; 
        }

        if (Input.GetKeyDown(pickupKey))
        {
            count++;
            Debug.Log("普通Mushroom Picked! Count: " + count);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.name == "SpecialMushroom")
        {
            isNearSpecialMushroom = true;
            currentSpecialMushroom = other.gameObject;

            if (weightlessUIPanel != null) weightlessUIPanel.SetActive(true);
            Debug.Log("【检测成功】靠近了特殊蘑菇，UI已弹窗。");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.name == "SpecialMushroom")
        {
            isNearSpecialMushroom = false;
            currentSpecialMushroom = null;

            if (weightlessUIPanel != null) weightlessUIPanel.SetActive(false);
        }
    }
}