using UnityEngine;
using UnityEngine.UI;

namespace TypeDuel.UI
{
    /// <summary>Efeito visual curto, reutilizado entre poderes e sem bloquear a entrada.</summary>
    public sealed class PowerFeedback
    {
        const float Duration = 0.7f;
        readonly RectTransform root;
        readonly Image tint;
        readonly Image[] edges = new Image[4];
        readonly Image[] sparks = new Image[12];
        PowerType power;
        Color color;
        float elapsed;

        public PowerFeedback(Transform parent)
        {
            root = UiKit.NewRect("PowerFeedback", parent);
            UiKit.Stretch(root);
            tint = UiKit.Box(root, "Tint", Color.clear);
            UiKit.Stretch(tint.rectTransform);
            for (int i = 0; i < edges.Length; i++)
            {
                edges[i] = UiKit.Box(root, "Edge" + i, Color.clear);
                var r = edges[i].rectTransform;
                bool horizontal = i < 2;
                r.anchorMin = horizontal ? new Vector2(0f, i) : new Vector2(i - 2, 0f);
                r.anchorMax = horizontal ? new Vector2(1f, i) : new Vector2(i - 2, 1f);
                r.pivot = horizontal ? new Vector2(0.5f, i) : new Vector2(i - 2, 0.5f);
                r.sizeDelta = horizontal ? new Vector2(0f, 6f) : new Vector2(6f, 0f);
            }
            for (int i = 0; i < sparks.Length; i++)
                sparks[i] = UiKit.Box(root, "Spark" + i, Color.clear);
            Reset();
        }

        public void Play(PowerType type)
        {
            power = type;
            color = PowerInfo.Color(type);
            elapsed = 0f;
            root.gameObject.SetActive(true);
            Tick(0f);
        }

        public void Tick(float dt)
        {
            if (!root.gameObject.activeSelf) return;
            elapsed += dt;
            float t = Mathf.Clamp01(elapsed / Duration);
            if (t >= 1f) { Reset(); return; }

            float fade = Mathf.Sin(t * Mathf.PI) * (1f - t);
            tint.color = WithAlpha(0.06f * fade);
            foreach (var edge in edges) edge.color = WithAlpha(0.65f * fade);
            for (int i = 0; i < sparks.Length; i++)
            {
                var r = sparks[i].rectTransform;
                float x, y, angle = 0f;
                Vector2 size;
                switch (power)
                {
                    case PowerType.Bobomb:
                        angle = i * 360f / sparks.Length;
                        float radians = angle * Mathf.Deg2Rad;
                        x = Mathf.Cos(radians) * (70f + 250f * t);
                        y = Mathf.Sin(radians) * (70f + 170f * t);
                        size = new Vector2(22f * (1f - t) + 4f, 5f);
                        break;
                    case PowerType.Glitch:
                        x = (i % 3 - 1) * 200f + Mathf.Sin((int)(t * 10f) + i * 4f) * 55f;
                        y = (i / 3 - 1.5f) * 65f;
                        size = new Vector2(45f + i % 4 * 25f, 3f);
                        break;
                    case PowerType.Change:
                        x = 320f - t * 620f + i % 3 * 30f;
                        y = (i / 3 - 1.5f) * 65f;
                        angle = i % 2 == 0 ? 35f : -35f;
                        size = new Vector2(24f, 4f);
                        break;
                    default: // AUTOCOMPLETE: pequenas particulas acompanhando o avanco.
                        x = -400f + t * 800f - i * 22f;
                        y = Mathf.Sin(i * 2f + t * 4f) * 85f;
                        angle = 45f + t * 100f;
                        size = new Vector2(6f, 6f);
                        break;
                }
                UiKit.Place(r, new Vector2(0.5f, 0.5f), new Vector2(-175f + x, -10f + y), size);
                r.localRotation = Quaternion.Euler(0f, 0f, angle);
                sparks[i].color = WithAlpha(0.5f * fade);
            }
        }

        Color WithAlpha(float alpha) => new Color(color.r, color.g, color.b, alpha);

        public void Reset()
        {
            elapsed = Duration;
            root.gameObject.SetActive(false);
        }
    }
}
