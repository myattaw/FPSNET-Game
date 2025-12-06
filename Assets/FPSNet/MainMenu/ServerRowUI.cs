using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

namespace FPSNet.MainMenu
{
    // Simple row UI that stores IP/port and connects when the button is pressed
    [RequireComponent(typeof(Button))]
    public class ServerRowUI : MonoBehaviour
    {
        public TMP_Text  serverNameText;
        public TMP_Text  modeText;
        public TMP_Text  playersText;
        public TMP_Text  pingText;
        public Button connectButton;

        private string serverIp;
        private ushort serverPort;

        public void SetData(string name, string mode, string players, int ping, string ip, int port)
        {
            serverNameText.text = name;
            modeText.text = mode;
            playersText.text = players;

            // Set ping text
            pingText.text = ping + "ms";

            // Color logic
            if (ping < 30)
            {
                pingText.color = Color.green;
            }
            else if (ping < 70)
            {
                pingText.color = new Color(1f, 0.64f, 0f); // orange
            }
            else
            {
                pingText.color = Color.red;
            }

            serverIp = ip;
            serverPort = (ushort)port;

            if (connectButton != null)
            {
                connectButton.onClick.RemoveAllListeners();
                connectButton.onClick.AddListener(OnConnectPressed);
            }
        }

        private void OnConnectPressed()
        {
            if (NetworkManager.Singleton == null)
            {
                Debug.LogError("No NetworkManager present in scene.");
                return;
            }

            var utp = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (utp == null)
            {
                Debug.LogError("UnityTransport not found on NetworkManager.");
                return;
            }

            // Set the server IP and port before starting the client
            utp.SetConnectionData(serverIp, serverPort);
            Debug.Log($"Connecting to {serverIp}:{serverPort}");
            NetworkManager.Singleton.StartClient();
        }
    }
}