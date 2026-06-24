using UnityEngine;

namespace VRLAN.Network.Teleport
{
    // PEÇA QUE O DESENVOLVEDOR USA.
    // Coloque este componente num GameObject VAZIO na SharedWorld pra marcar um ponto de
    // teleporte. A posição = onde o aluno fica de pé; o forward (eixo Z azul) = pra onde ele encara.
    //
    // Não precisa de NetworkObject: é só um transform de referência. A SharedWorld é a mesma
    // cena sincronizada em todos os peers, e o host lê a pose deste anchor e manda a pose crua
    // pro Quest do aluno (ver PlayerTeleporter). O integrador só posiciona objetos vazios.
    public class TeleportAnchor : MonoBehaviour
    {
        [Tooltip("Id único usado pra referenciar este ponto (ex.: \"bancada\", \"saida\").")]
        [SerializeField] private string anchorId;

        [Tooltip("Texto mostrado no botão do professor (ex.: \"Bancada\").")]
        [SerializeField] private string displayLabel;

        public string AnchorId => anchorId;
        public string DisplayLabel => string.IsNullOrEmpty(displayLabel) ? anchorId : displayLabel;

        // A pose de destino: posição (chão) + rotação (yaw que o aluno vai encarar).
        public Vector3 Position => transform.position;
        public Quaternion Rotation => transform.rotation;

        private void OnEnable()  => TeleportAnchorRegistry.Register(this);
        private void OnDisable() => TeleportAnchorRegistry.Unregister(this);

        // Gizmo: deixa o ponto e a direção visíveis no editor mesmo sem mesh.
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, 0.25f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 0.6f);
            Gizmos.DrawSphere(transform.position + transform.forward * 0.6f, 0.05f);
        }
    }
}
