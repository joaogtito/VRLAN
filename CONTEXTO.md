# VRLAN — Contexto Técnico do Projeto

> **O que é:** camada modular de VR multiplayer em LAN offline para uso pedagógico.
> **Engine:** Unity 6.3 (6000.3.13f1)
> **Última atualização:** sessão de desenvolvimento de junho/2026 (23/06: M3 teleporte + seletor de aluno + identidade)
> **Este documento:** estado técnico completo pra continuar o desenvolvimento. Para a visão pedagógica/de produto, ver `Downloads/CONTEXTO_PROJETO_VRLAN.md`.

---

## 1. Visão em uma frase

VRLAN é uma **camada modular** que se aplica sobre projetos VR existentes para: (a) o **professor** ver no tablet a visão do **aluno** no mundo compartilhado, e (b) dar comandos como **ativar/desativar objetos** e **teleportar o aluno** para pontos pré-setados. Tudo em **rede local, offline**.

- **Tablet = Host = Professor** (monitor: overlay de controles + câmera espectadora).
- **Meta Quest 3 = Client = Aluno** (rig VR, interage com o mundo).
- O VRLAN não é o "jogo" — é a camada de rede + spectator + comandos que entra **por cima** da cena/interações do projeto host.

---

## 2. Stack técnica

| Item | Valor |
|---|---|
| Engine | Unity 6.3 (6000.3.13f1) |
| Rede | Netcode for GameObjects (NGO) 2.11.2 — **Scene Management ON**. Também compatível com **NGO 1.x (Unity 2022)** via dual-target (ver §12). |
| VR SDK | XR Interaction Toolkit (XRI) 3.3.1 + OpenXR 1.16.1 |
| Render | URP 17.3 |
| Input | Input System 1.19 |
| Transporte | Unity Transport (UTP), porta 7777 |
| Discovery | UDP broadcast custom, porta 47777 |
| Módulo de código | asmdef `VRLAN.Network` (cobre `Assets/VRLAN/Runtime/`) |

Build: APK Android. **Quest com OpenXR ON; Tablet com OpenXR OFF** (definido por Build Profile — é também o discriminador de papel).

---

## 3. Arquitetura de cenas (o coração)

A arquitetura passou por uma virada importante (ver §6). Modelo atual: **Scene Management ON**, 3 cenas.

### Cenas canônicas — pasta `Assets/Scenes/SM-ON/`

As cenas válidas (habilitadas no Build Settings) vivem em **`Assets/Scenes/SM-ON/`**:

> ⚠️ **Cenas legadas desabilitadas:** `Assets/Scenes/SampleScene.unity` e `Assets/Scenes/TabletScene.unity` (raiz) são da fase antiga (SM OFF) e estão **desabilitadas no build**. Remover na limpeza (M5). Não editar — a fonte é a pasta `SM-ON/`.

1. **`SM-ON/TabletScene Teste`** (shell do host/professor — boot do tablet):
   - NetworkManager (SM ON) + `NetworkSceneCoordinator` (sharedSceneName=`SharedWorld`).
   - LanDiscovery + LanDiscoveryHostBridge.
   - `ConnectionUI` (Canvas **Screen Space Overlay** 2D — controles) + painéis de comando (`StudentSelectorPanel`, `TeleportCommandPanel`).
   - Câmera espectadora (`SpectatorView`).
   - EventSystem (touch).
   - **Tudo que o tablet precisa pra UI/visão tem `PersistAcrossScenes`**: câmera espectadora + Canvas + **EventSystem**.

2. **`SM-ON/QuestLobby`** (shell do client/aluno — boot do Quest):
   - NetworkManager (SM ON) + `NetworkSceneCoordinator`.
   - LanDiscovery.
   - **XR Origin** (rig) com: `LocalXRRig` + `PersistAcrossScenes` (key "QuestRig") + `InteractorReRegister` + `RigSceneAffordances`.
   - `QuestConnectionUI` (Canvas **World Space** — scan/conexão, campo de nome).

3. **`SM-ON/SharedWorld`** (cena comum, **device-agnostic**, gerenciada pelo NGO):
   - O mundo (chão, props) + **objetos in-scene com NetworkObject** (NetworkedToggle, NetworkGrabbable, etc.) + **anchors de teleporte** (`TeleportAnchor` em objetos vazios).
   - **SEM** NetworkManager, câmera, rig ou UI. Só o conteúdo compartilhado.

### Fluxo de conexão

```
TABLET (host):
  boot SM-ON/TabletScene Teste → clica Host
  → NetworkSceneCoordinator.OnServerStarted → SceneManager.LoadScene("SharedWorld", Single)
  → TabletScene Teste descarrega, MAS câmera+Canvas+EventSystem PERSISTEM (DontDestroyOnLoad)
  → tablet renderiza SharedWorld atrás do overlay

QUEST (client):
  boot SM-ON/QuestLobby → scan → conecta
  → NGO sincroniza SharedWorld (Single, padrão) → QuestLobby descarrega, MAS XR rig PERSISTE
  → Quest está no mundo compartilhado, com seu rig
  → StudentAvatar (networked) espelha o rig; SpectatorView do tablet segue a cabeça do avatar
```

