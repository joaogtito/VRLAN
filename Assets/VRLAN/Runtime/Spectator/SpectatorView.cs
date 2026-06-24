using Unity.Netcode;
using UnityEngine;
using VRLAN.Network.Avatar;

namespace VRLAN.Network.Spectator
{
    // No tablet (host): cola uma Camera na pose da CABEÇA do avatar do aluno selecionado,
    // renderizando a visão em 1ª pessoa do aluno na tela do tablet.
    //
    // S2: para 1 aluno, pega automaticamente o primeiro cliente que não é o host.
    // S3 (futuro): SetTarget(clientId) chamado por um menu de seleção de aluno.
    [RequireComponent(typeof(Camera))]
    public class SpectatorView : MonoBehaviour
    {
        [SerializeField] private float searchIntervalSeconds = 0.5f;

        private Transform _target;          // transform da CABEÇA do avatar do aluno
        private ulong _targetClientId;
        private float _nextSearchAt;

        // Permite ao S3 fixar um aluno específico. 0 / não setado = auto (primeiro aluno).
        public void SetTarget(ulong clientId)
        {
            _targetClientId = clientId;
            _target = null;  // força nova busca pro novo alvo
        }

        public void ClearTarget()
        {
            _targetClientId = 0;
            _target = null;
        }

        private void LateUpdate()
        {
            if (_target == null)
            {
                if (Time.time >= _nextSearchAt)
                {
                    _nextSearchAt = Time.time + searchIntervalSeconds;
                    _target = ResolveTarget();
                }
                return;
            }

            // O avatar pode despawnar (aluno saiu) — solta o alvo.
            if (!_target)
            {
                _target = null;
                return;
            }

            transform.SetPositionAndRotation(_target.position, _target.rotation);
        }

        private Transform ResolveTarget()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer) return null;

            // Alvo explícito (S3): busca o avatar daquele clientId.
            if (_targetClientId != 0 &&
                nm.ConnectedClients.TryGetValue(_targetClientId, out var client) &&
                client.PlayerObject != null)
            {
                return HeadOf(client.PlayerObject);
            }

            // Auto (S2, 1 aluno): primeiro cliente que não seja o host local.
            foreach (var kvp in nm.ConnectedClients)
            {
                if (kvp.Key == nm.LocalClientId) continue;  // pula o host
                var po = kvp.Value.PlayerObject;
                if (po != null)
                {
                    Debug.Log($"[SpectatorView] alvo automático = aluno {kvp.Key}");
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
