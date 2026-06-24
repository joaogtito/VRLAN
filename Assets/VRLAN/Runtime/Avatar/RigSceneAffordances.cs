using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VRLAN.Network.Avatar
{
    // Liga/desliga affordances do rig conforme a cena ativa.
    //
    // Caso de uso 1 (toggle por cena): os Near-Far Interactors desenham um RAY ("LineVisual")
    // necessário na lobby pra mirar no canvas World Space. Ao persistir pra SharedWorld o ray vira
    // poluição → esconder na SharedWorld, reexibir na lobby. (hideInSharedWorld)
    //
    // Caso de uso 2 (refresh do visual): o Teleport Interactor tem o LineRenderer NO PRÓPRIO
    // GameObject. Na troca de cena o estado do line visual fica "preso" (linha vermelha). Um
    // SetActive síncrono cedo demais não resolve (o rig ainda não assentou). Aqui fazemos um
    // ciclo disable→enable→disable ATRASADO (por corrotina) + zeramos a geometria do LineRenderer,
    // terminando em IDLE (off). O teleporte continua funcional: o ControllerInputActionManager
    // religa o interactor no próximo input de TP. (refreshAndIdleOnSharedWorld)
    //
    // Sem dependência do assembly do XRI — só SetActive + LineRenderer (UnityEngine).
    // Coloque no rig persistido (XR Origin).
    public class RigSceneAffordances : MonoBehaviour
    {
        [SerializeField] private string sharedSceneName = "SharedWorld";

        [Tooltip("Toggle por cena: ESCONDE na SharedWorld e REEXIBE nas demais (lobby). " +
                 "Ex.: os 'LineVisual' dos Near-Far Interactors (ray da UI World Space).")]
        [SerializeField] private GameObject[] hideInSharedWorld;

        [Tooltip("Refresh do line visual ao entrar na SharedWorld: ciclo disable→enable→disable " +
                 "atrasado + limpa o LineRenderer, terminando IDLE. Ex.: o 'Teleport Interactor' " +
                 "(GameObject que tem o LineRenderer). Não desativa o TP — o manager religa no input.")]
        [SerializeField] private GameObject[] refreshAndIdleOnSharedWorld;

        [Tooltip("Frames de espera antes do refresh (deixa o rig/manager assentarem após o load).")]
        [SerializeField] private int settleFrames = 2;

        private void OnEnable() => SceneManager.sceneLoaded += HandleSceneLoaded;
        private void OnDisable() => SceneManager.sceneLoaded -= HandleSceneLoaded;

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            bool inShared = scene.name == sharedSceneName;

            foreach (var go in hideInSharedWorld)
                if (go != null) go.SetActive(!inShared);

            if (inShared && isActiveAndEnabled)
                StartCoroutine(RefreshIdle());
        }

        private IEnumerator RefreshIdle()
        {
            // Espera o rig/manager assentarem (o estado preso volta se mexermos cedo demais).
            for (int i = 0; i < Mathf.Max(1, settleFrames); i++)
                yield return null;

            // 1) Garante OFF (estado base).
            SetAll(false);
            yield return null;

            // 2) ON por 1 frame → OnEnable do line visual re-inicializa.
            SetAll(true);
            yield return null;

            // 3) OFF de novo → OnDisable limpa o line visual; e zeramos a geometria do LineRenderer
            //    pra matar qualquer resíduo. Fica IDLE; o manager religa no próximo input de TP.
            ClearLineRenderers();
            SetAll(false);
        }

        private void SetAll(bool active)
        {
            foreach (var go in refreshAndIdleOnSharedWorld)
                if (go != null) go.SetActive(active);
        }

        private void ClearLineRenderers()
        {
            foreach (var go in refreshAndIdleOnSharedWorld)
            {
                if (go == null) continue;
                foreach (var lr in go.GetComponentsInChildren<LineRenderer>(includeInactive: true))
                {
                    lr.positionCount = 0;
                    lr.enabled = false;
                }
            }
        }
    }
}
