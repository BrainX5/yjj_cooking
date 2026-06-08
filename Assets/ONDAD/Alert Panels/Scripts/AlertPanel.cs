using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace com.ondad.alertpanels
{
    public class AlertPanel : MonoBehaviour
    {
        [SerializeField] private Button exitBtn;
        [SerializeField] private TextMeshProUGUI bodyBtn;

        private int tweenId;
        private float bounceStrength = 0.5f;
        private Action exitAction;

        private void Start()
        {
            exitBtn.onClick.AddListener(ExitPanel);
        }

        public void DisplayPanel(string bodyContent, Action exitAction = null)
        {
            bodyBtn.text = bodyContent;
            if (exitAction != null) this.exitAction = exitAction;

            ShowAnimation();
        }

        protected void ExitPanel()
        {
            HideAnimation();

            if (exitAction != null)
            {
                exitAction.Invoke();
                exitAction = null;
            }
        }

        void ShowAnimation()
        {
            LeanTween.cancel(tweenId);
            EnablePanel();

            tweenId = LeanTween.scale(gameObject, Vector2.one, AlertPanel_Config.Instance.alertConfig.uiPanelAnimSpeed)
                .setEase(LeanTweenType.easeInBounce)
                .setOvershoot(bounceStrength)
                .id;
        }

        void HideAnimation()
        {
            LeanTween.cancel(tweenId);
            tweenId = LeanTween.scale(gameObject, Vector2.zero, AlertPanel_Config.Instance.alertConfig.uiPanelAnimSpeed)
                .setEase(LeanTweenType.easeOutBounce)
                .setOvershoot(bounceStrength).setOnComplete(DisablePanel)
                .id;
        }

        private void EnablePanel()
        {
            AlertManager.GetInstance().Enable_Disable_BlurImg(true);
            gameObject.SetActive(true);
            gameObject.transform.localScale = Vector2.zero;
        }
        private void DisablePanel()
        {
            gameObject.SetActive(false);
            AlertManager.GetInstance().Enable_Disable_BlurImg(false);
        }
    }
}
