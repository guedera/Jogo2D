using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TypeDuel.UI
{
    /// <summary>
    /// Desenha o texto da partida: o que foi digitado (branco = certo, vermelho = errado) seguido do resto do alvo em cinza.
    /// A linha onde o jogador esta digitando fica sempre parada no centro vertical (sobre uma faixa escura) e o resto do
    /// texto rola em volta dela; somente a linha anterior e a proxima tambem ficam visiveis.
    /// </summary>
    public sealed class TypingView
    {
        // Largura fixa por caractere (mspace) para o texto nao "dancar" quando uma letra errada tem largura diferente.
        // Trocar para false deixa o texto proporcional (Open Sans pura), ao custo de o texto se mexer em erros.
        const bool FixedWidth = true;
        const string MonoWidth = "0.6em";

        const float FontSize = 40f;
        const float LineSpacing = 4f;
        const float PadY = 20f;
        const float ScrollSpeed = 16f;      // velocidade da rolagem suave (maior = mais rapido)
        const float BandMargin = 22f;       // quanto a faixa da linha atual passa do texto nas laterais
        const float BandPadding = 10f;      // folga vertical da faixa
        const float CursorWidth = 3f;
        const float CapWidth = 12f;
        const float CapHeight = 3f;

        // Opacidade por distancia (em linhas) ate a linha atual.
        static readonly float[] LineAlpha = { 1f, 1f, 0f };

        const string Gray = "929BAD";
        const string White = "F1F3F9";
        static readonly string Purple = UiKit.HexRgb(UiKit.Purple);
        readonly HashSet<int> rewardCharacters = new HashSet<int>();
        int currentLine;
        static readonly string Red = UiKit.HexRgb(UiKit.Red);

        readonly StringBuilder sb = new StringBuilder(4096);
        readonly Dictionary<int, CursorGlyph> opponentCursors = new Dictionary<int, CursorGlyph>();
        readonly Dictionary<int, Color> opponentColors = new Dictionary<int, Color>();
        readonly Dictionary<int, int> opponentPrefix = new Dictionary<int, int>();
        readonly CursorGlyph ownCursor;
        readonly Image band;
        readonly float padLeft;
        readonly float padRight;

        public readonly TextMeshProUGUI Text;
        public readonly Image Cursor;

        int[] lineOf = new int[0];  // linha (no layout atual) de cada caractere, usada para apagar por distancia
        float scroll;               // posicao vertical atual do texto
        float scrollTarget;         // posicao que poe a linha do cursor no centro
        bool snapNext = true;
        int lastTickFrame = -100;

        public TypingView(Transform parent, float padLeft = 30f, float padRight = 380f)
        {
            this.padLeft = padLeft;
            this.padRight = padRight;

            // Mascara: o texto que passar da area e cortado.
            parent.gameObject.AddComponent<RectMask2D>();

            // Faixa da linha atual (atras do texto), fixa no centro da area.
            band = UiKit.Box(parent, "LineBand", UiKit.PanelLight);
            var br = band.rectTransform;
            br.anchorMin = new Vector2(0f, 0.5f);
            br.anchorMax = new Vector2(1f, 0.5f);
            br.pivot = new Vector2(0.5f, 0.5f);
            br.anchoredPosition = new Vector2((padLeft - padRight) * 0.5f, 0f);
            br.sizeDelta = new Vector2(-(padLeft - BandMargin + padRight - BandMargin), 60f);
            band.gameObject.SetActive(false);

            Text = UiKit.Label(parent, "TypingText", "", FontSize, Color.white, TextAlignmentOptions.TopLeft);
            Text.textWrappingMode = TextWrappingModes.Normal;
            Text.richText = true;
            Text.lineSpacing = LineSpacing;
            UiKit.Stretch(Text.rectTransform);

            ownCursor = new CursorGlyph(Text.transform, "Cursor", UiKit.PlayerColor(0));
            Cursor = ownCursor.Bar;

            ApplyScroll();
        }

        public void SetPowerWords(TypingModel model, IEnumerable<int> words)
        {
            rewardCharacters.Clear();
            foreach (int word in words)
            {
                if (word < 0 || word >= model.WordEnds.Count) continue;
                int start = word == 0 ? 0 : model.WordEnds[word - 1];
                for (int i = start; i < model.WordEnds[word]; i++)
                    if (model.Target[i] != ' ') rewardCharacters.Add(i);
            }
        }

        public void ConsumePowerWord(TypingModel model, int word)
        {
            int start = word == 0 ? 0 : model.WordEnds[word - 1];
            for (int i = start; i < model.WordEnds[word]; i++) rewardCharacters.Remove(i);
        }

        public void SetOwnColor(Color color) => ownCursor.SetColor(color);

        /// <summary>Cor do cursor do adversario `id` (a mesma do icone dele na lista de jogadores).</summary>
        public void SetOpponentColor(int id, Color color)
        {
            opponentColors[id] = color;
            if (opponentCursors.TryGetValue(id, out var glyph)) glyph.SetColor(color);
        }

        public void Clear()
        {
            Text.text = "";
            rewardCharacters.Clear();
            ownCursor.Hide();
            band.gameObject.SetActive(false);
            foreach (var glyph in opponentCursors.Values) glyph.Hide();
            opponentPrefix.Clear();
            lineOf = new int[0];
            scroll = scrollTarget = 0f;
            snapNext = true;
            ApplyScroll();
        }

        /// <summary>Chamado todo frame (GameUI.Tick): faz a rolagem deslizar ate a linha do cursor.</summary>
        public void Tick(float dt)
        {
            lastTickFrame = Time.frameCount;
            if (Mathf.Approximately(scroll, scrollTarget)) return;

            scroll = Mathf.Lerp(scroll, scrollTarget, 1f - Mathf.Exp(-ScrollSpeed * dt));
            if (Mathf.Abs(scroll - scrollTarget) < 0.25f) scroll = scrollTarget;
            ApplyScroll();
        }

        /// <summary>Atualiza onde o adversario `id` esta (quantas letras ele ja acertou em sequencia).</summary>
        public void SetOpponent(int id, int prefix)
        {
            opponentPrefix[id] = prefix;
            PlaceOpponentCursor(id);
        }

        /// <summary>Com peek, mostra so o texto original (mantendo a janela de tres linhas), sem o que foi digitado nem o cursor.</summary>
        public void Render(TypingModel m, bool glitch, bool peek)
        {
            int typedLen = peek ? 0 : m.TypedLength;
            int total = Math.Max(typedLen, m.Target.Length);

            BuildString(m, glitch, peek, typedLen, total);
            Text.text = sb.ToString();
            Text.ForceMeshUpdate();

            // As linhas so mudam quando o texto e trocado; nesse caso refaz uma vez com o apagado certo.
            if (RefreshLines())
            {
                BuildString(m, glitch, peek, typedLen, total);
                Text.text = sb.ToString();
                Text.ForceMeshUpdate();
            }

            currentLine = LineOf(m.Cursor);
            FollowCursor(m.Cursor);
            if (peek)
            {
                ownCursor.Hide();
                band.gameObject.SetActive(false);
            }
            else
            {
                PlaceCursor(m.Cursor);
            }

            foreach (var id in opponentPrefix.Keys) PlaceOpponentCursor(id);
        }

        int LineOf(int index)
        {
            if (lineOf.Length == 0) return 0;
            return lineOf[Mathf.Clamp(index, 0, lineOf.Length - 1)];
        }

        void BuildString(TypingModel m, bool glitch, bool peek, int typedLen, int total)
        {
            sb.Clear();
            if (FixedWidth) sb.Append("<mspace=").Append(MonoWidth).Append('>');

            int cursorLine = LineOf(m.Cursor);
            int run = -1; // estado * 16 + faixa de opacidade; estado: 0 cinza, 1 branco, 2 vermelho, 3 roxo

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
                    state = rewardCharacters.Contains(i) ? 3 : (peek ? 1 : 0);
                }

                int fade = Math.Min(Math.Abs(LineOf(i) - cursorLine), LineAlpha.Length - 1);
                int key = state * 16 + fade;
                if (key != run)
                {
                    CloseRun(run);
                    OpenRun(key);
                    run = key;
                }

                if (glitch && !peek) ch = LeetMap.Apply(ch);
                if (ch == '<') sb.Append("<noparse><</noparse>");
                else sb.Append(ch);
            }

            CloseRun(run);
            if (FixedWidth) sb.Append("</mspace>");
        }

        void OpenRun(int key)
        {
            int state = key / 16;
            float alpha = LineAlpha[key % 16];
            string a = ((int)(alpha * 255f)).ToString("X2");

            sb.Append("<color=#").Append(state == 0 ? Gray : state == 1 ? White : state == 3 ? Purple : Red).Append(a).Append('>');
            if (state == 2) sb.Append("<mark=#").Append(Red).Append(((int)(alpha * 64f)).ToString("X2")).Append('>');
        }

        void CloseRun(int key)
        {
            if (key < 0) return;
            if (key / 16 == 2) sb.Append("</mark>");
            sb.Append("</color>");
        }

        /// <summary>Guarda a linha de cada caractere; devolve true se o layout mudou desde a ultima vez.</summary>
        bool RefreshLines()
        {
            var info = Text.textInfo;
            int count = info.characterCount;
            bool changed = false;

            if (lineOf.Length != count)
            {
                lineOf = new int[count];
                changed = true;
            }

            for (int i = 0; i < count; i++)
            {
                int line = info.characterInfo[i].lineNumber;
                if (lineOf[i] != line)
                {
                    lineOf[i] = line;
                    changed = true;
                }
            }

            return changed;
        }

        /// <summary>Poe o centro da linha do cursor no centro vertical da area; o resto do texto se move em volta.</summary>
        void FollowCursor(int cursor)
        {
            var info = Text.textInfo;
            int count = info.characterCount;
            if (count == 0 || info.lineCount == 0)
            {
                band.gameObject.SetActive(false);
                return;
            }

            int lineNumber = info.characterInfo[Mathf.Clamp(cursor, 0, count - 1)].lineNumber;
            var line = info.lineInfo[lineNumber];

            // As medidas do TMP sao relativas ao pivo (centro) do texto: levar esse centro para -centroDaLinha deixa a linha em y = 0.
            scrollTarget = -(line.ascender + line.descender) * 0.5f;

            // Sem Tick rodando (ou logo apos Clear) nao ha o que animar: vai direto.
            bool ticking = Time.frameCount - lastTickFrame <= 2;
            if (snapNext || !ticking)
            {
                scroll = scrollTarget;
                snapNext = false;
            }
            ApplyScroll();

            band.gameObject.SetActive(true);
            band.rectTransform.sizeDelta = new Vector2(band.rectTransform.sizeDelta.x, line.ascender - line.descender + BandPadding * 2f);
        }

        void ApplyScroll()
        {
            Text.rectTransform.offsetMin = new Vector2(padLeft, PadY + scroll);
            Text.rectTransform.offsetMax = new Vector2(-padRight, -PadY + scroll);
        }

        /// <summary>Posicao (x, base e topo da linha) de um caractere; depois do ultimo, fica no fim dele. Falso se nao ha texto.</summary>
        bool CursorMetrics(int index, out float x, out float bottom, out float top)
        {
            var info = Text.textInfo;
            int count = info.characterCount;
            x = bottom = top = 0f;
            if (count == 0) return false;

            var ci = info.characterInfo[Mathf.Min(index, count - 1)];
            x = index < count ? ci.origin : ci.xAdvance;
            bottom = ci.descender;
            top = ci.ascender;
            return true;
        }

        void PlaceCursor(int index)
        {
            if (!CursorMetrics(index, out float x, out float bottom, out float top))
            {
                ownCursor.Hide();
                return;
            }

            ownCursor.Show(x, bottom, top - bottom);
        }

        void PlaceOpponentCursor(int id)
        {
            if (!opponentPrefix.TryGetValue(id, out int index)) return;

            if (!opponentCursors.TryGetValue(id, out var glyph))
            {
                var color = opponentColors.TryGetValue(id, out var c) ? c : UiKit.Red;
                glyph = new CursorGlyph(Text.transform, "OpponentCursor" + id, color);
                opponentCursors[id] = glyph;
            }

            if (Math.Abs(LineOf(index) - currentLine) > 1 || !CursorMetrics(index, out float x, out float bottom, out float top))
            {
                glyph.Hide();
                return;
            }

            glyph.Show(x, bottom, top - bottom);
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

        /// <summary>Cursor em "I": barra fina com uma tampa curta em cima e embaixo, na cor do jogador.</summary>
        sealed class CursorGlyph
        {
            public readonly Image Bar;
            readonly Image top;
            readonly Image bottom;

            public CursorGlyph(Transform parent, string name, Color color)
            {
                Bar = UiKit.Box(parent, name, color);
                var r = Bar.rectTransform;
                r.pivot = new Vector2(0.5f, 0f);
                r.anchorMin = new Vector2(0.5f, 0.5f);
                r.anchorMax = new Vector2(0.5f, 0.5f);

                top = Cap(Bar.transform, "Top", color, 1f);
                bottom = Cap(Bar.transform, "Bottom", color, 0f);
                Bar.gameObject.SetActive(false);
            }

            static Image Cap(Transform parent, string name, Color color, float y)
            {
                var img = UiKit.Box(parent, name, color);
                var r = img.rectTransform;
                r.anchorMin = new Vector2(0.5f, y);
                r.anchorMax = new Vector2(0.5f, y);
                r.pivot = new Vector2(0.5f, y);
                r.anchoredPosition = Vector2.zero;
                r.sizeDelta = new Vector2(CapWidth, CapHeight);
                return img;
            }

            public void SetColor(Color color)
            {
                Bar.color = color;
                top.color = color;
                bottom.color = color;
            }

            public void Show(float x, float baseY, float height)
            {
                Bar.gameObject.SetActive(true);
                Bar.rectTransform.sizeDelta = new Vector2(CursorWidth, height);
                Bar.rectTransform.localPosition = new Vector3(x, baseY, 0f);
            }

            public void Hide() => Bar.gameObject.SetActive(false);
        }
    }
}
