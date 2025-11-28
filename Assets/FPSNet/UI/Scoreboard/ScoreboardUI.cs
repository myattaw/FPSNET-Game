using System.Collections.Generic;
using FPSNet.Network;
using Unity.Netcode;
using UnityEngine;

namespace FPSNet.UI.Scoreboard
{
    public class ScoreboardUI : MonoBehaviour
    {
        public Transform playerListParent;     // PlayerList object
        public GameObject rowPrefab;           // Your row prefab

        private Dictionary<ulong, GameObject> rows = new();

        private void OnEnable()
        {
            PlayerStats.OnStatsChanged += Refresh;
            Refresh(null);
        }

        private void OnDisable()
        {
            PlayerStats.OnStatsChanged -= Refresh;
        }

        private void Refresh(PlayerStats changed)
        {
            // Create rows for players that don't have one yet
            foreach (var p in PlayerStats.AllPlayers)
            {
                if (!rows.ContainsKey(p.OwnerClientId))
                {
                    GameObject row = Instantiate(rowPrefab, playerListParent);
                    rows.Add(p.OwnerClientId, row);
                }

                UpdateRow(p);
            }

            // Remove rows for players who left
            List<ulong> toRemove = new();
            foreach (var id in rows.Keys)
            {
                bool exists = PlayerStats.AllPlayers.Exists(p => p.OwnerClientId == id);
                if (!exists)
                    toRemove.Add(id);
            }
            foreach (var id in toRemove)
            {
                Destroy(rows[id]);
                rows.Remove(id);
            }
        }

        private void UpdateRow(PlayerStats p)
        {
            if (!rows.ContainsKey(p.OwnerClientId)) return;

            Transform row = rows[p.OwnerClientId].transform;

            var nameText  = row.Find("PlayerName").GetComponent<TMPro.TextMeshProUGUI>();
            var killsText = row.Find("PlayerKills").GetComponent<TMPro.TextMeshProUGUI>();

            // Update text
            nameText.text  = p.PlayerName.Value.ToString();
            killsText.text = p.Kills.Value.ToString();

            // Highlight YOUR player in green
            if (p.OwnerClientId == NetworkManager.Singleton.LocalClientId)
            {
                nameText.color  = Color.green;
                killsText.color = Color.green;
            }
            else
            {
                nameText.color  = Color.white;
                killsText.color = Color.white;
            }
        }

    }
}