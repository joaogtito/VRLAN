using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using VRLAN.Network.UI;

namespace VRLAN.Network.EditorTools
{
    // Gera imagens de prévia das cenas de exemplo (pro README / revisão visual), sem Play Mode:
    //   tablet_setup / tablet_waiting / tablet_live, quest_view / quest_room / quest_ui / quest_manual.
    // As cenas são abertas e modificadas só em memória (nunca salvas aqui).
    public static class VRLANPreview
    {
        const int W = 1920, H = 1080;

        [MenuItem("VRLAN/Capture Previews")]
        static void CaptureMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            VRLANUIKit.EnsureAssets();
            CaptureAll("Temp/VRLANPreview");
            EditorUtility.RevealInFinder("Temp/VRLANPreview");
        }

        public static void CaptureAll(string dir)
        {
            Directory.CreateDirectory(dir);
            // Sem isso o 1º render usa o placeholder ciano de shaders ainda compilando.
            bool async = EditorSettings.asyncShaderCompilation;
            EditorSettings.asyncShaderCompilation = false;
            try
            {
                CaptureQuest(dir);
                CaptureTablet(dir);
            }
            finally { EditorSettings.asyncShaderCompilation = async; }
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Debug.Log($"[VRLANPreview] prévias salvas em {Path.GetFullPath(dir)}");
        }

        // ------------------------------------------------------------------ Quest

        static void CaptureQuest(string dir)
        {
            EditorSceneManager.OpenScene(VRLANSceneBuilder.QuestScenePath, OpenSceneMode.Single);
            var ui = Object.FindFirstObjectByType<QuestConnectionUI>(FindObjectsInactive.Include);
            var canvas = (RectTransform)ui.transform;

            // conteúdo de exemplo na lista de salas
            var content = (Transform)Get(ui, "hostListContent");
            var item = (Button)Get(ui, "hostListItemPrefab");
            FakeHost(content, item, "Laboratório de Química", "192.168.0.12  ·  porta 7777");
            FakeHost(content, item, "Sala 2B · Física", "192.168.0.31  ·  porta 7777");
            var status = (TMP_Text)Get(ui, "statusText");
            status.text = "2 salas encontradas";
            var name = (TMP_InputField)Get(ui, "nameInput");
            name.text = "Beatriz";
            Refresh(canvas);

            Shot(dir + "/quest_view.png", new Vector3(0, 1.62f, 0), new Vector3(0, 1.38f, 1.8f), 78, everything: true);
            Shot(dir + "/quest_room.png", new Vector3(3.9f, 2.55f, -2.6f), new Vector3(-0.8f, 0.9f, 3.6f), 72, everything: true);
            Shot(dir + "/quest_room_back.png", new Vector3(-0.5f, 1.7f, 0.6f), new Vector3(0.3f, 1.3f, 7f), 80, everything: true,
                hide: canvas.gameObject);
            Shot(dir + "/quest_ui.png", canvas.position + canvas.rotation * new Vector3(0, 0, -1.3f), canvas.position, 50, everything: true);

            ((GameObject)Get(ui, "scanningUi")).SetActive(false);
            var manual = (GameObject)Get(ui, "manualEntryPanel");
            manual.SetActive(true);
            ((TMP_Text)Get(ui, "ipDisplay")).text = "192.168.0.12";
            status.text = "Procurando salas...";
            Refresh(canvas);
            Shot(dir + "/quest_manual.png", canvas.position + canvas.rotation * new Vector3(0, 0, -1.3f), canvas.position, 50, everything: true);
        }

        static void FakeHost(Transform content, Button prefab, string title, string detail)
        {
            var b = Object.Instantiate(prefab, content);
            foreach (var t in b.GetComponentsInChildren<TMP_Text>(true))
            {
                if (t.name == "Title") t.text = title;
                if (t.name == "Detail") t.text = detail;
            }
        }

        // ------------------------------------------------------------------ Tablet

