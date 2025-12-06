using UnityEngine;

namespace FPSNet.MainMenu
{
    public class MenuManager : MonoBehaviour
    {
        public GameObject serverBrowserPanel;
        public GameObject optionsPanel;

        public void ShowServerBrowser()
        {
            serverBrowserPanel.SetActive(true);
            optionsPanel.SetActive(false);
        }

        public void ShowOptions()
        {
            serverBrowserPanel.SetActive(false);
            optionsPanel.SetActive(true);
        }
    }
}