### O padrão central: PERSIST + Single sync

**O NGO só gerencia a `SharedWorld` (Single sync — caminho robusto e provado).** Tudo que é específico de cada device **NÃO** vai na SharedWorld; ele **sobrevive à troca de cena** via `PersistAcrossScenes` (DontDestroyOnLoad):

- **Quest:** XR rig (XR Origin) — "leva só o rig" pra cena compartilhada.
- **Tablet:** câmera espectadora + Canvas overlay + EventSystem.

> ⚠️ **REGRA DE OURO:** tudo que um device precisa pra funcionar (renderizar, receber input, interagir) **tem que persistir junto**. Se algo ficar pra trás na troca de cena, quebra:
> - EventSystem não persiste → Canvas não recebe toque.
> - XR Interaction Manager não persiste → não dá pra pegar objetos.
> - Câmera não persiste → tela preta / sem visão.
> - `PersistAcrossScenes` só funciona em objeto **RAIZ**.

---

## 4. Componentes (scripts) — `Assets/VRLAN/Runtime/`

### Estrutura do pacote (`Assets/VRLAN/`)

Tudo do VRLAN está consolidado sob `Assets/VRLAN/` pra exportar como plugin (`.unitypackage`):

```
Assets/VRLAN/
  Runtime/      → asmdef VRLAN.Network + scripts (subpastas Discovery/Avatar/Spectator/Interaction/Teleport/UI)
    Compat1x/   → assembly VRLAN.Network.Ngo1 (OwnerNetworkTransform) — compila só no NGO 1.x (ver §12)
  Prefabs/      → StudentAvatar, SessionManager, HostListItem, SharedWorld, DefaultNetworkPrefabs
  Materials/    → Verde.mat
  Samples/Scenes/ → TabletScene Teste, QuestLobby, SharedWorld (exemplo)
  README.md     → visão geral + guia de integração (XRI e Meta SDK)
```

> ⚠️ **`DefaultNetworkPrefabs.asset`:** existe **um só**, em `VRLAN/Prefabs/` (referenciado pelo NetworkManager, GUID `8d00129...`). O NGO **regenera** uma cópia `IsDefault` na raiz do projeto se a flag *Generate Default Network Prefabs List* estiver ligada (Project Settings → Netcode for GameObjects). Pro pacote ficar limpo, **desligar essa flag** (a lista do plugin já é referenciada explicitamente) ou apenas apagar a cópia da raiz quando reaparecer (ela não é referenciada).

> Exportar: selecionar `Assets/VRLAN/` → botão direito → **Export Package**. Detalhes e integração Meta SDK no `Assets/VRLAN/README.md`.


### Conexão / Sessão
| Script | Papel |
|---|---|
| `ConnectionUI.cs` | UI 2D do host (Host/Stop, status, lista de alunos via SessionManager). Inscreve eventos em `Start()`. Tem campo `hostNameInput` + ref `discoveryBridge`: no clique de Host, passa o nome digitado pro bridge (`SetHostName`) antes do `StartHost`. |
| `UI/QuestConnectionUI.cs` | UI World Space do Quest (scan contínuo, lista de hosts, IP manual, estados connected/disconnected). **Crítico:** chama `transport.SetConnectionData(ip,port)` antes de `StartClient()`. Tem campo de **nome** (`nameInput`) que seta `SessionManager.LocalStudentName` no `Connect()`. Flag **`autoConnectFirstHost`**: conecta automático no 1º host achado (pula a lista); guard `_connecting` evita conexão dupla e re-scaneia em falha. |
| `UI/SystemKeyboardInput.cs` | Abre o **teclado do sistema** (`TouchScreenKeyboard`) pro `TMP_InputField` em XR — no Quest o campo World Space não sobe o teclado sozinho. Self-contained (engata no `onSelect`); `OpenKeyboard()` público pra disparar por botão. |
| `UI/StudentSelectorPanel.cs` | **Seletor de aluno (host).** Lê `SessionManager.Students`, gera um botão por aluno (nome). Clicar fixa o aluno como ALVO: dirige `SpectatorView.SetTarget` + `TeleportCommandPanel.SetTargetStudent` juntos. Marca o selecionado com `▶`. Auto-seleciona o primeiro; re-seleciona quando o alvo cai. |
| `Discovery/LanDiscovery.cs` | Discovery UDP. **MulticastLock no Android** + broadcast direcionado por interface (`192.168.x.255`). |
| `Discovery/LanDiscoveryHostBridge.cs` | Liga discovery ao NGO no host (sobe discovery-server em OnServerStarted). `SetHostName(string)` público: o nome anunciado pode vir de um `TMP_InputField` (definido antes do servidor subir). A lista no Quest mostra **só o nome** (fallback pro IP se vazio). |
| `SessionManager.cs` | Fonte da verdade da sessão: `NetworkList<StudentInfo>` (alunos), `NetworkVariable<int> SessionStep`, RPC de registro de aluno. Singleton spawnado. |
| `NetworkSessionSpawner.cs` | Spawna o SessionManager quando o servidor sobe. |

