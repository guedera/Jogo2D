using System.Text;

namespace TypeDuel
{
    /// <summary>
    /// Prepara um texto enviado pelo jogador: tudo em minusculo, letras com acento viram letras normais,
    /// qualquer outro caractere vira espaco (espacos repetidos colapsam) e o texto so e valido com 50 palavras ou mais.
    /// </summary>
    public static class TextCleaner
    {
        public const int MinWords = 50;
        public const int MaxWords = 300;
        public const long MaxFileBytes = 1000000;

        // Letras acentuadas do Latin-1 (portugues, espanhol, frances, alemao...). Outras viram espaco.
        const string AccentFrom = "ÀÁÂÃÄÅàáâãäåÇçÈÉÊËèéêëÌÍÎÏìíîïÑñÒÓÔÕÖòóôõöÙÚÛÜùúûüÝýÿ";
        const string AccentTo = "AAAAAAaaaaaaCcEEEEeeeeIIIIiiiiNnOOOOOoooooUUUUuuuuYyy";

        /// <summary>Limpa `raw`. Devolve false (e cleaned vazio) se sobrarem menos de MinWords palavras; corta em MaxWords.</summary>
        public static bool TryClean(string raw, out string cleaned, out int words)
        {
            var sb = new StringBuilder(raw.Length);
            words = 0;
            bool inWord = false;

            foreach (char c in raw)
            {
                char letter = ToPlainLetter(c);
                if (letter == '\0')
                {
                    inWord = false;
                    continue;
                }

                if (!inWord)
                {
                    if (words >= MaxWords) break;
                    if (words > 0) sb.Append(' ');
                    words++;
                    inWord = true;
                }
                sb.Append(letter);
            }

            if (words < MinWords)
            {
                cleaned = "";
                return false;
            }

            cleaned = sb.ToString();
            return true;
        }

        static char ToPlainLetter(char c)
        {
            if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')) return char.ToLowerInvariant(c);

            int i = AccentFrom.IndexOf(c);
            return i >= 0 ? char.ToLowerInvariant(AccentTo[i]) : '\0';
        }
    }
}
