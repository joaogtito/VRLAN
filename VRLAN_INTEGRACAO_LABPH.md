# Integração VRLAN no LabPH — diário de problemas, correções e diferenças do plugin original

Documento da integração do plugin **VRLAN** (VR multiplayer em LAN) no projeto **LabPH** (laboratório de química em VR).
Cobre: cada problema que apareceu, a causa raiz, a correção, e o que ficou **diferente do plugin VRLAN original**.

- **Stack:** Unity 2022 / NGO (Netcode for GameObjects) **1.11.0** / **URP** / **Meta XR SDK 85** (OpenXR, Single Pass Instanced).
- **Topologia:** Tablet = Host = Professor (espectador) · Quest = Client = Aluno (rig VR).
- **Rede:** LAN offline, NGO com **Scene Management ON**, descoberta por UDP broadcast.

> ⚠️ Premissa central que explica metade dos bugs: **o tablet NÃO vê a tela do aluno.** O `SpectatorView` renderiza a **própria cópia da SharedWorld** no host, a partir da pose da cabeça do aluno. **Só o que é explicitamente networkado atravessa.** Qualquer mudança visual local (um `SetActive`, uma cor, um líquido) acontece só na máquina do aluno e o host nunca vê.

---

## Índice de problemas

1. [Setup Meta SDK + permissões do AndroidManifest](#1-setup-meta-sdk--permissões-do-androidmanifest)
2. [Loop de conexão: conecta e desconecta na hora](#2-loop-de-conexão-conecta-e-desconecta-na-hora)
3. [OVRCameraRig duplicado + NullReferenceException](#3-ovrcamerarig-duplicado--nullreferenceexception)
4. [Câmera do tablet não segue a cabeça do aluno](#4-câmera-do-tablet-não-segue-a-cabeça-do-aluno)
5. [Botão inicial: "CameraRigRoot ou TeleportPoint não configurado"](#5-botão-inicial-camerarigroot-ou-teleportpoint-não-configurado)
6. [Líquido renderiza errado em um olho (estéreo)](#6-líquido-renderiza-errado-em-um-olho-estéreo)
7. [Reação química não replicava pro tablet](#7-reação-química-não-replicava-pro-tablet)
8. [Canvases e extrato do conta-gotas não apareciam no tablet](#8-canvases-e-extrato-do-conta-gotas-não-apareciam-no-tablet)
9. [Recipiente do extrato "pula" pra origem no host](#9-recipiente-do-extrato-pula-pra-origem-no-host)
10. [Botão da prática (2º canvas) — investigação](#10-botão-da-prática-2º-canvas--investigação)

---

## 1. Setup Meta SDK + permissões do AndroidManifest

**O que estava certo:** o rig Meta estava bem montado — `LocalXRRig` no OVRCameraRig com `head → CenterEyeAnchor`, `leftHand → LeftHandAnchor`, `rightHand → RightHandAnchor`, `PersistAcrossScenes` no mesmo objeto raiz, NetworkManager com `EnableSceneManagement = ON` e `PlayerPrefab = StudentAvatar`.

**Problema:** o `AndroidManifest.xml` estava **sem as permissões** que o README do VRLAN exige.

**Correção (recomendada):** adicionar ao `Assets/Plugins/Android/AndroidManifest.xml`:

```xml
<!-- teclado de sistema VR (digitar nome do aluno) -->
<uses-feature android:name="oculus.software.overlay_keyboard" android:required="false" />
<uses-permission android:name="com.oculus.permission.USE_VR_SYST_KEYBOARD" />
<!-- rede local / descoberta de host (NGO + UDP broadcast + MulticastLock) -->
<uses-permission android:name="android.permission.INTERNET" />
<uses-permission android:name="android.permission.ACCESS_NETWORK_STATE" />
<uses-permission android:name="android.permission.ACCESS_WIFI_STATE" />
<uses-permission android:name="android.permission.CHANGE_WIFI_MULTICAST_STATE" />
```

Sem `CHANGE_WIFI_MULTICAST_STATE` + `ACCESS_WIFI_STATE`, o `LanDiscovery.AcquireMulticastLock()` falha no device e a descoberta de host não funciona.

> ⚠️ O Meta XR SDK pode **regenerar/mesclar** esse manifest. Depois de cada build, confira se as 5 entradas continuam lá. Em `Player Settings → Android → Other → Internet Access = Require` como rede de segurança pro `INTERNET`.

---

## 2. Loop de conexão: conecta e desconecta na hora

**Sintoma (logcat):** o Quest achava o host, conectava e era expulso ~70 ms depois, em loop:
```
[Netcode] Connected
[Netcode] Disconnect Event From 0
```

**Causa raiz:** host e cliente usavam **listas de NetworkPrefabs DIFERENTES**, e com `ForceSamePrefabs = 1` o NGO compara o hash da lista no handshake — diferente → o servidor expulsa o cliente.
- Host (`TabletScene`) → `Assets/VRLAN/Prefabs/DefaultNetworkPrefabs.asset` (curada, 4 prefabs).
- Cliente (`QuestLobby`) → `Assets/DefaultNetworkPrefabs.asset` (auto-gerada na raiz, 20 prefabs).

**Correção:**
1. Apontar os **dois** NetworkManagers para a **mesma** lista (a do VRLAN).
2. Apagar a `Assets/DefaultNetworkPrefabs.asset` da raiz (não versionada).
3. `Project Settings → Netcode for GameObjects`: desligar **"Generate Default Network Prefabs List"** (ou apontar pro asset do VRLAN), pra não recriar a lista errada.

---

## 3. OVRCameraRig duplicado + NullReferenceException

**Sintoma (logcat), ao entrar na SharedWorld:**
```
Only one instance of OVRCameraRig should be created at once!
[OVRManager] OnDestroy
NullReferenceException: ... at OVRManager.Update ()
```

**Causa raiz:** a `SharedWorld 2` tinha um **OVRCameraRig embutido**, e o Quest também trazia o **rig persistido** da `QuestLobby` (via `PersistAcrossScenes`). Resultado: dois rigs/OVRManager ao mesmo tempo.

**Correção:** **remover o OVRCameraRig/OVRManager/câmera da `SharedWorld 2`** — ela deve ser **device-agnostic** (o rig persiste da lobby). Confirmado: `PersistAcrossScenes` está no mesmo objeto raiz do `OVRManager` na lobby, então o rig sobrevive ao Single load como rig único.

> 💡 Regra: a SharedWorld **não** contém NetworkManager, câmera, rig nem UI de conexão. Tudo isso é device-specific e persiste das cenas-boot.

---

## 4. Câmera do tablet não segue a cabeça do aluno

**Sintoma:** no tablet, a câmera espectadora ficava parada; o avatar do aluno não se mexia.

**Causa raiz (dois bugs no prefab `StudentAvatar`):**
1. O campo **`head` estava NULO** e **não existia um nó "Head"** no avatar (só `LeftHand`/`RightHand` + root). O `SpectatorView` faz `avatar.Head != null ? avatar.Head : root` → caía no **root**, que é âncora estável na origem que nunca se move → câmera congelada.
2. Os transforms de head/mãos usavam **`NetworkTransform` padrão = server-authoritative** (NGO 1.x). As poses que o **cliente** (aluno) escreve **não replicam** pro host.

**Correção (no prefab StudentAvatar):**
- Criar um nó **`Head`** filho do root.
- Adicionar **`OwnerNetworkTransform`** (o do VRLAN, autoridade do dono) em `Head`, `LeftHand`, `RightHand` — substituindo o `NetworkTransform` padrão.
- Atribuir o nó `Head` ao campo **`Head`** do `StudentAvatar`.

> `OwnerNetworkTransform` existe na pasta `Compat1x` do VRLAN, ativada pelo define `VRLAN_NGO1` (NGO 1.x). No NGO 2.x usa-se o `NetworkTransform` padrão com `AuthorityMode = Owner`.

---

## 5. Botão inicial: "CameraRigRoot ou TeleportPoint não configurado"

**Sintoma (logcat):**
```
BOTAO CLICADO!
CameraRigRoot ou TeleportPoint não configurado!
```
(O clique funcionava — via `VRButtonTrigger` por collider, não por raycast de UI.)

**Causa raiz:** o `UIStartButton` tinha uma referência **serializada** ao `cameraRigRoot` (o rig **da cena**). Como o rig foi removido da SharedWorld e agora **persiste da lobby**, a referência ficou **nula**. Nenhuma referência de cena consegue apontar pro rig persistido — tem que resolver em runtime.

**Correção (`UIStartButton.cs`):** resolver o rig por `LocalXRRig.Instance` e teleportar com `rig.TeleportTo(...)` (que compensa o offset da cabeça dentro do rig — o `position = ...` cru tinha bug de VR). O `cameraRigRoot` virou só **fallback offline**.

---

## 6. Líquido renderiza errado em um olho (estéreo)

**Sintoma:** na visão do Quest, um olho renderizava o líquido dentro dos frascos deslocado ("pra fora").

**Causa raiz:** o Shader Graph `Assets/Shaders/Graphs/LiquidReactionShader.shadergraph` tem target **`BuiltInTarget`/`BuiltInLitSubTarget`** (Built-in RP) e **nenhum target Universal** — mas o projeto renderiza em **URP** com **Single Pass Instanced**. Shader Built-in sob URP não recebe o setup de instancing estéreo → quebra num olho. (O material tem props `_BUILTIN_*`, confirmando.)

**Correção:** abrir o shadergraph → **Graph Settings → Active Targets → remover "Built In", adicionar "Universal"** (Lit). Salvar e conferir o material.
**Band-aid:** Stereo Rendering Mode → **Multi Pass** (compatível, porém renderiza 2× — perde performance e não corrige o pipeline).

---

## 7. Reação química não replicava pro tablet

**Sintoma:** as cores das reações / progresso aconteciam só no aluno.

**Causa raiz:** `ReactionManager` é lógica **single-player** (MonoBehaviour local) — nada replicava.

**Correção — Opção A (lógica desacoplada de rede):**
- `ReactionManager` virou MonoBehaviour com **métodos "burros"** que só aplicam estado (`ApplyDrop`, `ApplyIndicator`, `ApplyReset`) + **roteadores** (`RegisterDrop`, `OnContinueButtonPressed`, `ResetToStart`).
- Novo componente **`ReactionNetworkSync : NetworkBehaviour`**:
  - `NetworkVariable<int>` do indicador + `NetworkList<int>` dos frascos que reagiram (server-write).
  - Aluno → `ServerRpc` → servidor atualiza estado → `OnValueChanged`/`OnListChanged` chamam os `Apply*` em **todos os peers** (incl. host).
- **Sincronização por índice** em `allFlasks` (mesma ordem em todos os peers, pois é uma única cena).

**Garantia offline:** sem o componente de rede, os roteadores caem no caminho local → comportamento idêntico ao original.

---

## 8. Canvases e extrato do conta-gotas não apareciam no tablet

**Sintoma:** o tablet não via os canvases sumirem (painel inicial, canvas de prática), nem a barreira sumir, nem o extrato (cor) no conta-gotas.

**Causa raiz:** tudo isso era **estado visual local** (`SetActive`, `liquidController.AbsorbIndicator`) — não networkado. Mesma premissa do topo do doc.

**Correção — Opção A:**
- Novo **`SceneStateNetworkSync : NetworkBehaviour`**: `NetworkVariable<bool>` pra "painel escondido" e "prática iniciada"; aluno pede via `ServerRpc` → todos aplicam `SetActive`.
- Novo **`DropperNetworkSync : NetworkBehaviour`**: replica o booleano "tem amostra" (owner-write); a **cor é derivada localmente** do indicador já sincronizado (não trafega cor).
- `UIStartButton`, `PracticeStartController`, `DropperController` roteados por esses componentes (teleporte e áudio continuam **locais** por design).

**O que continua local de propósito:** a **gota caindo** (`dropPrefab` instanciado local). O tablet vê o **resultado** (frasco mudando), não a animação da gota.

---

## 9. Recipiente do extrato "pula" pra origem no host

**Sintoma:** online, no host, o extrato de repolho ia pra origem ("ao lado do player") em vez de ficar no lugar.

**Causa raiz:** o líquido do extrato (`CRepolho`, um `LiquidController`) é **filho de um recipiente que foi tornado grabbable em rede** (NetworkObject + Rigidbody **kinematic** + NetworkTransform). Como é kinematic, **não é física** — é a **NetworkTransform resetando a pose no spawn** (comportamento clássico de NetworkObject in-scene aninhado).

**Correção:**
- **Sacada:** objeto **estático não precisa de rede** — ele já está na cena sincronizada e aparece certo no host só por existir. Networkar um objeto parado **só introduz o bug do snap**.
- **Se o recipiente é fixo (caso provável):** remover `NetworkObject` + `OwnerNetworkTransform` + `NetworkGrabbable` dele → vira geometria estática, sem snap.
- **Se for mesmo pego/movido:** manter em rede mas ligar **"In Local Space"** na `OwnerNetworkTransform` **ou** mover o NetworkObject pra **raiz da cena** (NGO quer NetworkObjects in-scene na raiz ou sob outro NetworkObject, não aninhados sob móveis).

> 🔁 Regra geral: **só networkar o que o aluno fisicamente pega e move** (conta-gotas, frascos levantados). Bancada, vidrarias fixas e decoração devem ficar **estáticas**.

---

## 10. Botão da prática (2º canvas) — investigação

**Sintoma:** o botão de iniciar prática "não funcionava".

**Investigação:** a fiação está **correta** — `StartPracticeButton` tem `BoxCollider (Is Trigger = 1)` + `VRButtonTrigger.onPressed → PracticeStartController.StartPractice`, e o controller tem todas as refs atribuídas. Não é problema de EventSystem/raycast (é por collider físico, igual ao 1º botão).

**Pendente (diagnóstico aberto):** confirmar no logcat se aparece `Botão VR pressionado` ao tocar:
- **Não aparece nada** → físico: o collider pode estar deslocado do visual, fora de alcance, ou o canvas pai inativo no momento.
- **Aparece mas nada acontece** → `canvasStart` apontado não é o canvas visível.

---

## Arquivos NOVOS (não existem no plugin VRLAN original)

Scripts de rede **específicos do jogo** (ficam em `Assets/Scripts/`, no Assembly-CSharp, que auto-referencia o `VRLAN.Network`):

| Arquivo | Papel |
|---|---|
| `Assets/Scripts/ReactionNetworkSync.cs` | Replica indicador + frascos reagidos (NetworkVariable/NetworkList + ServerRpc) |
| `Assets/Scripts/SceneStateNetworkSync.cs` | Replica visibilidade de painel inicial / canvas de prática / barreira |
| `Assets/Scripts/DropperNetworkSync.cs` | Replica o líquido absorvido pelo conta-gotas (cor derivada do indicador) |

---

## Arquivos MODIFICADOS (jogo)

Todos refatorados pro padrão **roteador + Apply** (online roteia pela rede; offline aplica local — **comportamento offline preservado**):

| Arquivo | Mudança |
|---|---|
| `Assets/Scripts/ReactionManager.cs` | `Awake` resolve `net`; `RegisterDrop`/`OnContinueButtonPressed`/`ResetToStart` viraram roteadores; extraídos `ApplyDrop(x2)`/`ApplyIndicator`/`ApplyReset` |
| `Assets/Scripts/UIStartButton.cs` | Teleporte via `LocalXRRig.Instance.TeleportTo`; esconder painel via `SceneStateNetworkSync`; `cameraRigRoot` virou fallback |
| `Assets/Scripts/PracticeStartController.cs` | Esconder canvas/barreira via `SceneStateNetworkSync`; áudio local |
| `Assets/Scripts/DropperController.cs` | Absorver/esvaziar via `DropperNetworkSync` |

---

## Diferenças em relação ao plugin VRLAN original

O VRLAN original é **XRI-first** e seu código **não depende do Meta SDK** (o asmdef `VRLAN.Network` só referencia Netcode/TMP/UI/Transport/Collections). Esta integração:

1. **Rig é Meta SDK (OVRCameraRig)** em vez de XRI — `LocalXRRig` ligado a `CenterEyeAnchor`/`Left+RightHandAnchor`. O `InteractorReRegister` (fix do XRI) é dispensável aqui.
2. **Grab é Meta Interaction SDK** (`Grabbable`), não `XR Grab Interactable`. O evento de grab do Meta liga em `NetworkGrabbable.OnGrabbed` (via `InteractableUnityEventWrapper`).
3. **Camada de rede de gameplay nova** (não faz parte do VRLAN): `ReactionNetworkSync`, `SceneStateNetworkSync`, `DropperNetworkSync` — porque o lab tem muito estado visual local que precisa replicar pro espectador.
4. **NGO 1.x**: uso explícito de `OwnerNetworkTransform` (Compat1x) pra autoridade do dono no avatar e nos grabbables.
5. **SharedWorld** = a cena de química real (`SharedWorld 2`), tornada device-agnostic (sem rig/câmera/NetworkManager).

---

## Pendências / checklist de wiring no editor

- [ ] Aplicar as permissões no `AndroidManifest.xml` (seção 1) e revalidar após cada build.
- [ ] `StudentAvatar`: criar nó `Head` + `OwnerNetworkTransform` em head/mãos + ligar campo `Head` (seção 4).
- [ ] Converter `LiquidReactionShader.shadergraph` pra target **URP/Universal** (seção 6).
- [ ] Adicionar `ReactionNetworkSync` + `SceneStateNetworkSync` no objeto `ReactionManager` (já tem NetworkObject) e atribuir os campos (painel/canvas/barreira).
- [ ] Adicionar `DropperNetworkSync` no conta-gotas (grabbable em rede).
- [ ] **Destatizar** recipientes/objetos fixos que foram networkados por engano (seção 9) — manter rede só no que o aluno pega/move.
- [ ] Confirmar o diagnóstico do botão de prática pelo logcat (seção 10).

---

## Princípios de arquitetura (pra não repetir os bugs)

1. **O tablet renderiza a própria cópia da cena.** Só o que é networkado aparece nele. Toda mudança visual relevante precisa de rede — ou some no espectador.
2. **Objeto estático não leva NetworkObject.** Ele já está na cena sincronizada; networkar só adiciona o bug do snap e gasta banda.
3. **Networkar só o que se move por interação** (avatar, conta-gotas, frascos pegos). Decoração e mobília ficam estáticos.
4. **Autoridade do dono (NGO 1.x):** quem move o objeto (o cliente) precisa de `OwnerNetworkTransform`; o `NetworkTransform` padrão é server-auth e não replica o movimento do cliente.
5. **Padrão roteador + Apply:** lógica de jogo fica offline-safe (MonoBehaviour); a rede entra como camada opcional que só roteia quando há NetworkManager. Mesma cena roda offline e online.
6. **Device-specific persiste; SharedWorld é agnóstica.** Rig, câmera, UI de conexão e espectador persistem das cenas-boot via `PersistAcrossScenes`; a SharedWorld não os contém.
