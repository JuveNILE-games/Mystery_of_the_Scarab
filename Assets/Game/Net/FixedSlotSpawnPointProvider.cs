using PurrNet;
using UnityEngine;

namespace Game.Net
{
    /// <summary>
    /// Fixed 2-player co-op: host always spawns at slot 1, joining client at slot 2.
    /// Expected to live on the same GameObject as the PurrNet.PlayerSpawner it registers with.
    /// </summary>
    [RequireComponent(typeof(PlayerSpawner))]
    public class FixedSlotSpawnPointProvider : MonoBehaviour, IProvideSpawnPoints
    {
        [SerializeField] private Transform hostSpawnPoint;
        [SerializeField] private Transform clientSpawnPoint;

        private void Awake()
        {
            GetComponent<PlayerSpawner>().SetRespawnPointProvider(this);
        }

        public SpawnPoint NextSpawnPoint(PlayerID player, SceneID scene)
        {
            bool isHostsOwnPlayer = player == NetworkManager.main.localPlayer;
            var point = isHostsOwnPlayer ? hostSpawnPoint : clientSpawnPoint;

            if (point == null)
            {
                Debug.LogError($"[FixedSlotSpawnPointProvider] " +
                    $"{(isHostsOwnPlayer ? nameof(hostSpawnPoint) : nameof(clientSpawnPoint))} not assigned — falling back to this object's own position.", this);
                return new SpawnPoint { position = transform.position, rotation = transform.rotation };
            }

            return new SpawnPoint
            {
                position = point.position,
                rotation = point.rotation,
            };
        }
    }
}
