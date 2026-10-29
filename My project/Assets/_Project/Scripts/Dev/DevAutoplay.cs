using System;
using System.IO;
using UnityEngine;

namespace TypeDuel.Dev
{
    /// <summary>
    /// Bot de teste, so liga com `-td-auto`. Serve para verificar host/cliente sem digitar a mao:
    ///   -td-auto host|join   -td-ip 192.168.0.5   -td-cps 12   -td-shot pasta   -td-quit
    /// Joga sozinho (digita o texto, comete erros, usa poderes) e imprime linhas "[TD-BOT]" no log.
    /// </summary>
    public sealed class DevAutoplay : MonoBehaviour
    {
        public MatchController Controller;

        string role = "host";
        string joinIp = "127.0.0.1";
        string shotDir;
        float cps = 10f;
        bool quitWhenDone;

        float timer;
        float typeAccumulator;
        float nextPowerAt;
        float quitAt = -1f;
        bool started;
        bool errorPending;
        bool playingShotTaken;
        float lobbyReadyTime;
        MatchState lastState = MatchState.Menu;

        void Start()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-td-auto" && i + 1 < args.Length) role = args[i + 1];
                if (args[i] == "-td-ip" && i + 1 < args.Length) joinIp = args[i + 1];
                if (args[i] == "-td-cps" && i + 1 < args.Length) float.TryParse(args[i + 1], out cps);
                if (args[i] == "-td-shot" && i + 1 < args.Length) shotDir = args[i + 1];
                if (args[i] == "-td-quit") quitWhenDone = true;
            }

            if (shotDir != null) Directory.CreateDirectory(shotDir);
            Debug.Log($"[TD-BOT] role={role} ip={joinIp} cps={cps}");
        }

        void Update()
        {
            timer += Time.deltaTime;

            if (!started && timer > 1.0f)
            {
                started = true;
                Shot("menu");
                if (role == "host") Controller.StartHosting();
                else Controller.StartJoining(joinIp);
            }

            if (Controller.State != lastState)
            {
                Debug.Log($"[TD-BOT] state {lastState} -> {Controller.State}");
                lastState = Controller.State;
                if (lastState == MatchState.Lobby) Shot("lobby");
                if (lastState == MatchState.Over) { Shot("result"); if (quitWhenDone) quitAt = timer + 2f; }
            }

            // Espera um pouco no lobby (para dar tempo da captura de tela) e inicia a partida.
            if (role == "host" && Controller.State == MatchState.Lobby && Controller.CanHostStart)
            {
                lobbyReadyTime += Time.deltaTime;
                if (lobbyReadyTime > 1.5f)
                {
                    lobbyReadyTime = -999f;
                    Debug.Log("[TD-BOT] host starting match");
                    Controller.HostStartMatch();
                }
            }

            if (Controller.State == MatchState.Playing) Play();

            if (quitAt > 0f && timer > quitAt)
            {
                Debug.Log($"[TD-BOT] RESULT {Controller.LastResult}");
                Application.Quit();
            }
        }

        void Play()
        {
            var model = Controller.Model;
            if (model == null) return;

            if (nextPowerAt <= 0f) nextPowerAt = timer + UnityEngine.Random.Range(1.5f, 3f);
            if (Controller.PowerCount > 0 && timer > nextPowerAt)
            {
                Controller.UsePower();
                nextPowerAt = timer + UnityEngine.Random.Range(1.5f, 3f);
            }

            typeAccumulator += Time.deltaTime * cps;
            while (typeAccumulator >= 1f)
            {
                typeAccumulator -= 1f;
                Step(model);
            }

            if (!playingShotTaken && model.CorrectPrefix > 120)
            {
                playingShotTaken = true;
                Shot("playing");
            }
        }

        void Step(TypingModel model)
        {
            // Se o cursor foi movido (CHANGE), volta ao final com as setas (uma por vez).
            if (model.Cursor < model.TypedLength)
            {
                Controller.MoveCursorRight();
                return;
            }

            // Corrige erros: apaga ate o prefixo correto.
            if (model.TypedLength > model.CorrectPrefix)
            {
                Controller.DoBackspace();
                return;
            }

            // Ocasionalmente erra uma letra.
            if (!errorPending && UnityEngine.Random.value < 0.03f)
            {
                errorPending = true;
                Controller.Type('#');
                return;
            }
            errorPending = false;

            if (model.TypedLength < model.Target.Length)
                Controller.Type(model.Target[model.TypedLength]);
        }

        void Shot(string name)
        {
            if (shotDir == null || Application.isBatchMode) return;
            ScreenCapture.CaptureScreenshot(Path.Combine(shotDir, $"{role}_{name}.png"));
        }
    }
}
