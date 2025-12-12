using UnityEngine;

namespace FPSNet.MainMenu
{
    // Creates hardcoded server rows including IP and port
    public class ServerListManager : MonoBehaviour
    {
        public GameObject serverRowPrefab;
        public Transform contentParent;

        void Start()
        {
            // Replace these IPs with your EC2 Elastic IP and port
            CreateServer("US-EAST-1", "FFA", "0/8", 15, "54.210.1.2", 7777);
            CreateServer("US-EAST-2", "FFA", "0/8", 25, "18.223.120.121", 7777);
            CreateServer("TESTTT", "FFA", "0/8", 70, "54.212.4.5", 7777);

            Debug.Log("Server list created (hardcoded).");
        }

        void CreateServer(string name, string mode, string players, int ping, string ip, int port)
        {
            GameObject row = Instantiate(serverRowPrefab, contentParent);
            ServerRowUI ui = row.GetComponent<ServerRowUI>();
            if (ui != null)
                ui.SetData(name, mode, players, ping, ip, port);
        }
    }
}