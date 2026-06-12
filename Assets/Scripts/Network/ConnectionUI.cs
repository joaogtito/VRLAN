using System.Linq;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.UI;

namespace VRLAN.Network
{
    public class ConnectionUI : MonoBehaviour
    {
        [SerializeField] private Button hostButton;
        [SerializeField] private Button clientButton;
        [SerializeField] private Button stopButton;
        [SerializeField] private TMP_InputField ipInput;
        [SerializeField] private TMP_InputField portInput;
        [SerializeField] private TMP_Text statusText;

        private const string DefaultIp = "127.0.0.1";
        private const ushort DefaultPort = 7777;

        private SessionManager _hookedSession;  // referência atual pra desinscrever quando trocar

        private void Awake()
        {
            if (ipInput != null && string.IsNullOrEmpty(ipInput.text)) ipInput.text = DefaultIp;
            if (portInput != null && string.IsNullOrEmpty(portInput.text)) portInput.text = DefaultPort.ToString();

            hostButton.onClick.AddListener(OnHostClicked);
            clientButton.onClick.AddListener(OnClientClicked);
            stopButton.onClick.AddListener(OnStopClicked);
        }

        // Inscreve em Start() (não OnEnable): Start roda depois de TODOS os Awake(),
        // então NetworkManager.Singleton já está garantidamente atribuído.
        private void Start()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null)
            {
                Debug.LogError("[ConnectionUI] NetworkManager.Singleton == null no Start(). " +
                               "Tem um NetworkManager ativo na cena?");
                return;
            }
            nm.OnServerStarted += HandleServerStarted;
            nm.OnServerStopped += HandleServerStopped;
            nm.OnClientConnectedCallback += HandleClientConnected;
            nm.OnClientDisconnectCallback += HandleClientDisconnected;
            UpdateStatus("idle");
        }

        private void OnDestroy()
        {
            UnhookSession();
            var nm = NetworkManager.Singleton;
            if (nm == null) return;
            nm.OnServerStarted -= HandleServerStarted;
            nm.OnServerStopped -= HandleServerStopped;
            nm.OnClientConnectedCallback -= HandleClientConnected;
            nm.OnClientDisconnectCallback -= HandleClientDisconnected;
        }

        private void OnHostClicked()
        {
            ApplyTransportConfig(listen: true);
            if (NetworkManager.Singleton.StartHost())
                UpdateStatus("host started");
            else
                UpdateStatus("host failed");
        }

        private void OnClientClicked()
        {
            ApplyTransportConfig(listen: false);
            if (NetworkManager.Singleton.StartClient())
                UpdateStatus($"connecting to {ipInput.text}:{portInput.text}...");
            else
                UpdateStatus("client failed");
        }

        private void OnStopClicked()
        {
            NetworkManager.Singleton.Shutdown();
            UpdateStatus("stopped");
        }

        private void ApplyTransportConfig(bool listen)
        {
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            var ip = string.IsNullOrEmpty(ipInput.text) ? DefaultIp : ipInput.text.Trim();
            var port = ushort.TryParse(portInput.text, out var p) ? p : DefaultPort;

            // Host escuta em 0.0.0.0; client conecta no IP digitado.
            var listenAddress = listen ? "0.0.0.0" : ip;
            transport.SetConnectionData(ip, port, listenAddress);
        }

        // --- NGO callbacks ---

        private void HandleServerStarted()
        {
            UpdateStatus("server up");
            TryHookSession();  // SessionManager pode já estar spawnada nesse momento
        }

        private void HandleServerStopped(bool _)
        {
            UnhookSession();
            UpdateStatus("server stopped");
        }

        private void HandleClientConnected(ulong clientId)
        {
            // SessionManager pode acabar de ser spawnada agora — tentar engatar.
            TryHookSession();
            RefreshStatus();
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return;
            if (!nm.IsServer && !nm.IsClient)
            {
                UnhookSession();
                UpdateStatus("disconnected");
                return;
            }
            RefreshStatus();
        }

        // --- SessionManager hook (H5) ---

        private void TryHookSession()
        {
            var sm = SessionManager.Instance;
            if (sm == null || sm == _hookedSession) return;
            UnhookSession();
            _hookedSession = sm;
            _hookedSession.OnStudentsChanged += RefreshStatus;
            RefreshStatus();
        }

        private void UnhookSession()
        {
            if (_hookedSession == null) return;
            _hookedSession.OnStudentsChanged -= RefreshStatus;
            _hookedSession = null;
        }

        private void RefreshStatus()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || (!nm.IsServer && !nm.IsClient))
            {
                UpdateStatus("idle");
                return;
            }

            // Sem SessionManager ainda: fallback pro contador cru.
            if (_hookedSession == null)
            {
                var raw = nm.ConnectedClientsIds?.Count ?? 0;
                UpdateStatus(nm.IsServer ? $"host up — {raw} client(s) (aguardando SessionManager)"
                                         : "connected to host");
                return;
            }

            var students = _hookedSession.StudentsSnapshot;
            if (nm.IsServer)
            {
                var names = students.Count == 0
                    ? "(nenhum)"
                    : string.Join(", ", students.Select(s => $"{s.DisplayName}#{s.ClientId}"));
                UpdateStatus($"host up — {students.Count} aluno(s): {names}");
            }
            else
            {
                UpdateStatus($"connected — {students.Count} aluno(s) na sala");
            }
        }

        private void UpdateStatus(string msg)
        {
            if (statusText != null) statusText.text = msg;
            Debug.Log($"[ConnectionUI] {msg}");
        }
    }
}
