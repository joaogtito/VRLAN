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

> **Regra de ouro:** tudo que um device precisa pra funcionar (renderizar, receber input, interagir) **tem que persistir junto**.

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

## Interface e sala de espera (geradas por código)

As UIs das cenas de exemplo e a sala de espera do Quest são montadas pela ferramenta `Assets/VRLAN/Editor/`:

- **VRLAN → Rebuild Sample UI + Lobby:** remonta o painel do professor (`TabletScene Teste`), a UI de conexão do Quest e a sala "coffee break" (`QuestLobby`), e o `HostListItem.prefab`, religando todos os campos. Para mudar o visual, altere o `VRLANUIKit` (tema) ou o `VRLANSceneBuilder` e rode de novo; não edite essas UIs à mão na cena.
- **VRLAN → Capture Previews:** gera PNGs das telas sem entrar em Play Mode.
- **Painel do professor (`TabletDashboard`):** sala fechada mostra o cartão "Abrir sala"; com a sala aberta e nenhum óculos, a tela fica preta com "Aguardando óculos"; com um aluno, mostra a visão dele com as listas de alunos e de teleporte.

Créditos: móveis do Kenney Furniture Kit (CC0), fonte Poppins (SIL OFL 1.1). As licenças estão junto dos arquivos.

## Comportamentos automáticos

- **Avatar do host escondido:** o NGO cria um PlayerObject também para o tablet. O `StudentAvatar` esconde esse avatar em todos os peers, porque o professor não participa do mundo.
- **Volta para a lobby:** se o aluno cair estando na SharedWorld, o `NetworkSceneCoordinator` recarrega a `QuestLobby` e troca o NetworkManager pelo da lobby. Isso pode ser desligado pela opção `returnToBootSceneOnClientDisconnect`.

## Pendências

- Avatar com visual próprio, sem os prefabs `XR Controller` das Starter Assets do XRI. Hoje, em projetos com Meta SDK, eles aparecem como *missing nested prefab*.
- Painel dinâmico de `NetworkedToggle` no overlay do professor, no mesmo padrão do painel de teleporte.
