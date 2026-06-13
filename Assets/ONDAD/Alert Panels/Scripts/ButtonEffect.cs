using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace com.ondad.alertpanels
{
    public class ButtonEffect : MonoBehaviour
    {
        private float bounceStrength = 0.5f; 

        private int tweenId;
        private float initScale;

        private void Start()
        {
            initScale = transform.localScale.x;

            EventTrigger eventTrigger = gameObject.AddComponent<EventTrigger>();

            EventTrigger.Entry pointerEnterEntry = new EventTrigger.Entry
            {
                eventID = EventTriggerType.PointerEnter
            };

            pointerEnterEntry.callback.AddListener((eventData) => { OnPointerEnter((PointerEventData)eventData); });

            EventTrigger.Entry pointerExitEntry = new EventTrigger.Entry
            {
                eventID = EventTriggerType.PointerExit
            };

            pointerExitEntry.callback.AddListener((eventData) => { OnPointerExit((PointerEventData)eventData); });

            eventTrigger.triggers.Add(pointerEnterEntry);
            eventTrigger.triggers.Add(pointerExitEntry);
        }

        private void OnDisable()
        {
            LeanTween.cancel(tweenId);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            LeanTween.cancel(tweenId);
            tweenId = LeanTween.scale(gameObject, Vector3.one * AlertPanel_Config.Instance.alertConfig.uiButtonHoverScale, AlertPanel_Config.Instance.alertConfig.uiBtnAnimSpeed)
                .setEase(LeanTweenType.easeOutBounce)
                .setOvershoot(bounceStrength)
                .id;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            LeanTween.cancel(tweenId);
            tweenId = LeanTween.scale(gameObject, Vector3.one * initScale, AlertPanel_Config.Instance.alertConfig.uiBtnAnimSpeed)
                .setEase(LeanTweenType.easeOutBounce)
                .setOvershoot(bounceStrength)
                .id;
        }
    }
}
