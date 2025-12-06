using UnityEngine;

namespace FPSNet.MainMenu
{
    public class ServerListManager : MonoBehaviour
    {
        public GameObject serverRowPrefab;
        public Transform contentParent;

        void Start()
        {
            // Hardcoded example servers
            CreateServer("US-EAST-1", "FFA", "1/8", 15);
            CreateServer("US-EAST-2", "FFA", "0/8", 25);
            CreateServer("US-WEST-1", "FFA", "0/8", 70);

            Debug.Log("Created server US-EAST-1");
        }

        void CreateServer(string name, string mode, string players, int ping)
        {
            GameObject row = Instantiate(serverRowPrefab, contentParent);
            ServerRowUI ui = row.GetComponent<ServerRowUI>();

            ui.SetData(name, mode, players, ping);
        }
    }
}