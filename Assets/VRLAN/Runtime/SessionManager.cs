using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace VRLAN.Network
{
    // Raiz da sessão de rede. Spawnada pelo NetworkSessionSpawner no host.
    // Fonte única de verdade do domínio "sessão":
    //  - Lista de alunos conectados (com nome) — NetworkList sincronizada
    //  - Estado da atividade (SessionStep, placeholder por enquanto)
    //
    // H3: esqueleto inicial.
    // H4: identidade de aluno via RPC + NetworkList<StudentInfo>.
    public class SessionManager : NetworkBehaviour
    {
        public static SessionManager Instance { get; private set; }

        // Nome local do aluno enviado ao servidor no register. Default VAZIO → o servidor
        // aplica "Aluno #<id>". Sobrescrito antes do connect pela tela de nome (QuestConnectionUI).
        // ⚠️ NÃO inicializar com SystemInfo.deviceName aqui: chamar SystemInfo em field initializer
        //    estático lança UnityException ("not allowed from a MonoBehaviour constructor...").
        //    Se quiser o nome do device como default, resolver em Awake/Start de um componente.
        public static string LocalStudentName { get; set; } = "";

        // Estado da sessão. Placeholder em H3/H4. Servidor escreve.
        public NetworkVariable<int> SessionStep = new(
            writePerm: NetworkVariableWritePermission.Server);

        // Lista autoritativa de alunos (clientes remotos — não inclui o host).
        // Servidor escreve via RPC de registro; cliente lê (sincronizado).
        public NetworkList<StudentInfo> Students = new();

        // Dispara em ambos os lados sempre que a lista de alunos muda
        // (registrou, desconectou, etc.). UIs leem daqui em vez de eventos crus do NetworkManager.
        public event Action OnStudentsChanged;

        public override void OnNetworkSpawn()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[SessionManager] já existe uma instância; destruindo esta.");
                if (IsServer) NetworkObject.Despawn(); else Destroy(gameObject);
                return;
            }
            Instance = this;

            Students.OnListChanged += HandleStudentsListChanged;
            SessionStep.OnValueChanged += HandleSessionStepChanged;

            if (IsServer)
            {
                // Limpa registro quando um cliente cai.
                NetworkManager.OnClientDisconnectCallback += HandleClientDisconnectedServer;
            }

            // Cliente remoto se registra no host. Host (que também é client local)
            // não se registra — host = professor, não aluno.
            if (IsClient && !IsServer)
            {
                RegisterStudentServerRpc(new FixedString64Bytes(LocalStudentName ?? ""));
            }

            Debug.Log($"[SessionManager] spawned (IsServer={IsServer}, IsClient={IsClient})");
        }

        public override void OnNetworkDespawn()
        {
            if (Students != null) Students.OnListChanged -= HandleStudentsListChanged;
            SessionStep.OnValueChanged -= HandleSessionStepChanged;

            if (IsServer && NetworkManager != null)
                NetworkManager.OnClientDisconnectCallback -= HandleClientDisconnectedServer;

            if (Instance == this) Instance = null;
        }

        // --- API pública pra UIs ---

        // Snapshot dos alunos atuais. Útil pra UI rebuilds sob demanda.
        public IReadOnlyList<StudentInfo> StudentsSnapshot
        {
            get
            {
                var list = new List<StudentInfo>(Students?.Count ?? 0);
                if (Students != null) foreach (var s in Students) list.Add(s);
                return list;
            }
        }

        // Avança o "passo" da sessão. Só servidor. UI do host chama.
        public void AdvanceStep()
        {
            if (!IsServer) { Debug.LogWarning("[SessionManager] AdvanceStep só no servidor."); return; }
            SessionStep.Value++;
        }

        // --- RPC: cliente → servidor ---

#if VRLAN_NGO2
        [Rpc(SendTo.Server)]
        private void RegisterStudentServerRpc(FixedString64Bytes name, RpcParams rpcParams = default)
#else
        [ServerRpc(RequireOwnership = false)]
        private void RegisterStudentServerRpc(FixedString64Bytes name, ServerRpcParams rpcParams = default)
#endif
        {
            var clientId = rpcParams.Receive.SenderClientId;
            var displayName = name.IsEmpty ? new FixedString64Bytes($"Aluno #{clientId}") : name;

            // Substitui se já existe (caso raro de re-registro).
            RemoveStudentByClientId(clientId);

            Students.Add(new StudentInfo { ClientId = clientId, DisplayName = displayName });
            Debug.Log($"[SessionManager] student registered: clientId={clientId} name='{displayName}'");
        }

        // --- Internals ---

        private void HandleClientDisconnectedServer(ulong clientId)
        {
            RemoveStudentByClientId(clientId);
        }

        private void RemoveStudentByClientId(ulong clientId)
        {
            if (Students == null) return;
            for (var i = Students.Count - 1; i >= 0; i--)
            {
                if (Students[i].ClientId == clientId) Students.RemoveAt(i);
            }
        }

        private void HandleStudentsListChanged(NetworkListEvent<StudentInfo> e)
        {
            Debug.Log($"[SessionManager] students changed ({e.Type}), count={Students.Count} (IsServer={IsServer})");
            OnStudentsChanged?.Invoke();
        }

        private void HandleSessionStepChanged(int oldValue, int newValue)
        {
            Debug.Log($"[SessionManager] step {oldValue} -> {newValue} (IsServer={IsServer})");
        }
    }

    // Entry da lista de alunos. Precisa ser struct unmanaged + INetworkSerializable + IEquatable
    // pra funcionar dentro de NetworkList<T>.
    public struct StudentInfo : INetworkSerializable, IEquatable<StudentInfo>
    {
        public ulong ClientId;
        public FixedString64Bytes DisplayName;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ClientId);
            serializer.SerializeValue(ref DisplayName);
        }

        public bool Equals(StudentInfo other) =>
            ClientId == other.ClientId && DisplayName.Equals(other.DisplayName);

        public override bool Equals(object obj) => obj is StudentInfo other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(ClientId, DisplayName);
    }
}
