using UnityEngine;
using TMPro;

public class WeightlessMissionUI : MonoBehaviour
{
    public TMP_Text missionText;

    public int targetCount = 5;

    private int currentCount = 0;

    void Start()
    {
        missionText.gameObject.SetActive(false);
    }

    public void StartMission()
    {
        currentCount = 0;

        missionText.gameObject.SetActive(true);

        missionText.text =
            $"请获取{targetCount}个蘑菇（0/{targetCount}）";
    }

    public void AddOne()
    {
        currentCount++;

        missionText.text =
            $"请获取{targetCount}个蘑菇（{currentCount}/{targetCount}）";

        if(currentCount >= targetCount)
        {
            missionText.text =
                "恭喜你通过失重室小游戏\n接下来去做美食吧";
        }
    }
}