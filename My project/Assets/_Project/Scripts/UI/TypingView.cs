using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TypeDuel.UI
{
    /// <summary>
    /// Desenha o texto da partida: o que foi digitado (branco = certo, vermelho = errado) seguido do resto do alvo em cinza.
    /// Largura fixa por caractere (mspace) para o texto nao "dancar" quando uma letra errada tem largura diferente.
    /// </summary>
    public sealed class TypingView
    {
        const string MonoWidth = "0.64em";
        const float Pad = 36f;

        static readonly string Gray = UiKit.HexRgb(new Color(0.55f, 0.57f, 0.62f));
        static readonly string White = "FFFFFF";
        static readonly string Red = UiKit.HexRgb(UiKit.Bad);

        readonly StringBuilder sb = new StringBuilder(2048);
        public readonly TextMeshProUGUI Text;
        public readonly Image Cursor;
        float scroll;

        public TypingView(Transform parent)
        {
            // Mascara: o texto que passar do painel e cortado, e o FollowCursor sobe a parte de cima.
            parent.gameObject.AddComponent<RectMask2D>();

            Text = UiKit.Label(parent, "TypingText", "", 40f, Color.white, TextAlignmentOptions.TopLeft);
            Text.textWrappingMode = TextWrappingModes.Normal;
            Text.richText = true;
            Text.lineSpacing = 12f;
            UiKit.Stretch(Text.rectTransform, Pad);

            Cursor = UiKit.Box(Text.transform, "Cursor", UiKit.Accent);
            Cursor.rectTransform.pivot = new Vector2(0.5f, 0f);
            Cursor.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            Cursor.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            Cursor.gameObject.SetActive(false);
        }

        public void Clear()
        {
            Text.text = "";
            Cursor.gameObject.SetActive(false);
            scroll = 0f;
            ApplyScroll();
        }

        /// <summary>Com peek, mostra so o texto original (branco), sem o que foi digitado nem o cursor.</summary>
        public void Render(TypingModel m, bool glitch, bool peek)
        {
            sb.Clear();
            sb.Append("<mspace=").Append(MonoWidth).Append('>');

            int typedLen = peek ? 0 : m.TypedLength;
            int total = System.Math.Max(typedLen, m.Target.Length);
            int run = -1; // 0 cinza, 1 branco, 2 vermelho

            for (int i = 0; i < total; i++)
            {
                char ch;
                int state;
                if (i < typedLen)
                {
                    ch = m.TypedAt(i);
                    state = m.IsCorrectAt(i) ? 1 : 2;
                }
                else
                {
                    ch = m.Target[i];
                    state = peek ? 1 : 0;
                }

                if (state != run)
                {
                    CloseRun(run);
                    OpenRun(state);
                    run = state;
                }

                if (glitch && !peek) ch = LeetMap.Apply(ch);
                if (ch == '<') sb.Append("<noparse><</noparse>");
                else sb.Append(ch);
            }

            CloseRun(run);
            sb.Append("</mspace>");

            Text.text = sb.ToString();
            Text.ForceMeshUpdate();
            if (!peek) FollowCursor(m.Cursor);
            if (peek) Cursor.gameObject.SetActive(false);
            else PlaceCursor(m.Cursor);
        }

        void OpenRun(int state)
        {
            if (state == 0) sb.Append("<color=#").Append(Gray).Append('>');
            else if (state == 1) sb.Append("<color=#").Append(White).Append('>');
            else sb.Append("<color=#").Append(Red).Append("><mark=#").Append(Red).Append("40>");
        }

        void CloseRun(int state)
        {
            if (state < 0) return;
            if (state == 2) sb.Append("</mark>");
            sb.Append("</color>");
        }

        /// <summary>Rola o texto para a linha do cursor ficar no meio do painel; as linhas ja escritas saem por cima.</summary>
        void FollowCursor(int cursor)
        {
            var info = Text.textInfo;
            int count = info.characterCount;
            if (count == 0 || info.lineCount == 0) return;

            // Distancias medidas a partir do topo da primeira linha (y para baixo).
            int line = info.characterInfo[Mathf.Min(cursor, count - 1)].lineNumber;
            float top = info.lineInfo[0].ascender - info.lineInfo[line].ascender;
            float bottom = info.lineInfo[0].ascender - info.lineInfo[line].descender;
            float middle = Text.rectTransform.rect.height * 0.5f;

            if (bottom > scroll + middle) scroll = bottom - middle;
            else if (top < scroll) scroll = top;
            scroll = Mathf.Max(0f, scroll);
            ApplyScroll();
        }

        void ApplyScroll()
        {
            Text.rectTransform.offsetMin = new Vector2(Pad, Pad + scroll);
            Text.rectTransform.offsetMax = new Vector2(-Pad, -Pad + scroll);
        }

        void PlaceCursor(int index)
        {
            var info = Text.textInfo;
            int count = info.characterCount;
            if (count == 0)
            {
                Cursor.gameObject.SetActive(false);
                return;
            }

            float x, bottom, top;
            if (index < count)
            {
                var ci = info.characterInfo[index];
                x = ci.origin;
                bottom = ci.descender;
                top = ci.ascender;
            }
            else
            {
                var ci = info.characterInfo[count - 1];
                x = ci.xAdvance;
                bottom = ci.descender;
                top = ci.ascender;
            }

            Cursor.gameObject.SetActive(true);
            Cursor.rectTransform.sizeDelta = new Vector2(4f, top - bottom);
            Cursor.rectTransform.localPosition = new Vector3(x, bottom, 0f);
        }

        /// <summary>Posicao (mundo/tela) do meio do caractere, usada para a bomba cair na palavra atual.</summary>
        public Vector3 CharWorldPosition(int index)
        {
            var info = Text.textInfo;
            int count = info.characterCount;
            if (count == 0) return Text.transform.position;

            index = Mathf.Clamp(index, 0, count - 1);
            var ci = info.characterInfo[index];
            var local = new Vector3((ci.origin + ci.xAdvance) * 0.5f, (ci.ascender + ci.descender) * 0.5f, 0f);
            return Text.transform.TransformPoint(local);
        }
    }
}
