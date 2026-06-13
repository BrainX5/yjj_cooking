using UnityEngine;
using TMPro;

public class WeightlessMissionUI : MonoBehaviour
{
    public TMP_Text missionText;

    public int targetCount = 5;
    [SerializeField] private string missionIntro = "失重室里真假蘑菇混在一起啦，持续专注，把真正的蘑菇稳稳吸回来。";
    [SerializeField] private string missionProgressPrefix = "找回真正的蘑菇";
    [SerializeField] private string missionComplete = "太棒啦，走失的蘑菇都回家了。\n现在带着它们去做一锅热乎乎的美食吧。";

    private int currentCount = 0;

    void Start()
    {
        missionText.gameObject.SetActive(false);
    }

    public void StartMission()
    {
        currentCount = 0;

        missionText.gameObject.SetActive(true);
        missionText.text = $"{missionIntro}\n{missionProgressPrefix}（0/{targetCount}）";
    }

    public void AddOne()
    {
        currentCount++;

        missionText.text = $"{missionIntro}\n{missionProgressPrefix}（{currentCount}/{targetCount}）";

        if(currentCount >= targetCount)
        {
            missionText.text = missionComplete;
        }
    }
}
