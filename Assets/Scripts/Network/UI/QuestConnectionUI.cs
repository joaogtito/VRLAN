using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.UI;
using VRLAN.Network.Discovery;

namespace VRLAN.Network.UI
{
    // UI de conexão pro Quest (Canvas World Space).
    // Estados:
    //  - Disconnected: mostra scan/lista/manual entry.
    //  - Connecting: status "connecting to ...", aguarda evento.
    //  - Connected: esconde lista/scan/manual, mostra disconnect.
    public class QuestConnectionUI : MonoBehaviour
    {
        [Header("Discovery")]
        [SerializeField] private LanDiscovery    discovery;
        [SerializeField] private int broadcastTimes = 5;
        [SerializeField] private float broadcastIntervalSeconds = 1f;

        [Header("Main UI")]
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private GameObject scanningUi;         // pai que contém lista + scan + manual entry
        [SerializeField] private Transform hostListContent;     // VerticalLayoutGroup parent
        [SerializeField] private Button hostListItemPrefab;     // Button + TMP_Text filho
        [SerializeField] private Button scanButton;
        [SerializeField] private Button manualEntryButton;

        [Header("Connected UI")]
        [SerializeField] private GameObject connectedUi;        // pai que contém o botão de disconnect (e outros futuros)
        [SerializeField] private Button disconnectButton;

        [Header("Manual Entry")]
        [SerializeField] private GameObject manualEntryPanel;
        [SerializeField] private TMP_Text ipDisplay;
        [SerializeField] private Button connectManualButton;
        [SerializeField] private Button backManualButton;
        [SerializeField] private ushort defaultPort = 7777;

        private readonly Dictionary<string, DiscoveredHost> _hosts = new();
        private string _manualIp = "";
        private Coroutine _scanCoroutine;
        private string _pendingHostLabel;  // mostrado no status ao conectar

        private void Start()
        {
            discovery.OnHostDiscovered += HandleHostDiscovered;
            scanButton.onClick.AddListener(StartScan);
            manualEntryButton.onClick.AddListener(ShowManualEntry);
            connectManualButton.onClick.AddListener(ConnectManual);
            backManualButton.onClick.AddListener(HideManualEntry);
            disconnectButton.onClick.AddListener(Disconnect);

            var nm = NetworkManager.Singleton;
            if (nm != null)
            {
                nm.OnClientConnectedCallback += HandleClientConnected;
                nm.OnClientDisconnectCallback += HandleClientDisconnected;
            }

            ShowDisconnectedUi();
        }

        private void OnDestroy()
        {
            if (discovery != null) discovery.OnHostDiscovered -= HandleHostDiscovered;
            var nm = NetworkManager.Singleton;
            if (nm != null)
            {
                nm.OnClientConnectedCallback -= HandleClientConnected;
                nm.OnClientDisconnectCallback -= HandleClientDisconnected;
            }
        }

        // --- Métodos públicos chamados pelo teclado numérico ---

        public void AppendChar(string c)
        {
            _manualIp += c;
            ipDisplay.text = _manualIp;
        }

        public void Backspace()
        {
            if (_manualIp.Length == 0) return;
            _manualIp = _manualIp.Substring(0, _manualIp.Length - 1);
            ipDisplay.text = _manualIp;
        }

        public void ClearIp()
        {
            _manualIp = "";
            ipDisplay.text = "";
        }

        // --- State helpers ---

        private void ShowDisconnectedUi()
        {
            scanningUi.SetActive(true);
            connectedUi.SetActive(false);
            manualEntryPanel.SetActive(false);
            UpdateStatus("ready");
            StartScan();  // sempre re-scaneia ao voltar pra tela de disconnect
        }

        private void ShowConnectedUi(string hostLabel)
        {
            // para qualquer scan em andamento
            if (_scanCoroutine != null) { StopCoroutine(_scanCoroutine); _scanCoroutine = null; }
            discovery.Stop();

            scanningUi.SetActive(false);
            manualEntryPanel.SetActive(false);
            connectedUi.SetActive(true);
            UpdateStatus($"connected to {hostLabel}");
        }

