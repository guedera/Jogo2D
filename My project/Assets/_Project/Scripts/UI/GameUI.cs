using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace TypeDuel.UI
{
    /// <summary>Constroi e controla todas as telas (menu, lobby, partida, resultado) por codigo.</summary>
    public sealed class GameUI
    {
        const int MaxSlots = Config.MaxHeldPowers;
        const int SlotColumns = 3;

        // Layout do lobby (referencia 1920x1080)
        const float LobbyTop = 300f;
        const float LobbyColumnW = 540f;
        const float LobbyRightX = 360f;
        const float LobbyButtonH = 96f;

        // Layout dos paineis laterais da partida
        const float SidePanelW = 340f;
        const float SidePanelTop = 100f;
        const float SidePanelRight = 60f;
        const float SidePanelGap = 20f;
        const float PanelPad = 20f;
        const float TrainingPanelH = 150f;
        const float PlayerRowH = 56f;
        const float ProgressWidth = 1230f;
        const float SlotSize = 92f;
        const float SlotLabelH = 26f;
        const float SlotGap = 12f;
        const float EffectsHeaderH = 104f;

        public Canvas Canvas { get; private set; }

        // Menu
        GameObject menuPanel;
        public TMP_Text IpText { get; private set; }
        public TMP_Text NameText { get; private set; }
        public Button IpBoxButton { get; private set; }
        public Button NameBoxButton { get; private set; }
        public TMP_Text MenuStatus { get; private set; }
        public Button HostButton { get; private set; }
        public Button JoinButton { get; private set; }
        public Button TrainButton { get; private set; }

        // Lobby
        GameObject lobbyPanel;
        GameObject lobbyPlayersPanel;
        RectTransform lobbyRight;
        RectTransform textoPanel;
        Image choiceBox;
        RectTransform startRect;
        RectTransform leaveRect;
        public TMP_Text LobbyInfo { get; private set; }
        public TMP_Text LobbyIp { get; private set; }
        public TMP_Text LobbyPlayers { get; private set; }
        public Button StartButton { get; private set; }
        public Button LeaveButton { get; private set; }
        TMP_Text lobbyTitle;
        TMP_Text lobbyChoice;
        public Button GenerateButton { get; private set; }
        public Button UploadButton { get; private set; }
        public Button[] SuggestionButtons { get; private set; }
        TMP_Text[] suggestionLabels;
        GameObject[] rosterRoots;
        TMP_Text[] rosterLabels;
        GameObject[] rosterKicks;
        Action<int> rosterKick;
        int suggestionCount;
        bool choiceVisible;
        bool lobbySolo;

        // Partida
        GameObject gamePanel;
        RectTransform trainingPanel;
        RectTransform playersPanel;
        RectTransform effectsPanel;
        RectTransform rowsRoot;
        PlayerRow ownRow;
        readonly Dictionary<int, PlayerRow> opponentRows = new Dictionary<int, PlayerRow>();
        public RectTransform TextPanel { get; private set; }
        public TypingView Typing { get; private set; }
        public PowerPromptView Prompt { get; private set; }
        TMP_Text glitchBanner;
        TMP_Text trainingTimer;
        TMP_Text toast;
        TMP_Text countdown;
        Image flash;
        PowerFeedback powerFeedback;
        public Image BombImage { get; private set; }
        public Image ExplosionImage { get; private set; }
        RectTransform slotsRoot;
        TMP_Text slotsHint;
        TMP_Text slotsMore;
        readonly RectTransform[] slotCells = new RectTransform[MaxSlots];
        readonly Image[] slotImages = new Image[MaxSlots];
        readonly Outline[] slotOutlines = new Outline[MaxSlots];
        readonly Image[] slotIcons = new Image[MaxSlots];
        readonly TMP_Text[] slotLabels = new TMP_Text[MaxSlots];
        float playersPanelH = 100f;
        int slotRows = 1;
        float toastTimer;
        float flashAlpha;

        /// <summary>Icone de cada efeito (opcional). Enquanto devolver null, o slot mostra so o quadrado colorido de placeholder.</summary>
        public Func<PowerType, Sprite> PowerIconProvider { get; set; }

        // Confirmacao de abandono, desenhada acima de todas as telas.
        GameObject leaveConfirmation;
        TMP_Text leaveTitle;
        TMP_Text leaveMessage;
        public Button ContinueButton { get; private set; }
        public Button ConfirmLeaveButton { get; private set; }
        public bool LeaveConfirmationVisible => leaveConfirmation != null && leaveConfirmation.activeSelf;

        // Resultado
        GameObject resultPanel;
        TMP_Text resultTitle;
        TMP_Text resultSub;
        public Button PlayAgainButton { get; private set; }
        TMP_Text resultWaiting;
        public Button ResultMenuButton { get; private set; }
        public Button TrainAgainButton { get; private set; }

        public void Build(Action onHost, Action onJoin, Action onStart, Action onLeave, Action onPlayAgain, Action onTrain, Action onTrainAgain)
        {
            var canvasGo = new GameObject("TypeDuelCanvas", typeof(RectTransform));
            Canvas = canvasGo.AddComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            UnityEngine.Object.DontDestroyOnLoad(canvasGo);

            EnsureEventSystem();

            var root = (RectTransform)canvasGo.transform;
            var bg = UiKit.Box(root, "Background", UiKit.Background);
            UiKit.Stretch(bg.rectTransform);

            BuildMenu(root, onHost, onJoin, onTrain);
            BuildLobby(root, onStart, onLeave);
            BuildGame(root);
            BuildResult(root, onPlayAgain, onLeave, onTrainAgain);
            BuildLeaveConfirmation(root, onLeave);
            ShowMenu();
        }

        static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;

            var go = new GameObject("EventSystem", typeof(EventSystem));
            var module = go.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
            UnityEngine.Object.DontDestroyOnLoad(go);
        }

        // ------------------------------------------------------------ menu

        void BuildMenu(RectTransform root, Action onHost, Action onJoin, Action onTrain)
        {
            var panel = UiKit.NewRect("MenuPanel", root);
            UiKit.Stretch(panel);
            menuPanel = panel.gameObject;

            var title = UiKit.Label(panel, "Title", "TYPE DUEL", 150f, UiKit.TextMain);
            UiKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -272f), new Vector2(1400f, 180f));

            var nameBox = UiKit.Frame(panel, "NameBox", raycast: true);
            UiKit.Place(nameBox.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 117f), new Vector2(770f, 100f));
            NameBoxButton = nameBox.gameObject.AddComponent<Button>();
            NameBoxButton.targetGraphic = nameBox;
            NameText = UiKit.Label(nameBox.transform, "NameText", "", 42f, UiKit.TextMain);
            UiKit.Stretch(NameText.rectTransform);

            var ipBox = UiKit.Frame(panel, "IpBox", raycast: true);
            UiKit.Place(ipBox.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -12f), new Vector2(770f, 100f));
            IpBoxButton = ipBox.gameObject.AddComponent<Button>();
            IpBoxButton.targetGraphic = ipBox;
            IpText = UiKit.Label(ipBox.transform, "IpText", "", 42f, UiKit.TextMain);
            UiKit.Stretch(IpText.rectTransform);

            JoinButton = UiKit.MakeButton(panel, "JoinButton", "ENTRAR NO JOGO", UiKit.Green, onJoin, new Vector2(370f, 100f));
            UiKit.Place(JoinButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(-201f, -142f), new Vector2(370f, 100f));

            HostButton = UiKit.MakeButton(panel, "HostButton", "HOSPEDAR JOGO", UiKit.Blue, onHost, new Vector2(370f, 100f));
            UiKit.Place(HostButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(201f, -142f), new Vector2(370f, 100f));

            TrainButton = UiKit.MakeButton(panel, "TrainButton", "TREINAMENTO", UiKit.Yellow, onTrain, new Vector2(770f, 100f));
            UiKit.Place(TrainButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0f, -274f), new Vector2(770f, 100f));

            MenuStatus = UiKit.Label(panel, "Status", "", 34f, UiKit.Red);
            UiKit.Place(MenuStatus.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -385f), new Vector2(1400f, 60f));

            var hint = UiKit.Label(panel, "Hint", "Ambos Jogadores no mesmo WIFI/Lan. Digite o IP do Host ou CTRL/CMD+V", 26f, UiKit.TextMain);
            UiKit.Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(1700f, 50f));
        }

        public void SetIpText(string ip, bool showCaret)
        {
            IpText.text = "<b>Host:</b>   " + ip + (showCaret ? "_" : " ");
        }

        /// <summary>Campo de nome opcional; vazio e sem foco mostra "(opcional)".</summary>
        public void SetNameText(string name, bool focused, bool showCaret)
        {
            if (name.Length == 0 && !focused) NameText.text = "<b>Seu nome</b>   (opcional)";
            else NameText.text = "<b>Seu nome</b>   " + name + (focused && showCaret ? "_" : " ");
        }

        // ------------------------------------------------------------ lobby

        void BuildLobby(RectTransform root, Action onStart, Action onLeave)
        {
            var panel = UiKit.NewRect("LobbyPanel", root);
            UiKit.Stretch(panel);
            lobbyPanel = panel.gameObject;

            lobbyTitle = UiKit.Label(panel, "Title", "LOBBY", 150f, UiKit.TextMain);
            UiKit.Place(lobbyTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -208f), new Vector2(1400f, 180f));

            // Coluna esquerda: jogadores
            var players = UiKit.Frame(panel, "PlayersPanel");
            UiKit.PlaceTop(players.rectTransform, -290f, LobbyTop, 690f, 640f);
            lobbyPlayersPanel = players.gameObject;

            LobbyPlayers = UiKit.Label(players.transform, "Players", "Jogadores: 1/2", 56f, UiKit.TextMain, style: FontStyles.Bold);
            UiKit.PlaceTop(LobbyPlayers.rectTransform, 0f, 16f, 650f, 80f);

            LobbyIp = UiKit.Label(players.transform, "Ip", "", 36f, UiKit.Blue);
            UiKit.PlaceTop(LobbyIp.rectTransform, 0f, 96f, 650f, 50f);

            LobbyInfo = UiKit.Label(players.transform, "Info", "", 28f, UiKit.TextDim);
            UiKit.PlaceTop(LobbyInfo.rectTransform, 0f, 142f, 650f, 40f);

            // Lista de jogadores (opcional: so aparece quando SetLobbyRoster e chamado).
            int rosterSize = Math.Max(Config.MaxPlayers, 2);
            rosterRoots = new GameObject[rosterSize];
            rosterLabels = new TMP_Text[rosterSize];
            rosterKicks = new GameObject[rosterSize];
            for (int i = 0; i < rosterSize; i++)
            {
                int index = i;
                var row = UiKit.NewRect("Roster" + i, players.transform);
                UiKit.PlaceTop(row, 0f, 200f + 58f * i, 650f, 54f);

                var icon = UiKit.CaretIcon(row, "Icon", UiKit.PlayerColor(i), new Vector2(22f, 34f));
                UiKit.Place(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(11f, 0f), new Vector2(22f, 34f));

                var label = UiKit.Label(row, "Name", "", 38f, UiKit.TextMain, TextAlignmentOptions.MidlineLeft);
                label.overflowMode = TextOverflowModes.Ellipsis;
                label.rectTransform.anchorMin = Vector2.zero;
                label.rectTransform.anchorMax = Vector2.one;
                label.rectTransform.offsetMin = new Vector2(46f, 0f);
                label.rectTransform.offsetMax = new Vector2(-60f, 0f);

                var kick = MakeKickButton(row, () => rosterKick?.Invoke(index));
                UiKit.Place(kick.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(44f, 44f));

                rosterRoots[i] = row.gameObject;
                rosterLabels[i] = label;
                rosterKicks[i] = kick.gameObject;
                row.gameObject.SetActive(false);
            }

            // Coluna direita: texto, iniciar, abandonar
            lobbyRight = UiKit.NewRect("RightColumn", panel);

            var texto = UiKit.Frame(lobbyRight, "TextoPanel");
            textoPanel = texto.rectTransform;

            var textoTitle = UiKit.Label(texto.transform, "Title", "Texto", 48f, UiKit.TextMain, style: FontStyles.Bold);
            UiKit.PlaceTop(textoTitle.rectTransform, 0f, 12f, LobbyColumnW - 40f, 64f);

            // Origem do texto: Gerar (banco de palavras) ou Enviar (.txt). Os cliques sao ligados no MatchController.
            float half = (LobbyColumnW - 40f - 16f) * 0.5f;
            GenerateButton = UiKit.MakeButton(texto.transform, "GenerateButton", "Gerar", UiKit.PanelLight, () => { }, new Vector2(half, 64f), 28f);
            UiKit.PlaceTop(GenerateButton.GetComponent<RectTransform>(), -(half * 0.5f + 8f), 84f, half, 64f);

            UploadButton = UiKit.MakeButton(texto.transform, "UploadButton", "Enviar", UiKit.PanelLight, () => { }, new Vector2(half, 64f), 28f);
            UiKit.PlaceTop(UploadButton.GetComponent<RectTransform>(), half * 0.5f + 8f, 84f, half, 64f);

            // Escolha atual do texto (aparece so quando ha mensagem).
            choiceBox = UiKit.Frame(texto.transform, "ChoiceBox");
            lobbyChoice = UiKit.Label(choiceBox.transform, "Choice", "", 26f, UiKit.TextDim);
            lobbyChoice.textWrappingMode = TextWrappingModes.Normal;
            UiKit.Stretch(lobbyChoice.rectTransform, 12f);

            // Textos sugeridos pelos jogadores (o host escolhe um).
            SuggestionButtons = new Button[Config.MaxPlayers];
            suggestionLabels = new TMP_Text[Config.MaxPlayers];
            for (int i = 0; i < Config.MaxPlayers; i++)
            {
                var row = UiKit.MakeButton(texto.transform, "Suggestion" + i, "", UiKit.PanelLight, () => { }, new Vector2(LobbyColumnW - 40f, 36f), 22f);
                SuggestionButtons[i] = row;
                suggestionLabels[i] = row.GetComponentInChildren<TMP_Text>();
                suggestionLabels[i].overflowMode = TextOverflowModes.Ellipsis;
                suggestionLabels[i].rectTransform.offsetMin = new Vector2(12f, 0f);
                suggestionLabels[i].rectTransform.offsetMax = new Vector2(-12f, 0f);
                row.gameObject.SetActive(false);
            }

            StartButton = UiKit.MakeButton(lobbyRight, "StartButton", "INICIAR PARTIDA", UiKit.Green, onStart, new Vector2(LobbyColumnW, LobbyButtonH));
            startRect = StartButton.GetComponent<RectTransform>();

            LeaveButton = UiKit.MakeButton(lobbyRight, "LeaveButton", "ABANDONAR", UiKit.Red, onLeave, new Vector2(LobbyColumnW, LobbyButtonH));
            leaveRect = LeaveButton.GetComponent<RectTransform>();

            RelayoutLobby();
        }

        /// <summary>Botao "X" vermelho (duas barras cruzadas) para remover um jogador da sala.</summary>
        static Button MakeKickButton(Transform parent, Action onClick)
        {
            var hit = UiKit.Box(parent, "Kick", new Color(1f, 1f, 1f, 0f), raycast: true);
            hit.rectTransform.sizeDelta = new Vector2(44f, 44f);

            for (int i = 0; i < 2; i++)
            {
                var bar = UiKit.Box(hit.transform, "Bar" + i, UiKit.Red);
                bar.rectTransform.sizeDelta = new Vector2(34f, 3f);
                bar.rectTransform.localRotation = Quaternion.Euler(0f, 0f, i == 0 ? 45f : -45f);
            }

            var button = hit.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => onClick());
            return button;
        }

        /// <summary>
        /// Lista os jogadores da sala (nome com a cor de cada um). Com onKick, os que nao sao o host ganham o botao X.
        /// Opcional: se nunca for chamado, o painel mostra so o contador e o IP.
        /// </summary>
        public void SetLobbyRoster(IList<string> names, int hostIndex = 0, Action<int> onKick = null)
        {
            rosterKick = onKick;
            for (int i = 0; i < rosterRoots.Length; i++)
            {
                bool has = i < names.Count;
                rosterRoots[i].SetActive(has);
                if (!has) continue;

                bool host = i == hostIndex;
                rosterLabels[i].text = host ? names[i] + " (HOST)" : names[i];
                rosterLabels[i].fontStyle = host ? FontStyles.Bold : FontStyles.Normal;
                rosterKicks[i].SetActive(onKick != null && !host);
            }
        }

        /// <summary>Reposiciona a coluna direita: o painel de texto cresce com a mensagem e as sugestoes, e os botoes descem junto.</summary>
        void RelayoutLobby()
        {
            float w = LobbyColumnW - 40f;
            float y = 84f + 64f;

            choiceBox.gameObject.SetActive(choiceVisible);
            if (choiceVisible)
            {
                y += 14f;
                UiKit.PlaceTop(choiceBox.rectTransform, 0f, y, w, 70f);
                y += 70f;
            }

            if (suggestionCount > 0)
            {
                y += 14f;
                for (int i = 0; i < suggestionCount; i++)
                    UiKit.PlaceTop(SuggestionButtons[i].GetComponent<RectTransform>(), 0f, y + 42f * i, w, 36f);
                y += suggestionCount * 42f - 6f;
            }

            y += 20f;
            UiKit.PlaceTop(textoPanel, 0f, 0f, LobbyColumnW, y);

            UiKit.PlaceTop(lobbyRight, lobbySolo ? 0f : LobbyRightX, LobbyTop, LobbyColumnW, y + 28f + LobbyButtonH * 2f + 24f);
            UiKit.PlaceTop(startRect, 0f, y + 28f, LobbyColumnW, LobbyButtonH);
            UiKit.PlaceTop(leaveRect, 0f, y + 28f + LobbyButtonH + 24f, LobbyColumnW, LobbyButtonH);
        }

        /// <summary>Tela de espera do treino (solo) ou da sala em grupo; limpa a escolha de texto anterior.</summary>
        public void SetLobbyMode(bool solo)
        {
            lobbySolo = solo;
            lobbyTitle.text = solo ? "TREINAMENTO" : "LOBBY";
            lobbyPlayersPanel.SetActive(!solo);
            LobbyIp.gameObject.SetActive(!solo);
            LobbyPlayers.gameObject.SetActive(!solo);
            LobbyInfo.gameObject.SetActive(!solo);
            StartButton.GetComponentInChildren<TMP_Text>().text = solo ? "INICIAR TREINO" : "INICIAR PARTIDA";

            SetSuggestions(new List<string>(), -1);
            SetSourceSelection(false, false);
            SetLobbyChoice("", UiKit.TextDim);
        }

        /// <summary>Destaca a origem escolhida (Gerar ou Enviar).</summary>
        public void SetSourceSelection(bool generateSelected, bool uploadSelected)
        {
            UiKit.SetSelected(GenerateButton, generateSelected, UiKit.Purple);
            UiKit.SetSelected(UploadButton, uploadSelected, UiKit.Purple);
        }

        /// <summary>Lista os textos sugeridos; selectedIndex destaca o escolhido (-1 = nenhum).</summary>
        public void SetSuggestions(IList<string> labels, int selectedIndex)
        {
            suggestionCount = Math.Min(labels.Count, SuggestionButtons.Length);
            for (int i = 0; i < SuggestionButtons.Length; i++)
            {
                bool has = i < suggestionCount;
                SuggestionButtons[i].gameObject.SetActive(has);
                if (!has) continue;

                suggestionLabels[i].text = labels[i];
                UiKit.SetSelected(SuggestionButtons[i], i == selectedIndex, UiKit.Purple);
            }

            RelayoutLobby();
        }

        public void SetLobbyChoice(string text, Color color)
        {
            lobbyChoice.text = text;
            lobbyChoice.color = color;
            choiceVisible = !string.IsNullOrEmpty(text);
            RelayoutLobby();
        }

        // ------------------------------------------------------------ partida

        void BuildGame(RectTransform root)
        {
            var panel = UiKit.NewRect("GamePanel", root);
            UiKit.Stretch(panel);
            gamePanel = panel.gameObject;

            // Area do texto: sem moldura. O TypingView mantem a linha do cursor no centro desta area.
            TextPanel = UiKit.NewRect("TextPanel", panel);
            UiKit.Place(TextPanel, new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(1640f, 760f));
            Typing = new TypingView(TextPanel);

            float inner = SidePanelW - PanelPad * 2f;

            // Cronometro funcional apenas no treino
            trainingPanel = UiKit.Frame(panel, "TrainingPanel").rectTransform;
            var matchTitle = UiKit.Label(trainingPanel, "Title", "Treino", 32f, UiKit.TextMain, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            UiKit.PlaceTop(matchTitle.rectTransform, 0f, 16f, inner, 44f);
            trainingTimer = UiKit.Label(trainingPanel, "Timer", "000s", 64f, UiKit.TextMain, TextAlignmentOptions.MidlineLeft);
            UiKit.PlaceTop(trainingTimer.rectTransform, 0f, 62f, inner, 80f);

            trainingPanel.gameObject.SetActive(false);

            // Barras de progresso no topo, alinhadas a area do texto.
            playersPanel = UiKit.NewRect("ProgressBars", panel);
            rowsRoot = UiKit.NewRect("Rows", playersPanel);

            // Efeitos (fila de poderes)
            effectsPanel = UiKit.Frame(panel, "EffectsPanel").rectTransform;
            var effectsTitle = UiKit.Label(effectsPanel, "Title", "Efeitos", 32f, UiKit.TextMain, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            UiKit.PlaceTop(effectsTitle.rectTransform, 0f, 16f, inner, 40f);

            slotsHint = UiKit.Label(effectsPanel, "SlotsHint", "", 20f, UiKit.TextDim, TextAlignmentOptions.TopLeft);
            slotsHint.textWrappingMode = TextWrappingModes.Normal;
            UiKit.PlaceTop(slotsHint.rectTransform, 0f, 56f, inner, 44f);

            slotsMore = UiKit.Label(effectsPanel, "SlotsMore", "", 30f, UiKit.TextDim, TextAlignmentOptions.MidlineRight);
            UiKit.PlaceTop(slotsMore.rectTransform, 0f, 16f, inner, 40f);

            slotsRoot = UiKit.NewRect("Slots", effectsPanel);
            var grid = slotsRoot.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(SlotSize, SlotSize + 4f + SlotLabelH);
            grid.spacing = new Vector2(SlotGap, 8f);
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperLeft;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = SlotColumns;

            for (int i = 0; i < MaxSlots; i++)
            {
                var cell = UiKit.NewRect("Slot" + i, slotsRoot);

                // Quadrado do efeito: colorido quando ocupado, so borda quando vazio. O icone entra por cima (PowerIconProvider).
                var square = UiKit.Box(cell, "Square", UiKit.PanelLight);
                var sr = square.rectTransform;
                sr.anchorMin = new Vector2(0f, 1f);
                sr.anchorMax = new Vector2(1f, 1f);
                sr.pivot = new Vector2(0.5f, 1f);
                sr.anchoredPosition = Vector2.zero;
                sr.sizeDelta = new Vector2(0f, SlotSize);
                var outline = UiKit.AddBorder(square);

                var icon = UiKit.Box(square.transform, "Icon", Color.white);
                UiKit.Stretch(icon.rectTransform, 14f);
                icon.preserveAspect = true;
                icon.enabled = false;

                var label = UiKit.Label(cell, "Label", "", 20f, UiKit.TextMain);
                label.enableAutoSizing = true;
                label.fontSizeMin = 10f;
                label.fontSizeMax = 20f;
                var lr = label.rectTransform;
                lr.anchorMin = new Vector2(0f, 0f);
                lr.anchorMax = new Vector2(1f, 0f);
                lr.pivot = new Vector2(0.5f, 0f);
                lr.anchoredPosition = Vector2.zero;
                lr.sizeDelta = new Vector2(0f, SlotLabelH);

                slotCells[i] = cell;
                slotImages[i] = square;
                slotOutlines[i] = outline;
                slotIcons[i] = icon;
                slotLabels[i] = label;
                cell.gameObject.SetActive(i < SlotColumns);
            }

            slotsHint.text = "Palavras roxas dão efeitos (máx. 3)";
            RefreshEffectsLayout();
            LayoutSidePanels();

            // Posicoes abaixo sao relativas ao centro da area do texto (x = -175).
            glitchBanner = UiKit.Label(panel, "GlitchBanner", "", 40f, PowerInfo.Color(PowerType.Glitch), style: FontStyles.Bold);
            UiKit.Place(glitchBanner.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-175f, 330f), new Vector2(1230f, 60f));

            toast = UiKit.Label(panel, "Toast", "", 46f, UiKit.TextMain, style: FontStyles.Bold);
            UiKit.Place(toast.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-175f, 260f), new Vector2(1230f, 70f));

            countdown = UiKit.Label(panel, "Countdown", "", 260f, UiKit.TextMain, style: FontStyles.Bold);
            UiKit.Place(countdown.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-175f, 0f), new Vector2(1230f, 320f));

            flash = UiKit.Box(panel, "Flash", new Color(1f, 0.3f, 0.2f, 0f));
            UiKit.Stretch(flash.rectTransform);

            BombImage = UiKit.Box(panel, "Bomb", new Color(0.1f, 0.1f, 0.12f));
            BombImage.sprite = UiKit.Circle;
            BombImage.rectTransform.sizeDelta = new Vector2(96f, 96f);
            var fuse = UiKit.Box(BombImage.transform, "Fuse", PowerInfo.Color(PowerType.Bobomb));
            fuse.sprite = UiKit.Circle;
            UiKit.Place(fuse.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(30f, 44f), new Vector2(30f, 30f));
            BombImage.gameObject.SetActive(false);

            ExplosionImage = UiKit.Box(panel, "Explosion", PowerInfo.Color(PowerType.Bobomb));
            ExplosionImage.sprite = UiKit.Circle;
            ExplosionImage.rectTransform.sizeDelta = new Vector2(120f, 120f);
            ExplosionImage.gameObject.SetActive(false);

            powerFeedback = new PowerFeedback(panel);
            Prompt = new PowerPromptView(panel);
        }

        /// <summary>Posiciona o progresso no topo e os efeitos/cronometro de treino na lateral.</summary>
        void LayoutSidePanels()
        {
            float x = 960f - SidePanelRight - SidePanelW * 0.5f;
            float y = SidePanelTop;

            UiKit.PlaceTop(playersPanel, -175f, 50f, ProgressWidth, playersPanelH);
            UiKit.PlaceTop(trainingPanel, x, y, SidePanelW, TrainingPanelH);
            if (trainingPanel.gameObject.activeSelf) y += TrainingPanelH + SidePanelGap;

            UiKit.PlaceTop(effectsPanel, x, y, SidePanelW, EffectsHeaderH + GridHeight() + 16f);
        }

        float GridHeight() => slotRows * (SlotSize + 4f + SlotLabelH) + (slotRows - 1) * 8f;

        void RefreshEffectsLayout()
        {
            UiKit.PlaceTop(slotsRoot, 0f, EffectsHeaderH, SidePanelW - PanelPad * 2f, GridHeight());
        }

        /// <summary>Monta a lista de jogadores: voce e um item por oponente, cada um com sua cor. Sem nome, usa Voce / Oponente / Jogador N.</summary>
        public void SetupBars(string ownName, IList<int> opponentIds, IList<string> opponentNames)
        {
            foreach (Transform child in rowsRoot) UnityEngine.Object.Destroy(child.gameObject);
            opponentRows.Clear();

            float rowW = ProgressWidth;
            int index = 0;

            ownRow = new PlayerRow(rowsRoot, "OwnRow", string.IsNullOrEmpty(ownName) ? "Você" : ownName, UiKit.PlayerColor(index), rowW, PlayerRowH);
            UiKit.PlaceTop(ownRow.Root, 0f, index * PlayerRowH, rowW, PlayerRowH);
            Typing.SetOwnColor(UiKit.PlayerColor(index));
            index++;

            for (int i = 0; i < opponentIds.Count; i++)
            {
                int id = opponentIds[i];
                string name = opponentNames[i];
                string label = !string.IsNullOrEmpty(name) ? name : (opponentIds.Count > 1 ? "Jogador " + id : "Oponente");
                var color = UiKit.PlayerColor(index);

                var row = new PlayerRow(rowsRoot, "Row" + id, label, color, rowW, PlayerRowH);
                UiKit.PlaceTop(row.Root, 0f, index * PlayerRowH, rowW, PlayerRowH);
                opponentRows[id] = row;
                Typing.SetOpponentColor(id, color);
                index++;
            }

            UiKit.PlaceTop(rowsRoot, 0f, 0f, rowW, index * PlayerRowH);
            playersPanelH = index * PlayerRowH;
            LayoutSidePanels();

            ownRow.Set(0f);
            foreach (var row in opponentRows.Values) row.Set(0f);
        }

        public void SetOwnProgress(float value) => ownRow?.Set(value);

        public void SetOpponentProgress(int id, float value)
        {
            if (opponentRows.TryGetValue(id, out var row)) row.Set(value);
        }

        public void SetSlots(IList<PowerType> queue)
        {
            int shown = Math.Min(queue.Count, MaxSlots);
            slotRows = shown > SlotColumns ? 2 : 1;

            for (int i = 0; i < MaxSlots; i++)
            {
                slotCells[i].gameObject.SetActive(i < slotRows * SlotColumns);

                bool has = i < shown;
                slotOutlines[i].enabled = !has;
                if (!has)
                {
                    slotImages[i].color = UiKit.PanelLight;
                    slotIcons[i].enabled = false;
                    slotLabels[i].text = "";
                    continue;
                }

                var type = queue[i];
                slotImages[i].color = PowerInfo.Color(type);
                slotLabels[i].text = PowerInfo.Name(type);

                var sprite = PowerIconProvider?.Invoke(type);
                slotIcons[i].sprite = sprite;
                slotIcons[i].enabled = sprite != null;
            }

            int extra = queue.Count - MaxSlots;
            slotsMore.text = extra > 0 ? "+" + extra : "";
            slotsHint.text = queue.Count > 0 ? "Ctrl, depois digite o nome do efeito" : "Palavras roxas dão efeitos (máx. 3)";

            RefreshEffectsLayout();
            LayoutSidePanels();
        }

        public void SetCountdown(string text) => countdown.text = text;

        /// <summary>Mostra ou esconde o painel de efeitos (o treino nao usa poderes).</summary>
        public void SetPowersVisible(bool visible)
        {
            effectsPanel.gameObject.SetActive(visible);
        }

        /// <summary>Exibe o cronometro somente no treino; negativo oculta o painel no versus.</summary>
        public void SetTrainingTimer(float seconds)
        {
            bool visible = seconds >= 0f;
            if (trainingPanel.gameObject.activeSelf != visible)
            {
                trainingPanel.gameObject.SetActive(visible);
                LayoutSidePanels();
            }
            if (visible) trainingTimer.text = Mathf.CeilToInt(seconds).ToString("000") + "s";
        }

        public void SetGlitch(float secondsLeft)
        {
            glitchBanner.text = secondsLeft > 0f ? "GLITCH!  " + Mathf.CeilToInt(secondsLeft) + "s" : "";
        }

        public void ShowToast(string text, Color color)
        {
            toast.text = text;
            toast.color = color;
            toastTimer = 1.8f;
        }

        public void PlayPowerFeedback(PowerType power) => powerFeedback.Play(power);

        public void FlashScreen(float strength = 0.4f) => flashAlpha = strength;

        public void Tick(float dt)
        {
            Typing?.Tick(dt);
            powerFeedback?.Tick(dt);

            if (toastTimer > 0f)
            {
                toastTimer -= dt;
                var c = toast.color;
                c.a = Mathf.Clamp01(toastTimer / 0.5f);
                toast.color = c;
                if (toastTimer <= 0f) toast.text = "";
            }

            if (flashAlpha > 0f)
            {
                flashAlpha = Mathf.Max(0f, flashAlpha - dt * 1.6f);
                var c = flash.color;
                c.a = flashAlpha;
                flash.color = c;
            }
        }

        public void ResetEffects()
        {
            powerFeedback.Reset();
            BombImage.gameObject.SetActive(false);
            ExplosionImage.gameObject.SetActive(false);
            TextPanel.anchoredPosition = new Vector2(0f, -10f);
            glitchBanner.text = "";
            toast.text = "";
            toastTimer = 0f;
            flashAlpha = 0f;
            var c = flash.color;
            c.a = 0f;
            flash.color = c;
        }

        // ------------------------------------------------------------ resultado

        void BuildResult(RectTransform root, Action onPlayAgain, Action onMenu, Action onTrainAgain)
        {
            var panel = UiKit.Box(root, "ResultPanel", UiKit.Background, raycast: true);
            UiKit.Stretch(panel.rectTransform);
            resultPanel = panel.gameObject;

            resultTitle = UiKit.Label(panel.transform, "Title", "", 170f, UiKit.Green);
            UiKit.Place(resultTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 150f), new Vector2(1600f, 220f));

            resultSub = UiKit.Label(panel.transform, "Sub", "", 40f, UiKit.TextDim);
            UiKit.Place(resultSub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(1600f, 60f));

            PlayAgainButton = UiKit.MakeButton(panel.transform, "PlayAgain", "JOGAR NOVAMENTE", UiKit.Green, onPlayAgain, new Vector2(480f, 96f));
            UiKit.Place(PlayAgainButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0f, -100f), new Vector2(480f, 96f));

            resultWaiting = UiKit.Label(panel.transform, "Waiting", "Aguardando o host iniciar outra partida...", 36f, UiKit.TextDim);
            UiKit.Place(resultWaiting.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -100f), new Vector2(1600f, 60f));

            TrainAgainButton = UiKit.MakeButton(panel.transform, "TrainAgain", "TREINAR NOVAMENTE", UiKit.Green, onTrainAgain, new Vector2(480f, 96f));
            UiKit.Place(TrainAgainButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0f, -100f), new Vector2(480f, 96f));

            ResultMenuButton = UiKit.MakeButton(panel.transform, "Menu", "MENU PRINCIPAL", UiKit.Red, onMenu, new Vector2(360f, 80f));
            UiKit.Place(ResultMenuButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0f, -230f), new Vector2(360f, 80f));
        }

        void BuildLeaveConfirmation(RectTransform parent, Action onLeave)
        {
            var dim = UiKit.Box(parent, "LeaveConfirmation", new Color(0.04f, 0.05f, 0.08f, 0.88f), raycast: true);
            UiKit.Stretch(dim.rectTransform);
            leaveConfirmation = dim.gameObject;

            var box = UiKit.Frame(dim.transform, "Dialog", raycast: true);
            UiKit.Place(box.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, 400f));
            leaveTitle = UiKit.Label(box.transform, "Title", "", 48f, UiKit.TextMain, style: FontStyles.Bold);
            UiKit.PlaceTop(leaveTitle.rectTransform, 0f, 40f, 920f, 65f);
            leaveMessage = UiKit.Label(box.transform, "Message", "", 28f, UiKit.TextDim);
            leaveMessage.textWrappingMode = TextWrappingModes.Normal;
            UiKit.PlaceTop(leaveMessage.rectTransform, 0f, 118f, 880f, 100f);

            ContinueButton = UiKit.MakeButton(box.transform, "Continue", "CONTINUAR JOGANDO", UiKit.Blue,
                HideLeaveConfirmation, new Vector2(420f, 80f), 28f);
            UiKit.PlaceTop(ContinueButton.GetComponent<RectTransform>(), -230f, 260f, 420f, 80f);
            ConfirmLeaveButton = UiKit.MakeButton(box.transform, "ConfirmLeave", "ABANDONAR PARTIDA", UiKit.Red,
                () => { HideLeaveConfirmation(); onLeave(); }, new Vector2(420f, 80f), 28f);
            UiKit.PlaceTop(ConfirmLeaveButton.GetComponent<RectTransform>(), 230f, 260f, 420f, 80f);

            // Mantem a navegacao por teclado dentro do modal.
            ContinueButton.navigation = new Navigation { mode = Navigation.Mode.Explicit,
                selectOnLeft = ConfirmLeaveButton, selectOnRight = ConfirmLeaveButton };
            ConfirmLeaveButton.navigation = new Navigation { mode = Navigation.Mode.Explicit,
                selectOnLeft = ContinueButton, selectOnRight = ContinueButton };
            var hint = UiKit.Label(box.transform, "Hint", "Esc para continuar", 22f, UiKit.TextDim);
            UiKit.PlaceTop(hint.rectTransform, 0f, 354f, 880f, 30f);
            leaveConfirmation.SetActive(false);
        }

        public void ShowLeaveConfirmation(bool lobby, bool training, bool host)
        {
            leaveTitle.text = lobby ? "SAIR DO LOBBY?" : training ? "ABANDONAR O TREINO?" : "ABANDONAR A PARTIDA?";
            leaveMessage.text = lobby
                ? (host ? "Ao sair, a sala será encerrada para todos." : "Você voltará ao menu principal.")
                : (host ? "Ao abandonar, a partida será encerrada para todos.\n" : "Seu progresso nesta partida será perdido.\n")
                    + "A partida continua enquanto esta janela estiver aberta.";
            ContinueButton.GetComponentInChildren<TMP_Text>().text = lobby ? "CONTINUAR NO LOBBY" : "CONTINUAR JOGANDO";
            ConfirmLeaveButton.GetComponentInChildren<TMP_Text>().text = lobby ? "SAIR DO LOBBY" : training ? "ABANDONAR TREINO" : "ABANDONAR PARTIDA";
            leaveConfirmation.SetActive(true);
            leaveConfirmation.transform.SetAsLastSibling();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(ContinueButton.gameObject);
        }

        public void HideLeaveConfirmation()
        {
            if (!LeaveConfirmationVisible) return;
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null
                && EventSystem.current.currentSelectedGameObject.transform.IsChildOf(leaveConfirmation.transform))
                EventSystem.current.SetSelectedGameObject(null);
            leaveConfirmation.SetActive(false);
        }

        // ------------------------------------------------------------ navegacao

        void Only(GameObject active)
        {
            HideLeaveConfirmation();
            menuPanel.SetActive(active == menuPanel);
            lobbyPanel.SetActive(active == lobbyPanel);
            gamePanel.SetActive(active == gamePanel);
            resultPanel.SetActive(false);
        }

        public void ShowMenu() => Only(menuPanel);
        public void ShowLobby() => Only(lobbyPanel);
        public void ShowGame() => Only(gamePanel);

        public void ShowResult(bool won, string subtitle, bool isHost)
        {
            HideLeaveConfirmation();
            resultPanel.SetActive(true);
            resultTitle.text = won ? "VOCÊ VENCEU!" : "VOCÊ PERDEU";
            resultTitle.color = won ? UiKit.Green : UiKit.Red;
            resultSub.text = subtitle;
            PlayAgainButton.gameObject.SetActive(isHost);
            resultWaiting.gameObject.SetActive(!isHost);
            TrainAgainButton.gameObject.SetActive(false);
        }

        public void ShowTrainingResult(float wpm, float accuracy)
        {
            HideLeaveConfirmation();
            resultPanel.SetActive(true);
            resultTitle.text = "TEMPO!";
            resultTitle.color = UiKit.Blue;
            resultSub.text = Mathf.RoundToInt(wpm) + " PPM   |   " + Mathf.RoundToInt(accuracy * 100f) + "% de precisão";
            PlayAgainButton.gameObject.SetActive(false);
            resultWaiting.gameObject.SetActive(false);
            TrainAgainButton.gameObject.SetActive(true);
        }

        public void HideResult() => resultPanel.SetActive(false);
    }
}
