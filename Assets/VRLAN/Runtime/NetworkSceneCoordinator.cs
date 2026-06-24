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
    // com o mesmo sharedSceneName.
    public class NetworkSceneCoordinator : MonoBehaviour
    {
        [SerializeField] private string sharedSceneName = "SharedWorld";

        private void Start()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) { Debug.LogError("[SceneCoordinator] sem NetworkManager na cena"); return; }
            nm.OnServerStarted += HandleServerStarted;
        }

        private void OnDestroy()
        {
            var nm = NetworkManager.Singleton;
            if (nm != null) nm.OnServerStarted -= HandleServerStarted;
        }

        private void HandleServerStarted()
        {
            var nm = NetworkManager.Singleton;
            var status = nm.SceneManager.LoadScene(sharedSceneName, LoadSceneMode.Single);
            Debug.Log($"[SceneCoordinator] HOST LoadScene('{sharedSceneName}', Single) -> {status}");
        }
    }
}
