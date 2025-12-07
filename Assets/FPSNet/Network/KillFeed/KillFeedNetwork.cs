using Unity.Netcode;
using UnityEngine;

namespace FPSNet.Network.KillFeed
{
    [RequireComponent(typeof(NetworkObject))]
    // Server-side broadcaster that calls a ClientRpc to show kills on every client.
    public class KillFeedNetwork : NetworkBehaviour
    {
        public static KillFeedNetwork Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // Call this from server code (e.g. PlayerNetwork.HandleDeath)
        public void BroadcastKill(string attacker, string victim)
        {
            if (!IsServer) return;
            ShowKillClientRpc(attacker, victim);
        }

        [ClientRpc]
        private void ShowKillClientRpc(string attacker, string victim)
        {
            if (KillFeedManager.Instance != null)
                KillFeedManager.Instance.ShowKill(attacker, victim);
        }
        
    }
}