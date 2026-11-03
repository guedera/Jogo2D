using TMPro;
using UnityEngine;

namespace TypeDuel.UI
{
    /// <summary>Campo grande centralizado onde o jogador escreve o nome do poder (Ctrl abre, Ctrl ou Espaco fecha).</summary>
    public sealed class PowerPromptView
    {
        readonly GameObject root;
        readonly TMP_Text text;

        public PowerPromptView(Transform parent)
        {
            var dim = UiKit.Box(parent, "PromptDim", new Color(0f, 0f, 0f, 0.6f));
            UiKit.Stretch(dim.rectTransform);
            root = dim.gameObject;

            var box = UiKit.Box(dim.transform, "PromptBox", UiKit.Panel);
            UiKit.Place(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(1000f, 360f));

            var title = UiKit.Label(box.transform, "PromptTitle", "CAST A POWER", 34f, UiKit.TextDim, style: FontStyles.Bold);
            UiKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -45f), new Vector2(900f, 50f));

            text = UiKit.Label(box.transform, "PromptText", "", 150f, UiKit.TextMain, style: FontStyles.Bold);
            UiKit.Place(text.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(900f, 200f));

            var hint = UiKit.Label(box.transform, "PromptHint", "BOBOMB  GLITCH  CHANGE   |   Ctrl or Space to cancel", 28f, UiKit.TextDim);
            UiKit.Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(950f, 50f));

            root.SetActive(false);
        }

        public void Show(string typed)
        {
            SetText(typed);
            root.SetActive(true);
        }

        public void SetText(string typed) => text.text = typed + "_";

        public void Hide() => root.SetActive(false);
    }
}