        static void CaptureTablet(string dir)
        {
            EditorSceneManager.OpenScene(VRLANSceneBuilder.TabletScenePath, OpenSceneMode.Single);
            var shared = EditorSceneManager.OpenScene(VRLANSceneBuilder.SharedScenePath, OpenSceneMode.Additive);

            var dash = Object.FindFirstObjectByType<TabletDashboard>(FindObjectsInactive.Include);
            var canvasGo = dash.gameObject;
            var canvas = canvasGo.GetComponent<Canvas>();
            var rt = (RectTransform)canvasGo.transform;

            var setup = (GameObject)Get(dash, "setupGroup");
            var waiting = (GameObject)Get(dash, "waitingGroup");
            var live = (GameObject)Get(dash, "liveGroup");
            var top = (GameObject)Get(dash, "topBar");
            ((TMP_Text)Get(dash, "roomNameText")).text = "Laboratório de Química";
            ((TMP_Text)Get(dash, "waitingHintText")).text =
                "Peça para o aluno colocar o Meta Quest, abrir o VRLAN e entrar na sala <b>Laboratório de Química</b>.";
            ((TMP_Text)Get(dash, "liveBadgeText")).text = "AO VIVO  ·  Beatriz";
            var connection = canvasGo.GetComponent<ConnectionUI>();
            var status = (TMP_Text)Get(connection, "statusText");
            var hostName = (TMP_InputField)Get(connection, "hostNameInput");

            // câmera de prévia = "cabeça do aluno" na SharedWorld
            var camPos = new Vector3(0.2f, 1.62f, -1.6f);
            var look = new Vector3(-0.3f, 1.2f, 5f);
            const float fov = 62f, dist = 0.6f;
            var rot = Quaternion.LookRotation(look - camPos);
            canvas.renderMode = RenderMode.WorldSpace;
            rt.sizeDelta = new Vector2(W, H);
            float worldH = 2f * dist * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
            rt.localScale = Vector3.one * (worldH / H);
            rt.SetPositionAndRotation(camPos + rot * Vector3.forward * dist, rot);

            void State(bool s, bool w, bool l)
            {
                setup.SetActive(s); waiting.SetActive(w); live.SetActive(l); top.SetActive(!s);
                Refresh(rt);
            }

            hostName.text = "Laboratório de Química";
            State(true, false, false);
            Shot(dir + "/tablet_setup.png", camPos, look, fov, everything: false);

            status.text = "Sala aberta · nenhum aluno";
            State(false, true, false);
            Shot(dir + "/tablet_waiting.png", camPos, look, fov, everything: false);

            status.text = "Sala aberta · 3 alunos";
            var selector = live.GetComponentInChildren<StudentSelectorPanel>(true);
            var stContent = (Transform)Get(selector, "container");
            var stTemplate = (Button)Get(selector, "buttonTemplate");
            var names = new[] { "Beatriz", "Lucas", "Rafael" };
            for (int i = 0; i < names.Length; i++)
            {
                var b = Object.Instantiate(stTemplate, stContent);
                b.gameObject.SetActive(true);
                b.GetComponentInChildren<TMP_Text>(true).text = names[i];
                var cb = b.colors;
                cb.normalColor = i == 0 ? VRLANUIKit.Accent : VRLANUIKit.CardHi;
                b.colors = cb;
                b.targetGraphic.canvasRenderer.SetColor(cb.normalColor);   // sem Play Mode a transição não roda
            }
            var tp = live.GetComponentInChildren<Teleport.TeleportCommandPanel>(true);
            var tpContent = (Transform)Get(tp, "container");
            var tpTemplate = (Button)Get(tp, "buttonTemplate");
            foreach (var label in new[] { "Bancada de testes", "Entrada", "Janela" })
            {
                var b = Object.Instantiate(tpTemplate, tpContent);
                b.gameObject.SetActive(true);
                b.GetComponentInChildren<TMP_Text>(true).text = label;
            }
            State(false, false, true);
            Shot(dir + "/tablet_live.png", camPos, look, fov, everything: true);
        }

        // ------------------------------------------------------------------ util

        static object Get(Object target, string field)
        {
            var p = new SerializedObject(target).FindProperty(field);
            return p?.objectReferenceValue;
        }

        static void Refresh(RectTransform root)
        {
            foreach (var t in root.GetComponentsInChildren<TMP_Text>(true)) t.ForceMeshUpdate(true, true);
            foreach (var lg in root.GetComponentsInChildren<LayoutGroup>(true))
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)lg.transform);
            LayoutRebuilder.ForceRebuildLayoutImmediate(root);
            Canvas.ForceUpdateCanvases();
            foreach (var t in root.GetComponentsInChildren<TMP_Text>(true)) t.ForceMeshUpdate(true, true);
            Canvas.ForceUpdateCanvases();
        }

        static void Shot(string path, Vector3 pos, Vector3 lookAt, float fovV, bool everything, GameObject hide = null)
        {
            bool hidden = hide != null && hide.activeSelf;
            if (hidden) hide.SetActive(false);

            var go = new GameObject("PreviewCam");
            var cam = go.AddComponent<Camera>();
            go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(lookAt - pos));
            cam.fieldOfView = fovV;
            cam.nearClipPlane = 0.02f;
            cam.farClipPlane = 200f;
            cam.clearFlags = everything ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.cullingMask = everything ? ~0 : 1 << 5;   // 5 = UI
            var rtex = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
            cam.targetTexture = rtex;
            cam.Render();   // 1º render aquece shaders/pipeline (pode sair com placeholder)
            cam.Render();

            var prev = RenderTexture.active;
            RenderTexture.active = rtex;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(path, tex.EncodeToPNG());

            cam.targetTexture = null;
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(rtex);
            Object.DestroyImmediate(go);
            if (hidden) hide.SetActive(true);
        }
    }
}
