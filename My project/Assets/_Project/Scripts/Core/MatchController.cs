using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using TypeDuel.Net;
using TypeDuel.UI;

namespace TypeDuel
{
    public enum MatchState { Menu, Lobby, Countdown, Playing, Over }

    /// <summary>
    /// Lado "jogador" da partida: estado, entrada de teclado, poderes e UI. O host tambem e um jogador
    /// (alem de rodar a HostLogic), entao todos passam pelo mesmo caminho de mensagens.
    /// </summary>
    public sealed class MatchController : MonoBehaviour
    {
        const string IpPrefKey = "td_last_ip";

        readonly List<PowerType> queue = new List<PowerType>();
        readonly List<int> opponentIds = new List<int>();

        GameUI ui;
        GameNet net;
        HostLogic host;
        TypingModel model;

        string ip = "127.0.0.1";
        int lobbyPlayers = 1;
        float countdownEnd;
        float goBannerUntil;
        float glitchUntil;
        float lastInputTime;
        int wordsReported;
        int lastSentPrefix;
        bool finishedSent;
        Keyboard subscribedKeyboard;

        public MatchState State { get; private set; } = MatchState.Menu;
        public TypingModel Model => model;
        public int PowerCount => queue.Count;
        public bool IsHost => host != null;
        public bool CanHostStart => host != null && host.CanStart;
        public bool IsConnected => net != null;
        public string LastResult { get; private set; } = "";
        public string Ip { get => ip; set => ip = value; }

        bool GlitchActive => Time.time < glitchUntil;

        // ------------------------------------------------------------ ciclo de vida

        void Awake()
        {
            try { ip = PlayerPrefs.GetString(IpPrefKey, ip); } catch { /* ignorado */ }

            ui = new GameUI();
            ui.Build(StartHosting, () => StartJoining(ip), () => host?.StartMatch(), () => LeaveToMenu(null), () => host?.StartMatch());
            ui.SetIpText(ip, true);
        }

        void OnDestroy()
        {
            if (subscribedKeyboard != null) subscribedKeyboard.onTextInput -= OnTextInput;
            net?.Dispose();
        }

        void Update()
        {
            float dt = Time.deltaTime;

            AttachKeyboard();
            net?.Tick();
            ui.Tick(dt);

            switch (State)
            {
                case MatchState.Menu: UpdateMenu(); break;
                case MatchState.Lobby: UpdateLobby(); break;
                case MatchState.Countdown: UpdateCountdown(); break;
                case MatchState.Playing: UpdatePlaying(); break;
            }
        }

        void AttachKeyboard()
        {
            var kb = Keyboard.current;
            if (kb == subscribedKeyboard) return;

            if (subscribedKeyboard != null) subscribedKeyboard.onTextInput -= OnTextInput;
            subscribedKeyboard = kb;
            if (kb != null) kb.onTextInput += OnTextInput;
        }

        // ------------------------------------------------------------ menu / lobby

        void UpdateMenu()
        {
            ui.SetIpText(ip, (int)(Time.unscaledTime * 2f) % 2 == 0);

            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.backspaceKey.wasPressedThisFrame && ip.Length > 0) ip = ip.Substring(0, ip.Length - 1);
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) StartJoining(ip);

