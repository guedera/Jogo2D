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

        static readonly string Gray = UiKit.HexRgb(new Color(0.55f, 0.57f, 0.62f));
        static readonly string White = "FFFFFF";
        static readonly string Red = UiKit.HexRgb(UiKit.Bad);

        readonly StringBuilder sb = new StringBuilder(2048);
        public readonly TextMeshProUGUI Text;
        public readonly Image Cursor;

        public TypingView(Transform parent)
        {
            Text = UiKit.Label(parent, "TypingText", "", 40f, Color.white, TextAlignmentOptions.TopLeft);
            Text.textWrappingMode = TextWrappingModes.Normal;
            Text.richText = true;
            Text.lineSpacing = 12f;
            UiKit.Stretch(Text.rectTransform, 36f);

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
        }

        public void Render(TypingModel m, bool glitch)
        {
            sb.Clear();
            sb.Append("<mspace=").Append(MonoWidth).Append('>');

            int typedLen = m.TypedLength;
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
                    state = 0;
                }

                if (state != run)
                {
                    CloseRun(run);
                    OpenRun(state);
                    run = state;
                }

                if (glitch) ch = LeetMap.Apply(ch);
                if (ch == '<') sb.Append("<noparse><</noparse>");
                else sb.Append(ch);
            }

            CloseRun(run);
            sb.Append("</mspace>");

            Text.text = sb.ToString();
            Text.ForceMeshUpdate();
            PlaceCursor(m.Cursor);
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
