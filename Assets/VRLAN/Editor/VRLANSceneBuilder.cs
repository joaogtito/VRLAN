using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VRLAN.Network.Discovery;
using VRLAN.Network.Spectator;
using VRLAN.Network.Teleport;
using VRLAN.Network.UI;
using static VRLAN.Network.EditorTools.VRLANUIKit;

namespace VRLAN.Network.EditorTools
{
    // Monta (ou remonta) as interfaces das cenas de exemplo no padrão visual do VRLAN:
    //   - TabletScene Teste: painel do professor (setup → aguardando óculos → ao vivo).
    //   - QuestLobby: UI de conexão do aluno + sala de espera "coffee break".
    //   - HostListItem.prefab: item da lista de salas no Quest.
    //
    // É idempotente: apaga os filhos dos Canvas e reconstrói, religando os campos dos componentes
    // (ConnectionUI, QuestConnectionUI, StudentSelectorPanel, TeleportCommandPanel, TabletDashboard).
    // Menu: VRLAN → Rebuild Sample UI. Batch: -executeMethod VRLAN.Network.EditorTools.VRLANSceneBuilder.BuildAllBatch
    public static class VRLANSceneBuilder
    {
        public const string ScenesDir = "Assets/VRLAN/Samples/Scenes";
        public const string TabletScenePath = ScenesDir + "/TabletScene Teste.unity";
        public const string QuestScenePath = ScenesDir + "/QuestLobby.unity";
        public const string SharedScenePath = ScenesDir + "/SharedWorld.unity";
        const string HostItemPath = "Assets/VRLAN/Prefabs/HostListItem.prefab";
        const string SharedWorldPrefabPath = "Assets/VRLAN/Prefabs/SharedWorld.prefab";

        [MenuItem("VRLAN/Rebuild Sample UI + Lobby")]
        public static void BuildAll()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildAllInternal();
        }

        public static void BuildAllBatch()
        {
            BuildAllInternal();
            var dir = ArgValue("-vrlanPreviewDir");
            if (!string.IsNullOrEmpty(dir)) VRLANPreview.CaptureAll(dir);
        }

        public static void PreviewOnlyBatch()
        {
            EnsureAssets();
            VRLANPreview.CaptureAll(ArgValue("-vrlanPreviewDir") ?? "Temp/VRLANPreview");
        }

        static void BuildAllInternal()
        {
            EnsureAssets();
            BuildHostListItem();
            BuildTabletScene();
            BuildQuestScene();
            AssetDatabase.SaveAssets();
            Debug.Log("[VRLANSceneBuilder] cenas de exemplo reconstruídas.");
        }