### Cenas / Persistência
| Script | Papel |
|---|---|
| `NetworkSceneCoordinator.cs` | Host carrega a SharedWorld (Single) em OnServerStarted. Cliente recebe via sync padrão. **Tem que estar nas DUAS shells.** |
| `PersistAcrossScenes.cs` | DontDestroyOnLoad com singletonKey (evita duplicata na reconexão). Deve estar em objeto raiz. |

### Avatar / Spectator
| Script | Papel |
|---|---|
| `Avatar/StudentAvatar.cs` | Avatar networkado (PlayerPrefab). Root = âncora estável; `head`/`leftHand`/`rightHand` = filhos que espelham o rig. Esconde os visuais pro próprio dono (1ª pessoa). |
| `Avatar/LocalXRRig.cs` | No XR Origin. Expõe transforms locais (cabeça/mãos) pro StudentAvatar copiar. Tem `TeleportTo(pos, rot)`: math de teleporte VR correta (projeção da cabeça no chão + yaw), chamada pelo `PlayerTeleporter`. |
| `Avatar/InteractorReRegister.cs` | Resolve bug do XRI: re-registra os interactors quando a SharedWorld carrega (senão o aluno não pega objetos até perder/recuperar tracking). |
| `Avatar/RigSceneAffordances.cs` | Affordances do rig por cena, 2 listas: **`hideInSharedWorld`** (toggle por cena — esconde na SharedWorld, reexibe na lobby; ex.: `LineVisual` dos Near-Far) e **`refreshAndIdleOnSharedWorld`** (ciclo disable→enable→disable atrasado + zera o `LineRenderer`, terminando idle; ex.: **`Teleport Interactor`** pra matar a linha vermelha presa SEM desativar o TP). Sem dependência do XRI. **⚠️ Não sobrepor objetos com o `InteractorReRegister`** (ver armadilha #25). |
| `Spectator/SpectatorView.cs` | Câmera do tablet segue a **cabeça** do avatar do aluno alvo. `SetTarget(clientId)` agora dirigido pelo `StudentSelectorPanel`. |

### Interação / Comandos
| Script | Papel |
|---|---|
| `Interaction/NetworkedToggle.cs` | **Comando ativar/desativar.** `NetworkVariable<bool>` (servidor escreve) dirige um GameObject alvo (filho). API `SetOn/Toggle`. Reutilizável. |
| `Interaction/NetworkGrabbable.cs` | Objeto agarrável: transfere posse pro aluno no grab (via XRGrabInteractable → `OnGrabbed` → ServerRpc → ChangeOwnership). |
| `Interaction/TestObjectSpawner.cs` | Scaffolding: spawna prefabs em pontos no boot do servidor. |

### Teleporte (M3) — submódulo `VRLAN.Network.Teleport`
| Script | Papel |
|---|---|
| `Teleport/TeleportAnchor.cs` | **Peça que o dev usa.** Marca um GameObject **vazio** na SharedWorld como ponto de teleporte (`anchorId` + `displayLabel` + gizmo). Sem NetworkObject — é só transform de referência. Auto-registra no registry. |
| `Teleport/TeleportAnchorRegistry.cs` | Registry estático dos anchors (`OnChanged`). O painel do professor enumera daqui; absorve o timing do load da SharedWorld. |
| `Teleport/PlayerTeleporter.cs` | `NetworkBehaviour` no prefab **StudentAvatar**. `RequestTeleport(pos,rot)` (servidor) → `[Rpc(SendTo.Owner)]` roteia pro Quest daquele aluno → move o rig local. O avatar segue o rig e o `NetworkTransform` replica. |
| `Teleport/TeleportCommandPanel.cs` | Painel do professor (overlay): gera um botão por anchor; no clique teleporta o **aluno selecionado** (via `SetTargetStudent`, dirigido pelo `StudentSelectorPanel`; fallback = 1º não-host). |

### Plataforma / Sandbox
| Script | Papel |
|---|---|
| `ForceLandscape.cs` | Trava o tablet em Landscape Left (`Screen.orientation` fixo — não AutoRotation). |
| `RoleManager.cs` | **NÃO USADO** no modelo atual (papel = qual boot scene). Remover na limpeza. |
| `_Sandbox/SceneMgmtTestBootstrap.cs` | Teste descartável de Scene Management. Remover na limpeza. |

**Deletados (cruft):** `PingTester`, `PingButton`, `AutoConnect`.

### Config do NetworkManager
- **Enable Scene Management = ON**
- PlayerPrefab = `StudentAvatar`
- `DefaultNetworkPrefabs.asset`: SessionManager + StudentAvatar (objetos **in-scene NÃO** precisam estar aqui — só os spawnados em runtime).

---

## 5. Como fazer as coisas (receitas)

### Objeto interativo/sincronizado na SharedWorld (in-scene)
Coloca direto na SharedWorld com `NetworkObject` + os componentes de rede. **Não precisa registrar no DefaultNetworkPrefabs** (in-scene é resolvido pela cena). Funciona porque a SharedWorld é NGO-managed (Single sync).

### Objeto agarrável
Cube + Rigidbody (Is Kinematic ON, Use Gravity OFF) + Collider + XRGrabInteractable (Movement Type Kinematic) + NetworkObject + NetworkTransform (Authority **Owner**, world space) + `NetworkGrabbable`. Liga o `Select Entered` do XRGrabInteractable → `NetworkGrabbable.OnGrabbed`.

### Objeto ligável/desligável (comando do professor)
`NetworkObject` + `NetworkedToggle` (target = um **filho** visual). Botão no overlay do tablet chama `Toggle()`/`SetOn()`. Servidor é dono → replica direto.

### Cenário estático (não-interativo)
Direto na SharedWorld, sem NetworkObject.

### Ponto de teleporte (comando do professor)
GameObject **vazio** na SharedWorld + `TeleportAnchor` (preencher `anchorId` único + `displayLabel`). A posição = onde o aluno fica de pé; o eixo Z (forward) = pra onde ele encara. O `TeleportCommandPanel` no overlay gera o botão sozinho. **Nenhum código pro integrador** — só posicionar objetos vazios.

### Seletor de aluno (host)
`StudentSelectorPanel` no overlay com `container` + `buttonTemplate` ligados, mais refs ao `SpectatorView` e ao `TeleportCommandPanel`. Os botões aparecem automaticamente conforme os alunos se registram. Clicar troca câmera + alvo dos comandos juntos.

### Campo de nome no Quest
`TMP_InputField` na QuestLobby ligado em `QuestConnectionUI.nameInput`. Vazio → host nomeia `Aluno #<id>`. **Pré-requisito (senão o teclado não abre):** no `AndroidManifest.xml` ter a feature `oculus.software.overlay_keyboard` + permissão `com.oculus.permission.USE_VR_SYST_KEYBOARD` (ver armadilha #22). `SystemKeyboardInput` no mesmo objeto é opcional (espelha texto + abertura manual).

### Nome do host (lista do Quest)
Fixo: campo `Host Name` no `LanDiscoveryHostBridge` (TabletScene). Dinâmico: `TMP_InputField` no overlay ligado em `ConnectionUI.hostNameInput` + ref `discoveryBridge` → o nome digitado vai pro discovery no clique de Host. No tablet (touch/overlay 2D) o teclado do sistema abre sozinho (não precisa de `SystemKeyboardInput`).

### Desligar rays/teleporte do aluno na SharedWorld
`RigSceneAffordances` no rig persistido. **Ray do Near-Far** (poluição no mundo): pôr o `LineVisual` em `hideInSharedWorld`. **Linha vermelha do Teleport Interactor** (estado preso na troca de cena): pôr o GameObject `Teleport Interactor` em `refreshAndIdleOnSharedWorld` (refresh + idle, mantém o TP funcional). **Nunca** repetir nesses arrays um objeto que precisa terminar ligado pra agarrar (está no `InteractorReRegister`) — ver #25.

---

## 6. Decisões-chave e o porquê

| Decisão | Porquê |
|---|---|
| **Scene Management ON** (era OFF) | Permite NetworkObjects **in-scene** no mundo compartilhado — essencial pro objetivo de módulo (a cena do projeto existente, com seus objetos, "só funciona"). Com OFF teria que converter tudo pra prefab spawnado. |
| **SharedWorld via Single sync + device stuff via Persist** (não additive) | A tentativa de cenas-shell **aditivas** (host e client mantendo shells + carregando a comum aditivamente) **quebrou**: o cliente rejeitar a shell do host corrompe o mapa de handles de cena → falha de soft-sync de objetos in-scene → NRE. O Single sync (padrão, provado) + DontDestroyOnLoad pros objetos do device é robusto e evita essa fragilidade. |
| **Tablet = monitor** (spectator + overlay, sem avatar próprio) | O professor observa e comanda; não é um participante no mundo. |
| **Quest "leva só o rig"** | O XR rig persiste (DontDestroyOnLoad) da QuestLobby pra SharedWorld; o resto da lobby fica pra trás. |
| **Avatar = proxy networkado do rig local** | O XR Origin é local (não networkado); o `StudentAvatar` espelha cabeça/mãos e replica. Permite o spectator no tablet sem streaming de vídeo. |
| **Avatar root = âncora estável, head/hands = filhos** | Fazer o root ser a cabeça acoplava as mãos à rotação da cabeça (mão orbitava no eixo da cabeça). Desacoplar resolveu. |
| **Discovery custom (não o sample NGO)** | O sample não cobria o caso Android (MulticastLock, broadcast direcionado). |
| **Inscrição em eventos do NGO sempre em `Start()`** | `OnEnable()` com `if(null)return` pode rodar antes do `NetworkManager.Singleton` existir → não inscreve silenciosamente. |
| **Spectator = câmera-espectadora (não vídeo)** | Leve, escala, alinha com NGO. Streaming de pixels é beco sem saída pra 10 alunos. |
| **⭐ O tablet renderiza a PRÓPRIA cópia da cena** | O `SpectatorView` só recoloca a câmera do host na pose da cabeça do aluno — **só o que é networkado atravessa**. Qualquer mudança visual local do aluno (`SetActive`, cor, líquido, canvas sumindo) **não aparece no tablet** a menos que seja replicada (NetworkVariable/RPC). Premissa que explica metade dos bugs de integração (validado no LabPH). |

---

## 7. Armadilhas conhecidas (catálogo de bugs já resolvidos)

Não repetir estes — todos custaram tempo:

1. **Tela preta no Quest** → faltava Project Validation (aba Android) → Fix All antes do 1º build.
2. **Canvas Overlay não interage em VR** → World Space + Tracked Device Graphic Raycaster + XR UI Input Module.
3. **EventSystem com action asset quebrado** → apontar pro `XRI Default Input Actions`.
4. **NRE "Scene Hash does not exist"** → era SM OFF + cenas diferentes. SM ON + cena comum resolve.
5. **NRE no `ConnectionApprovedMessage`** → in-scene NetworkObject com SM OFF. SM ON suporta in-scene.
6. **Build usa a cena errada** → conferir Override Scene List no Build Profile.
7. **Discovery não acha host (Android)** → MulticastLock + broadcast direcionado.
8. **2 UIs / 1 LanDiscovery brigando** → separar por cena/papel.
9. **Tablet não atualiza contagem de clients** → inscrever em `Start()`, não `OnEnable()`.
10. **`StartClient` não conecta** → faltou `transport.SetConnectionData(ip,port)` antes.
11. **Mãos não sincronizam** → campos `leftHand`/`rightHand` do prefab não ligados.
12. **Mão gira no eixo da cabeça** → desacoplar (root âncora; head/hands filhos).
13. **Cabeça não sincroniza** → o objeto da cabeça (Sphere) sem `NetworkTransform`.
14. **Avatar trava a locomoção** → remover colliders do avatar (Sphere primitiva traz SphereCollider).
15. **Tablet não inicia em landscape** → Player Settings Default Orientation = Landscape Left + `ForceLandscape` fixo (não AutoRotation).
16. **Canvas distorce entre telas** → Canvas Scaler: Scale With Screen Size + Expand.
17. **Additive client sync quebra in-scene objects** → usar Single sync + Persist (ver §6).
18. **Quest "pega a câmera do host" + skybox amarelo** → `NetworkSceneCoordinator` faltando na TabletScene (host não carregava SharedWorld → cliente sincronizava a cena do host). + câmera com Clear Flags = Nothing (amarelo). Fix: coordinator no host + Clear Flags Skybox.
19. **Canvas do host não recebe toque após carregar SharedWorld** → EventSystem não persistido (destruído no Single load). Fix: `PersistAcrossScenes` no EventSystem.
20. **Aluno não pega objetos até perder/recuperar o controle** → timing de registro do XRI (interactables carregam depois dos interactors). Fix: `InteractorReRegister`.
21. **Rays dos controles ficam sempre ligados na SharedWorld** → o rig persiste e os `LineVisual` dos Near-Far Interactors (affordance da UI World Space da lobby) continuam desenhando no mundo (o far-cast bate no chão). **Não é o avatar** (as mãos dele são só os modelos do controle, sem interactor). Fix: `RigSceneAffordances.hideInSharedWorld` desliga os `LineVisual` quando a SharedWorld carrega.
22. **Teclado não abre no campo de nome (Quest)** → o log dava `Oculus overlay keyboard is disabled, add 'oculus.software.overlay_keyboard'...`. **Causa real:** faltava declarar a feature no AndroidManifest. **Fix (resolvido):** em `Assets/Plugins/Android/AndroidManifest.xml` adicionar `<uses-feature android:name="oculus.software.overlay_keyboard" android:required="false" />` + `<uses-permission android:name="com.oculus.permission.USE_VR_SYST_KEYBOARD" />`. Com isso o `TouchScreenKeyboard.Open()` sobe o teclado do sistema do Quest (não precisa do Meta XR SDK). Obs.: o próprio `TMP_InputField` já chama `TouchScreenKeyboard.Open()` no activate; o `SystemKeyboardInput` é redundante pra abrir, mas mantém o espelhamento do texto e o `OpenKeyboard()` manual.
23. **Teleporte VR enterra/desloca o jogador** → setar a posição do XR Origin direto ignora o offset da cabeça dentro do rig. Fix: mover o origin pela **projeção da cabeça no chão** + yaw (centralizado em `LocalXRRig.TeleportTo`).
24. **Linha vermelha do teleporte fica presa na SharedWorld** → é o **Teleport Interactor** (separado do Near-Far; o `LineRenderer` está NO próprio GameObject dele). O `ControllerInputActionManager` só o liga no input (`OnStartTeleport`→`SetActive(true)`), então um `SetActive(false)` síncrono cedo demais não cola (o estado volta antes do rig assentar). Fix: `RigSceneAffordances.refreshAndIdleOnSharedWorld` faz ciclo disable→enable→disable **atrasado** (`settleFrames`) + zera o `LineRenderer`, terminando idle. **Mantém o TP** (o manager religa no próximo input). Não confundir com o ray do Near-Far (#21).
25. **Near-Far não seleciona / um controle perde o grab na SharedWorld** → objeto sobreposto entre `InteractorReRegister.interactors` (termina LIGADO pro grab) e `RigSceneAffordances.refreshAndIdleOnSharedWorld` (termina DESLIGADO). O `RigSceneAffordances` roda depois (settleFrames) e ganha a corrida → deixa o interactor morto. Fix: **zero sobreposição** entre os dois arrays. `refreshAndIdle` só com os `Teleport Interactor`; Near-Far/Poke só no `InteractorReRegister` (nos dois controles).
26. **`UnityException: GetDeviceName ... not allowed from a MonoBehaviour constructor`** → chamar `SystemInfo.deviceName` (ou outras APIs da engine) num **field initializer estático** roda no `.cctor`, fora da thread/timing permitido. Era o default de `SessionManager.LocalStudentName`. Fix: inicializar com `""` e resolver qualquer valor da engine em `Awake/Start`. Vale pra qualquer `static ... = API_da_Unity()`.
27. **Cliente conecta e é expulso ~70ms depois, em loop** → host e cliente usam **listas de NetworkPrefabs diferentes**; com `ForceSamePrefabs = 1` o NGO compara o hash da lista no handshake → diferente → kick. Causa comum: NM do host aponta pra lista curada e o do cliente pra `Assets/DefaultNetworkPrefabs.asset` **auto-gerada na raiz** pelo NGO. Fix: os dois NMs na **mesma** lista + apagar a da raiz + desligar "Generate Default Network Prefabs List" (Project Settings → Netcode). (Mesmo asset duplicado da nota do §4.)
28. **"A Native Collection has not been disposed" (memory leak)** no stop do Play → é o `NativeList` do `NetworkList` (ex.: `SessionManager.Students`) que o NGO nem sempre dispõe no **teardown abrupto do Editor**. **Benigno e Editor-only** — não ocorre em build (detecção vem desligada). Ignorar, ou baixar em Edit > Preferences > Jobs > Leak Detection. Não vale mexer no `OnDestroy` (varia entre NGO 1.x/2.x).
29. **Objeto estático "pula" pra origem no host / snap** → um objeto **parado** foi networkado (NetworkObject + NetworkTransform), às vezes **aninhado** sob um móvel kinematic → a NetworkTransform reseta a pose no spawn. Fix: **objeto estático não leva NetworkObject** (a cena já está sincronizada — ele aparece certo só por existir). Só networkar o que o aluno **pega/move**. Se for mesmo móvel e aninhado: "In Local Space" na `OwnerNetworkTransform` ou mover o NetworkObject pra raiz da cena (NGO não gosta de NetworkObject in-scene aninhado sob não-NetworkObject).

---

## 8. Estado atual (o que funciona)

- ✅ Discovery LAN + conexão (tablet host ↔ Quest client).
- ✅ Spectator: tablet mostra a visão (1ª pessoa) do aluno no mundo.
- ✅ Avatar networkado: cabeça + mãos sincronizam.
- ✅ Arquitetura SM ON: SharedWorld via Single sync + device stuff persistente. Conecta limpo.
- ✅ NetworkObjects **in-scene** na SharedWorld sincronizam.
- ✅ Objetos **agarráveis** funcionam (com `InteractorReRegister`).
- ✅ Tablet em landscape; Canvas com proporção correta; overlay interativo após troca de cena.
- ✅ **Teleporte do aluno (M3):** anchors (objetos vazios) + `PlayerTeleporter` (RPC SendTo.Owner) + math de teleporte VR em `LocalXRRig.TeleportTo`. Painel gera botões por anchor.
- ✅ **Seletor de aluno + identidade:** aluno digita nome na lobby (default `Aluno #<id>`); lista clicável no host troca câmera + alvo dos comandos juntos.
- ✅ **Rays desligam na SharedWorld** (`RigSceneAffordances.hideInSharedWorld`) e a **linha vermelha do teleporte some** via refresh+idle (`refreshAndIdleOnSharedWorld`), mantendo o TP do aluno funcional. Cuidado com sobreposição de objetos com o `InteractorReRegister` (#25).
- ✅ **Nome do host:** fixo (Inspector) ou digitado no overlay; a lista no Quest mostra só o nome.
- ✅ **Conexão auto:** flag `autoConnectFirstHost` no `QuestConnectionUI` (conecta no 1º host achado, sem interação) — além da lista/manual.
- ✅ **Dual-target NGO 1.x/2.x** (Unity 2022 + Unity 6): `#if VRLAN_NGO2` nos RPCs + assembly `Compat1x` (`OwnerNetworkTransform`) gated por `VRLAN_NGO1` (ver §12).
- ✅ **Integração Meta SDK validada em campo (LabPH):** rig OVRCameraRig + grab Meta (`Grabbable` → `NetworkGrabbable.OnGrabbed`) + `OwnerNetworkTransform` funcionam. O código do VRLAN não muda entre XRI e Meta.
- 🟡 `NetworkedToggle` (comando ativar/desligar) **pronto como componente**, mas ainda **não ligado** a um botão do overlay (M2). Padrão recomendado: registry por `commandId` + painel dinâmico (igual ao do teleporte).
- 🟡 Peças novas precisam de **wiring no Editor** (componentes no overlay/rig/prefab + anchors na cena) e teste no device.

---

## 9. Roadmap

| Marco | Descrição | Estado |
|---|---|---|
| **M3** | Teleportar o aluno: anchors (pontos) na SharedWorld + RPC do host pro rig local do aluno. | 🟡 Código pronto, falta wiring/teste no device |
| **M3.5** | Seletor de aluno (lista no host) + identidade (nome digitado na lobby). Dirige spectator + comandos. | 🟡 Código pronto, falta wiring/teste |
| **M2** | Comandos do professor: botão no overlay liga/desliga objeto in-scene (`NetworkedToggle`). | ⬜ Próximo (reaproveitar padrão registry+painel do teleporte) |
| **M4** | Empacotar como **módulo**: asmdef + prefabs + cena-sample + doc de integração. | 🟡 Estrutura `Assets/VRLAN/` criada (Runtime/Prefabs/Materials/Samples + README). Exportado e **integrado em campo (LabPH: Unity 2022 + Meta SDK)**. Falta limpeza (M5) e portar o avatar pra visual próprio (sem Starter Assets). |
| **Compat** | Suporte dual-target NGO 1.x (Unity 2022) + 2.x (Unity 6). | ✅ Feito (`#if VRLAN_NGO2` + `Compat1x`/`OwnerNetworkTransform`, ver §12) |
| **M5** | Limpeza. ✅ Já removidos do pacote: `RoleManager`, `_Sandbox`, `TestObjectSpawner`. ⬜ Falta: cenas legadas desabilitadas (`Assets/Scenes/SampleScene.unity` e `Assets/Scenes/TabletScene.unity` da raiz — a fonte é `Assets/Scenes/SM-ON/`) e portar o avatar pra visual próprio (sem Starter Assets). | 🟡 Parcial |

### Sobre o objetivo de módulo (M4)
A cena do projeto VR existente vira a `SharedWorld` (com os NetworkObjects in-scene dele). O VRLAN entra por cima: as shells (TabletScene/QuestLobby), o NetworkManager configurado, o spectator e os componentes de comando. O integrador adiciona `NetworkedToggle` nos objetos que o professor controla e define os anchors de teleporte.

---

## 10. Convenções

- Namespace raiz: `VRLAN.Network` (submódulos: `.Discovery`, `.UI`, `.Avatar`, `.Spectator`, `.Interaction`).
- Eventos do NGO: inscrever em `Start()` + limpar em `OnDestroy()`.
- Antes de `StartClient()`: sempre `SetConnectionData(ip, port)`.
- `NetworkList`/`NetworkVariable`: inicializar como field (`= new()`).
- Struct em `NetworkList<T>`: `unmanaged` + `INetworkSerializable` + `IEquatable` (usar `FixedString64Bytes`, não `string`).
- `NetworkedToggle`/desativação: mirar um **filho**, nunca o GameObject do próprio NetworkObject.
- `PersistAcrossScenes`: só em objeto **raiz**; usar `singletonKey` pra dedup.
- Logs: `Debug.Log($"[Componente] msg")` (facilita `adb logcat -s Unity`).

---

## 11. Documentos relacionados (em `C:\Users\joao.moraes\Downloads\`)

- `CONTEXTO_PROJETO_VRLAN.md` — visão de produto/pedagógica (mantida pela Ana).
- `ARQUITETURA_TECNICA.md` — doc de arquitetura (parcialmente desatualizado: foi escrito na fase SM OFF; este CONTEXTO.md reflete o estado atual SM ON).
- `LOG_2026-05-13.md`, `LOG_2026-05-18.md` — logs de sessões anteriores.
- `VRLAN_INTEGRACAO_LABPH.md` (raiz do projeto) — diário da integração real no LabPH (Unity 2022 + Meta SDK): bugs, correções e diferenças do plugin. Fonte de vários aprendizados de campo (spectator, prefabs, snap).

> ⚠️ Nota: o `ARQUITETURA_TECNICA.md` foi escrito quando a decisão era **Scene Management OFF + 2 cenas + prefab-only**. Essa decisão foi **revertida** para SM ON (ver §6). Em caso de conflito, **este CONTEXTO.md é a fonte atual**.

---

## 12. Compatibilidade e portabilidade

### Dual-target NGO 1.x (Unity 2022) / 2.x (Unity 6)
O plugin compila nos dois via `versionDefines` no asmdef (define `VRLAN_NGO2` quando NGO ≥ 2.0):

- **RPCs** (`SessionManager`, `NetworkGrabbable`, `PlayerTeleporter`): blocos `#if VRLAN_NGO2 ... #else ... #endif`. No 2.x usam `[Rpc(SendTo.Server/Owner)]` + `RpcParams`; no 1.x `[ServerRpc(RequireOwnership=false)]`/`[ClientRpc]` + `ServerRpcParams`/`ClientRpcParams` (RPC ao dono via `TargetClientIds`).
- **Autoridade do dono no NetworkTransform:** no 2.x usa-se o `NetworkTransform` padrão com **AuthorityMode = Owner** (Inspector). No 1.x não existe isso → usa-se **`OwnerNetworkTransform`** (subclasse que faz `OnIsServerAuthoritative() => false`).
- **`OwnerNetworkTransform` isolado no assembly `Compat1x`** (`VRLAN.Network.Ngo1.asmdef`): referencia `Unity.Netcode.Runtime` **+ `Unity.Netcode.Components`** (no NGO 1.x o `NetworkTransform` mora nesse assembly separado; no 2.x ele está dentro do Runtime). O asmdef tem `defineConstraints: ["VRLAN_NGO1"]` + `versionDefines` que define `VRLAN_NGO1` só em NGO `[1.0.0,2.0.0)` → no NGO 2.x o assembly é **excluído inteiro** (a referência ao `Unity.Netcode.Components`, que não existe no 2.x, nem é avaliada).

**Checklist ao rodar no Unity 2022 (NGO 1.x):** trocar `NetworkTransform` por `OwnerNetworkTransform` nos prefabs (StudentAvatar head/hands + grabbables). No Unity 6 isso **não** se faz (usa AuthorityMode=Owner).

### Portabilidade de SDK (XRI ↔ Meta) — validado em campo (LabPH)
O código do VRLAN **não depende de XRI nem Meta** (asmdef só referencia Netcode/TMP/UI/Transport/Collections). Trocar de SDK é **wiring de cena**, não código:

- **Rig:** `LocalXRRig` no OVRCameraRig (Meta) → `head=CenterEyeAnchor`, `hands=Left/RightHandAnchor`. `InteractorReRegister` (fix do XRI) é dispensável no Meta.
- **Grab:** o evento de grab do Meta (`Grabbable` + `InteractableUnityEventWrapper`) liga em `NetworkGrabbable.OnGrabbed` — foi pra isso que o método é `UnityEvent` sem tipos do XRI.
- **SharedWorld device-agnostic:** remover OVRCameraRig/OVRManager/câmera dela (senão dá **rig duplicado** — "Only one instance of OVRCameraRig"). O rig persiste da lobby.
- `StudentAvatar`, `SpectatorView`, teleporte, discovery, sessão e seletor **não mudam** entre XRI e Meta.

### Estado visual local ≠ networkado (a regra que evita metade dos bugs de integração)
Ver a decisão ⭐ no §6 e as armadilhas #27/#29. Como o tablet renderiza a própria cópia da cena, **todo estado visual que o professor precisa ver tem que ser replicado** (NetworkVariable/RPC). Padrão recomendado no jogo integrador: **"roteador + Apply"** — a lógica fica offline-safe (MonoBehaviour) e a rede entra como camada opcional que só roteia quando há NetworkManager. E **só networkar o que o aluno pega/move**; estático fica sem NetworkObject.

### Nota sobre o avatar (portabilidade)
O `StudentAvatar` original embute os prefabs `XR Controller Left/Right` das **XRI Starter Assets** nas mãos → em projeto Meta dá erro de "missing nested Prefabs" (precisa importar o sample Starter Assets, ou reconstruir as mãos com meshes próprias). **Pendência de M4:** trocar por visual próprio pra remover a dependência das Starter Assets.
