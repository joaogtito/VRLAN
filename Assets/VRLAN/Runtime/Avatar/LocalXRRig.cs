using UnityEngine;

namespace VRLAN.Network.Avatar
{
    // Colocado no XR Origin da cena do Quest (SampleScene).
    // Expõe as transforms locais do rig (cabeça + controles) pro StudentAvatar
    // copiar e sincronizar pela rede. Centraliza as referências do rig num lugar só,
    // facilitando adicionar torso/outras partes depois.
    public class LocalXRRig : MonoBehaviour
    {
        public static LocalXRRig Instance { get; private set; }

        [SerializeField] private Transform head;       // câmera do XR Origin
        [SerializeField] private Transform leftHand;   // Left Controller
        [SerializeField] private Transform rightHand;  // Right Controller

        public Transform Head => head;
        public Transform LeftHand => leftHand;
        public Transform RightHand => rightHand;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[LocalXRRig] já existe uma instância; destruindo a duplicata.");
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // Teleporta o rig pra uma pose de destino (ex.: um TeleportAnchor).
        // Chamado no Quest dono via PlayerTeleporter (comando do professor).
        //
        // ARMADILHA VR: não basta setar a posição do origin — a cabeça tem offset DENTRO do rig.
        // Setar direto enterra o jogador no chão ou desloca lateralmente. O correto é mover o origin
        // de modo que a PROJEÇÃO DA CABEÇA NO CHÃO caia no destino (semântica "fique de pé aqui"),
        // e girar o rig em torno da cabeça pra alinhar o yaw com o forward do destino.
        public void TeleportTo(Vector3 position, Quaternion rotation)
        {
            var origin = transform;

            if (head == null)
            {
                // Sem referência de cabeça: fallback simples (move o origin direto).
                origin.SetPositionAndRotation(position, rotation);
                return;
            }

            // 1) Rotação: alinhar o yaw da cabeça com o do destino, pivotando NA cabeça
            //    (mantém a cabeça parada, só reorienta o rig). Só yaw — up do VR fica intacto.
            float deltaYaw = Mathf.DeltaAngle(head.eulerAngles.y, rotation.eulerAngles.y);
            origin.RotateAround(head.position, Vector3.up, deltaYaw);

            // 2) Translação: levar a projeção da cabeça no chão (y do origin) até o destino.
            Vector3 camFloor = head.position;
            camFloor.y = origin.position.y;
            origin.position += position - camFloor;
        }
    }
}
