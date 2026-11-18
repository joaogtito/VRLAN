using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace VRLAN.Network.EditorTools
{
    // Design system do VRLAN (tema escuro) + fábrica de elementos UGUI/TMP.
    // Usado pelo VRLANSceneBuilder pra montar o painel do tablet e a UI do Quest no MESMO padrão.
    // Gera (uma vez) os assets de apoio: sprite arredondado 9-slice, círculo, anel e as fontes TMP (Poppins).
    public static class VRLANUIKit
    {
        // ---------- Tema ----------
        public static readonly Color Bg         = Hex("0B0D12");
        public static readonly Color Panel      = Hex("141821");
        public static readonly Color Card       = Hex("1C212C");
        public static readonly Color CardHi     = Hex("252B38");
        public static readonly Color Accent     = Hex("4F8CFF");
        public static readonly Color Success    = Hex("34D399");
        public static readonly Color Danger     = Hex("F2605C");
        public static readonly Color Live       = Hex("FF4D5E");
        public static readonly Color Ink        = Hex("EEF1F6");
        public static readonly Color TextDim    = Hex("A3ACBD");
        public static readonly Color TextFaint  = Hex("6E778A");

        public enum Style { Primary, Secondary, Danger, Ghost, List }

        public const string UiRoot = "Assets/VRLAN/UI";
        const string SpriteDir = UiRoot + "/Sprites";
        const string FontDir = UiRoot + "/Fonts";

        public static Sprite Rounded { get; private set; }   // 9-slice, raio base 32 px
        public static Sprite Circle { get; private set; }
        public static Sprite Ring { get; private set; }
        public static TMP_FontAsset FontRegular { get; private set; }
        public static TMP_FontAsset FontBold { get; private set; }

        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var c);
            return c;
        }

        public static Color A(Color c, float a) { c.a = a; return c; }

        // ---------- Assets de apoio ----------

        public static void EnsureAssets()
        {
            Directory.CreateDirectory(SpriteDir);
            Directory.CreateDirectory(FontDir);
            Rounded = EnsureSprite(SpriteDir + "/UI_Rounded.png", ShapeRounded, 32);
            Circle  = EnsureSprite(SpriteDir + "/UI_Circle.png", ShapeCircle, 0);
            Ring    = EnsureSprite(SpriteDir + "/UI_Ring.png", ShapeRing, 0);
            FontRegular = EnsureFont(FontDir + "/Poppins-Regular.ttf", FontDir + "/Poppins-Regular SDF.asset");
            FontBold    = EnsureFont(FontDir + "/Poppins-SemiBold.ttf", FontDir + "/Poppins-SemiBold SDF.asset");
        }

        delegate float Shape(float x, float y, float size);  // retorna alpha 0..1

        static float ShapeRounded(float x, float y, float s)
        {
            const float r = 32f;
            float cx = Mathf.Clamp(x, r, s - r), cy = Mathf.Clamp(y, r, s - r);
            float d = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy)) - r;
            return Mathf.Clamp01(0.5f - d);
        }

        static float ShapeCircle(float x, float y, float s)
        {
            float d = Vector2.Distance(new Vector2(x, y), new Vector2(s / 2f, s / 2f)) - (s / 2f - 1f);
            return Mathf.Clamp01(0.5f - d);
        }

        static float ShapeRing(float x, float y, float s)
        {
            float r = s / 2f - 1f, w = 7f;
            float d = Mathf.Abs(Vector2.Distance(new Vector2(x, y), new Vector2(s / 2f, s / 2f)) - (r - w / 2f)) - w / 2f;
            return Mathf.Clamp01(0.5f - d);
        }

        static Sprite EnsureSprite(string path, Shape shape, int border)
        {
            if (!File.Exists(path))
            {
                const int size = 128;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                var px = new Color32[size * size];
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    // amostragem 4x pra anti-aliasing
                    float a = 0f;
                    for (int sy = 0; sy < 2; sy++)
                    for (int sx = 0; sx < 2; sx++)
                        a += shape(x + 0.25f + sx * 0.5f, y + 0.25f + sy * 0.5f, size);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a / 4f * 255f));
                }
                tex.SetPixels32(px);
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path);
            }

            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            bool dirty = imp.textureType != TextureImporterType.Sprite || imp.mipmapEnabled ||
                         imp.spriteBorder != new Vector4(border, border, border, border);
            if (dirty)
            {
                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.mipmapEnabled = false;
                imp.alphaIsTransparency = true;
                imp.spritePixelsPerUnit = 100;
                imp.spriteBorder = new Vector4(border, border, border, border);
                imp.filterMode = FilterMode.Bilinear;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                var settings = new TextureImporterSettings();
                imp.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                imp.SetTextureSettings(settings);
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        const string Charset =
            " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~" +
            "ÀÁÂÃÄÇÈÉÊËÌÍÎÏÑÒÓÔÕÖÙÚÛÜàáâãäçèéêëìíîïñòóôõöùúûü·–—…‘’“”•←→›‹°ºª";

        static TMP_FontAsset EnsureFont(string ttfPath, string assetPath)
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (existing != null) return existing;

            var font = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
            if (font == null)
            {
                Debug.LogWarning($"[VRLANUIKit] fonte não encontrada em {ttfPath} — usando a padrão do TMP.");
                return TMP_Settings.defaultFontAsset;
            }

            var fa = TMP_FontAsset.CreateFontAsset(font, 64, 7, GlyphRenderMode.SDFAA, 1024, 1024,
                AtlasPopulationMode.Dynamic, enableMultiAtlasSupport: false);
            fa.name = Path.GetFileNameWithoutExtension(assetPath);
            AssetDatabase.CreateAsset(fa, assetPath);
            fa.atlasTextures[0].name = fa.name + " Atlas";
            AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
            fa.material.name = fa.name + " Material";
            AssetDatabase.AddObjectToAsset(fa.material, fa);

            fa.TryAddCharacters(Charset, out var missing);
            if (!string.IsNullOrEmpty(missing)) Debug.Log($"[VRLANUIKit] {fa.name}: sem glifos para '{missing}' (vai pro fallback).");

            if (TMP_Settings.defaultFontAsset != null)
                fa.fallbackFontAssetTable = new List<TMP_FontAsset> { TMP_Settings.defaultFontAsset };

            EditorUtility.SetDirty(fa);
            AssetDatabase.SaveAssets();
            return fa;
        }

        // ---------- RectTransform ----------

        public static RectTransform Node(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent != null ? parent.gameObject.layer : 5;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static RectTransform Stretch(RectTransform rt, float left = 0, float right = 0, float top = 0, float bottom = 0)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom); rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        // anchor + pivot iguais (ex.: TopLeft = (0,1)); pos relativa ao anchor; tamanho fixo.
        public static RectTransform Pin(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor; rt.pivot = anchor;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            return rt;
        }

        // Faixa horizontal no topo do pai: margens laterais + distância do topo + altura.
        public static RectTransform TopRow(RectTransform rt, float top, float height, float left = 0, float right = 0)
        {
            rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(0.5f, 1);
            rt.offsetMin = new Vector2(left, -top - height); rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        public static readonly Vector2 TopLeft = new(0, 1), TopRight = new(1, 1), Center = new(0.5f, 0.5f),
            BottomLeft = new(0, 0), BottomRight = new(1, 0), TopCenter = new(0.5f, 1), BottomCenter = new(0.5f, 0);

        // ---------- Elementos ----------

        public static Image Fill(RectTransform rt, Color color, float radius = 0, bool raycast = false)
        {
            var img = rt.gameObject.GetComponent<Image>();
            if (img == null) img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = raycast;
            if (radius > 0)
            {
                img.sprite = Rounded;
                img.type = Image.Type.Sliced;
                img.pixelsPerUnitMultiplier = 32f / radius;
            }
            return img;
        }

        public static Image Dot(Transform parent, string name, Color color, float size, Sprite sprite = null)
        {
            var rt = Node(name, parent);
            rt.sizeDelta = new Vector2(size, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite != null ? sprite : Circle;
            img.color = color;
            img.raycastTarget = false;
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = le.minWidth = size; le.preferredHeight = le.minHeight = size;
            return img;
        }

        public static TextMeshProUGUI Label(Transform parent, string name, string text, float size, bool bold,
            Color color, TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft)
        {
            var rt = Node(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = bold ? FontBold : FontRegular;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Overflow;   // Poppins tem entrelinha alta: Ellipsis esconderia a linha toda
            return t;
        }

        public static Button Btn(Transform parent, string name, string text, Style style, float fontSize = 26, float radius = 16)
        {
            var rt = Node(name, parent);
            Color bg = style switch
            {
                Style.Primary => Accent,
                Style.Danger => A(Danger, 0.16f),
                Style.Secondary => CardHi,
                Style.List => Color.white,
                _ => A(Color.white, 0.001f),
            };
            Color fg = style switch
            {
                Style.Danger => Danger,
                Style.Ghost => Accent,
                _ => Ink,
            };
            var img = Fill(rt, bg, radius, raycast: true);
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var cb = b.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(0.9f, 0.92f, 0.96f, 1f);
            cb.pressedColor = new Color(0.72f, 0.75f, 0.82f, 1f);
            cb.selectedColor = Color.white;
            cb.disabledColor = new Color(1f, 1f, 1f, 0.4f);
            cb.fadeDuration = 0.08f;
            if (style == Style.List)
            {
                cb.normalColor = CardHi;
                cb.highlightedColor = Hex("2E3546");
                cb.pressedColor = Hex("3A4357");
                cb.selectedColor = cb.normalColor;
            }
            b.colors = cb;

            var label = Label(rt, "Text (TMP)", text, fontSize, bold: true, fg,
                style == Style.List ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.Center);
            Stretch(label.rectTransform, style == Style.List ? 22 : 12, 12, 0, 0);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            return b;
        }

        public static TMP_InputField Input(Transform parent, string name, string placeholder, float fontSize = 28)
        {
            var rt = Node(name, parent);
            var bg = Fill(rt, Card, 16, raycast: true);
            var field = rt.gameObject.AddComponent<TMP_InputField>();

            var area = Node("Text Area", rt);
            Stretch(area, 26, 26, 6, 6);
            area.gameObject.AddComponent<RectMask2D>();

            var ph = Label(area, "Placeholder", placeholder, fontSize, false, TextFaint);
            Stretch(ph.rectTransform);
            ph.textWrappingMode = TextWrappingModes.NoWrap;
            var phLe = ph.gameObject.AddComponent<LayoutElement>(); phLe.ignoreLayout = true;

            var tx = Label(area, "Text", "", fontSize, false, Ink);
            Stretch(tx.rectTransform);
            tx.textWrappingMode = TextWrappingModes.NoWrap;
            tx.overflowMode = TextOverflowModes.Overflow;

            field.textViewport = area;
            field.textComponent = tx;
            field.placeholder = ph;
            field.targetGraphic = bg;
            field.fontAsset = FontRegular;
            field.pointSize = fontSize;
            field.customCaretColor = true;
            field.caretColor = Accent;
            field.caretWidth = 3;
            field.selectionColor = A(Accent, 0.35f);
            field.characterLimit = 32;
            var cb = field.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.0f, 1.0f, 1.0f, 1f);
            cb.selectedColor = Color.white;
            field.colors = cb;
            return field;
        }

        // Pílula com ponto colorido + texto (status). Cresce com o texto (ContentSizeFitter).
        public static TextMeshProUGUI Pill(Transform parent, string name, string text, Color dotColor,
            float fontSize, Color bg, out RectTransform pill, out Image dot)
        {
            pill = Node(name, parent);
            Fill(pill, bg, 24);
            var h = pill.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(20, 24, 10, 10);
            h.spacing = 12;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = h.childForceExpandHeight = false;
            var fit = pill.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            dot = Dot(pill, "Dot", dotColor, fontSize * 0.55f);
            var t = Label(pill, "Text", text, fontSize, true, Ink);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }

        // ScrollRect vertical com conteúdo em VerticalLayoutGroup. Retorna o Content.
        public static RectTransform ScrollList(RectTransform area, float spacing = 10, int padding = 0)
        {
            var sr = area.gameObject.AddComponent<ScrollRect>();
            var vp = Node("Viewport", area);
            Stretch(vp);
            vp.gameObject.AddComponent<RectMask2D>();
            var vpImg = vp.gameObject.AddComponent<Image>();   // recebe o drag
            vpImg.color = A(Color.white, 0f);
            vpImg.raycastTarget = true;

            var content = Node("Content", vp);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(0.5f, 1);
            content.offsetMin = content.offsetMax = Vector2.zero;
            var v = content.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.padding = new RectOffset(padding, padding, padding, padding);
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            var fit = content.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            sr.viewport = vp;
            sr.content = content;
            sr.horizontal = false;
            sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.scrollSensitivity = 30;
            return content;
        }

        public static LayoutElement Height(Component c, float h)
        {
            var le = c.GetComponent<LayoutElement>();
            if (le == null) le = c.gameObject.AddComponent<LayoutElement>();
            le.minHeight = le.preferredHeight = h;
            return le;
        }

        // ---------- Wiring via SerializedObject ----------

        public static void Ref(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null) { Debug.LogError($"[VRLANUIKit] campo '{field}' não existe em {target.GetType().Name}"); return; }
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void Bool(Object target, string field, bool value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null) { Debug.LogError($"[VRLANUIKit] campo '{field}' não existe em {target.GetType().Name}"); return; }
            p.boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void Col(Object target, string field, Color value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null) { Debug.LogError($"[VRLANUIKit] campo '{field}' não existe em {target.GetType().Name}"); return; }
            p.colorValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void Str(Object target, string field, string value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null) { Debug.LogError($"[VRLANUIKit] campo '{field}' não existe em {target.GetType().Name}"); return; }
            p.stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void ClearChildren(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--) Object.DestroyImmediate(t.GetChild(i).gameObject);
        }
    }
}
