using Unity.Netcode;
using UnityEngine;
using VRLAN.Network.Avatar;

namespace VRLAN.Network.Spectator
{
    // No tablet (host): cola uma Camera na pose da CABEÇA do avatar do aluno selecionado,
    // renderizando a visão em 1ª pessoa do aluno na tela do tablet.
    //
    // Sem aluno vinculado (antes de abrir a sala, ou com a sala vazia) a câmera fica PRETA:
    // não renderiza nenhuma layer e limpa a tela com preto. Assim o professor não vê um mundo
    // "vazio" sem sentido — o overlay mostra a mensagem de espera por cima.
    //
    // S2: para 1 aluno, pega automaticamente o primeiro cliente que não é o host.
    // S3: SetTarget(clientId) chamado pelo StudentSelectorPanel.
    [RequireComponent(typeof(Camera))]
    public class SpectatorView : MonoBehaviour
    {
        [SerializeField] private float searchIntervalSeconds = 0.5f;

        [Tooltip("Sem aluno vinculado, a câmera não renderiza nada e mostra esta cor.")]
        [SerializeField] private bool blackoutWithoutTarget = true;
        [SerializeField] private Color blackoutColor = Color.black;

        private Transform _target;          // transform da CABEÇA do avatar do aluno
        private ulong _targetClientId;
        private ulong _currentClientId;     // aluno efetivamente seguido
        private float _nextSearchAt;

        private Camera _cam;
        private int _savedCullingMask;
        private CameraClearFlags _savedClearFlags;
        private Color _savedBackground;
        private bool _blackedOut;

        // Há um aluno sendo mostrado agora?
        public bool HasTarget => _target != null;
        // ClientId do aluno sendo mostrado (válido só se HasTarget).
        public ulong CurrentClientId => _currentClientId;

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            _savedCullingMask = _cam.cullingMask;
            _savedClearFlags = _cam.clearFlags;
            _savedBackground = _cam.backgroundColor;
            ApplyBlackout(true);
        }

        // Permite fixar um aluno específico. 0 / não setado = auto (primeiro aluno).
        public void SetTarget(ulong clientId)
        {
            _targetClientId = clientId;
            _target = null;  // força nova busca pro novo alvo
            _nextSearchAt = 0f;
        }

        public void ClearTarget()
        {
            _targetClientId = 0;
            _target = null;
        }

        private void LateUpdate()
        {
            // O avatar pode despawnar (aluno saiu) — solta o alvo.
            if (_target != null && !_target) _target = null;

            if (_target == null && Time.time >= _nextSearchAt)
            {
                _nextSearchAt = Time.time + searchIntervalSeconds;
                _target = ResolveTarget(out _currentClientId);
            }

            ApplyBlackout(_target == null);
            if (_target == null) return;

            transform.SetPositionAndRotation(_target.position, _target.rotation);
        }

        private void ApplyBlackout(bool on)
        {
            if (!blackoutWithoutTarget || _cam == null) return;
            if (on == _blackedOut) return;
            _blackedOut = on;
            if (on)
            {
                _cam.cullingMask = 0;
                _cam.clearFlags = CameraClearFlags.SolidColor;
                _cam.backgroundColor = blackoutColor;
            }
            else
            {
                _cam.cullingMask = _savedCullingMask;
                _cam.clearFlags = _savedClearFlags;
                _cam.backgroundColor = _savedBackground;
            }
        }

        private Transform ResolveTarget(out ulong clientId)
        {
            clientId = 0;
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer) return null;

            // Alvo explícito: busca o avatar daquele clientId.
            // Se o aluno está conectado mas o avatar ainda não spawnou, ESPERA por ele (retorna null
            // e tenta de novo) — não cai pro automático, senão a câmera trava em outro aluno.
            if (_targetClientId != 0 &&
                nm.ConnectedClients.TryGetValue(_targetClientId, out var client))
            {
                clientId = _targetClientId;
                return client.PlayerObject != null ? HeadOf(client.PlayerObject) : null;
            }

            // Auto (1 aluno): primeiro cliente que não seja o host local.
            foreach (var kvp in nm.ConnectedClients)
            {
                if (kvp.Key == nm.LocalClientId) continue;  // pula o host
                var po = kvp.Value.PlayerObject;
                if (po != null)
                {
                    Debug.Log($"[SpectatorView] alvo automático = aluno {kvp.Key}");
                    clientId = kvp.Key;
                    return HeadOf(po);
                }
            }
            return null;
        }

        // Segue a CABEÇA do avatar (root é só âncora estável). Fallback pro root se faltar.
        private static Transform HeadOf(NetworkObject po)
        {
            var avatar = po.GetComponent<StudentAvatar>();
            return avatar != null && avatar.Head != null ? avatar.Head : po.transform;
        }
    }
}
