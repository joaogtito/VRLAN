using Unity.Netcode;
using UnityEngine;

namespace VRLAN.Network.Interaction
{
    // Objeto interativo em rede. Vai num prefab com NetworkObject + NetworkTransform (Owner)
    // + XR Grab Interactable (XRI) + Rigidbody + Collider.
    //
    // Conceito de autoridade: o NetworkTransform é AuthorityMode=Owner. Quem é DONO controla a pose.
    // O objeto nasce com o host como dono. Quando o ALUNO pega (grab), ele pede a posse ao servidor;
    // virando dono, a movimentação dele (via XRI) passa a replicar pro host/tablet.
    //
    // Wiring (Inspector): no XR Grab Interactable → Interactable Events →
    //   Select Entered  → NetworkGrabbable.OnGrabbed
    //   Select Exited   → NetworkGrabbable.OnReleased   (opcional)
    // Métodos sem argumento de propósito: o UnityEvent do XRI consegue chamá-los sem referenciar tipos do XRI
    // (assim este script não precisa depender do assembly do XR Interaction Toolkit).
    [RequireComponent(typeof(NetworkObject))]
    public class NetworkGrabbable : NetworkBehaviour
    {
        public void OnGrabbed()
        {
            // Já sou dono? nada a fazer. Senão, peço a posse pro servidor.
            if (IsOwner) return;
            RequestOwnershipServerRpc();
        }

        public void OnReleased()
        {
            // Mantém a posse com quem pegou por último (objeto fica onde foi solto, sincronizado).
            // Se quiser devolver a posse ao servidor ao soltar, descomente:
            // ReturnOwnershipServerRpc();
        }

        [Rpc(SendTo.Server)]
        private void RequestOwnershipServerRpc(RpcParams rpcParams = default)
        {
            var sender = rpcParams.Receive.SenderClientId;
            if (OwnerClientId != sender)
            {
                NetworkObject.ChangeOwnership(sender);
                Debug.Log($"[NetworkGrabbable] {name}: posse -> cliente {sender}");
            }
        }

        [Rpc(SendTo.Server)]
        private void ReturnOwnershipServerRpc(RpcParams rpcParams = default)
        {
            NetworkObject.RemoveOwnership();  // volta pro servidor
            Debug.Log($"[NetworkGrabbable] {name}: posse devolvida ao servidor");
        }
    }
}
