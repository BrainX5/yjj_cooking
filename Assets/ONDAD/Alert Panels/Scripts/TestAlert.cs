using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace com.ondad.alertpanels
{
    public class TestAlert : MonoBehaviour
    {
        public void ShowAlertPanelInfo()
        {
            AlertManager.GetInstance().ShowInfoPanel("This is an Info Panel");
        }

        public void ShowAlertPanelWarning()
        {
            AlertManager.GetInstance().ShowWarningPanel("This is a Warning Panel");
        }

        public void ShowAlertPanelError()
        {
            AlertManager.GetInstance().ShowErrorPanel("This is an Error Panel");
        }

        public void ShowPanelConfirmation()
        {
            AlertManager.GetInstance().ShowConfirmationPanel("Are you sure ?");
        }
    }
}
