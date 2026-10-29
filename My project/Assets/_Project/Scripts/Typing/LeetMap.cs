namespace TypeDuel
{
    /// <summary>Troca letras por numeros so na exibicao. Sempre 1 caractere -> 1 caractere, para os indices continuarem alinhados.</summary>
    public static class LeetMap
    {
        public static char Apply(char c)
        {
            switch (char.ToLowerInvariant(c))
            {
                case 'a': return '4';
                case 'e': return '3';
                case 'i': return '1';
                case 'l': return '1';
                case 'o': return '0';
                case 's': return '5';
                case 't': return '7';
                case 'b': return '8';
                case 'g': return '9';
                case 'z': return '2';
                default: return c;
            }
        }
    }
}
