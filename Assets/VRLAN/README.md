# VRLAN — camada modular de VR multiplayer em LAN

Camada modular que entra **por cima** de um projeto VR existente para: (a) o **professor** ver no tablet a visão do **aluno** no mundo compartilhado, (b) **selecionar** alunos, (c) **teleportar** o aluno para pontos pré-setados e (d) **ativar/desativar** objetos. Tudo em **rede local, offline**.

- **Tablet = Host = Professor** (overlay 2D + câmera espectadora).
- **Quest = Client = Aluno** (rig VR).
- Rede: Netcode for GameObjects (NGO), **Scene Management ON**.

> O código não depende do assembly do XRI nem do Meta SDK (asmdef `VRLAN.Network` só referencia Netcode/TMP/UI/Transport/Collections). A integração é **wiring de cena/prefab**, não reescrita. Funciona com XRI **ou** Meta SDK (ver §Integração).

---

## Estrutura do pacote

```
Assets/VRLAN/
  Runtime/      → código (asmdef VRLAN.Network + submódulos)
  Prefabs/      → StudentAvatar, SessionManager, HostListItem, DefaultNetworkPrefabs
  Materials/    → materiais usados pelos prefabs (ex.: Verde)
  Samples/
    Scenes/     → TabletScene Teste, QuestLobby, SharedWorld (exemplo funcional)
  README.md
```

Exportar: selecionar `Assets/VRLAN/` no Project → botão direito → **Export Package** (`.unitypackage`).

---

## Conceito central: PERSIST + Single sync

O NGO gerencia só a **SharedWorld** (Single sync). O que é específico de cada device **não** vai na SharedWorld — sobrevive à troca de cena via `PersistAcrossScenes` (DontDestroyOnLoad):
- **Quest:** o XR rig (cabeça/mãos).
- **Tablet:** câmera espectadora + Canvas overlay + EventSystem.

> ⚠️ Regra de ouro: tudo que um device precisa pra funcionar (renderizar, receber input, interagir) **tem que persistir junto**.

---

## Componentes-chave (Runtime)

| Submódulo | Peças | Papel |
|---|---|---|
| `Discovery` | LanDiscovery, LanDiscoveryHostBridge | Descoberta de host por UDP broadcast em LAN. |
| (raiz) | SessionManager, NetworkSessionSpawner | Lista de alunos (nome+id), estado da sessão. |
| (raiz) | NetworkSceneCoordinator, PersistAcrossScenes | Host carrega a SharedWorld (Single); device-stuff persiste. |
| `Avatar` | LocalXRRig, StudentAvatar, InteractorReRegister, RigSceneAffordances | Rig local → avatar networkado; fixes de interactor. |
| `Spectator` | SpectatorView | Câmera do tablet segue a cabeça do avatar alvo. |
| `Interaction` | NetworkGrabbable, NetworkedToggle | Agarrar (posse) e ligar/desligar objetos. |
| `Teleport` | TeleportAnchor, TeleportAnchorRegistry, PlayerTeleporter, TeleportCommandPanel | Pontos de teleporte + comando do professor. |
| `UI` | ConnectionUI, QuestConnectionUI, StudentSelectorPanel, SystemKeyboardInput | Overlay do host, UI do Quest, seletor de aluno, teclado. |

**Conexão do Quest:** dois modos no `QuestConnectionUI` — (1) **lista** (escolhe o host) ou IP **manual**; (2) **`autoConnectFirstHost`** (flag): conecta sozinho no primeiro host encontrado, sem interação.

---

## Integração num projeto novo

1. Importar o `.unitypackage` (ou copiar `Assets/VRLAN/`).
2. **NetworkManager:** Scene Management **ON**, PlayerPrefab=`StudentAvatar`, `DefaultNetworkPrefabs` com SessionManager+StudentAvatar.
3. **A cena VR do projeto vira a `SharedWorld`** (device-agnostic): tirar dela NetworkManager/câmera/rig/UI; adicionar `NetworkObject` nos objetos in-scene; espalhar `TeleportAnchor` (objetos vazios) nos pontos de teleporte.
4. Usar as shells de exemplo (`Samples/Scenes`): `TabletScene Teste` (host) e `QuestLobby` (aluno), ajustando o rig ao seu SDK.

### Rig — XRI (padrão)
`LocalXRRig` no XR Origin (head=câmera, hands=controllers) + `PersistAcrossScenes` + `InteractorReRegister` + `RigSceneAffordances`.

### Rig — Meta SDK
`LocalXRRig` na raiz do **OVRCameraRig** → head=`CenterEyeAnchor`, hands=`Left/RightHandAnchor`. `PersistAcrossScenes`. UI World Space da lobby usa o **pointer do Interaction SDK** (RayInteractor + PointableCanvas) no lugar do XR UI Input Module. Grab: ligar o evento de grab do Meta → `NetworkGrabbable.OnGrabbed`. `InteractorReRegister` é fix do XRI — dispensável no Meta. `RigSceneAffordances` é genérico (SetActive/LineRenderer) — apontar pros visuais do Meta.

> O `StudentAvatar`, `SpectatorView`, teleporte, discovery, sessão e seletor de aluno são **agnósticos** — não mudam entre XRI e Meta.

### Pré-requisitos Android (Quest)
Manifest: `oculus.software.overlay_keyboard` + `com.oculus.permission.USE_VR_SYST_KEYBOARD` (teclado de nome) e permissões de rede (INTERNET, ACCESS_WIFI_STATE, CHANGE_WIFI_MULTICAST_STATE).

---

## Limpeza pendente (antes de publicar)
Remover do Runtime: `RoleManager` (sem uso), `_Sandbox/`, `TestObjectSpawner` (scaffolding).
