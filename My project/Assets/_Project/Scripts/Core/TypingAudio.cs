using System;
using UnityEngine;

namespace TypeDuel
{
    /// <summary>Toque de teclado bem baixo: clipes curtos gerados uma vez, sem assets externos.</summary>
    public sealed class TypingAudio : MonoBehaviour
    {
        readonly AudioClip[] clips = new AudioClip[3];
        AudioSource source;
        int nextClip;

        void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = Config.TypingVolume;
            const int rate = 44100;
            const int samples = 1323; // 30 ms, com ataque e cauda suaves.
            var noise = new System.Random(27);
            for (int variation = 0; variation < clips.Length; variation++)
            {
                var data = new float[samples];
                float smoothNoise = 0f;
                for (int i = 0; i < samples; i++)
                {
                    float t = i / (float)rate;
                    float progress = i / (float)(samples - 1);
                    smoothNoise = smoothNoise * 0.75f + ((float)noise.NextDouble() * 2f - 1f) * 0.25f;
                    float envelope = Mathf.Min(1f, t / 0.002f) * Mathf.Exp(-t * 160f) * (1f - progress);
                    float tone = Mathf.Sin(2f * Mathf.PI * (650f + variation * 75f) * t);
                    data[i] = (tone * 0.35f + smoothNoise * 0.65f) * envelope;
                }
                clips[variation] = AudioClip.Create("SoftKey" + variation, samples, 1, rate, false);
                clips[variation].SetData(data, 0);
            }
        }

        public void PlayLetter(char c)
        {
            if (!char.IsLetter(c)) return;
            source.PlayOneShot(clips[nextClip]);
            nextClip = (nextClip + 1) % clips.Length;
        }

        public void Stop() => source.Stop();

        void OnDestroy()
        {
            foreach (var clip in clips)
                if (clip != null) Destroy(clip);
        }
    }
}
