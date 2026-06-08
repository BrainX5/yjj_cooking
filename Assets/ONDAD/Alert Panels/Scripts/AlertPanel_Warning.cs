using System;
using UnityEngine;
using UnityEngine.UI;

namespace com.ondad.alertpanels {
    public class AlertPanel_Warning : AlertPanel
    {
        [SerializeField] private Button okeyBtn;
        Action okeyAction;

        private void Awake()
        {
            okeyBtn.onClick.AddListener(OkeyBtn);
        }

        public void ShowPanel(string bodyContent, Action exitAction = null, Action okeyAction = null)
        {
            if (okeyAction != null) this.okeyAction = okeyAction;
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
    }
}
