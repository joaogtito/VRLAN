using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace VRLAN.Network.Teleport
{
    // Painel do PROFESSOR (host), vive no overlay 2D persistido (TabletScene).
    // Gera um botão por TeleportAnchor presente na SharedWorld (lista dinâmica) e, no clique,
    // teleporta o aluno selecionado pra aquele ponto.
    //
    // Wiring (Inspector): ligar 'container' (pai dos botões) e 'buttonTemplate' (um Button
    // desativado com um TMP_Text filho pro label). O template fica desativado; cópias ativas
    // são instanciadas em runtime.
    //
    // Seleção de aluno: v1 usa o primeiro aluno conectado (mesma lógica do SpectatorView).
    // Pra múltiplos alunos, um seletor futuro chama SetTargetStudent(clientId).
    public class TeleportCommandPanel : MonoBehaviour
    {
        [SerializeField] private Transform container;       // pai onde os botões entram
        [SerializeField] private Button buttonTemplate;     // botão modelo (fica desativado)

        private ulong? _explicitTarget;                     // setado por um seletor futuro
        private readonly List<Button> _spawned = new();

        private void Start()
        {
            if (buttonTemplate != null) buttonTemplate.gameObject.SetActive(false);
            TeleportAnchorRegistry.OnChanged += Rebuild;
            Rebuild();
        }

        private void OnDestroy()
        {
            TeleportAnchorRegistry.OnChanged -= Rebuild;
        }

        // Pro futuro seletor de aluno fixar um alvo específico.
        public void SetTargetStudent(ulong clientId) => _explicitTarget = clientId;
        public void ClearTargetStudent() => _explicitTarget = null;

        private void Rebuild()
        {
            if (container == null || buttonTemplate == null)
            {
                Debug.LogWarning("[TeleportCommandPanel] container/buttonTemplate não ligados.");
                return;
            }

            foreach (var b in _spawned)
                if (b != null) Destroy(b.gameObject);
            _spawned.Clear();

            foreach (var anchor in TeleportAnchorRegistry.Anchors)
            {
                if (anchor == null) continue;
                var btn = Instantiate(buttonTemplate, container);
                btn.gameObject.SetActive(true);

                var label = btn.GetComponentInChildren<TMP_Text>(includeInactive: true);
                if (label != null) label.text = anchor.DisplayLabel;

                var captured = anchor;  // captura por iteração (seguro em foreach C#)
                btn.onClick.AddListener(() => TeleportTargetTo(captured));
                _spawned.Add(btn);
            }
        }

        private void TeleportTargetTo(TeleportAnchor anchor)
        {
            if (anchor == null) return;

            var teleporter = ResolveTargetTeleporter();
            if (teleporter == null)
            {
                Debug.LogWarning("[TeleportCommandPanel] nenhum aluno alvo pra teleportar.");
                return;
            }
            teleporter.RequestTeleport(anchor.Position, anchor.Rotation);
        }

        // Resolve o PlayerTeleporter do aluno alvo (explícito, ou o primeiro não-host).
        private PlayerTeleporter ResolveTargetTeleporter()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer) return null;

            if (_explicitTarget.HasValue &&
                nm.ConnectedClients.TryGetValue(_explicitTarget.Value, out var chosen))
                return TeleporterOf(chosen.PlayerObject);

            foreach (var kvp in nm.ConnectedClients)
            {
                if (kvp.Key == nm.LocalClientId) continue;  // pula o host (professor)
                var tp = TeleporterOf(kvp.Value.PlayerObject);
                if (tp != null) return tp;
            }
            return null;
        }

        private static PlayerTeleporter TeleporterOf(NetworkObject po)
            => po != null ? po.GetComponent<PlayerTeleporter>() : null;
    }
}
