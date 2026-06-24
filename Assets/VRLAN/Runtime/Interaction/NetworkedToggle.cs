using Unity.Netcode;
using UnityEngine;

namespace VRLAN.Network.Interaction
{
    // Liga/desliga um objeto pela rede. Componente de COMANDO reutilizável do VRLAN:
    // o professor (servidor) chama SetOn/Toggle; o estado é um NetworkVariable que replica
    // pra todos e dirige um GameObject alvo (visual/collider).
    //
    // IMPORTANTE: o "target" deve ser um FILHO/objeto à parte, NUNCA o próprio GameObject do
    // NetworkObject — desativar o GameObject de um NetworkObject quebra o networking.
    public class NetworkedToggle : NetworkBehaviour
    {
        [SerializeField] private GameObject target;   // o que liga/desliga
        [SerializeField] private bool startOn = true;

        public NetworkVariable<bool> IsOn = new(
            value: true, writePerm: NetworkVariableWritePermission.Server);

        public override void OnNetworkSpawn()
        {
            IsOn.OnValueChanged += HandleChanged;
            if (IsServer) IsOn.Value = startOn;  // servidor define o estado inicial; clientes leem o sincronizado
            Apply(IsOn.Value);
        }

        public override void OnNetworkDespawn()
        {
            IsOn.OnValueChanged -= HandleChanged;
        }

        // --- API do servidor (professor) ---
        public void SetOn(bool on) { if (IsServer) IsOn.Value = on; }
        public void Toggle()       { if (IsServer) IsOn.Value = !IsOn.Value; }

        private void HandleChanged(bool _, bool now) => Apply(now);

        private void Apply(bool on)
        {
            if (target != null) target.SetActive(on);
            Debug.Log($"[NetworkedToggle] {name}: {(on ? "ON" : "OFF")} (IsServer={IsServer})");
        }
    }
}
