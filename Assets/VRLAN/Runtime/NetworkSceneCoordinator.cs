using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VRLAN.Network
{
    // O HOST (tablet) carrega a cena compartilhada (Single) ao subir o servidor.
    // O CLIENTE (Quest) recebe a cena automaticamente pela sincronização padrão do NGO
    // (Single) ao conectar — não precisa fazer nada aqui.
    //
    // Os objetos específicos de cada device (overlay+spectator no tablet, XR rig no Quest)
    // NÃO ficam na cena compartilhada — eles sobrevivem à troca de cena via PersistAcrossScenes
    // (DontDestroyOnLoad). Assim a cena compartilhada é device-agnostic.
    //
    // Coloque este componente na cena-boot de CADA device (TabletScene e QuestLobby),
    // com o mesmo sharedSceneName. Fica no GameObject do NetworkManager (que persiste).
    //
    // Volta pra lobby (cliente): quando o cliente cai (host parou, Wi-Fi caiu, Disconnect),
    // a UI de conexão do Quest já foi destruída junto com a lobby. Sem isso o aluno fica preso
    // num mundo morto. Aqui recarregamos a cena-boot e destruímos este NetworkManager persistido,
    // pra que o NetworkManager novo da lobby assuma como Singleton (NGO não deduplica sozinho).
    public class NetworkSceneCoordinator : MonoBehaviour
    {
        [SerializeField] private string sharedSceneName = "SharedWorld";

        [Tooltip("Cliente: ao desconectar estando na SharedWorld, recarrega a cena-boot (lobby).")]
        [SerializeField] private bool returnToBootSceneOnClientDisconnect = true;

        private string _bootSceneName;
        private bool _pendingReturn;

        private void Start()
        {
            _bootSceneName = gameObject.scene.name == "DontDestroyOnLoad"
                ? SceneManager.GetActiveScene().name
                : gameObject.scene.name;

            var nm = NetworkManager.Singleton;
            if (nm == null) { Debug.LogError("[SceneCoordinator] sem NetworkManager na cena"); return; }
            nm.OnServerStarted += HandleServerStarted;
            nm.OnClientStopped += HandleClientStopped;
        }

        private void OnDestroy()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return;
            nm.OnServerStarted -= HandleServerStarted;
            nm.OnClientStopped -= HandleClientStopped;
        }

        private void HandleServerStarted()
        {
            var nm = NetworkManager.Singleton;
            var status = nm.SceneManager.LoadScene(sharedSceneName, LoadSceneMode.Single);
            Debug.Log($"[SceneCoordinator] HOST LoadScene('{sharedSceneName}', Single) -> {status}");
        }

        private void HandleClientStopped(bool wasHost)
        {
            // Host (tablet) fica onde está: pode subir de novo e a SharedWorld recarrega.
            if (wasHost || !returnToBootSceneOnClientDisconnect) return;
            if (SceneManager.GetActiveScene().name != sharedSceneName) return;  // ainda na lobby: a UI trata
            _pendingReturn = true;  // processa no Update, fora da pilha de shutdown do NGO
        }

        private void Update()
        {
            if (!_pendingReturn) return;
            _pendingReturn = false;

            if (string.IsNullOrEmpty(_bootSceneName))
            {
                Debug.LogWarning("[SceneCoordinator] cena-boot desconhecida — não dá pra voltar pra lobby.");
                return;
            }

            Debug.Log($"[SceneCoordinator] cliente desconectado → voltando pra '{_bootSceneName}'");
            SceneManager.LoadScene(_bootSceneName, LoadSceneMode.Single);

            // A lobby traz um NetworkManager novo; este (persistido) sai pra não duplicar.
            // Destroy roda no fim do frame, antes da lobby carregar → o novo vira Singleton.
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.gameObject == gameObject) Destroy(gameObject);
            else if (nm != null) Destroy(nm.gameObject);
        }
    }
}
