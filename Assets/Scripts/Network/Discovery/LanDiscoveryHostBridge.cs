using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace VRLAN.Network.Discovery
{
    // Liga LanDiscovery ao NetworkManager no lado do host (tablet/PC).
    // Quando o servidor sobe, começa a responder discovery.
    // Quando cai, para.
    [RequireComponent(typeof(LanDiscovery))]
    public class LanDiscoveryHostBridge : MonoBehaviour
    {
        [SerializeField] private string hostName = "VRLAN Host";

        private LanDiscovery _discovery;

        private void Awake() => _discovery = GetComponent<LanDiscovery>();

        // Start (não OnEnable): garante que NetworkManager.Singleton já existe.
        private void Start()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null)
            {
                Debug.LogError("[LanDiscoveryHostBridge] NetworkManager.Singleton == null no Start()");
                return;
            }
            nm.OnServerStarted += HandleServerStarted;
            nm.OnServerStopped += HandleServerStopped;
        }

        private void OnDestroy()
        {
            var nm = NetworkManager.Singleton;
            if (nm != null)
            {
                nm.OnServerStarted -= HandleServerStarted;
                nm.OnServerStopped -= HandleServerStopped;
            }
            _discovery?.Stop();
        }

        private void HandleServerStarted()
        {
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            var gamePort = transport.ConnectionData.Port;
            _discovery.StartServer(gamePort, hostName);
        }

        private void HandleServerStopped(bool _)
        {
            _discovery.Stop();
        }
    }
}
