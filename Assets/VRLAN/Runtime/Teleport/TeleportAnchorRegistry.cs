using System;
using System.Collections.Generic;
using UnityEngine;

namespace VRLAN.Network.Teleport
{
    // Registry estático dos pontos de teleporte presentes na cena.
    // Os TeleportAnchor se registram sozinhos no OnEnable (quando a SharedWorld carrega) e
    // saem no OnDisable. O painel do professor (TeleportCommandPanel) enumera daqui e reage
    // ao evento OnChanged pra (re)construir os botões.
    //
    // É estático de propósito: sobrevive à troca de cena (Single load) e absorve o timing —
    // os anchors aparecem quando a SharedWorld é carregada, não no boot.
    public static class TeleportAnchorRegistry
    {
        private static readonly Dictionary<string, TeleportAnchor> _anchors = new();

        // Disparado sempre que um anchor entra/sai. O painel se inscreve pra reconstruir a lista.
        public static event Action OnChanged;

        public static IReadOnlyCollection<TeleportAnchor> Anchors => _anchors.Values;

        public static void Register(TeleportAnchor anchor)
        {
            if (anchor == null) return;

            var id = anchor.AnchorId;
            if (string.IsNullOrWhiteSpace(id))
            {
                Debug.LogWarning($"[TeleportAnchorRegistry] '{anchor.name}' tem anchorId vazio — ignorado.");
                return;
            }
            if (_anchors.TryGetValue(id, out var existing) && existing != anchor)
            {
                Debug.LogWarning($"[TeleportAnchorRegistry] anchorId duplicado '{id}' " +
                                 $"('{anchor.name}' colide com '{existing.name}') — ignorado.");
                return;
            }

            _anchors[id] = anchor;
            OnChanged?.Invoke();
        }

        public static void Unregister(TeleportAnchor anchor)
        {
            if (anchor == null) return;
            var id = anchor.AnchorId;
            if (string.IsNullOrEmpty(id)) return;
            if (_anchors.TryGetValue(id, out var current) && current == anchor)
            {
                _anchors.Remove(id);
                OnChanged?.Invoke();
            }
        }

        public static bool TryGet(string anchorId, out TeleportAnchor anchor)
            => _anchors.TryGetValue(anchorId ?? string.Empty, out anchor);
    }
}
