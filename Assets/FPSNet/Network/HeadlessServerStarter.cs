using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using System;
using UnityEngine.SceneManagement;

namespace FPSNet.Network
{
    // Auto-start server when launched with -server or in batchmode.
    public class HeadlessServerStarter : MonoBehaviour
    {
        public int listenPort = 7777;

        private void Start()
        {
            string[] args = Environment.GetCommandLineArgs();
            bool startServer = Application.isBatchMode || Array.Exists(args, a => a == "-server");

            Debug.Log($"[Headless] Active Scene: {SceneManager.GetActiveScene().name}");
            Debug.Log($"[Headless] BatchMode: {Application.isBatchMode}, -server arg: {startServer}");

            if (!startServer)
                return;

            // Get the NetworkManager on THIS GameObject, don’t use Singleton here
            var nm = GetComponent<NetworkManager>();
            if (nm == null)
            {
                Debug.LogError("[Headless] NetworkManager component not found on this GameObject.");
                return;
            }

            var utp = nm.GetComponent<UnityTransport>();
            if (utp != null)
            {
                utp.SetConnectionData("0.0.0.0", (ushort)listenPort);
            }

            Debug.Log("[Headless] Starting server…");
            nm.StartServer();
            Debug.Log("[Headless] Server started.");
        }
    }
}