            bool modifier = kb.ctrlKey.isPressed || kb.leftCommandKey.isPressed || kb.rightCommandKey.isPressed;
            if (modifier && kb.vKey.wasPressedThisFrame)
            {
                foreach (char c in GUIUtility.systemCopyBuffer)
                    if (char.IsDigit(c) || c == '.') ip += c;
            }
        }

        void UpdateLobby()
        {
            if (host != null) ui.StartButton.interactable = host.CanStart;
        }

        public void StartHosting()
        {
            if (net != null) return;

            net = new GameNet();
            try
            {
                net.Host(Config.Port);
            }
            catch (Exception e)
            {
                net = null;
                ui.MenuStatus.text = "Could not host: " + e.Message;
                return;
            }

            net.ClientMessage += OnServerMessage;
            net.Disconnected += reason => LeaveToMenu(reason);
            host = new HostLogic(net);

            var addresses = GameNet.LocalAddresses();
            ui.LobbyIp.text = addresses.Count > 0
                ? "Tell the other player to join:  " + string.Join("  or  ", addresses)
                : "No network found. Connect to Wi-Fi/LAN and restart.";
            ui.LobbyInfo.text = "Port " + Config.Port + "  |  firewall must allow TypeDuel";
            ui.StartButton.gameObject.SetActive(true);

            EnterLobby();
            host.SendLobbyState();
        }

        public void StartJoining(string address)
        {
            if (net != null) return;

            net = new GameNet();
            try
            {
                net.Join(address.Trim(), Config.Port);
            }
            catch (Exception e)
            {
                net = null;
                ui.MenuStatus.text = e.Message + " (use the host's IPv4, e.g. 192.168.0.12)";
                return;
            }

            try { PlayerPrefs.SetString(IpPrefKey, address.Trim()); } catch { /* ignorado */ }

            net.ClientMessage += OnServerMessage;
            net.Connected += () =>
            {
                ui.LobbyIp.text = "Connected to " + address.Trim();
                ui.LobbyInfo.text = "Waiting for the host to start the match...";
                ui.StartButton.gameObject.SetActive(false);
                EnterLobby();
            };
            net.Disconnected += reason => LeaveToMenu(reason);
            ui.MenuStatus.text = "Connecting...";
            ui.MenuStatus.color = UiKit.TextDim;
        }

        void EnterLobby()
        {
            State = MatchState.Lobby;
            ui.MenuStatus.text = "";
            ui.MenuStatus.color = UiKit.Bad;
            ui.LobbyPlayers.text = "Players: " + lobbyPlayers + "/" + Config.MaxPlayers;
            ui.ShowLobby();
        }

        public void LeaveToMenu(string message)
        {
            StopAllCoroutines();
            net?.Dispose();
            net = null;
            host = null;
            lobbyPlayers = 1;
            State = MatchState.Menu;
            ui.ResetEffects();
            ui.HideResult();
            ui.ShowMenu();
            ui.MenuStatus.color = UiKit.Bad;
            ui.MenuStatus.text = message ?? "";
        }

        // ------------------------------------------------------------ mensagens do servidor

        void OnServerMessage(MsgType type, BinaryReader r)
        {
            switch (type)
            {
                case MsgType.Lobby:
                {
                    lobbyPlayers = r.ReadInt32();
                    ui.LobbyPlayers.text = "Players: " + lobbyPlayers + "/" + Config.MaxPlayers;
                    if (host == null && State == MatchState.Over) { /* aguardando revanche */ }
                    break;
                }
                case MsgType.Start:
                {
                    string text = r.ReadString();
                    int n = r.ReadInt32();
                    var ids = new List<int>(n);
                    for (int i = 0; i < n; i++) ids.Add(r.ReadInt32());
                    BeginMatch(text, ids);
                    break;
                }
                case MsgType.PowerGranted:
                {
                    if (State != MatchState.Playing) break;
                    var power = (PowerType)r.ReadByte();
                    queue.Add(power);
                    ui.SetSlots(queue);
                    Debug.Log($"[TD] Got power {power}");
                    break;
                }
                case MsgType.PowerEffect:
                {
                    var power = (PowerType)r.ReadByte();
                    int from = r.ReadInt32();
                    ApplyEffect(power, from);
                    break;
                }
                case MsgType.OpponentProgress:
                {
                    int id = r.ReadInt32();
                    int prefix = r.ReadInt32();
                    if (model != null) ui.SetOpponentProgress(id, (float)prefix / Mathf.Max(1, model.Target.Length));
                    break;
                }
                case MsgType.GameOver:
                {
                    int winner = r.ReadInt32();
                    byte reason = r.ReadByte();
                    EndMatch(winner, reason);
                    break;
                }
            }
        }

        // ------------------------------------------------------------ partida

        void BeginMatch(string text, List<int> ids)
        {
            StopAllCoroutines();
            ui.ResetEffects();
            ui.HideResult();

            model = new TypingModel(text, Config.ExtraTypedLimit);
            queue.Clear();
            opponentIds.Clear();
            foreach (var id in ids) if (id != net.LocalId) opponentIds.Add(id);

            glitchUntil = 0f;
            wordsReported = 0;
            lastSentPrefix = -1;
            finishedSent = false;
            LastResult = "";

            ui.SetupBars(opponentIds);
            ui.SetSlots(queue);
            ui.Typing.Clear();
            ui.ShowGame();

            State = MatchState.Countdown;
            countdownEnd = Time.time + Config.CountdownSeconds;
            Debug.Log($"[TD] Countdown started ({text.Length} chars)");
        }

        void UpdateCountdown()
        {
            float left = countdownEnd - Time.time;
            if (left > 0f)
            {
                ui.SetCountdown(Mathf.CeilToInt(left).ToString());
                return;
            }

            State = MatchState.Playing;
            goBannerUntil = Time.time + 0.7f;
            ui.SetCountdown("GO!");
            lastInputTime = Time.time;
            Refresh();
            Debug.Log("[TD] GO");
        }

        void UpdatePlaying()
        {
            if (goBannerUntil > 0f && Time.time > goBannerUntil)
            {
                goBannerUntil = 0f;
                ui.SetCountdown("");
            }

            // Glitch: mostra o tempo restante e redesenha uma vez quando acaba.
            bool glitching = GlitchActive;
            ui.SetGlitch(glitching ? glitchUntil - Time.time : 0f);
            if (!glitching && glitchUntil > 0f)
            {
                glitchUntil = 0f;
                Refresh();
            }

            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.backspaceKey.wasPressedThisFrame) DoBackspace();
                if (kb.leftArrowKey.wasPressedThisFrame) DoMove(-1);
                if (kb.rightArrowKey.wasPressedThisFrame) DoMove(1);
            }

            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame) UsePower();

            // Cursor piscando (fica fixo logo apos digitar).
            bool solid = Time.time - lastInputTime < 0.5f;
            ui.Typing.Cursor.enabled = solid || (int)(Time.time * 2f) % 2 == 0;
        }

        void EndMatch(int winnerId, byte reason)
        {
            StopAllCoroutines();
            ui.ResetEffects();
            State = MatchState.Over;

            bool won = winnerId == net.LocalId;
            LastResult = won ? "win" : "lose";
            string sub = reason == 1 ? "The other player left the match" : (won ? "You finished the text first" : "The other player finished first");
            ui.ShowResult(won, sub, host != null);
            Debug.Log($"[TD] Match over. Winner={winnerId} local={net.LocalId} result={LastResult}");
        }

        // ------------------------------------------------------------ entrada

        void OnTextInput(char c)
        {
            if (State == MatchState.Menu)
            {
                if (char.IsDigit(c) || c == '.') ip += c;
                return;
            }

            // O texto do jogo e ASCII; no macOS as setas chegam aqui como U+F700..F8FF (area privada), que viram quadrados.
            if (State != MatchState.Playing || c < ' ' || c > '~') return;

            var kb = Keyboard.current;
            if (kb != null && (kb.ctrlKey.isPressed || kb.leftCommandKey.isPressed || kb.rightCommandKey.isPressed || kb.altKey.isPressed))
                return;

            Type(c);
        }

        /// <summary>Insere uma letra no cursor (tambem usado pelo bot de testes).</summary>
        public void Type(char c)
        {
            if (State != MatchState.Playing || model == null) return;
            if (model.Insert(c)) AfterEdit();
        }

        public void DoBackspace()
        {
            if (State != MatchState.Playing || model == null) return;
            if (model.Backspace()) AfterEdit();
        }

        public void MoveCursorRight()
        {
            if (State == MatchState.Playing && model != null) DoMove(1);
        }

        public void HostStartMatch() => host?.StartMatch();

        void DoMove(int dir)
        {
            bool moved = dir < 0 ? model.MoveLeft() : model.MoveRight();
            if (!moved) return;
            lastInputTime = Time.time;
            Refresh();
        }

        void AfterEdit()
        {
            lastInputTime = Time.time;
            Refresh();
            ReportProgress();
        }

        void Refresh()
        {
            if (model == null) return;
            ui.Typing.Render(model, GlitchActive);
            ui.SetOwnProgress((float)model.CorrectPrefix / Mathf.Max(1, model.Target.Length));
        }

        void ReportProgress()
        {
            // Palavras completadas (conta 1 vez cada; o host tambem valida).
            var ends = model.WordEnds;
            while (wordsReported < ends.Count - 1 && model.CorrectPrefix >= ends[wordsReported])
            {
                int index = wordsReported++;
                net.SendToServer(MsgType.WordDone, w => w.Write(index));
            }

            if (model.CorrectPrefix != lastSentPrefix)
            {
                lastSentPrefix = model.CorrectPrefix;
                int prefix = lastSentPrefix;
                net.SendToServer(MsgType.Progress, w => w.Write(prefix));
            }

            if (!finishedSent && model.IsComplete)
            {
                finishedSent = true;
                net.SendToServer(MsgType.Finished);
                Debug.Log("[TD] Finished typing");
            }
        }

        // ------------------------------------------------------------ poderes

        public void UsePower()
        {
            if (State != MatchState.Playing || queue.Count == 0) return;

            var power = queue[0];
            queue.RemoveAt(0);
            ui.SetSlots(queue);
            net.SendToServer(MsgType.UsePower, w => w.Write((byte)power));
            ui.ShowToast("Sent " + PowerInfo.Name(power) + "!", PowerInfo.Color(power));
            Debug.Log($"[TD] Used power {power}");
        }

        void ApplyEffect(PowerType power, int from)
        {
            if (State != MatchState.Playing || model == null) return;
            Debug.Log($"[TD] Hit by {power} from {from}");

            switch (power)
            {
                case PowerType.Bobomb:
                {
                    // A bomba cai na palavra onde o jogador esta digitando agora.
                    var target = ui.Typing.CharWorldPosition(Mathf.Max(0, model.TypedLength - 1));
                    StartCoroutine(BombRoutine(target));
                    ui.ShowToast("BOBOMB incoming!", PowerInfo.Color(PowerType.Bobomb));
                    break;
                }
                case PowerType.Glitch:
                {
                    glitchUntil = Time.time + Config.GlitchSeconds;
                    ui.ShowToast("GLITCH! Your text is scrambled", PowerInfo.Color(PowerType.Glitch));
                    ui.FlashScreen(0.25f);
                    Refresh();
                    break;
                }
                case PowerType.Change:
                {
                    int len = model.TypedLength;
                    if (len > 3) model.SetCursor(UnityEngine.Random.Range(0, len - 2));
                    else model.SetCursor(0);
                    lastInputTime = Time.time;
                    ui.ShowToast("CHANGE! Cursor moved - use the arrow keys", PowerInfo.Color(PowerType.Change));
                    ui.FlashScreen(0.2f);
                    Refresh();
                    break;
                }
            }
        }

        IEnumerator BombRoutine(Vector3 target)
        {
            var bomb = ui.BombImage.rectTransform;
            ui.BombImage.gameObject.SetActive(true);

            var start = new Vector3(target.x, Screen.height + 120f, 0f);
            const float fall = 0.7f;
            for (float t = 0f; t < fall; t += Time.deltaTime)
            {
                float k = t / fall;
                bomb.position = Vector3.Lerp(start, target, k * k);
                bomb.localRotation = Quaternion.Euler(0f, 0f, t * 420f);
                yield return null;
            }

            ui.BombImage.gameObject.SetActive(false);

            if (State == MatchState.Playing && model != null)
            {
                int erased = model.DeleteLast(Config.BombEraseChars);
                Debug.Log($"[TD] Bomb exploded, erased {erased}");
                AfterEdit();
            }

            ui.FlashScreen(0.45f);
            StartCoroutine(ExplosionRoutine(target));
            yield return ShakeRoutine();
        }

        IEnumerator ExplosionRoutine(Vector3 at)
        {
            var img = ui.ExplosionImage;
            img.gameObject.SetActive(true);
            img.rectTransform.position = at;

            const float duration = 0.4f;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = t / duration;
                img.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.6f, 3.2f, k);
                var c = img.color;
                c.a = 1f - k;
                img.color = c;
                yield return null;
            }

            var end = img.color;
            end.a = 1f;
            img.color = end;
            img.gameObject.SetActive(false);
        }

        IEnumerator ShakeRoutine()
        {
            var panel = ui.TextPanel;
            var rest = new Vector2(0f, -30f);
            for (float t = 0f; t < 0.35f; t += Time.deltaTime)
            {
                panel.anchoredPosition = rest + UnityEngine.Random.insideUnitCircle * 14f * (1f - t / 0.35f);
                yield return null;
            }
            panel.anchoredPosition = rest;
        }
    }
}
