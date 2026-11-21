using UnityEngine;

namespace F1Game
{
    /// <summary>IMGUI styling + helpers. Everything is laid out on a 1920×1080 virtual canvas and scaled.</summary>
    public static class UI
    {
        public static float Scale = 1f;
        public static float W = 1920f, H = 1080f;
        public static GUIStyle title, h1, h2, label, labelBold, small, tiny, button, buttonOn, panel, darkPanel, big, huge, center, right, field, warn;
        public static Texture2D white, dot;
        static bool ready;

        public static readonly Color Accent = new Color(0.9f, 0.12f, 0.16f);
        public static readonly Color Gold = new Color(1f, 0.8f, 0.25f);
        public static readonly Color Good = new Color(0.3f, 0.9f, 0.4f);
        public static readonly Color Mid = new Color(1f, 0.82f, 0.2f);
        public static readonly Color Bad = new Color(1f, 0.3f, 0.25f);
        public static readonly Color Purple = new Color(0.75f, 0.35f, 1f);
        public static readonly Color Dim = new Color(0.72f, 0.74f, 0.8f);

        public static void Begin()
        {
            Scale = Mathf.Min(Screen.height / 1080f, Screen.width / 1280f);
            if (Scale <= 0f) Scale = 1f;
            W = Screen.width / Scale;
            H = Screen.height / Scale;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(Scale, Scale, 1f));
            if (!ready) Init();
        }

        static GUIStyle Make(GUIStyle baseStyle, int size, FontStyle fs = FontStyle.Normal, TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            var s = new GUIStyle(baseStyle) { fontSize = size, fontStyle = fs, alignment = anchor, wordWrap = false, richText = true };
            s.normal.textColor = Color.white;
            return s;
        }

        static void Init()
        {
            ready = true;
            white = ProceduralTextures.Solid(Color.white);
            dot = ProceduralTextures.Dot();
            var baseLabel = GUI.skin.label;
            title = Make(baseLabel, 66, FontStyle.Bold);
            h1 = Make(baseLabel, 40, FontStyle.Bold);
            h2 = Make(baseLabel, 28, FontStyle.Bold);
            label = Make(baseLabel, 22);
            label.wordWrap = true;
            labelBold = Make(baseLabel, 22, FontStyle.Bold);
            small = Make(baseLabel, 18);
            small.wordWrap = true;
            tiny = Make(baseLabel, 15);
            big = Make(baseLabel, 54, FontStyle.Bold, TextAnchor.MiddleRight);
            huge = Make(baseLabel, 110, FontStyle.Bold, TextAnchor.MiddleCenter);
            center = Make(baseLabel, 24, FontStyle.Bold, TextAnchor.MiddleCenter);
            right = Make(baseLabel, 20, FontStyle.Normal, TextAnchor.MiddleRight);
            warn = Make(baseLabel, 30, FontStyle.Bold, TextAnchor.MiddleCenter);

            button = Make(GUI.skin.button, 23, FontStyle.Bold, TextAnchor.MiddleCenter);
            button.normal.background = ProceduralTextures.Rounded(new Color(0.12f, 0.13f, 0.17f, 0.92f), new Color(0.35f, 0.37f, 0.45f, 1f));
            button.hover.background = ProceduralTextures.Rounded(new Color(0.22f, 0.23f, 0.3f, 0.95f), new Color(0.9f, 0.2f, 0.25f, 1f));
            button.active.background = ProceduralTextures.Rounded(new Color(0.6f, 0.08f, 0.12f, 1f), new Color(1f, 0.4f, 0.4f, 1f));
            button.normal.textColor = button.hover.textColor = button.active.textColor = Color.white;
            button.border = new RectOffset(8, 8, 8, 8);
            button.padding = new RectOffset(10, 10, 6, 6);
            buttonOn = new GUIStyle(button);
            buttonOn.normal.background = ProceduralTextures.Rounded(new Color(0.75f, 0.1f, 0.14f, 0.95f), new Color(1f, 0.55f, 0.55f, 1f));
            buttonOn.hover.background = buttonOn.normal.background;

            panel = new GUIStyle(GUI.skin.box);
            panel.normal.background = ProceduralTextures.Rounded(new Color(0.05f, 0.06f, 0.08f, 0.84f), new Color(0.25f, 0.27f, 0.33f, 0.9f), 8);
            panel.border = new RectOffset(10, 10, 10, 10);
            darkPanel = new GUIStyle(panel);
            darkPanel.normal.background = ProceduralTextures.Rounded(new Color(0.02f, 0.02f, 0.03f, 0.92f), new Color(0.9f, 0.15f, 0.2f, 1f), 8);

            field = new GUIStyle(GUI.skin.textField) { fontSize = 24, alignment = TextAnchor.MiddleLeft };
            field.normal.textColor = Color.white;
        }

        public static void Panel(Rect r, bool dark = false) => GUI.Box(r, GUIContent.none, dark ? darkPanel : panel);

        public static bool Button(Rect r, string text, bool on = false)
        {
            if (GUI.Button(r, text, on ? buttonOn : button))
            {
                GameManager.Instance?.Click();
                return true;
            }
            return false;
        }

        public static void Text(Rect r, string text, GUIStyle st, Color? c = null)
        {
            var old = GUI.color;
            if (c.HasValue) GUI.color = c.Value;
            GUI.Label(r, text, st);
            GUI.color = old;
        }

        public static void Fill(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, white);
            GUI.color = old;
        }

        public static void Dot(Vector2 center, float size, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(new Rect(center.x - size * 0.5f, center.y - size * 0.5f, size, size), dot);
            GUI.color = old;
        }

        public static void Bar(Rect r, float t, Color c, Color? bg = null)
        {
            Fill(r, bg ?? new Color(1f, 1f, 1f, 0.12f));
            Fill(new Rect(r.x, r.y, r.width * Mathf.Clamp01(t), r.height), c);
        }

        /// <summary>Labelled slider. Returns the new value.</summary>
        public static float Slider(Rect r, string name, float v, float min, float max, string valueText, string hint = null)
        {
            Text(new Rect(r.x, r.y, r.width * 0.42f, 30), name, labelBold);
            Text(new Rect(r.x + r.width * 0.78f, r.y, r.width * 0.22f, 30), valueText, right, Gold);
            float nv = GUI.HorizontalSlider(new Rect(r.x + r.width * 0.42f, r.y + 10, r.width * 0.36f - 8, 20), v, min, max);
            if (!string.IsNullOrEmpty(hint)) Text(new Rect(r.x, r.y + 28, r.width, 24), hint, tiny, Dim);
            return nv;
        }

        public static bool Toggle(Rect r, string name, bool v, string hint = null)
        {
            Text(new Rect(r.x, r.y, r.width - 140, 32), name, labelBold);
            if (Button(new Rect(r.x + r.width - 130, r.y, 130, 34), v ? "AÇIK" : "KAPALI", v)) v = !v;
            if (!string.IsNullOrEmpty(hint)) Text(new Rect(r.x, r.y + 32, r.width - 140, 24), hint, tiny, Dim);
            return v;
        }

        public static Color WearColor(float wear)
        {
            if (wear < 0.4f) return Color.Lerp(Good, Mid, wear / 0.4f);
            return Color.Lerp(Mid, Bad, Mathf.Clamp01((wear - 0.4f) / 0.3f));
        }

        public static void Swatch(Rect r, Color c) => Fill(r, c);
    }
}
