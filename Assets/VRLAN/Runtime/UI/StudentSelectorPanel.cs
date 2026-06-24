using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using VRLAN.Network.Spectator;
using VRLAN.Network.Teleport;

namespace VRLAN.Network.UI
{
    // Painel do PROFESSOR (host), no overlay 2D persistido.
    // Lê a lista de alunos do SessionManager e gera um botão por aluno (com o nome).
    // Clicar num aluno o torna o ALVO ATIVO: a câmera espectadora passa a mostrar a visão dele
    // (SpectatorView) e os comandos por-aluno (teleporte) passam a mirá-lo (TeleportCommandPanel).
    //
    // Mantém spectator + comandos em sincronia a partir de UMA fonte de seleção.
    public class StudentSelectorPanel : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private Transform container;      // pai dos botões (VerticalLayoutGroup)
        [SerializeField] private Button buttonTemplate;    // botão modelo com um TMP_Text filho (fica desativado)

        [Header("Alvos da seleção")]
        [SerializeField] private SpectatorView spectator;
        [SerializeField] private TeleportCommandPanel teleportPanel;

        private SessionManager _hookedSession;
        private ulong? _selected;

        private readonly List<Entry> _entries = new();
        private struct Entry { public ulong ClientId; public Button Button; public TMP_Text Label; public string Name; }

        private void Start()
        {
            if (buttonTemplate != null) buttonTemplate.gameObject.SetActive(false);

            var nm = NetworkManager.Singleton;
            if (nm != null)
            {
                nm.OnServerStarted += HandleServerStarted;
                nm.OnClientConnectedCallback += HandleClientConnected;
            }
            TryHookSession();
            Rebuild();
        }

        private void OnDestroy()
        {
            UnhookSession();
            var nm = NetworkManager.Singleton;
            if (nm == null) return;
            nm.OnServerStarted -= HandleServerStarted;
            nm.OnClientConnectedCallback -= HandleClientConnected;
        }

        // --- Hook do SessionManager (spawna depois do server subir; mesmo padrão do ConnectionUI) ---

        private void HandleServerStarted() => TryHookSession();
        private void HandleClientConnected(ulong _) => TryHookSession();

        private void TryHookSession()
        {
            var sm = SessionManager.Instance;
            if (sm == null || sm == _hookedSession) return;
            UnhookSession();
            _hookedSession = sm;
            _hookedSession.OnStudentsChanged += Rebuild;
            Rebuild();
        }

        private void UnhookSession()
        {
            if (_hookedSession == null) return;
            _hookedSession.OnStudentsChanged -= Rebuild;
            _hookedSession = null;
        }

        // --- Construção da lista ---

        private void Rebuild()
        {
            if (container == null || buttonTemplate == null) return;

            foreach (var e in _entries)
                if (e.Button != null) Destroy(e.Button.gameObject);
            _entries.Clear();

            var students = _hookedSession != null ? _hookedSession.StudentsSnapshot : null;
            if (students != null)
            {
                foreach (var s in students)
                {
                    var btn = Instantiate(buttonTemplate, container);
                    btn.gameObject.SetActive(true);
                    var label = btn.GetComponentInChildren<TMP_Text>(includeInactive: true);
                    var name = s.DisplayName.ToString();

                    var id = s.ClientId;  // captura por iteração
                    btn.onClick.AddListener(() => Select(id));
                    _entries.Add(new Entry { ClientId = id, Button = btn, Label = label, Name = name });
                }
            }

            // Mantém a seleção se o aluno ainda existe; senão auto-seleciona o primeiro.
            if (_selected.HasValue && !ContainsStudent(_selected.Value))
                _selected = null;
            if (_selected == null && _entries.Count > 0)
                Select(_entries[0].ClientId);
            else
                ApplySelectionVisual();
        }

        private void Select(ulong clientId)
        {
            _selected = clientId;
            if (spectator != null) spectator.SetTarget(clientId);
            if (teleportPanel != null) teleportPanel.SetTargetStudent(clientId);
            ApplySelectionVisual();
            Debug.Log($"[StudentSelectorPanel] aluno alvo = {clientId}");
        }

        // Marca o selecionado com "▶ " (evita brigar com as transições de cor do Button).
        private void ApplySelectionVisual()
        {
            foreach (var e in _entries)
            {
                if (e.Label == null) continue;
                bool isSel = _selected.HasValue && e.ClientId == _selected.Value;
                e.Label.text = isSel ? $"▶ {e.Name}" : e.Name;
            }
        }

        private bool ContainsStudent(ulong clientId)
        {
            foreach (var e in _entries) if (e.ClientId == clientId) return true;
            return false;
        }
    }
}
