using System;
using UnityEngine;

namespace TypeDuel
{
    /// <summary>
    /// Ponto de entrada: monta o jogo inteiro (camera, UI, controlador) ao carregar qualquer cena,
    /// entao nao ha nada para configurar a mao no editor. Basta dar Play na SampleScene.
    /// </summary>
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            if (UnityEngine.Object.FindAnyObjectByType<MatchController>() != null) return;

            // Precisa continuar rodando quando a janela perde o foco (dois jogos na mesma maquina).
            Application.runInBackground = true;
            Application.targetFrameRate = 60;

            EnsureCamera();

            var go = new GameObject("TypeDuel");
            UnityEngine.Object.DontDestroyOnLoad(go);
            var controller = go.AddComponent<MatchController>();

            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-td-auto") >= 0)
                go.AddComponent<Dev.DevAutoplay>().Controller = controller;
        }

        static void EnsureCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }

            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = UI.UiKit.Background;
            cam.orthographic = true;
        }
    }
}
