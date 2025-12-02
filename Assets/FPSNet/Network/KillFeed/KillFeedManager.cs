using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace FPSNet.Network.KillFeed
{
    [RequireComponent(typeof(NetworkObject))]
    public class KillFeedManager : NetworkBehaviour
    {
        public static KillFeedManager Instance;

        [Header("References")]
        public GameObject killFeedItemPrefab; // assign KillFeedItem prefab (UI)
        public Transform killFeedHolder; // assign UI holder (content)

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) Destroy(gameObject);
        }

        public override void OnNetworkSpawn()
        {
            // ensure Instance is set even when spawned by network
            if (Instance == null) Instance = this;
        }

        // Call this from server-side code to broadcast a kill to all clients
        public void BroadcastKill(string attackerName, string victimName)
        {
            if (!IsServer) return;
            ShowKillFeedClientRpc(attackerName, victimName);
        }

        [ClientRpc]
        private void ShowKillFeedClientRpc(string attackerName, string victimName)
        {
            if (killFeedItemPrefab == null || killFeedHolder == null) return;

            // Instantiate as a UI child and keep local transform (worldPositionStays = false)
            var item = Instantiate(killFeedItemPrefab, killFeedHolder, false);
            var text = item.GetComponentInChildren<TMPro.TMP_Text>();
            text.text = $"{attackerName} killed {victimName}";
            
            // Ensure RectTransform defaults (helps when prefab had different values)
            if (item.TryGetComponent<RectTransform>(out var rt))
            {
                rt.localScale = Vector3.one;
                rt.anchoredPosition = Vector2.zero;
            }
            
            var anim = item.GetComponent<KillFeedAnimation>();
            if (anim != null)
                anim.Play();
            else
                Destroy(item, 5f);


            // Force rebuild so VerticalLayoutGroup / ContentSizeFitter re-lays out immediately
            if (killFeedHolder is RectTransform holderRect)
                LayoutRebuilder.ForceRebuildLayoutImmediate(holderRect);
        }
    }
}