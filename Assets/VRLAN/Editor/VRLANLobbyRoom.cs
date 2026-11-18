using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace VRLAN.Network.EditorTools
{
    // Monta a sala de espera do Quest: um "coffee break" aconchegante onde o aluno aguarda o professor.
    // Casca (piso, paredes, teto, janela) com primitivas + móveis do Kenney Furniture Kit (CC0).
    //
    // Layout (piso em y=0, aluno nasce na origem olhando +Z, painel de conexão a ~1,5 m à frente):
    //   parede esquerda  (x=-4.5): bancada de café (cafeteira, micro-ondas, geladeira) + ilha com banquetas
    //   parede direita   (x=+4.5): janelão "dia claro" + lounge (sofá, poltrona, mesa de centro, tapete)
    //   fundo            (z=+7)  : parede de destaque com o letreiro "coffee break" + mesas redondas
    //   atrás do aluno   (z=-3)  : estante, cabideiro, plantas
    public static class VRLANLobbyRoom
    {
        const string ArtDir = "Assets/VRLAN/Samples/Art";
        const string KitDir = ArtDir + "/KenneyFurniture";
        const string MatDir = ArtDir + "/Materials";
        const string RootName = "CoffeeBreakRoom";

        // Yaw que faz a FRENTE dos modelos do kit olhar pra +Z.
        public static float ModelFrontYaw = 0f;

        const float HalfW = 4.5f, ZBack = 7f, ZFront = -3f, Height = 3.2f;

        static Transform _root, _furniture;
        static float _k = 1f;   // escala do kit (Kenney → metros)
        static Scene _scene;

        public static void Build(Scene scene)
        {
            _scene = scene;
            foreach (var go in scene.GetRootGameObjects())
                if (go.name == RootName) Object.DestroyImmediate(go);

            var root = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(root, scene);
            _root = root.transform;
            _furniture = new GameObject("Furniture").transform;
            _furniture.SetParent(_root, false);

            Directory.CreateDirectory(MatDir);
            AssetDatabase.Refresh();

            _k = 0.92f / Mathf.Max(0.01f, RawHeight("kitchenCabinet"));
            Debug.Log($"[VRLANLobbyRoom] escala do kit = {_k:0.###}");

            BuildShell();
            BuildCoffeeCorner();
            BuildLounge();
            BuildTables();
            BuildBackOfRoom();
            BuildLights();
            BuildSign();
        }

        // ------------------------------------------------------------------ casca

        static void BuildShell()
        {
            var shell = new GameObject("Shell").transform;
            shell.SetParent(_root, false);

            var floor = Mat("Room_Floor", VRLANUIKit.Hex("B48A62"), 0.45f);
            var wall = Mat("Room_Wall", VRLANUIKit.Hex("EEE7DC"), 0.15f);
            var accent = Mat("Room_AccentWall", VRLANUIKit.Hex("C46F4E"), 0.2f);
            var ceiling = Mat("Room_Ceiling", VRLANUIKit.Hex("F6F3EE"), 0.05f);
            var trim = Mat("Room_Trim", VRLANUIKit.Hex("3B3029"), 0.35f);
            var glass = Unlit("Room_Window", VRLANUIKit.Hex("DCEBFF"));
            var plank = Mat("Room_FloorPlank", VRLANUIKit.Hex("A07850"), 0.45f);

            float depth = ZBack - ZFront, zMid = (ZBack + ZFront) / 2f;
            Box(shell, "Floor", new Vector3(0, -0.05f, zMid), new Vector3(HalfW * 2, 0.1f, depth), floor, collider: true, shadows: false);
            // faixas de "tábuas" no piso
            for (float x = -HalfW + 0.6f; x < HalfW; x += 1.2f)
                Box(shell, "Plank", new Vector3(x, 0.001f, zMid), new Vector3(0.02f, 0.002f, depth), plank, collider: false, shadows: false);

            Box(shell, "Ceiling", new Vector3(0, Height + 0.05f, zMid), new Vector3(HalfW * 2, 0.1f, depth), ceiling, collider: false, shadows: false);
            Box(shell, "WallBack", new Vector3(0, Height / 2, ZBack + 0.05f), new Vector3(HalfW * 2, Height, 0.1f), accent, collider: true, shadows: false);
            Box(shell, "WallFront", new Vector3(0, Height / 2, ZFront - 0.05f), new Vector3(HalfW * 2, Height, 0.1f), wall, collider: true, shadows: false);
            Box(shell, "WallLeft", new Vector3(-HalfW - 0.05f, Height / 2, zMid), new Vector3(0.1f, Height, depth), wall, collider: true, shadows: false);
            Box(shell, "WallRight", new Vector3(HalfW + 0.05f, Height / 2, zMid), new Vector3(0.1f, Height, depth), wall, collider: true, shadows: false);

            // rodapé
            Box(shell, "BaseLeft", new Vector3(-HalfW + 0.01f, 0.05f, zMid), new Vector3(0.03f, 0.1f, depth), trim, false, false);
            Box(shell, "BaseRight", new Vector3(HalfW - 0.01f, 0.05f, zMid), new Vector3(0.03f, 0.1f, depth), trim, false, false);
            Box(shell, "BaseFront", new Vector3(0, 0.05f, ZFront + 0.01f), new Vector3(HalfW * 2, 0.1f, 0.03f), trim, false, false);

            // janelão na parede direita (vidro emissivo + caixilhos)
            float wz0 = -1.2f, wz1 = 5.8f, wy0 = 0.85f, wy1 = 2.75f;
            Box(shell, "WindowGlass", new Vector3(HalfW - 0.02f, (wy0 + wy1) / 2, (wz0 + wz1) / 2),
                new Vector3(0.01f, wy1 - wy0, wz1 - wz0), glass, false, false);
            Box(shell, "WindowSill", new Vector3(HalfW - 0.09f, wy0 - 0.03f, (wz0 + wz1) / 2), new Vector3(0.2f, 0.06f, wz1 - wz0 + 0.1f), trim, false, false);
            Box(shell, "WindowHead", new Vector3(HalfW - 0.05f, wy1 + 0.03f, (wz0 + wz1) / 2), new Vector3(0.1f, 0.06f, wz1 - wz0 + 0.1f), trim, false, false);
            for (int i = 0; i <= 5; i++)
            {
                float z = Mathf.Lerp(wz0, wz1, i / 5f);
                Box(shell, "Mullion", new Vector3(HalfW - 0.05f, (wy0 + wy1) / 2, z), new Vector3(0.08f, wy1 - wy0, 0.06f), trim, false, false);
            }
            Box(shell, "Transom", new Vector3(HalfW - 0.05f, 2.15f, (wz0 + wz1) / 2), new Vector3(0.08f, 0.04f, wz1 - wz0), trim, false, false);
        }

        // ------------------------------------------------------------------ café

        static void BuildCoffeeCorner()
        {
            float wallX = -HalfW;
            var run = new[] { "kitchenCabinetDrawer", "kitchenCabinet", "kitchenSink", "kitchenCabinet", "kitchenCabinetDrawer" };
            float z = 1.6f;
            var tops = new List<GameObject>();
            foreach (var m in run)
            {
                var go = Put(m, new Vector3(wallX + 0.4f, 0, z), Vector3.right);
                SnapMinX(go, wallX + 0.02f);
                var b = Bounds(go);
                go.transform.position += new Vector3(0, 0, z - b.min.z);
                z = Bounds(go).max.z;
                tops.Add(go);

                var up = Put("kitchenCabinetUpper", new Vector3(wallX + 0.3f, 1.5f, Bounds(go).center.z), Vector3.right);
                SnapMinX(up, wallX + 0.02f);
            }
            var fridge = Put("kitchenFridgeLarge", new Vector3(wallX + 0.5f, 0, z + 0.5f), Vector3.right);
            SnapMinX(fridge, wallX + 0.02f);
            SnapMinZ(fridge, z + 0.05f);

            OnTop("kitchenCoffeeMachine", tops[1], Vector3.right, 0f);
            OnTop("kitchenMicrowave", tops[3], Vector3.right, 0f);
            OnTop("kitchenBlender", tops[0], Vector3.right, 0.1f);
            OnTop("toaster", tops[4], Vector3.right, -0.05f);

            // ilha com banquetas
            float islandX = -2.35f, iz = 2.4f;
            for (int i = 0; i < 2; i++)
            {
                var bar = Put("kitchenBar", new Vector3(islandX, 0, iz), Vector3.right);
                var b = Bounds(bar);
                bar.transform.position += new Vector3(0, 0, iz - b.min.z);
                iz = Bounds(bar).max.z;
            }
            for (int i = 0; i < 3; i++)
                Put("stoolBar", new Vector3(-1.55f, 0, 2.75f + i * 0.75f), Vector3.left);

            Put("trashcan", new Vector3(-4.15f, 0, 1.15f), Vector3.right, 0.5f);
        }

        // ------------------------------------------------------------------ lounge

        static void BuildLounge()
        {
            float cz = 2.3f;
            var rug = Put("rugRectangle", new Vector3(3.05f, 0, cz), Vector3.left, 1.6f);
            rug.transform.position += Vector3.up * 0.002f;
            var sofa = Put("loungeSofa", new Vector3(4.0f, 0, cz), Vector3.left);
            SnapMaxX(sofa, HalfW - 0.06f);
            Put("tableCoffee", new Vector3(2.95f, 0, cz), Vector3.left);
            Put("loungeChair", new Vector3(1.95f, 0, cz - 0.45f), new Vector3(1, 0, 0.35f));
            Put("loungeDesignChair", new Vector3(2.1f, 0, cz + 0.95f), new Vector3(1, 0, -0.5f));
            Put("lampRoundFloor", new Vector3(4.05f, 0, 0.75f), Vector3.left);
            Put("pottedPlant", new Vector3(4.05f, 0, 3.95f), Vector3.left);
            Put("pillowBlue", new Vector3(3.95f, 0.45f, cz - 0.55f), Vector3.left);
        }

        // ------------------------------------------------------------------ mesas

        static void BuildTables()
        {
            Table(new Vector3(-0.2f, 0, 5.3f), 0f);
            Table(new Vector3(2.15f, 0, 5.5f), 35f);
            var rug = Put("rugRound", new Vector3(1.0f, 0, 5.4f), Vector3.back, 1.9f);
            rug.transform.position += Vector3.up * 0.002f;
        }

        static void Table(Vector3 c, float rot)
        {
            var t = Put("tableRound", c, Vector3.forward);
            OnTop(Random(new[] { "plantSmall1", "plantSmall2", "plantSmall3" }, c), t, Vector3.back, 0f);
            for (int i = 0; i < 3; i++)
            {
                float a = (rot + i * 120f) * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
                Put("chairCushion", c + dir * 0.75f, -dir);
            }
        }

        static string Random(string[] options, Vector3 seed) =>
            options[Mathf.Abs(Mathf.RoundToInt(seed.x * 7 + seed.z * 13)) % options.Length];

        // ------------------------------------------------------------------ fundo/frente

        static void BuildBackOfRoom()
        {
            // atrás do aluno
            var shelf = Put("bookcaseOpen", new Vector3(-2.0f, 0, ZFront + 0.3f), Vector3.forward);
            SnapMinZ(shelf, ZFront + 0.02f);
            Put("coatRackStanding", new Vector3(2.9f, 0, ZFront + 0.45f), Vector3.forward);
            Put("pottedPlant", new Vector3(-3.95f, 0, ZFront + 0.5f), Vector3.forward);
            Put("pottedPlant", new Vector3(3.95f, 0, ZFront + 0.5f), Vector3.forward);

            // fundo: aparador baixo com rádio + planta, ladeando o letreiro
            var low = Put("bookcaseOpenLow", new Vector3(3.2f, 0, ZBack - 0.3f), Vector3.back);
            SnapMaxZ(low, ZBack - 0.02f);
            OnTop("radio", low, Vector3.back, 0f);
            Put("pottedPlant", new Vector3(4.0f, 0, ZBack - 0.45f), Vector3.back);
            Put("plantSmall2", new Vector3(-4.0f, 0, ZBack - 0.4f), Vector3.back, 2.2f);

            foreach (var x in new[] { -3.7f, 3.7f })
            {
                var lamp = Put("lampWall", new Vector3(x, 1.95f, ZBack - 0.1f), Vector3.back);
                SnapMaxZ(lamp, ZBack - 0.01f);
            }
        }

        // ------------------------------------------------------------------ luz

        static void BuildLights()
        {
            var lights = new GameObject("Lights").transform;
            lights.SetParent(_root, false);

            var spots = new[] { new Vector3(-2.6f, 0, 3.4f), new Vector3(1.0f, 0, 5.4f), new Vector3(3.0f, 0, 2.3f), new Vector3(0f, 0, 0.6f) };
            foreach (var p in spots)
            {
                var lamp = Put("lampSquareCeiling", new Vector3(p.x, Height - 0.6f, p.z), Vector3.forward);
                var b = Bounds(lamp);
                lamp.transform.position += Vector3.up * (Height - b.max.y);

                var lg = new GameObject("Point Light");
                lg.transform.SetParent(lights, false);
                lg.transform.position = new Vector3(p.x, Height - 0.75f, p.z);
                var l = lg.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = new Color(1f, 0.83f, 0.62f);
                l.intensity = 2.2f;
                l.range = 6.5f;
                l.shadows = LightShadows.None;
            }

            // Sol entrando pelo janelão (a casca não projeta sombra, então ele "atravessa" o teto).
            Light sun = null;
            foreach (var go in _scene.GetRootGameObjects())
            {
                var l = go.GetComponentInChildren<Light>(true);
                if (l != null && l.type == LightType.Directional) { sun = l; break; }
            }
            if (sun == null)
            {
                var sg = new GameObject("Directional Light");
                SceneManager.MoveGameObjectToScene(sg, _scene);
                sun = sg.AddComponent<Light>();
                sun.type = LightType.Directional;
            }
            sun.transform.rotation = Quaternion.Euler(42f, -62f, 0f);
            sun.color = new Color(1f, 0.94f, 0.84f);
            sun.intensity = 1.15f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.55f;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.78f, 0.80f, 0.86f);
            RenderSettings.ambientEquatorColor = new Color(0.62f, 0.56f, 0.50f);
            RenderSettings.ambientGroundColor = new Color(0.34f, 0.28f, 0.24f);
            RenderSettings.fog = false;
        }

        static void BuildSign()
        {
            var sign = new GameObject("Sign").transform;
            sign.SetParent(_root, false);
            sign.position = new Vector3(0, 2.05f, ZBack - 0.02f);

            var t = Text3D(sign, "Title", "coffee break", 1.05f, VRLANUIKit.FontBold, VRLANUIKit.Hex("FFF4E6"));
            t.rectTransform.anchoredPosition = new Vector2(0, 0.12f);
            var s = Text3D(sign, "Sub", "VRLAN  ·  sala de espera", 0.32f, VRLANUIKit.FontRegular, VRLANUIKit.Hex("FFE2CC"));
            s.characterSpacing = 6;
            s.rectTransform.anchoredPosition = new Vector2(0, -0.42f);
        }

        static TextMeshPro Text3D(Transform parent, string name, string text, float size, TMP_FontAsset font, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshPro>();
            t.font = font;
            t.text = text;
            t.fontSize = size * 10f;
            t.color = color;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.rectTransform.sizeDelta = new Vector2(8f, 1.5f);
            return t;
        }

        // ------------------------------------------------------------------ helpers

        static Material Mat(string name, Color color, float smoothness, Color? emission = null)
        {
            var path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", color);
            m.color = color;
            m.SetFloat("_Smoothness", smoothness);
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material Unlit(string name, Color color)
        {
            var path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (m != null && m.shader != shader) { AssetDatabase.DeleteAsset(path); m = null; }   // sem keywords herdadas
            if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
            m.SetColor("_BaseColor", color);
            m.color = color;
            EditorUtility.SetDirty(m);
            return m;
        }

        static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 size, Material mat, bool collider, bool shadows)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = size;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            go.isStatic = true;
            return go;
        }

        static GameObject LoadModel(string model)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{KitDir}/{model}.fbx");
            if (asset == null) Debug.LogError($"[VRLANLobbyRoom] modelo não encontrado: {model}");
            return asset;
        }

        static float RawHeight(string model)
        {
            var asset = LoadModel(model);
            if (asset == null) return 1f;
            var tmp = (GameObject)PrefabUtility.InstantiatePrefab(asset, _scene);
            tmp.transform.position = Vector3.zero;
            float h = Bounds(tmp).size.y;
            Object.DestroyImmediate(tmp);
            return h;
        }

        // Coloca um modelo com a FRENTE virada pra 'face', base apoiada em pos.y e centro (xz) em pos.
        static GameObject Put(string model, Vector3 pos, Vector3 face, float extraScale = 1f)
        {
            var asset = LoadModel(model);
            if (asset == null) return new GameObject($"MISSING {model}");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, _scene);
            go.transform.SetParent(_furniture, true);
            go.transform.localScale = Vector3.one * (_k * extraScale);
            float yaw = Mathf.Atan2(face.x, face.z) * Mathf.Rad2Deg + ModelFrontYaw;
            go.transform.rotation = Quaternion.Euler(0, yaw, 0);
            go.transform.position = Vector3.zero;
            var b = Bounds(go);
            go.transform.position = new Vector3(pos.x - b.center.x, pos.y - b.min.y, pos.z - b.center.z);
            foreach (var r in go.GetComponentsInChildren<Renderer>())
                r.shadowCastingMode = ShadowCastingMode.On;
            go.isStatic = true;
            return go;
        }

        static GameObject OnTop(string model, GameObject surface, Vector3 face, float offsetAlong)
        {
            var b = Bounds(surface);
            return Put(model, new Vector3(b.center.x, b.max.y, b.center.z + offsetAlong), face);
        }

        static Bounds Bounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        static void SnapMinX(GameObject go, float x) => go.transform.position += new Vector3(x - Bounds(go).min.x, 0, 0);
        static void SnapMaxX(GameObject go, float x) => go.transform.position += new Vector3(x - Bounds(go).max.x, 0, 0);
        static void SnapMinZ(GameObject go, float z) => go.transform.position += new Vector3(0, 0, z - Bounds(go).min.z);
        static void SnapMaxZ(GameObject go, float z) => go.transform.position += new Vector3(0, 0, z - Bounds(go).max.z);
    }
}
