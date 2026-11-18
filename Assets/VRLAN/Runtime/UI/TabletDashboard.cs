using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using VRLAN.Network.Discovery;
using VRLAN.Network.Spectator;

namespace VRLAN.Network.UI
{
    // Orquestra os ESTADOS visuais do painel do professor (tablet), por cima da câmera espectadora:
    //
    //   Setup   → sala fechada: cartão "Abrir sala" (nome da sala + botão).
    //   Waiting → sala aberta, nenhum óculos vinculado: tela preta + "Aguardando óculos".
    //   Live    → um aluno sendo mostrado: barras laterais (alunos / teleporte) + badge "AO VIVO".
    //
    // A barra superior (nome da sala, status, encerrar) aparece em Waiting e Live.
    // Não faz rede: só LÊ o estado (NetworkManager, SpectatorView, SessionManager) e liga/desliga
    // grupos. A lógica de conexão continua no ConnectionUI; a seleção no StudentSelectorPanel.
    public class TabletDashboard : MonoBehaviour
    {
        public enum State { Setup, Waiting, Live }

        [Header("Fontes de estado")]
        [SerializeField] private SpectatorView spectator;
        [SerializeField] private LanDiscoveryHostBridge discoveryBridge;   // nome da sala

        [Header("Grupos (liga/desliga por estado)")]
        [SerializeField] private GameObject setupGroup;
        [SerializeField] private GameObject waitingGroup;
        [SerializeField] private GameObject liveGroup;
        [SerializeField] private GameObject topBar;

        [Header("Textos")]
        [SerializeField] private TMP_Text roomNameText;     // barra superior
        [SerializeField] private TMP_Text waitingHintText;  // "Conecte o óculos à sala X"
        [SerializeField] private TMP_Text liveBadgeText;    // "AO VIVO · Nome"

        [Header("Animação de espera (opcional)")]
        [SerializeField] private Graphic pulse;             // anel/ponto que pulsa no estado Waiting
        [SerializeField] private float pulseSpeed = 1.6f;

        public State Current { get; private set; } = State.Setup;

        private float _nextTextRefresh;
        private bool _initialized;

        private void Start() => Apply(Evaluate(), force: true);

        private void Update()
        {
            var s = Evaluate();
            if (s != Current || !_initialized) Apply(s, force: !_initialized);

            if (Time.unscaledTime >= _nextTextRefresh)
            {
                _nextTextRefresh = Time.unscaledTime + 0.25f;
                RefreshTexts();
            }

            if (pulse != null && Current == State.Waiting)
            {
                float t = (Mathf.Sin(Time.unscaledTime * pulseSpeed * Mathf.PI) + 1f) * 0.5f;
                var c = pulse.color; c.a = Mathf.Lerp(0.25f, 1f, t); pulse.color = c;
                pulse.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.92f, 1.08f, t);
            }
        }

        private State Evaluate()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer) return State.Setup;
            return spectator != null && spectator.HasTarget ? State.Live : State.Waiting;
        }

        private void Apply(State s, bool force)
        {
            if (!force && s == Current) return;
            Current = s;
            _initialized = true;
            SetActive(setupGroup, s == State.Setup);
            SetActive(waitingGroup, s == State.Waiting);
            SetActive(liveGroup, s == State.Live);
            SetActive(topBar, s != State.Setup);
            RefreshTexts();
        }

        private void RefreshTexts()
        {
            var room = discoveryBridge != null && !string.IsNullOrWhiteSpace(discoveryBridge.HostName)
                ? discoveryBridge.HostName : "VRLAN";

            if (roomNameText != null) roomNameText.text = room;
            if (waitingHintText != null)
                waitingHintText.text = $"Peça para o aluno colocar o Meta Quest, abrir o VRLAN e entrar na sala <b>{room}</b>.";
            if (liveBadgeText != null && Current == State.Live)
                liveBadgeText.text = $"AO VIVO  ·  {StudentName(spectator.CurrentClientId)}";
        }

        private static string StudentName(ulong clientId)
        {
            var sm = SessionManager.Instance;
            if (sm != null)
                foreach (var st in sm.StudentsSnapshot)
                    if (st.ClientId == clientId) return st.DisplayName.ToString();
            return $"Aluno #{clientId}";
        }

        private static void SetActive(GameObject go, bool on)
        {
            if (go != null && go.activeSelf != on) go.SetActive(on);
        }
    }
}
