using System;
using UnityEngine;
using UnityEngine.UI;

namespace com.ondad.alertpanels
{
    public class AlertPanel_Confirmation : AlertPanel
    {
        [SerializeField] private Button okeyBtn;
        [SerializeField] private Button cancelBtn;

        Action okeyAction;
        Action cancelAction;

        private void Awake()
        {
            okeyBtn.onClick.AddListener(OkeyBtn);
            cancelBtn.onClick.AddListener(CancelBtn);
        }

        public void ShowPanel(string bodyContent, Action exitAction = null, Action okeyAction = null, Action cancelAction = null)
        {
            if (okeyAction != null) this.okeyAction = okeyAction;
            if (cancelAction != null) this.cancelAction = cancelAction;

            DisplayPanel(bodyContent, exitAction);
        }

        void OkeyBtn()
        {
            if (okeyAction != null)
            {
                okeyAction.Invoke();
                okeyAction = null;
            }

            ExitPanel();
        }

        void CancelBtn()
        {
            if (cancelAction != null)
            {
                cancelAction.Invoke();
                cancelAction = null;
            }

            ExitPanel();
        }
    }
}
