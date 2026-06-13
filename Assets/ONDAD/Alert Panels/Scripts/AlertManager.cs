using System;
using UnityEngine;

namespace com.ondad.alertpanels
{
    public class AlertManager : MonoBehaviour
    {
        [SerializeField]
        private GameObject blurBG;

        [SerializeField]
        private AlertPanel_Info alertInfo;
        [SerializeField]
        private AlertPanel_Warning alertWarning;
        [SerializeField]
        private AlertPanel_Error alertError;
        [SerializeField]
        private AlertPanel_Confirmation alertConfirmation;

        private static AlertManager _instance;

        public static AlertManager GetInstance()
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<AlertManager>();
            }
            return _instance;
        }

        public void Enable_Disable_BlurImg(bool isBlurImgActive)
        {
            blurBG.SetActive(isBlurImgActive);
        }

        public void ShowInfoPanel(string bodyContent, Action exitAction = null, Action okeyAction = null)
        {
            alertInfo.ShowPanel(bodyContent, exitAction, okeyAction);
        }
        public void ShowWarningPanel(string bodyContent, Action exitAction = null, Action okeyAction = null)
        {
            alertWarning.ShowPanel(bodyContent, exitAction, okeyAction);
        }
        public void ShowErrorPanel(string bodyContent, Action exitAction = null, Action okeyAction = null)
        {
            alertError.ShowPanel(bodyContent, exitAction, okeyAction);
        }
        public void ShowConfirmationPanel(string bodyContent, Action exitAction = null, Action okeyAction = null, Action cancelAction = null)
        {
            alertConfirmation.ShowPanel(bodyContent, exitAction, okeyAction, cancelAction);
        }

    }
}
