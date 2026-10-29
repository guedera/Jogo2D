using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TypeDuel.UI
{
    /// <summary>Helpers para montar UI (uGUI + TextMeshPro) por codigo. Visual de protótipo, sem assets.</summary>
    public static class UiKit
    {
        public static readonly Color Background = Hex("#14161C");
        public static readonly Color Panel = Hex("#1D2029");
        public static readonly Color PanelLight = Hex("#2A2F3B");
        public static readonly Color Accent = Hex("#5B8CFF");
        public static readonly Color Good = Hex("#3DDC84");
        public static readonly Color Bad = Hex("#FF5A5A");
        public static readonly Color TextDim = Hex("#8A8F98");
        public static readonly Color TextMain = Color.white;

        static Sprite circle;

        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }

        public static string HexRgb(Color c) => ColorUtility.ToHtmlStringRGB(c);

        /// <summary>Circulo branco antialiasado, usado pela bomba e pela explosao.</summary>
        public static Sprite Circle
        {
            get
            {
                if (circle != null) return circle;

                const int size = 128;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                float r = size / 2f - 1f;
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f));
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(r - d)));
                    }
                }
                tex.Apply();
                circle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
                return circle;
            }
        }

        public static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static void Stretch(RectTransform r, float pad = 0f)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(pad, pad);
            r.offsetMax = new Vector2(-pad, -pad);
        }

        /// <summary>Posiciona por ancora (0..1), posicao em pixels de referencia e tamanho. Pivo no centro.</summary>
        public static void Place(RectTransform r, Vector2 anchor, Vector2 position, Vector2 size)
        {
            r.anchorMin = anchor;
            r.anchorMax = anchor;
            r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = position;
            r.sizeDelta = size;
        }

        public static Image Box(Transform parent, string name, Color color, bool raycast = false)
        {
            var rect = NewRect(name, parent);
            var img = rect.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        public static TextMeshProUGUI Label(Transform parent, string name, string text, float size, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.Center, FontStyles style = FontStyles.Normal)
        {
            var rect = NewRect(name, parent);
            var t = rect.gameObject.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.fontStyle = style;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }

        public static Button MakeButton(Transform parent, string name, string label, Color color, Action onClick, Vector2 size)
        {
            var img = Box(parent, name, color, raycast: true);
            img.rectTransform.sizeDelta = size;

            var button = img.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            var colors = button.colors;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.6f);
            button.colors = colors;
            button.onClick.AddListener(() => onClick());

            var text = Label(img.transform, "Label", label, size.y * 0.42f, Color.white, style: FontStyles.Bold);
            Stretch(text.rectTransform);
            return button;
        }
    }

    /// <summary>Barra de progresso simples (fundo + preenchimento + rotulo).</summary>
    public sealed class ProgressBar
    {
        readonly RectTransform fill;
        readonly TextMeshProUGUI percent;
        public readonly RectTransform Root;

        public ProgressBar(Transform parent, string name, string label, Color color, float width, float height)
        {
            Root = UiKit.NewRect(name, parent);
            Root.sizeDelta = new Vector2(width + 360f, height);

            var title = UiKit.Label(Root, "Title", label, height * 0.7f, UiKit.TextDim, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
            UiKit.Place(title.rectTransform, new Vector2(0f, 0.5f), new Vector2(150f, 0f), new Vector2(300f, height));

            var bg = UiKit.Box(Root, "Bg", UiKit.PanelLight);
            UiKit.Place(bg.rectTransform, new Vector2(0f, 0.5f), new Vector2(330f + width / 2f, 0f), new Vector2(width, height));

            var fillImg = UiKit.Box(bg.transform, "Fill", color);
            fill = fillImg.rectTransform;
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0f, 1f);
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;

            percent = UiKit.Label(Root, "Percent", "0%", height * 0.7f, UiKit.TextMain, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            UiKit.Place(percent.rectTransform, new Vector2(0f, 0.5f), new Vector2(330f + width + 70f, 0f), new Vector2(120f, height));
        }

        public void Set(float value)
        {
            value = Mathf.Clamp01(value);
            fill.anchorMax = new Vector2(value, 1f);
            percent.text = Mathf.RoundToInt(value * 100f) + "%";
        }
    }
}
