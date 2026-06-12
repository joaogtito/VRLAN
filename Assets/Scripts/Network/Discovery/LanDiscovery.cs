using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace VRLAN.Network.Discovery
{
    // Descoberta de host em LAN por UDP broadcast.
    // Host: modo Server, escuta na porta de discovery e responde com (gamePort, name).
    // Client: modo Client, broadcasta "REQUEST" e recebe "RESPONSE" dos hosts.
    public class LanDiscovery : MonoBehaviour
    {
        public enum Mode { None, Server, Client }

        [SerializeField] private ushort discoveryPort = 47777;
        [SerializeField] private string protocolMagic = "VRLAN_DISCOVERY_V1";

        public event Action<DiscoveredHost> OnHostDiscovered;

        public bool IsRunning { get; private set; }
        public Mode CurrentMode { get; private set; }

        private UdpClient _udp;
        private Thread _receiveThread;
        private volatile bool _stop;
        private readonly ConcurrentQueue<DiscoveredHost> _queue = new();

        private string _serverName;
        private ushort _serverGamePort;

#if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaObject _multicastLock;
#endif

        public void StartServer(ushort gamePort, string serverName)
        {
            Stop();
            _serverName = string.IsNullOrEmpty(serverName) ? "VRLAN Host" : serverName;
            _serverGamePort = gamePort;
            AcquireMulticastLock();

            try
            {
                _udp = new UdpClient();
                _udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _udp.Client.Bind(new IPEndPoint(IPAddress.Any, discoveryPort));
                _udp.EnableBroadcast = true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[LanDiscovery] server bind failed: {e.Message}");
                _udp = null;
                ReleaseMulticastLock();
                return;
            }

            _stop = false;
            CurrentMode = Mode.Server;
            IsRunning = true;
            _receiveThread = new Thread(ServerLoop) { IsBackground = true, Name = "LanDiscovery-Server" };
            _receiveThread.Start();
            Debug.Log($"[LanDiscovery] SERVER listening on UDP {discoveryPort} (name='{_serverName}', gamePort={_serverGamePort})");
        }

        public void StartClient()
        {
            Stop();
            AcquireMulticastLock();
            try
            {
                _udp = new UdpClient();
                _udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _udp.Client.Bind(new IPEndPoint(IPAddress.Any, 0));
                _udp.EnableBroadcast = true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[LanDiscovery] client bind failed: {e.Message}");
                _udp = null;
                ReleaseMulticastLock();
                return;
            }

            _stop = false;
            CurrentMode = Mode.Client;
            IsRunning = true;
            _receiveThread = new Thread(ClientLoop) { IsBackground = true, Name = "LanDiscovery-Client" };
            _receiveThread.Start();
            Debug.Log("[LanDiscovery] CLIENT started");
        }

        public void Broadcast()
        {
            if (CurrentMode != Mode.Client || _udp == null) return;
            var payload = Encoding.UTF8.GetBytes($"{protocolMagic}|REQUEST");

            // Manda em TODOS os endereços de broadcast possíveis:
            // limited broadcast + broadcast direcionado de cada interface IPv4.
            var targets = GetBroadcastTargets();
            foreach (var target in targets)
            {
                try
                {
                    _udp.Send(payload, payload.Length, new IPEndPoint(target, discoveryPort));
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[LanDiscovery] broadcast to {target} failed: {e.Message}");
                }
            }
            Debug.Log($"[LanDiscovery] broadcast sent to {targets.Count} target(s) on port {discoveryPort}");
        }

        public void Stop()
        {
            _stop = true;
            try { _udp?.Close(); } catch { /* ignore */ }
            _udp = null;
            _receiveThread = null;
            CurrentMode = Mode.None;
            IsRunning = false;
            ReleaseMulticastLock();
        }

        private void Update()
        {
            while (_queue.TryDequeue(out var host))
                OnHostDiscovered?.Invoke(host);
        }

        private void OnDisable() => Stop();
        private void OnDestroy() => Stop();

        private List<IPAddress> GetBroadcastTargets()
        {
            var list = new List<IPAddress> { IPAddress.Broadcast }; // 255.255.255.255
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                        if (IPAddress.IsLoopback(ua.Address)) continue;
                        var directed = ComputeDirectedBroadcast(ua.Address, ua.IPv4Mask);
                        if (directed != null && !list.Contains(directed)) list.Add(directed);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LanDiscovery] interface scan failed: {e.Message}");
            }
            return list;
        }

        private static IPAddress ComputeDirectedBroadcast(IPAddress addr, IPAddress mask)
        {
            if (mask == null) return null;
            var a = addr.GetAddressBytes();
            var m = mask.GetAddressBytes();
            if (a.Length != 4 || m.Length != 4) return null;
            var b = new byte[4];
            for (var i = 0; i < 4; i++) b[i] = (byte)(a[i] | ~m[i]);
            return new IPAddress(b);
        }

        private void ServerLoop()
        {
            var endpoint = new IPEndPoint(IPAddress.Any, 0);
            while (!_stop)
            {
                try
                {
                    var data = _udp.Receive(ref endpoint);
                    var text = Encoding.UTF8.GetString(data);
                    Debug.Log($"[LanDiscovery] SERVER got packet from {endpoint}: '{text}'");
                    if (text != $"{protocolMagic}|REQUEST") continue;

                    var response = $"{protocolMagic}|RESPONSE|{_serverGamePort}|{_serverName}";
                    var respBytes = Encoding.UTF8.GetBytes(response);
                    _udp.Send(respBytes, respBytes.Length, endpoint);
                    Debug.Log($"[LanDiscovery] SERVER replied to {endpoint}");
                }
                catch (SocketException) when (_stop) { return; }
                catch (ObjectDisposedException) { return; }
                catch (Exception e)
                {
                    Debug.LogWarning($"[LanDiscovery] server error: {e.Message}");
                }
            }
        }

        private void ClientLoop()
        {
            var endpoint = new IPEndPoint(IPAddress.Any, 0);
            while (!_stop)
            {
                try
                {
                    var data = _udp.Receive(ref endpoint);
                    var text = Encoding.UTF8.GetString(data);
                    Debug.Log($"[LanDiscovery] CLIENT got packet from {endpoint}: '{text}'");
                    var parts = text.Split('|');
                    if (parts.Length < 4 || parts[0] != protocolMagic || parts[1] != "RESPONSE") continue;
                    if (!ushort.TryParse(parts[2], out var port)) continue;

                    _queue.Enqueue(new DiscoveredHost
                    {
                        Address = endpoint.Address.ToString(),
                        Port = port,
                        Name = parts[3],
                        SeenAtUtc = DateTime.UtcNow,
                    });
                }
                catch (SocketException) when (_stop) { return; }
                catch (ObjectDisposedException) { return; }
                catch (Exception e)
                {
                    Debug.LogWarning($"[LanDiscovery] client error: {e.Message}");
                }
            }
        }

        // --- Android MulticastLock: sem isso o Android descarta broadcast recebido ---

        private void AcquireMulticastLock()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                using var wifiManager = activity.Call<AndroidJavaObject>("getSystemService", "wifi");
                _multicastLock = wifiManager.Call<AndroidJavaObject>("createMulticastLock", "VRLAN_Discovery");
                _multicastLock.Call("setReferenceCounted", true);
                _multicastLock.Call("acquire");
                Debug.Log("[LanDiscovery] Android MulticastLock acquired");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LanDiscovery] MulticastLock acquire failed: {e.Message}");
            }
#endif
        }

        private void ReleaseMulticastLock()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                if (_multicastLock != null)
                {
                    _multicastLock.Call("release");
                    _multicastLock.Dispose();
                    _multicastLock = null;
                    Debug.Log("[LanDiscovery] Android MulticastLock released");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LanDiscovery] MulticastLock release failed: {e.Message}");
            }
#endif
        }
    }

    [Serializable]
    public struct DiscoveredHost
    {
        public string Address;
        public ushort Port;
        public string Name;
        public DateTime SeenAtUtc;
    }
}