        static string ArgValue(string name)
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == name) return args[i + 1];
            return null;
        }

        // =====================================================================
        // TABLET
        // =====================================================================

        static void BuildTabletScene()
        {
            var scene = EditorSceneManager.OpenScene(TabletScenePath, OpenSceneMode.Single);
            RemoveSharedWorldInstances(scene);

            var connection = Object.FindFirstObjectByType<ConnectionUI>(FindObjectsInactive.Include);
            var canvasGo = connection.gameObject;
            var spectator = Object.FindFirstObjectByType<SpectatorView>(FindObjectsInactive.Include);
            var bridge = Object.FindFirstObjectByType<LanDiscoveryHostBridge>(FindObjectsInactive.Include);
            Str(bridge, "hostName", "Sala VRLAN");

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

            var root = (RectTransform)canvasGo.transform;
            ClearChildren(root);
            foreach (var old in canvasGo.GetComponents<TabletDashboard>()) Object.DestroyImmediate(old);

            // ---------- Aguardando óculos (tela preta) ----------
            var waiting = Stretch(Node("WaitingGroup", root));
            Fill(waiting, Color.black);
            var ringRt = Pin(Node("Pulse", waiting), Center, new Vector2(0, 150), new Vector2(170, 170));
            var ring = ringRt.gameObject.AddComponent<Image>();
            ring.sprite = Ring; ring.color = Accent; ring.raycastTarget = false;
            var core = Dot(ringRt, "Core", Accent, 34);
            Pin(core.rectTransform, Center, Vector2.zero, new Vector2(34, 34));
            var wTitle = Label(waiting, "Title", "Aguardando óculos", 54, true, Ink, TextAlignmentOptions.Center);
            Pin(wTitle.rectTransform, Center, new Vector2(0, 10), new Vector2(1200, 80));
            var wHint = Label(waiting, "Hint", "Peça para o aluno colocar o Meta Quest, abrir o VRLAN e entrar na sala.",
                26, false, TextDim, TextAlignmentOptions.Top);
            Pin(wHint.rectTransform, Center, new Vector2(0, -85), new Vector2(980, 90));

            var steps = Pin(Node("Steps", waiting), Center, new Vector2(0, -225), new Vector2(1200, 64));
            var sh = steps.gameObject.AddComponent<HorizontalLayoutGroup>();
            sh.spacing = 18; sh.childAlignment = TextAnchor.MiddleCenter;
            sh.childControlWidth = sh.childControlHeight = true; sh.childForceExpandWidth = sh.childForceExpandHeight = false;
            Step(steps, "1", "Coloque o Meta Quest");
            Step(steps, "2", "Abra o VRLAN");
            Step(steps, "3", "Toque na sala da turma");

            // ---------- Ao vivo (barras laterais por cima da visão do aluno) ----------
            var live = Stretch(Node("LiveGroup", root));

            var badgeText = Pill(live, "LiveBadge", "AO VIVO", Live, 22, Panel, out var badge, out _);
            badge.anchorMin = badge.anchorMax = TopCenter; badge.pivot = TopCenter;
            badge.anchoredPosition = new Vector2(0, -124);

            var studentsSide = Sidebar(live, "StudentsPanel", left: true, "Alunos", "Toque para ver a visão do aluno",
                out var studentsContent);
            var studentTemplate = Btn(studentsContent, "StudentButton", "Aluno", Style.List, 26, 16);
            Height(studentTemplate, 76);
            studentTemplate.gameObject.SetActive(false);

            var tpSide = Sidebar(live, "TeleportPanel", left: false, "Teleporte", "Leve o aluno até um ponto",
                out var tpContent);
            var tpTemplate = Btn(tpContent, "TeleportButton", "Ponto", Style.Secondary, 26, 16);
            Height(tpTemplate, 76);
            tpTemplate.gameObject.SetActive(false);

            var tpPanel = tpSide.gameObject.AddComponent<TeleportCommandPanel>();
            Ref(tpPanel, "container", tpContent);
            Ref(tpPanel, "buttonTemplate", tpTemplate);

            var selector = studentsSide.gameObject.AddComponent<StudentSelectorPanel>();
            Ref(selector, "container", studentsContent);
            Ref(selector, "buttonTemplate", studentTemplate);
            Ref(selector, "spectator", spectator);
            Ref(selector, "teleportPanel", tpPanel);
            Col(selector, "selectedColor", Accent);
            Col(selector, "normalColor", CardHi);

            // ---------- Barra superior ----------
            var top = TopRow(Node("TopBar", root), 0, 100);
            Fill(top, Panel);
            var brand = Label(top, "Brand", "VRLAN  ·  SALA", 17, true, Accent);
            brand.characterSpacing = 8;
            Pin(brand.rectTransform, TopLeft, new Vector2(40, -18), new Vector2(400, 26));
            var roomName = Label(top, "RoomName", "Sala VRLAN", 32, true, Ink);
            Pin(roomName.rectTransform, TopLeft, new Vector2(40, -42), new Vector2(560, 46));

            var statusText = Pill(top, "Status", "Sala aberta", Success, 22, CardHi, out var statusPill, out _);
            statusPill.anchorMin = statusPill.anchorMax = new Vector2(0, 0.5f); statusPill.pivot = new Vector2(0, 0.5f);
            statusPill.anchoredPosition = new Vector2(640, 0);

            var stop = Btn(top, "Stop", "Encerrar sala", Style.Danger, 24, 14);
            Pin((RectTransform)stop.transform, new Vector2(1, 0.5f), new Vector2(-40, 0), new Vector2(250, 60));

            // ---------- Setup (sala fechada) ----------
            var setup = Stretch(Node("SetupGroup", root));
            Fill(setup, Bg, raycast: true);
            var glow = Pin(Node("Glow", setup), Center, new Vector2(0, 0), new Vector2(1500, 1500));
            var glowImg = glow.gameObject.AddComponent<Image>();
            glowImg.sprite = Circle; glowImg.color = A(Accent, 0.03f); glowImg.raycastTarget = false;

            var card = Pin(Node("Card", setup), Center, Vector2.zero, new Vector2(780, 640));
            Fill(card, Panel, 32);
            var cBrand = Label(card, "Brand", "VRLAN", 22, true, Accent);
            cBrand.characterSpacing = 14;
            TopRow(cBrand.rectTransform, 56, 30, 60, 60);
            var cTitle = Label(card, "Title", "Painel do professor", 54, true, Ink);
            TopRow(cTitle.rectTransform, 92, 76, 60, 60);
            var cSub = Label(card, "Subtitle",
                "Abra uma sala para os óculos da turma se conectarem pela rede local — sem internet.",
                24, false, TextDim, TextAlignmentOptions.TopLeft);
            TopRow(cSub.rectTransform, 176, 76, 60, 60);
            var cLbl = Label(card, "RoomLabel", "Nome da sala", 20, true, TextDim);
            TopRow(cLbl.rectTransform, 286, 30, 60, 60);
            var hostName = Input(card, "HostNameInput", "Ex.: Laboratório de Química", 28);
            TopRow((RectTransform)hostName.transform, 322, 80, 60, 60);
            var host = Btn(card, "Host", "Abrir sala", Style.Primary, 30, 18);
            TopRow((RectTransform)host.transform, 432, 88, 60, 60);
            var cFoot = Label(card, "Footer", "Rede local  ·  funciona sem internet", 19, false, TextFaint,
                TextAlignmentOptions.Center);
            TopRow(cFoot.rectTransform, 562, 30, 60, 60);

            // ---------- Wiring ----------
            Ref(connection, "hostButton", host);
            Ref(connection, "clientButton", null);
            Ref(connection, "stopButton", stop);
            Ref(connection, "ipInput", null);
            Ref(connection, "portInput", null);
            Ref(connection, "hostNameInput", hostName);
            Ref(connection, "discoveryBridge", bridge);
            Ref(connection, "statusText", statusText);

            var dash = canvasGo.AddComponent<TabletDashboard>();
            Ref(dash, "spectator", spectator);
            Ref(dash, "discoveryBridge", bridge);
            Ref(dash, "setupGroup", setup.gameObject);
            Ref(dash, "waitingGroup", waiting.gameObject);
            Ref(dash, "liveGroup", live.gameObject);
            Ref(dash, "topBar", top.gameObject);
            Ref(dash, "roomNameText", roomName);
            Ref(dash, "waitingHintText", wHint);
            Ref(dash, "liveBadgeText", badgeText);
            Ref(dash, "pulse", ring);

            // Estado inicial salvo = Setup (o dashboard reaplica no Start).
            waiting.gameObject.SetActive(false);
            live.gameObject.SetActive(false);
            top.gameObject.SetActive(false);

            // Câmera espectadora: preto enquanto não há aluno (o SpectatorView aplica no Awake).
            var cam = spectator.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static void Step(Transform parent, string number, string text)
        {
            var t = Pill(parent, "Step" + number, text, Accent, 22, Card, out var pill, out var dot);
            t.fontStyle = FontStyles.Normal;
            t.font = FontRegular;
            t.color = TextDim;
            // número dentro do ponto
            dot.rectTransform.sizeDelta = new Vector2(34, 34);
            var le = dot.GetComponent<LayoutElement>(); le.preferredWidth = le.minWidth = le.preferredHeight = le.minHeight = 34;
            var n = Label(dot.transform, "N", number, 18, true, Ink, TextAlignmentOptions.Center);
            Stretch(n.rectTransform);
        }

        static RectTransform Sidebar(Transform parent, string name, bool left, string title, string subtitle,
            out RectTransform content)
        {
            var rt = Node(name, parent);
            rt.anchorMin = new Vector2(left ? 0 : 1, 0); rt.anchorMax = new Vector2(left ? 0 : 1, 1);
            rt.pivot = new Vector2(left ? 0 : 1, 0.5f);
            rt.sizeDelta = new Vector2(360, -(124 + 28));                  // top 124, bottom 28
            rt.anchoredPosition = new Vector2(left ? 28 : -28, -(124 - 28) / 2f);
            Fill(rt, Panel, 26);

            var t = Label(rt, "Title", title, 30, true, Ink);
            TopRow(t.rectTransform, 26, 40, 28, 28);
            var s = Label(rt, "Subtitle", subtitle, 18, false, TextDim);
            TopRow(s.rectTransform, 66, 28, 28, 28);

            var listArea = Stretch(Node("List", rt), 18, 18, 112, 18);
            content = ScrollList(listArea, 10);
            return rt;
        }

        // =====================================================================
        // HOST LIST ITEM (Quest)
        // =====================================================================

        static void BuildHostListItem()
        {
            var root = PrefabUtility.LoadPrefabContents(HostItemPath);
            try
            {
                foreach (var c in root.GetComponents<Component>())
                    if (c is not RectTransform && c is not Button && c is not Image && c is not CanvasRenderer)
                        Object.DestroyImmediate(c);
                ClearChildren(root.transform);

                var rt = (RectTransform)root.transform;
                rt.sizeDelta = new Vector2(600, 112);
                var img = Fill(rt, CardHi, 20, raycast: true);
                var b = root.GetComponent<Button>();
                b.targetGraphic = img;
                var cb = b.colors;
                cb.normalColor = new Color(0.8f, 0.8f, 0.82f, 1f); cb.highlightedColor = Color.white;
                cb.pressedColor = new Color(0.65f, 0.7f, 0.8f, 1f); cb.selectedColor = cb.normalColor;
                b.colors = cb;
                Height(rt, 112);

                var icon = Dot(rt, "Icon", A(Accent, 0.18f), 60);
                Pin(icon.rectTransform, new Vector2(0, 0.5f), new Vector2(26, 0), new Vector2(60, 60));
                var iconDot = Dot(icon.transform, "Signal", Accent, 20);
                Pin(iconDot.rectTransform, Center, Vector2.zero, new Vector2(20, 20));

                var title = Label(rt, "Title", "Sala VRLAN", 30, true, Ink);
                title.rectTransform.anchorMin = new Vector2(0, 0.5f); title.rectTransform.anchorMax = new Vector2(1, 0.5f);
                title.rectTransform.pivot = new Vector2(0, 0);
                title.rectTransform.offsetMin = new Vector2(108, 2); title.rectTransform.offsetMax = new Vector2(-170, 44);
                title.textWrappingMode = TextWrappingModes.NoWrap;
                title.overflowMode = TextOverflowModes.Ellipsis;

                var detail = Label(rt, "Detail", "192.168.0.10  ·  porta 7777", 20, false, TextDim);
                detail.rectTransform.anchorMin = new Vector2(0, 0.5f); detail.rectTransform.anchorMax = new Vector2(1, 0.5f);
                detail.rectTransform.pivot = new Vector2(0, 1);
                detail.rectTransform.offsetMin = new Vector2(108, -34); detail.rectTransform.offsetMax = new Vector2(-170, -2);
                detail.textWrappingMode = TextWrappingModes.NoWrap;
                detail.overflowMode = TextOverflowModes.Ellipsis;

                var enter = Node("EnterPill", rt);
                Pin(enter, new Vector2(1, 0.5f), new Vector2(-24, 0), new Vector2(128, 52));
                Fill(enter, Accent, 26);
                var et = Label(enter, "EnterLabel", "Entrar", 22, true, Ink, TextAlignmentOptions.Center);
                Stretch(et.rectTransform);

                PrefabUtility.SaveAsPrefabAsset(root, HostItemPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // =====================================================================
        // QUEST
        // =====================================================================

        static void BuildQuestScene()
        {
            var scene = EditorSceneManager.OpenScene(QuestScenePath, OpenSceneMode.Single);
            RemoveSharedWorldInstances(scene);

            // Rig no chão, na origem, olhando pra +Z (o painel fica à frente).
            var rig = Object.FindFirstObjectByType<VRLAN.Network.Avatar.LocalXRRig>(FindObjectsInactive.Include);
            if (rig != null)
            {
                var rigRoot = rig.transform;
                rigRoot.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            }

            VRLANLobbyRoom.Build(scene);
            BuildQuestUI();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static void BuildQuestUI()
        {
            var ui = Object.FindFirstObjectByType<QuestConnectionUI>(FindObjectsInactive.Include);
            var canvasGo = ui.gameObject;
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 10;   // UI por cima de outros transparentes (ex.: letreiro TMP da sala)
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            if (scaler != null) { scaler.dynamicPixelsPerUnit = 2; scaler.referencePixelsPerUnit = 100; }

            var root = (RectTransform)canvasGo.transform;
            root.sizeDelta = new Vector2(1320, 840);
            root.localScale = Vector3.one * 0.00125f;   // 1,65 m de largura
            root.SetPositionAndRotation(new Vector3(0f, 1.42f, 1.8f), Quaternion.Euler(5f, 0f, 0f));
            ClearChildren(root);

            // Fundo do cartão
            var bgRt = Stretch(Node("Background", root));
            Fill(bgRt, Panel, 44, raycast: true);

            // Cabeçalho
            var brand = Label(root, "Brand", "VRLAN", 22, true, Accent);
            brand.characterSpacing = 14;
            Pin(brand.rectTransform, TopLeft, new Vector2(64, -56), new Vector2(400, 30));
            var title = Label(root, "Title", "Sala de espera", 58, true, Ink);
            Pin(title.rectTransform, TopLeft, new Vector2(64, -88), new Vector2(800, 78));
            var sub = Label(root, "Subtitle", "Pegue um café — o professor já vai abrir a sala.", 26, false, TextDim);
            Pin(sub.rectTransform, TopLeft, new Vector2(64, -168), new Vector2(900, 40));

            var status = Pill(root, "Status", "Procurando salas...", Accent, 22, CardHi, out var statusPill, out _);
            statusPill.anchorMin = statusPill.anchorMax = TopRight; statusPill.pivot = TopRight;
            statusPill.anchoredPosition = new Vector2(-64, -60);

            // ---------- Scanning ----------
            var scanning = Stretch(Node("ScanningUi", root), 64, 64, 244, 56);

            // Coluna esquerda: nome + como funciona
            var leftCol = Node("LeftColumn", scanning);
            leftCol.anchorMin = new Vector2(0, 0); leftCol.anchorMax = new Vector2(0, 1); leftCol.pivot = new Vector2(0, 0.5f);
            leftCol.sizeDelta = new Vector2(500, 0); leftCol.anchoredPosition = Vector2.zero;

            var nameLbl = Label(leftCol, "NameLabel", "Seu nome", 22, true, TextDim);
            TopRow(nameLbl.rectTransform, 0, 32);
            var nameInput = Input(leftCol, "InputField (TMP)", "Toque para digitar", 30);
            TopRow((RectTransform)nameInput.transform, 40, 88);
            var kb = nameInput.gameObject.AddComponent<SystemKeyboardInput>();
            var nameHint = Label(leftCol, "NameHint", "É assim que o professor vai ver você na lista.", 20, false, TextFaint);
            TopRow(nameHint.rectTransform, 138, 30);

            var how = Node("HowItWorks", leftCol);
            how.anchorMin = new Vector2(0, 0); how.anchorMax = new Vector2(1, 0); how.pivot = new Vector2(0.5f, 0);
            how.offsetMin = new Vector2(0, 0); how.offsetMax = new Vector2(0, 300);
            Fill(how, Card, 24);
            var howT = Label(how, "Title", "Como funciona", 26, true, Ink);
            TopRow(howT.rectTransform, 26, 36, 30, 30);
            HowLine(how, 80, "1", "Digite seu nome");
            HowLine(how, 146, "2", "Escolha a sala do professor");
            HowLine(how, 212, "3", "Pronto: você entra no mundo da aula");

            // Coluna direita: salas + ações
            var rightCol = Node("RightColumn", scanning);
            rightCol.anchorMin = new Vector2(0, 0); rightCol.anchorMax = new Vector2(1, 1);
            rightCol.offsetMin = new Vector2(540, 0); rightCol.offsetMax = Vector2.zero;

            var roomsLbl = Label(rightCol, "RoomsLabel", "Salas na rede", 22, true, TextDim);
            TopRow(roomsLbl.rectTransform, 0, 32);
            var listBg = Node("HostListScroll", rightCol);
            listBg.anchorMin = Vector2.zero; listBg.anchorMax = Vector2.one;
            listBg.offsetMin = new Vector2(0, 112); listBg.offsetMax = new Vector2(0, -40);
            Fill(listBg, Bg, 24);
            var listArea = Stretch(Node("ListArea", listBg), 14, 14, 14, 14);
            var hostContent = ScrollList(listArea, 12);

            var scan = Btn(rightCol, "ScanButton", "Procurar de novo", Style.Secondary, 24, 16);
            var scanRt = (RectTransform)scan.transform;
            scanRt.anchorMin = new Vector2(0, 0); scanRt.anchorMax = new Vector2(0.5f, 0); scanRt.pivot = new Vector2(0, 0);
            scanRt.offsetMin = new Vector2(0, 0); scanRt.offsetMax = new Vector2(-8, 88);
            var manual = Btn(rightCol, "ManualEntry", "Digitar IP", Style.Ghost, 24, 16);
            var manRt = (RectTransform)manual.transform;
            manRt.anchorMin = new Vector2(0.5f, 0); manRt.anchorMax = new Vector2(1, 0); manRt.pivot = new Vector2(1, 0);
            manRt.offsetMin = new Vector2(8, 0); manRt.offsetMax = new Vector2(0, 88);
            Fill(manRt, A(Accent, 0.1f), 16, raycast: true);

            // ---------- Manual entry (sobreposto) ----------
            var manualPanel = Stretch(Node("ManualEntryPanel", root));
            Fill(manualPanel, Panel, 44, raycast: true);
            var mTitle = Label(manualPanel, "Title", "Conectar por IP", 50, true, Ink);
            Pin(mTitle.rectTransform, TopLeft, new Vector2(64, -64), new Vector2(620, 70));
            var mSub = Label(manualPanel, "Subtitle", "Use o endereço que aparece no tablet do professor.", 24, false, TextDim,
                TextAlignmentOptions.TopLeft);
            Pin(mSub.rectTransform, TopLeft, new Vector2(64, -140), new Vector2(560, 70));

            var display = Pin(Node("IpBox", manualPanel), TopLeft, new Vector2(64, -240), new Vector2(560, 112));
            Fill(display, Card, 20);
            var ipText = Label(display, "IpDisplay", "", 46, true, Ink, TextAlignmentOptions.Center);
            Stretch(ipText.rectTransform, 20, 20, 0, 0);
            ipText.textWrappingMode = TextWrappingModes.NoWrap;

            var back = Btn(manualPanel, "Back", "Voltar", Style.Secondary, 26, 16);
            Pin((RectTransform)back.transform, TopLeft, new Vector2(64, -392), new Vector2(270, 88));
            var connect = Btn(manualPanel, "Connect", "Conectar", Style.Primary, 26, 16);
            Pin((RectTransform)connect.transform, TopLeft, new Vector2(354, -392), new Vector2(270, 88));

            var pad = Pin(Node("KeyPad", manualPanel), TopRight, new Vector2(-64, -150), new Vector2(3 * 168 + 2 * 14, 4 * 112 + 3 * 14));
            var grid = pad.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(168, 112); grid.spacing = new Vector2(14, 14);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 3;
            string[] keys = { "1", "2", "3", "4", "5", "6", "7", "8", "9", ".", "0", "Apagar" };
            foreach (var k in keys)
            {
                var key = Btn(pad, k == "Apagar" ? "Delete" : k, k, Style.Secondary, k == "Apagar" ? 24 : 40, 18);
                if (k == "Apagar")
                    UnityEventTools.AddPersistentListener(key.onClick, new UnityAction(ui.Backspace));
                else
                    UnityEventTools.AddStringPersistentListener(key.onClick, new UnityAction<string>(ui.AppendChar), k);
            }

            // ---------- Conectado (sobreposto) ----------
            var connected = Stretch(Node("ConnectedUi", root));
            Fill(connected, Panel, 44, raycast: true);
            var okRing = Pin(Node("Ring", connected), Center, new Vector2(0, 170), new Vector2(150, 150));
            var okImg = okRing.gameObject.AddComponent<Image>(); okImg.sprite = Ring; okImg.color = Success; okImg.raycastTarget = false;
            var okDot = Dot(okRing, "Core", Success, 52);
            Pin(okDot.rectTransform, Center, Vector2.zero, new Vector2(52, 52));
            var cTitle = Label(connected, "Title", "Você está na sala!", 54, true, Ink, TextAlignmentOptions.Center);
            Pin(cTitle.rectTransform, Center, new Vector2(0, 20), new Vector2(1000, 80));
            var cSub = Label(connected, "Subtitle", "Aguarde: a aula vai começar em instantes.", 26, false, TextDim,
                TextAlignmentOptions.Center);
            Pin(cSub.rectTransform, Center, new Vector2(0, -44), new Vector2(1000, 44));
            var disconnect = Btn(connected, "Disconnect", "Sair da sala", Style.Danger, 26, 16);
            Pin((RectTransform)disconnect.transform, Center, new Vector2(0, -170), new Vector2(340, 88));

            // status por cima de tudo
            statusPill.SetAsLastSibling();

            // ---------- Wiring ----------
            Ref(ui, "nameInput", nameInput);
            Ref(ui, "statusText", status);
            Ref(ui, "scanningUi", scanning.gameObject);
            Ref(ui, "hostListContent", hostContent);
            Ref(ui, "hostListItemPrefab", AssetDatabase.LoadAssetAtPath<Button>(HostItemPath));
            Ref(ui, "scanButton", scan);
            Ref(ui, "manualEntryButton", manual);
            Ref(ui, "connectedUi", connected.gameObject);
            Ref(ui, "disconnectButton", disconnect);
            Ref(ui, "manualEntryPanel", manualPanel.gameObject);
            Ref(ui, "ipDisplay", ipText);
            Ref(ui, "connectManualButton", connect);
            Ref(ui, "backManualButton", back);

            manualPanel.gameObject.SetActive(false);
            connected.gameObject.SetActive(false);
        }

        static void HowLine(RectTransform parent, float top, string n, string text)
        {
            var row = TopRow(Node("Step" + n, parent), top, 50, 30, 30);
            var dot = Dot(row, "N", A(Accent, 0.2f), 44);
            Pin(dot.rectTransform, new Vector2(0, 0.5f), Vector2.zero, new Vector2(44, 44));
            var nt = Label(dot.transform, "Text", n, 20, true, Accent, TextAlignmentOptions.Center);
            Stretch(nt.rectTransform);
            var t = Label(row, "Text", text, 22, false, TextDim);
            Stretch(t.rectTransform, 62, 0, 0, 0);
        }

        // =====================================================================

        static void RemoveSharedWorldInstances(Scene scene)
        {
            foreach (var go in scene.GetRootGameObjects())
            {
                if (!PrefabUtility.IsAnyPrefabInstanceRoot(go)) continue;
                var path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
                if (path == SharedWorldPrefabPath)
                {
                    Debug.Log($"[VRLANSceneBuilder] removendo instância de SharedWorld.prefab de '{scene.name}'");
                    Object.DestroyImmediate(go);
                }
            }
        }
    }
}
