using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;

namespace VRLAN.Network
{
    // Spawna a raiz de sessão (SessionManager) quando o servidor sobe.
    // Necessário porque Scene Management está OFF: NetworkObjects não vêm da cena,
    // têm que ser spawnados em runtime e replicados pros clientes.
    public class NetworkSessionSpawner : MonoBehaviour
    {
        [FormerlySerializedAs("pingTesterPrefab")]
        [SerializeField] private GameObject sessionRootPrefab;

        private NetworkObject _spawned;

        // Start (não OnEnable): garante que NetworkManager.Singleton já existe.
        private void Start()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null)
            {
                Debug.LogError("[NetworkSessionSpawner] NetworkManager.Singleton == null no Start()");
                return;
            }
            nm.OnServerStarted += HandleServerStarted;
            nm.OnServerStopped += HandleServerStopped;
        }

        private void OnDestroy()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return;
            nm.OnServerStarted -= HandleServerStarted;
            nm.OnServerStopped -= HandleServerStopped;
        }

        private void HandleServerStarted()
        {
            if (sessionRootPrefab == null)
            {
                Debug.LogError("[NetworkSessionSpawner] sessionRootPrefab não setado");
                return;
            }
            var instance = Instantiate(sessionRootPrefab);
            _spawned = instance.GetComponent<NetworkObject>();
            if (_spawned == null)
            {
                Debug.LogError("[NetworkSessionSpawner] prefab sem NetworkObject");
                Destroy(instance);
                return;
            }
            _spawned.Spawn();
            Debug.Log($"[NetworkSessionSpawner] {sessionRootPrefab.name} spawned");
        }

        private void HandleServerStopped(bool _)
        {
            if (_spawned != null && _spawned.IsSpawned) _spawned.Despawn();
            _spawned = null;
        }
    }
}
