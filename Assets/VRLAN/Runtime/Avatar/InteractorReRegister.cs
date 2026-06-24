using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VRLAN.Network.Avatar
{
    // Resolve o bug do XRI: quando o rig persiste (DontDestroyOnLoad) e a cena com os
    // interactables carrega DEPOIS, os interactors não registram os objetos novos até
    // serem re-inicializados (o que o jogador faz manualmente perdendo/recuperando o tracking).
    //
    // Este script automatiza: quando a cena compartilhada carrega, dá um ciclo
    // disable→enable nos interactors, forçando o re-registro no Interaction Manager.
    //
    // Coloque no rig persistido (XR Origin) e ligue os GameObjects dos interactors
    // (os controllers / Near-Far Interactors). Sem dependência de NGO nem do assembly do XRI.
    public class InteractorReRegister : MonoBehaviour
    {
        [SerializeField] private string sharedSceneName = "SharedWorld";
        [SerializeField] private GameObject[] interactors;  // GameObjects dos interactors (controllers)

        private void OnEnable() => SceneManager.sceneLoaded += HandleSceneLoaded;
        private void OnDisable() => SceneManager.sceneLoaded -= HandleSceneLoaded;

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == sharedSceneName)
                StartCoroutine(ReRegister());
        }

        private IEnumerator ReRegister()
        {
            yield return null;  // deixa a cena assentar 1 frame

            foreach (var go in interactors)
                if (go != null) go.SetActive(false);

            yield return null;  // 1 frame desligado → dispara OnDisable (unregister)

            foreach (var go in interactors)
                if (go != null) go.SetActive(true);  // OnEnable → re-registra + redetecta os interactables
        }
    }
}
