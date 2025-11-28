using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;

namespace FPSNet.Network
{
    public class PlayerStats : NetworkBehaviour
    {
        public static event Action<PlayerStats> OnStatsChanged;
        public static readonly List<PlayerStats> AllPlayers = new List<PlayerStats>();

        public NetworkVariable<FixedString32Bytes> PlayerName = new();
        public NetworkVariable<int> Kills = new();
        public NetworkVariable<int> Deaths = new();

        public override void OnNetworkSpawn()
        {
            AllPlayers.Add(this);

            if (IsServer)
                PlayerName.Value = "Player " + OwnerClientId;

            PlayerName.OnValueChanged += (_, __) => OnStatsChanged?.Invoke(this);
            Kills.OnValueChanged += (_, __) => OnStatsChanged?.Invoke(this);
            Deaths.OnValueChanged += (_, __) => OnStatsChanged?.Invoke(this);

            OnStatsChanged?.Invoke(this);
        }

        public override void OnNetworkDespawn()
        {
            AllPlayers.Remove(this);
            OnStatsChanged?.Invoke(this);
        }

        [ServerRpc]
        public void AddKillServerRpc()
        {
            Kills.Value++;
        }

        [ServerRpc]
        public void AddDeathServerRpc()
        {
            Deaths.Value++;
        }
    }
}