        // --- Scan ---

        private void StartScan()
        {
            if (_scanCoroutine != null) StopCoroutine(_scanCoroutine);
            _hosts.Clear();
            ClearHostList();
            UpdateStatus("scanning...");
            discovery.StartClient();
            _scanCoroutine = StartCoroutine(ScanLoop());
        }

        private IEnumerator ScanLoop()
        {
            // Scan contínuo: broadcasta pra sempre até conectar ou sair da tela de scan.
            // Assim a ordem (host primeiro ou scan primeiro) não importa.
            var seconds = 0;
            while (discovery.IsRunning)
            {
                discovery.Broadcast();
                seconds++;
                UpdateStatus(_hosts.Count == 0
                    ? $"scanning... {seconds}s (nenhum host — ou use Manual)"
                    : $"{_hosts.Count} host(s) — clique em um ou continue escaneando");
                yield return new WaitForSeconds(broadcastIntervalSeconds);
            }
            _scanCoroutine = null;
        }

        private void HandleHostDiscovered(DiscoveredHost host)
        {
            if (_hosts.ContainsKey(host.Address)) return;
            _hosts[host.Address] = host;
            AddHostListItem(host);
        }

        private void AddHostListItem(DiscoveredHost host)
        {
            var item = Instantiate(hostListItemPrefab, hostListContent);
            var label = item.GetComponentInChildren<TMP_Text>();
            if (label != null) label.text = $"{host.Name}\n{host.Address}:{host.Port}";
            item.onClick.AddListener(() => Connect(host.Address, host.Port, host.Name));
        }

        private void ClearHostList()
        {
            foreach (Transform child in hostListContent) Destroy(child.gameObject);
        }

        // --- Manual entry ---

        private void ShowManualEntry()
        {
            manualEntryPanel.SetActive(true);
            ClearIp();
        }

        private void HideManualEntry()
        {
            manualEntryPanel.SetActive(false);
        }

        private void ConnectManual()
        {
            if (string.IsNullOrWhiteSpace(_manualIp))
            {
                UpdateStatus("type an IP first");
                return;
            }
            Connect(_manualIp.Trim(), defaultPort, label: null);
        }

        // --- Connect / Disconnect ---

        private void Connect(string ip, ushort port, string label)
        {
            _pendingHostLabel = string.IsNullOrEmpty(label) ? $"{ip}:{port}" : $"{label} ({ip})";

            discovery.Stop();
            if (_scanCoroutine != null) { StopCoroutine(_scanCoroutine); _scanCoroutine = null; }

            var nm = NetworkManager.Singleton;
            var transport = nm.GetComponent<UnityTransport>();
            transport.SetConnectionData(ip, port);  // CRÍTICO: sem isso o transport usa o IP default (127.0.0.1) e nunca alcança o host
            UpdateStatus($"connecting to {_pendingHostLabel}...");
            if (!nm.StartClient()) UpdateStatus("StartClient() failed");
        }

        private void Disconnect()
        {
            UpdateStatus("disconnecting...");
            NetworkManager.Singleton.Shutdown();
            // O evento OnClientDisconnectCallback faz o resto.
        }

        private void HandleClientConnected(ulong clientId)
        {
            var nm = NetworkManager.Singleton;
            if (clientId != nm.LocalClientId) return;
            ShowConnectedUi(_pendingHostLabel);
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            var nm = NetworkManager.Singleton;
            // Pode disparar mesmo quando o NetworkManager está parando — basta o LocalClientId bater.
            if (clientId != nm.LocalClientId && nm.IsConnectedClient) return;
            ShowDisconnectedUi();
        }

        private void UpdateStatus(string msg)
        {
            if (statusText != null) statusText.text = msg;
            Debug.Log($"[QuestConnectionUI] {msg}");
        }
    }
}
