using System.Linq;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.UI;
using VRLAN.Network.Discovery;

namespace VRLAN.Network
{
    // UI de conexão do HOST (tablet/professor): abrir/encerrar a sala + status.
    // Só hostButton e stopButton são obrigatórios. clientButton/ipInput/portInput são opcionais
    // (debug: permitem rodar um cliente pelo mesmo painel no Editor/PC).
    public class ConnectionUI : MonoBehaviour
    {
        [SerializeField] private Button hostButton;
        [SerializeField] private Button clientButton;                    // opcional (debug)
        [SerializeField] private Button stopButton;
        [SerializeField] private TMP_InputField ipInput;                 // opcional (debug)
        [SerializeField] private TMP_InputField portInput;               // opcional
        [SerializeField] private TMP_InputField hostNameInput;           // nome da sala anunciado no discovery (opcional)
        [SerializeField] private LanDiscoveryHostBridge discoveryBridge; // recebe o nome antes do StartHost
        [SerializeField] private TMP_Text statusText;

        private const string DefaultIp = "127.0.0.1";
        private const ushort DefaultPort = 7777;

        private SessionManager _hookedSession;  // referência atual pra desinscrever quando trocar

        private void Awake()
        {
            if (ipInput != null && string.IsNullOrEmpty(ipInput.text)) ipInput.text = DefaultIp;
            if (portInput != null && string.IsNullOrEmpty(portInput.text)) portInput.text = DefaultPort.ToString();

            if (hostButton != null) hostButton.onClick.AddListener(OnHostClicked);
            if (clientButton != null) clientButton.onClick.AddListener(OnClientClicked);
            if (stopButton != null) stopButton.onClick.AddListener(OnStopClicked);
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
            UpdateStatus("Sala fechada");
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
            // Define o nome do host ANTES de subir o servidor (o bridge lê no OnServerStarted).
            if (discoveryBridge != null && hostNameInput != null)
                discoveryBridge.SetHostName(hostNameInput.text);

            ApplyTransportConfig(listen: true);
            UpdateStatus(NetworkManager.Singleton.StartHost() ? "Abrindo a sala..." : "Não foi possível abrir a sala");
        }

        private void OnClientClicked()
        {
            ApplyTransportConfig(listen: false);
            UpdateStatus(NetworkManager.Singleton.StartClient()
                ? $"Conectando em {CurrentIp()}:{CurrentPort()}..."
                : "Falha ao conectar");
        }

        private void OnStopClicked()
        {
            NetworkManager.Singleton.Shutdown();
            UpdateStatus("Sala encerrada");
        }

        private string CurrentIp() =>
            ipInput == null || string.IsNullOrWhiteSpace(ipInput.text) ? DefaultIp : ipInput.text.Trim();

        private ushort CurrentPort() =>
            portInput != null && ushort.TryParse(portInput.text, out var p) ? p : DefaultPort;

        private void ApplyTransportConfig(bool listen)
        {
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            // Host escuta em 0.0.0.0; client conecta no IP digitado.
            var ip = CurrentIp();
            transport.SetConnectionData(ip, CurrentPort(), listen ? "0.0.0.0" : ip);
        }

        // --- NGO callbacks ---

        private void HandleServerStarted()
        {
            UpdateStatus("Sala aberta");
            TryHookSession();  // SessionManager pode já estar spawnada nesse momento
        }

        private void HandleServerStopped(bool _)
        {
            UnhookSession();
            UpdateStatus("Sala encerrada");
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
                UpdateStatus("Desconectado");
                return;
            }
            RefreshStatus();
        }

        // --- SessionManager hook ---

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
                UpdateStatus("Sala fechada");
                return;
            }

            // Sem SessionManager ainda: fallback pro contador cru.
            if (_hookedSession == null)
            {
                var raw = Mathf.Max(0, (nm.ConnectedClientsIds?.Count ?? 0) - (nm.IsHost ? 1 : 0));
                UpdateStatus(nm.IsServer ? $"Sala aberta · {raw} óculos" : "Conectado");
                return;
            }

            var students = _hookedSession.StudentsSnapshot;
            if (nm.IsServer)
            {
                UpdateStatus(students.Count switch
                {
                    0 => "Sala aberta · nenhum aluno",
                    1 => "Sala aberta · 1 aluno",
                    _ => $"Sala aberta · {students.Count} alunos",
                });
                Debug.Log($"[ConnectionUI] alunos: " +
                          string.Join(", ", students.Select(s => $"{s.DisplayName}#{s.ClientId}")));
            }
            else
            {
                UpdateStatus($"Conectado · {students.Count} aluno(s) na sala");
            }
        }

        private void UpdateStatus(string msg)
        {
            if (statusText != null) statusText.text = msg;
            Debug.Log($"[ConnectionUI] {msg}");
        }
    }
}
