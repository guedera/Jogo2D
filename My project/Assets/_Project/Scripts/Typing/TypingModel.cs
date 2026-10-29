using System;
using System.Collections.Generic;
using System.Text;

namespace TypeDuel
{
    /// <summary>
    /// Estado puro da digitacao (sem Unity, sem rede).
    /// Texto digitado + cursor, comparado letra a letra com o alvo.
    /// </summary>
    public sealed class TypingModel
    {
        readonly StringBuilder typed = new StringBuilder();
        readonly List<int> wordEnds = new List<int>();
        readonly int maxLength;

        public string Target { get; }
        public int Cursor { get; private set; }
        public int TypedLength => typed.Length;

        /// <summary>Tamanho do prefixo digitado que esta 100% correto (usado para progresso e palavras).</summary>
        public int CorrectPrefix { get; private set; }

        /// <summary>Para cada palavra, o indice (exclusivo) a partir do qual ela conta como completa: inclui o espaco seguinte.</summary>
        public IReadOnlyList<int> WordEnds => wordEnds;

        public bool IsComplete => typed.Length == Target.Length && CorrectPrefix == Target.Length;

        public TypingModel(string target, int extraAllowed)
        {
            Target = target;
            maxLength = target.Length + extraAllowed;

            for (int i = 0; i < target.Length; i++)
                if (target[i] == ' ') wordEnds.Add(i + 1);
            wordEnds.Add(target.Length);
        }

        public char TypedAt(int index) => typed[index];

        public bool IsCorrectAt(int index) => index < typed.Length && index < Target.Length && typed[index] == Target[index];

        public bool Insert(char c)
        {
            if (typed.Length >= maxLength) return false;
            typed.Insert(Cursor, c);
            Cursor++;
            Recompute();
            return true;
        }

        public bool Backspace()
        {
            if (Cursor == 0) return false;
            typed.Remove(Cursor - 1, 1);
            Cursor--;
            Recompute();
            return true;
        }

        public bool MoveLeft()
        {
            if (Cursor == 0) return false;
            Cursor--;
            return true;
        }

        public bool MoveRight()
        {
            if (Cursor >= typed.Length) return false;
            Cursor++;
            return true;
        }

        /// <summary>Apaga as ultimas n letras digitadas (BOBOMB).</summary>
        public int DeleteLast(int n)
        {
            n = Math.Min(n, typed.Length);
            if (n <= 0) return 0;
            typed.Remove(typed.Length - n, n);
            Cursor = Math.Min(Cursor, typed.Length);
            Recompute();
            return n;
        }

        public void SetCursor(int index) => Cursor = Math.Max(0, Math.Min(index, typed.Length));

        void Recompute()
        {
            int limit = Math.Min(typed.Length, Target.Length);
            int i = 0;
            while (i < limit && typed[i] == Target[i]) i++;
            CorrectPrefix = i;
        }
    }
}
