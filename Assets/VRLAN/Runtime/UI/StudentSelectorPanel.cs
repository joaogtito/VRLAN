using System;
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

        [Header("Visual da seleção")]
        [Tooltip("Cor de fundo (ColorBlock.normalColor) do aluno selecionado / dos demais.")]
        [SerializeField] private Color selectedColor = new(0.31f, 0.55f, 1f, 1f);
        [SerializeField] private Color normalColor = new(1f, 1f, 1f, 0.06f);
        [Tooltip("Prefixa o selecionado com \"▶ \" (modo antigo; a fonte precisa ter o glifo).")]
        [SerializeField] private bool arrowPrefix = false;

        // Disparado quando o aluno alvo muda (clientId, nome). Ex.: o badge "AO VIVO" do painel.
        public event Action<ulong, string> OnSelectionChanged;
        public string SelectedName { get; private set; } = "";

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
            {
                if (_selected == null && SelectedName.Length > 0)
                {
                    SelectedName = "";
                    OnSelectionChanged?.Invoke(0, "");
                }
                ApplySelectionVisual();
            }
        }

        private void Select(ulong clientId)
        {
            _selected = clientId;
            if (spectator != null) spectator.SetTarget(clientId);
            if (teleportPanel != null) teleportPanel.SetTargetStudent(clientId);
            SelectedName = NameOf(clientId);
            ApplySelectionVisual();
            OnSelectionChanged?.Invoke(clientId, SelectedName);
            Debug.Log($"[StudentSelectorPanel] aluno alvo = {clientId} ({SelectedName})");
        }

        // Destaca o selecionado pela cor de fundo do Button (ColorBlock.normalColor — mantém as
        // transições de toque funcionando). Opcionalmente prefixa com "▶ ".
        private void ApplySelectionVisual()
        {
            foreach (var e in _entries)
            {
                bool isSel = _selected.HasValue && e.ClientId == _selected.Value;
                if (e.Label != null) e.Label.text = isSel && arrowPrefix ? $"▶ {e.Name}" : e.Name;
                if (e.Button != null)
                {
                    var cb = e.Button.colors;
                    cb.normalColor = isSel ? selectedColor : normalColor;
                    cb.selectedColor = cb.normalColor;
                    e.Button.colors = cb;
                }
            }
        }

        private string NameOf(ulong clientId)
        {
            foreach (var e in _entries) if (e.ClientId == clientId) return e.Name;
            return $"Aluno #{clientId}";
        }

        private bool ContainsStudent(ulong clientId)
        {
            foreach (var e in _entries) if (e.ClientId == clientId) return true;
            return false;
        }
    }
}
