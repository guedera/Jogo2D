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
        const string NamePrefKey = "td_name";

        readonly List<PowerType> queue = new List<PowerType>();
        readonly List<int> opponentIds = new List<int>();
        readonly List<string> opponentNames = new List<string>();

        GameUI ui;
        GameNet net;
        HostLogic host;
        TypingModel model;

        string ip = "127.0.0.1";
        string playerName = "";
        bool editingName;

        // Origem do texto da partida: banco de palavras (GENERATE) ou um .txt enviado (UPLOAD TEXT).
        const int UploadChoice = 0;
        readonly List<int> suggestionOwners = new List<int>();
        readonly List<string> suggestionLabels = new List<string>();
        int lobbySelected = HostLogic.NoChoice;
        bool soloLobby;
        int soloChoice = HostLogic.NoChoice;
        string soloText = "";
        string soloTitle = "";
        int soloWords;
        int lobbyPlayers = 1;
        float countdownEnd;
        float goBannerUntil;
        float glitchUntil;
        float lastInputTime;
        int wordsReported;
        int lastSentPrefix;
        bool finishedSent;
        bool peeking;
        bool casting;
        string castText = "";
        bool training;
        float trainingLeft;
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
            try { playerName = PlayerPrefs.GetString(NamePrefKey, ""); } catch { /* ignorado */ }

            ui = new GameUI();
            ui.Build(StartHosting, () => StartJoining(ip), StartFromLobby, () => LeaveToMenu(null), () => host?.ReturnToLobby(), OpenTrainingLobby, ReturnToTrainingLobby);
            ui.SetIpText(ip, true);
            ui.SetNameText(playerName, false, false);
            ui.NameBoxButton.onClick.AddListener(() => editingName = true);
            ui.IpBoxButton.onClick.AddListener(() => editingName = false);

            ui.GenerateButton.onClick.AddListener(OnGenerateClicked);
            ui.UploadButton.onClick.AddListener(OnUploadClicked);
            for (int i = 0; i < ui.SuggestionButtons.Length; i++)
            {
                int index = i;
                ui.SuggestionButtons[i].onClick.AddListener(() => OnSuggestionClicked(index));
            }
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
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame
                && (State != MatchState.Menu || net != null))
            {
                LeaveToMenu(null);
                return;
            }

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
            bool caret = (int)(Time.unscaledTime * 2f) % 2 == 0;
            ui.SetIpText(ip, !editingName && caret);
            ui.SetNameText(playerName, editingName, caret);

            var kb = Keyboard.current;
            if (kb == null) return;

            // TAB (ou clique) troca entre o campo de nome e o de IP.
            if (kb.tabKey.wasPressedThisFrame) editingName = !editingName;

            if (kb.backspaceKey.wasPressedThisFrame)
            {
                if (editingName && playerName.Length > 0) playerName = playerName.Substring(0, playerName.Length - 1);
                else if (!editingName && ip.Length > 0) ip = ip.Substring(0, ip.Length - 1);
            }

            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
            {
                if (editingName) editingName = false;
                else StartJoining(ip);
            }

            bool modifier = kb.ctrlKey.isPressed || kb.leftCommandKey.isPressed || kb.rightCommandKey.isPressed;
            if (!editingName && modifier && kb.vKey.wasPressedThisFrame)
            {
                foreach (char c in GUIUtility.systemCopyBuffer)
                    if (char.IsDigit(c) || c == '.') ip += c;
            }
        }

        void UpdateLobby()
        {
            // So da para comecar depois de escolher a origem do texto.
            if (soloLobby) ui.StartButton.interactable = soloChoice != HostLogic.NoChoice;
            else if (host != null) ui.StartButton.interactable = host.CanStart;
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
            SendName();

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
                SendName();
                EnterLobby();
            };
            net.Disconnected += reason => LeaveToMenu(reason);
            ui.MenuStatus.text = "Connecting...";
            ui.MenuStatus.color = UiKit.TextDim;
        }

        /// <summary>Fixa o nome digitado (sem espacos nas pontas) e guarda para a proxima vez.</summary>
        string CommitName()
        {
            playerName = playerName.Trim();
            try { PlayerPrefs.SetString(NamePrefKey, playerName); } catch { /* ignorado */ }
            return playerName;
        }

        void SendName()
        {
            string name = CommitName();
            net.SendToServer(MsgType.SetName, w => w.Write(name));
        }

        void EnterLobby()
        {
            State = MatchState.Lobby;
            ui.MenuStatus.text = "";
            ui.MenuStatus.color = UiKit.Bad;
            ui.LobbyPlayers.text = "Players: " + lobbyPlayers + "/" + Config.MaxPlayers;
            ui.SetLobbyMode(false);
            lobbySelected = HostLogic.NoChoice;
            suggestionOwners.Clear();
            suggestionLabels.Clear();
            RefreshLobbyChoice();
            ui.ShowLobby();
        }

        // ------------------------------------------------------------ texto da partida (GENERATE / UPLOAD TEXT)

        void StartFromLobby()
        {
            if (soloLobby) StartTraining();
            else host?.StartMatch();
        }

        /// <summary>Sala em grupo: mostra o que o host escolheu e os textos sugeridos pelos jogadores.</summary>
        void RefreshLobbyChoice()
        {
            int selectedIndex = suggestionOwners.IndexOf(lobbySelected);
            ui.SetSuggestions(suggestionLabels, selectedIndex);
            ui.SetSourceSelection(lobbySelected == HostLogic.GenerateChoice, false);

            string text;
            if (lobbySelected == HostLogic.GenerateChoice) text = "Text: random words";
            else if (selectedIndex >= 0) text = "Text: " + suggestionLabels[selectedIndex];
            else text = host != null ? "Choose GENERATE or a suggested text to start" : "Waiting for the host to choose the text...";
            ui.SetLobbyChoice(text, UiKit.TextDim);
        }

        void OpenTrainingLobby() => EnterTrainingLobby(false);

        /// <summary>Treinar de novo: volta para a sala de espera com o texto anterior ja marcado.</summary>
        void ReturnToTrainingLobby() => EnterTrainingLobby(true);

        void EnterTrainingLobby(bool keepChoice)
        {
            CommitName();
            soloLobby = true;
            if (!keepChoice) soloChoice = HostLogic.NoChoice;
            State = MatchState.Lobby;
            ui.MenuStatus.text = "";
            ui.SetLobbyMode(true);
            ui.StartButton.gameObject.SetActive(true);
            RefreshSoloChoice();
            ui.ShowLobby();
        }

        void RefreshSoloChoice()
        {
            ui.SetSourceSelection(soloChoice == HostLogic.GenerateChoice, soloChoice == UploadChoice);

            string text;
            if (soloChoice == HostLogic.GenerateChoice) text = "Text: random words";
            else if (soloChoice == UploadChoice) text = "Text: " + soloTitle + "  (" + soloWords + " words)";
            else text = "Choose GENERATE or UPLOAD TEXT to start";
            ui.SetLobbyChoice(text, UiKit.TextDim);
        }

        void OnGenerateClicked()
        {
            if (State != MatchState.Lobby) return;

            if (soloLobby)
            {
                soloChoice = HostLogic.GenerateChoice;
                RefreshSoloChoice();
            }
            else if (host != null)
            {
                host.ChooseGenerate();
            }
        }

        void OnSuggestionClicked(int index)
        {
            if (State != MatchState.Lobby || host == null || index >= suggestionOwners.Count) return;
            host.ChooseSuggestion(suggestionOwners[index]);
        }

        void OnUploadClicked()
        {
            if (State != MatchState.Lobby) return;

            if (!FilePicker.IsSupported)
            {
                ui.SetLobbyChoice("File picker is not supported on this platform", UiKit.Bad);
                return;
            }

            string path = FilePicker.PickTextFile();
            if (path == null) return;

            if (!TryLoadText(path, out string title, out string cleaned, out int words))
            {
                // Texto invalido: avisa e volta a escolha (no treino, a escolha anterior e descartada).
                ui.SetLobbyChoice("Invalid text: it needs at least " + TextCleaner.MinWords + " words after cleaning", UiKit.Bad);
                if (soloLobby)
                {
                    soloChoice = HostLogic.NoChoice;
                    ui.SetSourceSelection(false, false);
                }
                return;
            }

            if (soloLobby)
            {
                soloChoice = UploadChoice;
                soloText = cleaned;
                soloTitle = title;
                soloWords = words;
                RefreshSoloChoice();
            }
            else
            {
                // Vai como sugestao: o host valida de novo e decide se usa.
                net.SendToServer(MsgType.SuggestText, w =>
                {
                    w.Write(title);
                    w.Write(cleaned);
                });
            }
        }

        /// <summary>Le o .txt e limpa (acentos viram letras normais, o resto vira espaco). Falha se tiver menos de 50 palavras.</summary>
        static bool TryLoadText(string path, out string title, out string cleaned, out int words)
        {
            title = Path.GetFileName(path);
            if (title.Length > 24) title = title.Substring(0, 24);
            cleaned = "";
            words = 0;

            try
            {
                var info = new FileInfo(path);
                if (!info.Exists || info.Length > TextCleaner.MaxFileBytes) return false;
                bool textFile = string.Equals(info.Extension, ".txt", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(info.Extension, ".md", StringComparison.OrdinalIgnoreCase);
                if (!textFile) return false;
                return TextCleaner.TryClean(File.ReadAllText(path), out cleaned, out words);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public void LeaveToMenu(string message)
        {
            StopAllCoroutines();
            StopCasting();
            peeking = false;
            net?.Dispose();
            net = null;
            host = null;
            lobbyPlayers = 1;
            training = false;
            soloLobby = false;
            soloChoice = HostLogic.NoChoice;
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
                case MsgType.ReturnToLobby:
                {
                    EnterLobby();
                    break;
                }
                case MsgType.Suggestions:
                {
                    lobbySelected = r.ReadInt32();
                    int count = r.ReadInt32();
                    suggestionOwners.Clear();
                    suggestionLabels.Clear();
                    for (int i = 0; i < count; i++)
                    {
                        int owner = r.ReadInt32();
                        string ownerName = r.ReadString();
                        string title = r.ReadString();
                        int words = r.ReadInt32();

                        string who = ownerName.Length > 0 ? ownerName : (owner == 0 ? "Host" : "Player " + owner);
                        suggestionOwners.Add(owner);
                        suggestionLabels.Add(who + "  -  " + title + "  (" + words + " words)");
                    }
                    RefreshLobbyChoice();
                    break;
                }
                case MsgType.Start:
                {
                    string text = r.ReadString();
                    int n = r.ReadInt32();
                    var ids = new List<int>(n);
                    for (int i = 0; i < n; i++) ids.Add(r.ReadInt32());
                    var names = new List<string>(n);
                    for (int i = 0; i < n; i++) names.Add(r.ReadString());
                    int rewardCount = r.ReadInt32();
                    var rewards = new List<int>(rewardCount);
                    for (int i = 0; i < rewardCount; i++) rewards.Add(r.ReadInt32());
                    BeginMatch(text, ids, names, false);
                    ui.Typing.SetPowerWords(model, rewards);
                    break;
                }
                case MsgType.PowerGranted:
                {
                    if (State != MatchState.Playing || queue.Count >= Config.MaxHeldPowers) break;
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
                    if (model != null)
                    {
                        ui.SetOpponentProgress(id, (float)prefix / Mathf.Max(1, model.Target.Length));
                        ui.Typing.SetOpponent(id, prefix);
                    }
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

        void BeginMatch(string text, List<int> ids, List<string> names, bool trainingMode)
        {
            StopAllCoroutines();
            ui.ResetEffects();
            ui.HideResult();

            model = new TypingModel(text, Config.ExtraTypedLimit);
            queue.Clear();
            opponentIds.Clear();
            opponentNames.Clear();
            for (int i = 0; i < ids.Count; i++)
            {
                if (ids[i] == net.LocalId) continue;
                opponentIds.Add(ids[i]);
                opponentNames.Add(names[i]);
            }

            glitchUntil = 0f;
            wordsReported = 0;
            lastSentPrefix = -1;
            finishedSent = false;
            peeking = false;
            StopCasting();
            training = trainingMode;
            trainingLeft = Config.TrainingSeconds;
            LastResult = "";

            ui.SetupBars(playerName, opponentIds, opponentNames);
            ui.SetSlots(queue);
            ui.Typing.Clear();
            ui.ShowGame();
            ui.SetTrainingTimer(training ? trainingLeft : -1f);
            ui.SetPowersVisible(!training);

            State = MatchState.Countdown;
            countdownEnd = Time.time + Config.CountdownSeconds;
            Debug.Log($"[TD] Countdown started ({text.Length} chars)");
        }

        // ------------------------------------------------------------ treino (solo, sem rede)

        void StartTraining()
        {
            CommitName();
            string text = soloChoice == UploadChoice
                ? soloText
                : WordBank.GenerateText(new System.Random(), Config.TrainingWordCount);
            BeginMatch(text, new List<int>(), new List<string>(), true);
        }

        void EndTraining()
        {
            State = MatchState.Over;
            StopCasting();

            int correct = model.CorrectTypedCount();
            float minutes = Mathf.Max(Config.TrainingSeconds - Mathf.Max(0f, trainingLeft), 0.1f) / 60f;
            float wpm = correct / 5f / minutes;
            float accuracy = model.TypedLength == 0 ? 0f : (float)correct / model.TypedLength;

            ui.ShowTrainingResult(wpm, accuracy);
            Debug.Log($"[TD] Training over. WPM={wpm:F1} accuracy={accuracy:P0}");
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
            if (training)
            {
                trainingLeft -= Time.deltaTime;
                ui.SetTrainingTimer(Mathf.Max(0f, trainingLeft));
                if (trainingLeft <= 0f)
                {
                    EndTraining();
                    return;
                }
            }

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
                if (kb.ctrlKey.wasPressedThisFrame && !training) ToggleCasting();

                if (casting)
                {
                    if (kb.backspaceKey.wasPressedThisFrame) DoCastBackspace();
                    if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) SubmitCast();
                }
                else
                {
                    if (kb.backspaceKey.wasPressedThisFrame) DoBackspace();
                    if (kb.deleteKey.wasPressedThisFrame) DoDelete();
                    if (kb.leftArrowKey.wasPressedThisFrame) DoMove(-1);
                    if (kb.rightArrowKey.wasPressedThisFrame) DoMove(1);
                }

                SetPeeking(!casting && kb.tabKey.isPressed);
            }

            // Cursor piscando (fica fixo logo apos digitar).
            bool solid = Time.time - lastInputTime < 0.5f;
            ui.Typing.Cursor.enabled = solid || (int)(Time.time * 2f) % 2 == 0;
        }

        void EndMatch(int winnerId, byte reason)
        {
            StopAllCoroutines();
            ui.ResetEffects();
            State = MatchState.Over;
            StopCasting();

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
                if (editingName)
                {
                    if (playerName.Length < Config.MaxNameLength && c >= ' ' && c <= '~' && c != '<' && c != '>') playerName += c;
                }
                else if (char.IsDigit(c) || c == '.')
                {
                    ip += c;
                }
                return;
            }

            if (State != MatchState.Playing) return;

            if (casting)
            {
                if (c == ' ') StopCasting();
                else if (char.IsLetter(c)) AddCastChar(c);
                return;
            }

            if (char.IsControl(c)) return;
            // O texto do jogo e ASCII; no macOS as setas chegam aqui como U+F700..F8FF (area privada), que viram quadrados.
            if (c < ' ' || c > '~') return;

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

        /// <summary>Segurar TAB mostra o texto original no lugar do que foi digitado.</summary>
        void SetPeeking(bool on)
        {
            if (peeking == on) return;
            peeking = on;
            Refresh();
        }

        public void DoBackspace()
        {
            if (State != MatchState.Playing || model == null) return;
            if (model.Backspace()) AfterEdit();
        }

        public void DoDelete()
        {
            if (State != MatchState.Playing || model == null) return;
            if (model.Delete()) AfterEdit();
        }

        public void MoveCursorRight()
        {
            if (State == MatchState.Playing && model != null) DoMove(1);
        }

        public void HostStartMatch() => host?.StartMatch();

        /// <summary>Escolhe o texto gerado (usado pelo bot de testes).</summary>
        public void HostChooseGenerate() => host?.ChooseGenerate();

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
            if (!training) ReportProgress();
            else if (model.IsComplete) EndTraining();
        }

        void Refresh()
        {
            if (model == null) return;
            ui.Typing.Render(model, GlitchActive, peeking);
            ui.SetOwnProgress((float)model.CorrectPrefix / Mathf.Max(1, model.Target.Length));
        }

        void ReportProgress()
        {
            // Envia o progresso antes das palavras para o host avaliar quem esta atras.
            if (model.CorrectPrefix != lastSentPrefix)
            {
                lastSentPrefix = model.CorrectPrefix;
                int prefix = lastSentPrefix;
                net.SendToServer(MsgType.Progress, w => w.Write(prefix));
            }

            var ends = model.WordEnds;
            while (wordsReported < ends.Count - 1 && model.CorrectPrefix >= ends[wordsReported])
            {
                int index = wordsReported++;
                ui.Typing.ConsumePowerWord(model, index);
                net.SendToServer(MsgType.WordDone, w => w.Write(index));
            }

            if (!finishedSent && model.IsComplete)
            {
                finishedSent = true;
                net.SendToServer(MsgType.Finished);
                Debug.Log("[TD] Finished typing");
            }
        }

        // ------------------------------------------------------------ poderes

        /// <summary>Usa o primeiro poder da fila (usado pelo bot de testes).</summary>
        public void UsePower()
        {
            if (queue.Count > 0) UsePower(queue[0]);
        }

        public void UsePower(PowerType power)
        {
            if (State != MatchState.Playing || queue.Count == 0 || queue[0] != power) return;
            queue.RemoveAt(0);

            ui.SetSlots(queue);
            net.SendToServer(MsgType.UsePower, w => w.Write((byte)power));
            ui.ShowToast("Used " + PowerInfo.Name(power) + "!", PowerInfo.Color(power));
            Debug.Log($"[TD] Used power {power}");
        }

        // ------------------------------------------------------------ escolher poder (Ctrl + nome)

        void ToggleCasting()
        {
            if (casting) StopCasting();
            else StartCasting();
        }

        void StartCasting()
        {
            if (queue.Count == 0)
            {
                ui.ShowToast("No powers yet - finish purple words to earn them", UiKit.TextDim);
                return;
            }

            casting = true;
            castText = "";
            ui.Prompt.Show(castText);
        }

        void StopCasting()
        {
            casting = false;
            castText = "";
            ui.Prompt.Hide();
        }

        void AddCastChar(char c)
        {
            castText += char.ToUpperInvariant(c);

            for (int i = 0; i < PowerInfo.Count; i++)
            {
                var power = (PowerType)i;
                if (PowerInfo.Name(power) != castText) continue;

                if (power == queue[0])
                {
                    StopCasting();
                    UsePower(power);
                }
                else
                {
                    ui.ShowToast("Next power is " + PowerInfo.Name(queue[0]), UiKit.TextDim);
                    castText = "";
                    ui.Prompt.SetText(castText);
                }
                return;
            }

            // Letra que nao leva a nenhum nome de poder: recomeca.
            bool isPrefix = false;
            for (int i = 0; i < PowerInfo.Count; i++)
                if (PowerInfo.Name((PowerType)i).StartsWith(castText, StringComparison.Ordinal)) isPrefix = true;
            if (!isPrefix) castText = "";

            ui.Prompt.SetText(castText);
        }

        // Enter fecha o campo; se o nome nao for um poder valido, avisa o que foi digitado.
        void SubmitCast()
        {
            if (castText.Length == 0) ui.ShowToast("Cast cancelled", UiKit.TextDim);
            else ui.ShowToast("Unknown power: " + castText, UiKit.Bad);
            StopCasting();
        }

        void DoCastBackspace()
        {
            if (castText.Length == 0) return;
            castText = castText.Substring(0, castText.Length - 1);
            ui.Prompt.SetText(castText);
        }

        void ApplyEffect(PowerType power, int from)
        {
            if (State != MatchState.Playing || model == null) return;
            Debug.Log($"[TD] Hit by {power} from {from}");

            switch (power)
            {
                case PowerType.Autocomplete:
                {
                    model.Autocomplete(Config.AutocompleteWords);
                    AfterEdit();
                    ui.ShowToast("AUTOCOMPLETE!", PowerInfo.Color(power));
                    break;
                }
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
                    model.SetCursor(Mathf.Max(0, model.Cursor - Config.ChangeRewindChars));
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
            var rest = new Vector2(0f, -10f);
            for (float t = 0f; t < 0.35f; t += Time.deltaTime)
            {
                panel.anchoredPosition = rest + UnityEngine.Random.insideUnitCircle * 14f * (1f - t / 0.35f);
                yield return null;
            }
            panel.anchoredPosition = rest;
        }
    }
}
