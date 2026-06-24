using System.Collections.Generic;
using UnityEngine;

namespace VRLAN.Network
{
    // Mantém o objeto vivo quando o NGO troca pra cena compartilhada (Single load).
    // Use no que é específico do device e precisa atravessar a troca de cena:
    //   - Quest: o XR rig (XR Origin).
    //   - Tablet: o conjunto overlay + câmera espectadora.
    //
    // Tem que estar num objeto RAIZ (DontDestroyOnLoad só funciona em raiz).
    public class PersistAcrossScenes : MonoBehaviour
    {
        // Evita duplicata se a cena-boot recarregar (ex.: reconexão).
        [SerializeField] private string singletonKey;

        private static readonly HashSet<string> s_Live = new();
        private bool _owns;

        private void Awake()
        {
            if (transform.parent != null)
                Debug.LogWarning($"[PersistAcrossScenes] '{name}' não é raiz — mova pra raiz, senão DontDestroyOnLoad não funciona.");

            if (!string.IsNullOrEmpty(singletonKey))
            {
                if (s_Live.Contains(singletonKey)) { Destroy(gameObject); return; }
                s_Live.Add(singletonKey);
                _owns = true;
            }

            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (_owns && !string.IsNullOrEmpty(singletonKey)) s_Live.Remove(singletonKey);
        }
    }
}
