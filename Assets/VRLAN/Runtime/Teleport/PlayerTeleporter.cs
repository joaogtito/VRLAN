using Unity.Netcode;
using UnityEngine;
using VRLAN.Network.Avatar;

namespace VRLAN.Network.Teleport
{
    // Conduíte de teleporte POR ALUNO. Adicione ao prefab StudentAvatar (que já é o objeto
    // networkado, owned pelo cliente). O professor (servidor) chama RequestTeleport; o RPC
    // SendTo.Owner é roteado pelo NGO pra EXATAMENTE o Quest dono deste avatar, que então
    // move o rig LOCAL (LocalXRRig). O avatar segue o rig e o NetworkTransform replica o
    // resultado pro spectator e demais peers.
    [RequireComponent(typeof(NetworkObject))]
    public class PlayerTeleporter : NetworkBehaviour
    {
        // --- API do servidor (professor) ---
        // Roda no host; dispara o RPC pro dono do avatar (o aluno).
        public void RequestTeleport(Vector3 position, Quaternion rotation)
        {
            if (!IsServer)
            {
                Debug.LogWarning("[PlayerTeleporter] RequestTeleport chamado fora do servidor — ignorado.");
                return;
            }
            ApplyTeleportRpc(position, rotation);
        }

        [Rpc(SendTo.Owner)]
        private void ApplyTeleportRpc(Vector3 position, Quaternion rotation)
        {
            var rig = LocalXRRig.Instance;
            if (rig == null)
            {
                Debug.LogWarning("[PlayerTeleporter] sem LocalXRRig no Quest — teleporte ignorado.");
                return;
            }
            rig.TeleportTo(position, rotation);
            Debug.Log($"[PlayerTeleporter] aluno {OwnerClientId} teleportado pra {position}.");
        }
    }
}
