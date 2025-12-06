using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace FPSNet.Network
{
    public class NetworkManagerUI : MonoBehaviour
    {
        [SerializeField] private Button serverButton;
        [SerializeField] private Button hostButton;
        [SerializeField] private Button clientButton;

        public GameObject serverBrowserPanel;
        public GameObject optionsPanel;
        
        private void Awake()
        {
            serverButton.onClick.AddListener((() =>
            {
                NetworkManager.Singleton.StartServer();
            }));

            hostButton.onClick.AddListener((() =>
            {
                Debug.Log("Starting Host...");
                // NetworkManager.Singleton.StartHost();
                serverBrowserPanel.SetActive(true);
                optionsPanel.SetActive(false);
            }));

            clientButton.onClick.AddListener((() => { NetworkManager.Singleton.StartClient(); }));
        }
    }
}