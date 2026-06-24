using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace VRLAN.Network.Avatar
{
    // Avatar de rede de um aluno. Spawnado como PlayerPrefab pelo NGO (1 por cliente).
    //
    // Rig DESACOPLADO:
    //   - Root = âncora ESTÁVEL na origem (o script NÃO o move). Isso evita acoplar as mãos
    //     à rotação da cabeça (que causava a mão orbitar no eixo da cabeça).
    //   - head / leftHand / rightHand = filhos, cada um com seu NetworkTransform (Owner),
    //     sincronizando a própria pose de mundo de forma INDEPENDENTE.
    //
    // No Quest do aluno DONO, copia as poses do LocalXRRig (cabeça + controles) a cada LateUpdate.
    // A câmera espectadora do tablet segue o transform Head (exposto abaixo), não o root.
    public class StudentAvatar : NetworkBehaviour
    {
        [SerializeField] private Transform head;       // filho com NetworkTransform
        [SerializeField] private Transform leftHand;   // filho com NetworkTransform
        [SerializeField] private Transform rightHand;  // filho com NetworkTransform

        // A câmera espectadora segue daqui (o root é só âncora estável).
        public Transform Head => head;

        private Transform _headSource;  // rig.Head ou Camera.main (fonte local da pose de cabeça)
        private LocalXRRig _rig;
        private bool _isLocalStudent;

        public override void OnNetworkSpawn()
        {
            _isLocalStudent = IsOwner && !IsServer;

            if (_isLocalStudent)
            {
                _rig = LocalXRRig.Instance;
                _headSource = _rig != null && _rig.Head != null
                    ? _rig.Head
                    : (Camera.main != null ? Camera.main.transform : null);

                if (_rig == null)
                    Debug.LogWarning("[StudentAvatar] LocalXRRig.Instance nulo — mãos não serão sincronizadas. " +
                                     "Adicione LocalXRRig no XR Origin e ligue as referências.");
                if (_headSource == null)
                    Debug.LogWarning("[StudentAvatar] sem fonte de pose de cabeça (rig.Head e Camera.main nulos).");

                // 1ª pessoa: esconde os visuais do próprio avatar (mãos duplicadas).
                SetOwnVisualsHidden();
            }

            Debug.Log($"[StudentAvatar] spawn owner={OwnerClientId} IsOwner={IsOwner} IsServer={IsServer}");
        }

        private void LateUpdate()
        {
            // LateUpdate: TrackedPoseDriver já atualizou as poses XR neste frame.
            // O root NÃO é tocado — fica âncora estável na origem, desacoplando as partes.
            if (!_isLocalStudent) return;

            if (head != null && _headSource != null)
                head.SetPositionAndRotation(_headSource.position, _headSource.rotation);

            if (_rig != null)
            {
                if (leftHand != null && _rig.LeftHand != null)
                    leftHand.SetPositionAndRotation(_rig.LeftHand.position, _rig.LeftHand.rotation);
                if (rightHand != null && _rig.RightHand != null)
                    rightHand.SetPositionAndRotation(_rig.RightHand.position, _rig.RightHand.rotation);
            }
        }

        private void SetOwnVisualsHidden()
        {
            var renderers = new List<Renderer>();
            GetComponentsInChildren(includeInactive: true, renderers);
            foreach (var r in renderers) r.enabled = false;
        }
    }
}
