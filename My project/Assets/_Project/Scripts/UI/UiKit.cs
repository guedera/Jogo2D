using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TypeDuel.UI
{
    /// <summary>Helpers para montar UI (uGUI + TextMeshPro) por codigo. Identidade visual: fundo branco, bordas finas cinza, botoes chapados, Open Sans.</summary>
    public static class UiKit
    {
        // Paleta da identidade visual.
        public static readonly Color Green = Hex("#21DB40");
        public static readonly Color Blue = Hex("#5F73F8");
        public static readonly Color Red = Hex("#F85F5F");
        public static readonly Color Yellow = Hex("#E5E558");
        public static readonly Color Pink = Hex("#F85FD9");
        public static readonly Color Purple = Hex("#9800DE");

        public static readonly Color Border = Hex("#B3B3B3");

        // Nomes antigos mantidos para nao quebrar quem usa (MatchController etc.).
        public static readonly Color Background = Color.white;
        public static readonly Color Panel = Color.white;
        public static readonly Color PanelLight = Color.white;
        public static readonly Color Accent = Blue;
        public static readonly Color Good = Green;
        public static readonly Color Bad = Red;
        public static readonly Color TextDim = Hex("#9A9A9A");
        public static readonly Color TextMain = Color.black;

        /// <summary>Cor de cada jogador, na ordem: voce primeiro, depois os adversarios.</summary>
        public static readonly Color[] PlayerColors = { Blue, Purple, Red, Green, Yellow, Pink };

        public static Color PlayerColor(int index) => PlayerColors[Mathf.Abs(index) % PlayerColors.Length];

        static Sprite circle;
        static TMP_FontAsset font;
        static bool fontSearched;

        /// <summary>
        /// Fonte (Open Sans). Se nao for atribuida manualmente, procura um TMP_FontAsset "OpenSans" carregado/em Resources
        /// (no Editor, tambem no projeto) e por fim usa a fonte padrao do TMP. Para garantir em build, ponha o asset numa pasta
        /// Resources ou defina UiKit.Font antes de GameUI.Build.
        /// </summary>
        public static TMP_FontAsset Font
        {
            get
            {
                if (font == null && !fontSearched)
                {
                    fontSearched = true;
                    font = FindOpenSans();
                }
                return font;
            }
            set { font = value; }
        }

        static TMP_FontAsset FindOpenSans()
        {
            TMP_FontAsset best = null;

            foreach (var f in Resources.FindObjectsOfTypeAll<TMP_FontAsset>()) best = Better(best, f);
            foreach (var f in Resources.LoadAll<TMP_FontAsset>("")) best = Better(best, f);

#if UNITY_EDITOR
            if (best == null)
            {
                foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:TMP_FontAsset OpenSans"))
                {
                    var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
                    best = Better(best, asset);
                }
            }
#endif
            return best != null ? best : TMP_Settings.defaultFontAsset;
        }

        // Prefere a variante regular (sem Bold/Italic/Light etc.) entre as que se chamam OpenSans.
        static TMP_FontAsset Better(TMP_FontAsset current, TMP_FontAsset candidate)
        {
            if (candidate == null) return current;
            string n = candidate.name.Replace(" ", "").Replace("-", "").ToLowerInvariant();
            if (!n.Contains("opensans")) return current;
            if (current == null) return candidate;

            bool candidatePlain = IsPlain(n);
            bool currentPlain = IsPlain(current.name.Replace(" ", "").Replace("-", "").ToLowerInvariant());
            return candidatePlain && !currentPlain ? candidate : current;
        }

        static bool IsPlain(string n) =>
            !(n.Contains("bold") || n.Contains("italic") || n.Contains("light") || n.Contains("semi") || n.Contains("extra") || n.Contains("condensed"));

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

        /// <summary>Ancora no topo-centro do pai; x e deslocamento horizontal do centro e top a distancia do topo (pivo no topo do elemento).</summary>
        public static void PlaceTop(RectTransform r, float x, float top, float width, float height)
        {
            r.anchorMin = new Vector2(0.5f, 1f);
            r.anchorMax = new Vector2(0.5f, 1f);
            r.pivot = new Vector2(0.5f, 1f);
            r.anchoredPosition = new Vector2(x, -top);
            r.sizeDelta = new Vector2(width, height);
        }

        public static Image Box(Transform parent, string name, Color color, bool raycast = false)
        {
            var rect = NewRect(name, parent);
            var img = rect.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        /// <summary>Borda fina cinza desenhada por fora do grafico (Outline do uGUI).</summary>
        public static Outline AddBorder(Graphic graphic)
        {
            var outline = graphic.gameObject.AddComponent<Outline>();
            outline.effectColor = Border;
            outline.effectDistance = new Vector2(1f, -1f);
            return outline;
        }

        /// <summary>Caixa branca com borda fina cinza (paineis e campos).</summary>
        public static Image Frame(Transform parent, string name, bool raycast = false)
        {
            var img = Box(parent, name, Color.white, raycast);
            AddBorder(img);
            return img;
        }

        /// <summary>Marca um botao como selecionado (cor cheia, sem borda) ou nao (branco com borda).</summary>
        public static void SetSelected(Button button, bool selected, Color selectedColor)
        {
            button.targetGraphic.color = selected ? selectedColor : Color.white;
            var outline = button.GetComponent<Outline>();
            if (outline != null) outline.enabled = !selected;
        }

        public static TextMeshProUGUI Label(Transform parent, string name, string text, float size, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.Center, FontStyles style = FontStyles.Normal)
        {
            var rect = NewRect(name, parent);
            var t = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (Font != null) t.font = Font;
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

        /// <summary>Botao chapado com texto preto. Cor branca vira botao so com borda.</summary>
        public static Button MakeButton(Transform parent, string name, string label, Color color, Action onClick, Vector2 size, float fontSize = 0f)
        {
            var img = Box(parent, name, color, raycast: true);
            img.rectTransform.sizeDelta = size;
            if (color.r > 0.95f && color.g > 0.95f && color.b > 0.95f) AddBorder(img);

            var button = img.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            var colors = button.colors;
            colors.highlightedColor = new Color(0.92f, 0.92f, 0.92f, 1f);
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
            colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.6f);
            button.colors = colors;
            button.onClick.AddListener(() => onClick());

            var text = Label(img.transform, "Label", label, fontSize > 0f ? fontSize : size.y * 0.42f, TextMain);
            Stretch(text.rectTransform);
            return button;
        }

        /// <summary>Icone do jogador: retangulo na cor dele com um "I" (cursor) branco.</summary>
        public static Image CaretIcon(Transform parent, string name, Color color, Vector2 size)
        {
            var bg = Box(parent, name, color);
            bg.rectTransform.sizeDelta = size;
            var glyph = Label(bg.transform, "Glyph", "I", size.y * 0.85f, Color.white, style: FontStyles.Bold);
            Stretch(glyph.rectTransform);
            return bg;
        }
    }

    /// <summary>Linha da lista de jogadores da partida: icone na cor do jogador, nome e porcentagem de progresso.</summary>
    public sealed class PlayerRow
    {
        readonly TextMeshProUGUI percent;
        public readonly RectTransform Root;

        public PlayerRow(Transform parent, string name, string label, Color color, float width, float height)
        {
            Root = UiKit.NewRect(name, parent);
            Root.sizeDelta = new Vector2(width, height);

            var icon = UiKit.CaretIcon(Root, "Icon", color, new Vector2(22f, 34f));
            UiKit.Place(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(11f, 0f), new Vector2(22f, 34f));

            var title = UiKit.Label(Root, "Name", label, 32f, UiKit.TextMain, TextAlignmentOptions.MidlineLeft);
            title.overflowMode = TextOverflowModes.Ellipsis;
            title.rectTransform.anchorMin = Vector2.zero;
            title.rectTransform.anchorMax = Vector2.one;
            title.rectTransform.offsetMin = new Vector2(46f, 0f);
            title.rectTransform.offsetMax = new Vector2(-96f, 0f);

            percent = UiKit.Label(Root, "Percent", "0%", 32f, UiKit.TextDim, TextAlignmentOptions.MidlineRight);
            UiKit.Place(percent.rectTransform, new Vector2(1f, 0.5f), new Vector2(-45f, 0f), new Vector2(90f, height));
        }

        public void Set(float value)
        {
            value = Mathf.Clamp01(value);
            percent.text = Mathf.RoundToInt(value * 100f) + "%";
        }
    }
}
