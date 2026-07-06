# VRLAN — Contexto Técnico do Projeto

> **O que é:** camada modular de VR multiplayer em LAN offline para uso pedagógico.
> **Engine:** Unity 6.3 (6000.3.13f1)
> **Última atualização:** sessão de desenvolvimento de junho/2026
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
| Rede | Netcode for GameObjects (NGO) 2.11.2 — **Scene Management ON** |
| VR SDK | XR Interaction Toolkit (XRI) 3.3.1 + OpenXR 1.16.1 |
| Render | URP 17.3 |
| Input | Input System 1.19 |
| Transporte | Unity Transport (UTP), porta 7777 |
| Discovery | UDP broadcast custom, porta 47777 |
| Módulo de código | asmdef `VRLAN.Network` (cobre `Assets/Scripts/Network/`) |

Build: APK Android. **Quest com OpenXR ON; Tablet com OpenXR OFF** (definido por Build Profile — é também o discriminador de papel).

---

## 3. Arquitetura de cenas (o coração)

A arquitetura passou por uma virada importante (ver §6). Modelo atual:

### Três cenas

1. **`TabletScene`** (shell do host/professor — boot do tablet):
   - NetworkManager (SM ON) + `NetworkSceneCoordinator` (sharedSceneName=`SharedWorld`).
   - LanDiscovery + LanDiscoveryHostBridge.
   - `ConnectionUI` (Canvas **Screen Space Overlay** 2D — controles).
   - Câmera espectadora (`SpectatorView`).
   - EventSystem (touch).
   - **Tudo que o tablet precisa pra UI/visão tem `PersistAcrossScenes`**: câmera espectadora + Canvas + **EventSystem**.

2. **`QuestLobby`** (shell do client/aluno — boot do Quest):
   - NetworkManager (SM ON) + `NetworkSceneCoordinator`.
   - LanDiscovery.
   - **XR Origin** (rig) com: `LocalXRRig` + `PersistAcrossScenes` (key "QuestRig") + `InteractorReRegister`.
   - `QuestConnectionUI` (Canvas **World Space** — scan/conexão).

3. **`SharedWorld`** (cena comum, **device-agnostic**, gerenciada pelo NGO):
   - O mundo (chão, props) + **objetos in-scene com NetworkObject** (NetworkedToggle, NetworkGrabbable, etc.).
   - **SEM** NetworkManager, câmera, rig ou UI. Só o conteúdo compartilhado.

### Fluxo de conexão

```
TABLET (host):
  boot TabletScene → clica Host
  → NetworkSceneCoordinator.OnServerStarted → SceneManager.LoadScene("SharedWorld", Single)
  → TabletScene descarrega, MAS câmera+Canvas+EventSystem PERSISTEM (DontDestroyOnLoad)
  → tablet renderiza SharedWorld atrás do overlay

QUEST (client):
  boot QuestLobby → scan → conecta
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

## 4. Componentes (scripts) — `Assets/Scripts/Network/`

### Conexão / Sessão
| Script | Papel |
|---|---|
| `ConnectionUI.cs` | UI 2D do host (Host/Stop, status, lista de alunos via SessionManager). Inscreve eventos em `Start()`. |
| `UI/QuestConnectionUI.cs` | UI World Space do Quest (scan contínuo, lista de hosts, IP manual, estados connected/disconnected). **Crítico:** chama `transport.SetConnectionData(ip,port)` antes de `StartClient()`. |
| `Discovery/LanDiscovery.cs` | Discovery UDP. **MulticastLock no Android** + broadcast direcionado por interface (`192.168.x.255`). |
| `Discovery/LanDiscoveryHostBridge.cs` | Liga discovery ao NGO no host (sobe discovery-server em OnServerStarted). |
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
| `Avatar/LocalXRRig.cs` | No XR Origin. Expõe transforms locais (cabeça/mãos) pro StudentAvatar copiar. |
| `Avatar/InteractorReRegister.cs` | Resolve bug do XRI: re-registra os interactors quando a SharedWorld carrega (senão o aluno não pega objetos até perder/recuperar tracking). |
| `Spectator/SpectatorView.cs` | Câmera do tablet segue a **cabeça** do avatar do aluno alvo. `SetTarget(clientId)` pronto pro seletor de aluno (futuro). |

### Interação / Comandos
| Script | Papel |
|---|---|
| `Interaction/NetworkedToggle.cs` | **Comando ativar/desativar.** `NetworkVariable<bool>` (servidor escreve) dirige um GameObject alvo (filho). API `SetOn/Toggle`. Reutilizável. |
| `Interaction/NetworkGrabbable.cs` | Objeto agarrável: transfere posse pro aluno no grab (via XRGrabInteractable → `OnGrabbed` → ServerRpc → ChangeOwnership). |
| `Interaction/TestObjectSpawner.cs` | Scaffolding: spawna prefabs em pontos no boot do servidor. |

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

---

## 8. Estado atual (o que funciona)

- ✅ Discovery LAN + conexão (tablet host ↔ Quest client).
- ✅ Spectator: tablet mostra a visão (1ª pessoa) do aluno no mundo.
- ✅ Avatar networkado: cabeça + mãos sincronizam.
- ✅ Arquitetura SM ON: SharedWorld via Single sync + device stuff persistente. Conecta limpo.
- ✅ NetworkObjects **in-scene** na SharedWorld sincronizam.
- ✅ Objetos **agarráveis** funcionam (com `InteractorReRegister`).
- ✅ Tablet em landscape; Canvas com proporção correta; overlay interativo após troca de cena.
- 🟡 `NetworkedToggle` (comando ativar/desativar) **pronto como componente**, mas ainda **não ligado** a um botão do overlay do professor.

---

## 9. Roadmap

| Marco | Descrição | Estado |
|---|---|---|
| **M2** | Comandos do professor: botão no overlay liga/desliga objeto in-scene (`NetworkedToggle`). | ⬜ Próximo |
| **M3** | Teleportar o aluno: anchors (pontos) na SharedWorld + RPC do host pro rig local do aluno. | ⬜ |
| **M4** | Empacotar como **módulo**: asmdef + prefabs (NetworkManager, shells, rig, comandos) + cena-sample + doc de integração. | ⬜ |
| **M5** | Limpeza: remover `RoleManager` (sem uso), `_Sandbox`, `TestObjectSpawner`, cenas antigas (`SampleScene`, duplicatas), scaffolding. Atualizar docs. | ⬜ |

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

> ⚠️ Nota: o `ARQUITETURA_TECNICA.md` foi escrito quando a decisão era **Scene Management OFF + 2 cenas + prefab-only**. Essa decisão foi **revertida** para SM ON (ver §6). Em caso de conflito, **este CONTEXTO.md é a fonte atual**.
