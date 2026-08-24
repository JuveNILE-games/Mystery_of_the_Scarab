using Core.Utility.Attributes;
using NetCore.Interfaces;
using UnityEngine;
using static PurrNet.UnityProxy;

namespace Game.Net
{
    /// <summary>
    /// Solo/SplitScreen path: instantiates two local Player.prefab copies directly, no PurrNet
    /// involved. LAN/Online uses PurrNet's own PlayerSpawner elsewhere in this scene instead.
    /// </summary>
    public class LocalPlayerSpawner : MonoBehaviour
    {
        [SerializeField] private GameObject _playerPrefab;
        [SerializeField] private Transform _spawnPoint1;
        [SerializeField] private Transform _spawnPoint2;

        [Inject] private ISessionService _session;

        private void Start()
        {
            if (_session == null || !_session.IsLocal) return;

            if (_playerPrefab == null || _spawnPoint1 == null || _spawnPoint2 == null)
            {
                Debug.LogError("[LocalPlayerSpawner] Missing prefab or spawn point reference.", this);
                return;
            }

            // Runtime Instantiate() is never swept by Bootstrapper's [Inject] pass, so we inject manually.
            SpawnAndInject(_spawnPoint1);
            SpawnAndInject(_spawnPoint2);
        }

        private void SpawnAndInject(Transform spawnPoint)
        {
            // Plain Instantiate() would get rewritten into a network-spawn attempt by PurrNet's
            // IL-weaver since Player.prefab has a NetworkIdentity; InstantiateDirectly bypasses that.
            var instance = InstantiateDirectly(_playerPrefab, spawnPoint.position, spawnPoint.rotation);
            MonoBehaviourInjection.InjectRuntimeInstance(instance);
        }
    }
}
