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
        const int MaxSlots = 6;

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

        // Partida
        GameObject gamePanel;
        RectTransform barsRoot;
        ProgressBar ownBar;
        readonly Dictionary<int, ProgressBar> opponentBars = new Dictionary<int, ProgressBar>();
        public RectTransform TextPanel { get; private set; }
        public TypingView Typing { get; private set; }
        public PowerPromptView Prompt { get; private set; }
        TMP_Text glitchBanner;
        TMP_Text trainingTimer;
        TMP_Text toast;
        TMP_Text countdown;
        Image flash;
        public Image BombImage { get; private set; }
        public Image ExplosionImage { get; private set; }
        RectTransform slotsRoot;
        TMP_Text slotsHint;
        TMP_Text slotsMore;
        readonly Image[] slotImages = new Image[MaxSlots];
        readonly TMP_Text[] slotLabels = new TMP_Text[MaxSlots];
        float toastTimer;
        float flashAlpha;

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

            var title = UiKit.Label(panel, "Title", "TYPE DUEL", 150f, UiKit.TextMain, style: FontStyles.Bold);
            UiKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -190f), new Vector2(1400f, 180f));

            var sub = UiKit.Label(panel, "Subtitle", "Type 100 words before your opponent does", 38f, UiKit.TextDim);
            UiKit.Place(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -310f), new Vector2(1400f, 60f));

            HostButton = UiKit.MakeButton(panel, "HostButton", "HOST GAME", UiKit.Accent, onHost, new Vector2(310f, 96f));
            UiKit.Place(HostButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(-165f, -170f), new Vector2(310f, 96f));

            var nameBox = UiKit.Box(panel, "NameBox", UiKit.Panel, raycast: true);
            UiKit.Place(nameBox.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 110f), new Vector2(640f, 76f));
            NameBoxButton = nameBox.gameObject.AddComponent<Button>();
            NameBoxButton.targetGraphic = nameBox;
            NameText = UiKit.Label(nameBox.transform, "NameText", "", 36f, UiKit.TextMain);
            UiKit.Stretch(NameText.rectTransform);

            var ipBox = UiKit.Box(panel, "IpBox", UiKit.Panel, raycast: true);
            UiKit.Place(ipBox.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), new Vector2(640f, 90f));
            IpBoxButton = ipBox.gameObject.AddComponent<Button>();
            IpBoxButton.targetGraphic = ipBox;
            IpText = UiKit.Label(ipBox.transform, "IpText", "", 40f, UiKit.TextMain);
            UiKit.Stretch(IpText.rectTransform);

            JoinButton = UiKit.MakeButton(panel, "JoinButton", "JOIN GAME", UiKit.Good, onJoin, new Vector2(310f, 96f));
            UiKit.Place(JoinButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(165f, -170f), new Vector2(310f, 96f));

            TrainButton = UiKit.MakeButton(panel, "TrainButton", "TRAINING", UiKit.PanelLight, onTrain, new Vector2(640f, 96f));
            UiKit.Place(TrainButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0f, -300f), new Vector2(640f, 96f));

            MenuStatus = UiKit.Label(panel, "Status", "", 34f, UiKit.Bad);
            UiKit.Place(MenuStatus.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -410f), new Vector2(1400f, 60f));

            var hint = UiKit.Label(panel, "Hint", "Both players on the same Wi-Fi / LAN. Type the host's IP above (Ctrl/Cmd+V pastes).", 28f, UiKit.TextDim);
            UiKit.Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(1700f, 50f));
        }

        public void SetIpText(string ip, bool showCaret)
        {
            IpText.text = "Host IP:  " + ip + (showCaret ? "_" : " ");
        }

        /// <summary>Campo de nome opcional; vazio e sem foco mostra "(optional)".</summary>
        public void SetNameText(string name, bool focused, bool showCaret)
        {
            if (name.Length == 0 && !focused) NameText.text = "Your name:  <color=#8A8F98>(optional)</color>";
            else NameText.text = "Your name:  " + name + (focused && showCaret ? "_" : " ");
        }

        // ------------------------------------------------------------ lobby

        void BuildLobby(RectTransform root, Action onStart, Action onLeave)
        {
            var panel = UiKit.NewRect("LobbyPanel", root);
            UiKit.Stretch(panel);
            lobbyPanel = panel.gameObject;

            lobbyTitle = UiKit.Label(panel, "Title", "LOBBY", 120f, UiKit.TextMain, style: FontStyles.Bold);
            UiKit.Place(lobbyTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -190f), new Vector2(1400f, 150f));

            LobbyIp = UiKit.Label(panel, "Ip", "", 44f, UiKit.Accent);
            UiKit.Place(LobbyIp.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 235f), new Vector2(1600f, 70f));

            LobbyPlayers = UiKit.Label(panel, "Players", "Players: 1/2", 54f, UiKit.TextMain, style: FontStyles.Bold);
            UiKit.Place(LobbyPlayers.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 155f), new Vector2(1400f, 80f));

            LobbyInfo = UiKit.Label(panel, "Info", "", 36f, UiKit.TextDim);
            UiKit.Place(LobbyInfo.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 85f), new Vector2(1600f, 60f));

            // Origem do texto: GENERATE (banco de palavras) ou UPLOAD TEXT (.txt). Os cliques sao ligados no MatchController.
            GenerateButton = UiKit.MakeButton(panel, "GenerateButton", "GENERATE", UiKit.PanelLight, () => { }, new Vector2(460f, 90f));
            UiKit.Place(GenerateButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(-250f, -10f), new Vector2(460f, 90f));

            UploadButton = UiKit.MakeButton(panel, "UploadButton", "UPLOAD TEXT", UiKit.PanelLight, () => { }, new Vector2(460f, 90f));
            UiKit.Place(UploadButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(250f, -10f), new Vector2(460f, 90f));

            // Textos sugeridos pelos jogadores (o host escolhe um).
            SuggestionButtons = new Button[Config.MaxPlayers];
            suggestionLabels = new TMP_Text[Config.MaxPlayers];
            for (int i = 0; i < Config.MaxPlayers; i++)
            {
                var row = UiKit.MakeButton(panel, "Suggestion" + i, "", UiKit.PanelLight, () => { }, new Vector2(960f, 60f));
                UiKit.Place(row.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0f, -100f - 70f * i), new Vector2(960f, 60f));
                SuggestionButtons[i] = row;
                suggestionLabels[i] = row.GetComponentInChildren<TMP_Text>();
                row.gameObject.SetActive(false);
            }

            lobbyChoice = UiKit.Label(panel, "Choice", "", 36f, UiKit.TextDim);
            UiKit.Place(lobbyChoice.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -235f), new Vector2(1600f, 60f));

            StartButton = UiKit.MakeButton(panel, "StartButton", "START MATCH", UiKit.Good, onStart, new Vector2(480f, 96f));
            UiKit.Place(StartButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0f, -330f), new Vector2(480f, 96f));

            LeaveButton = UiKit.MakeButton(panel, "LeaveButton", "LEAVE", UiKit.PanelLight, onLeave, new Vector2(320f, 80f));
            UiKit.Place(LeaveButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0f, -440f), new Vector2(320f, 80f));
        }

        /// <summary>Tela de espera do treino (solo) ou da sala em grupo; limpa a escolha de texto anterior.</summary>
        public void SetLobbyMode(bool solo)
        {
            lobbyTitle.text = solo ? "TRAINING" : "LOBBY";
            LobbyIp.gameObject.SetActive(!solo);
            LobbyPlayers.gameObject.SetActive(!solo);
            LobbyInfo.gameObject.SetActive(!solo);
            StartButton.GetComponentInChildren<TMP_Text>().text = solo ? "START TRAINING" : "START MATCH";

            SetSuggestions(new List<string>(), -1);
            SetSourceSelection(false, false);
            SetLobbyChoice("", UiKit.TextDim);
        }

        /// <summary>Destaca a origem escolhida (GENERATE ou UPLOAD TEXT).</summary>
        public void SetSourceSelection(bool generateSelected, bool uploadSelected)
        {
            GenerateButton.targetGraphic.color = generateSelected ? UiKit.Accent : UiKit.PanelLight;
            UploadButton.targetGraphic.color = uploadSelected ? UiKit.Accent : UiKit.PanelLight;
        }

        /// <summary>Lista os textos sugeridos; selectedIndex destaca o escolhido (-1 = nenhum).</summary>
        public void SetSuggestions(IList<string> labels, int selectedIndex)
        {
            for (int i = 0; i < SuggestionButtons.Length; i++)
            {
                bool has = i < labels.Count;
                SuggestionButtons[i].gameObject.SetActive(has);
                if (!has) continue;

                suggestionLabels[i].text = labels[i];
                SuggestionButtons[i].targetGraphic.color = i == selectedIndex ? UiKit.Accent : UiKit.PanelLight;
            }
        }

        public void SetLobbyChoice(string text, Color color)
        {
            lobbyChoice.text = text;
            lobbyChoice.color = color;
        }

        // ------------------------------------------------------------ partida

        void BuildGame(RectTransform root)
        {
            var panel = UiKit.NewRect("GamePanel", root);
            UiKit.Stretch(panel);
            gamePanel = panel.gameObject;

            barsRoot = UiKit.NewRect("Bars", panel);
            UiKit.Place(barsRoot, new Vector2(0.5f, 1f), new Vector2(0f, -90f), new Vector2(1500f, 160f));

            var textBg = UiKit.Box(panel, "TextPanel", UiKit.Panel);
            TextPanel = textBg.rectTransform;
            UiKit.Place(TextPanel, new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(1640f, 640f));
            Typing = new TypingView(TextPanel);

            trainingTimer = UiKit.Label(panel, "TrainingTimer", "", 54f, UiKit.TextMain, style: FontStyles.Bold);
            UiKit.Place(trainingTimer.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 470f), new Vector2(800f, 80f));

            glitchBanner = UiKit.Label(panel, "GlitchBanner", "", 40f, PowerInfo.Color(PowerType.Glitch), style: FontStyles.Bold);
            UiKit.Place(glitchBanner.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 330f), new Vector2(1400f, 60f));

            toast = UiKit.Label(panel, "Toast", "", 46f, UiKit.TextMain, style: FontStyles.Bold);
            UiKit.Place(toast.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 395f), new Vector2(1400f, 70f));

            slotsHint = UiKit.Label(panel, "SlotsHint", "POWERS  (Ctrl, then type the name)", 28f, UiKit.TextDim);
            UiKit.Place(slotsHint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 165f), new Vector2(1000f, 40f));

            slotsRoot = UiKit.NewRect("Slots", panel);
            UiKit.Place(slotsRoot, new Vector2(0.5f, 0f), new Vector2(0f, 75f), new Vector2(1200f, 140f));
            var layout = slotsRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 20f;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            for (int i = 0; i < MaxSlots; i++)
            {
                var img = UiKit.Box(slotsRoot, "Slot" + i, Color.white);
                img.rectTransform.sizeDelta = new Vector2(120f, 120f);
                var label = UiKit.Label(img.transform, "Label", "", 26f, Color.black, style: FontStyles.Bold);
                UiKit.Stretch(label.rectTransform);
                slotImages[i] = img;
                slotLabels[i] = label;
            }

            slotsMore = UiKit.Label(panel, "SlotsMore", "", 36f, UiKit.TextDim);
            UiKit.Place(slotsMore.rectTransform, new Vector2(0.5f, 0f), new Vector2(560f, 75f), new Vector2(200f, 60f));

            countdown = UiKit.Label(panel, "Countdown", "", 260f, UiKit.TextMain, style: FontStyles.Bold);
            UiKit.Place(countdown.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(1400f, 320f));

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

            Prompt = new PowerPromptView(panel);
        }

        /// <summary>Cria as barras de progresso: a sua e uma por oponente. Sem nome, usa YOU / OPPONENT.</summary>
        public void SetupBars(string ownName, IList<int> opponentIds, IList<string> opponentNames)
        {
            foreach (Transform child in barsRoot) UnityEngine.Object.Destroy(child.gameObject);
            opponentBars.Clear();

            const float width = 900f;
            const float height = 40f;
            float y = 0f;

            ownBar = new ProgressBar(barsRoot, "OwnBar", string.IsNullOrEmpty(ownName) ? "YOU" : ownName, UiKit.Good, width, height);
            UiKit.Place(ownBar.Root, new Vector2(0.5f, 1f), new Vector2(0f, y - height / 2f), ownBar.Root.sizeDelta);
            y -= height + 18f;

            for (int i = 0; i < opponentIds.Count; i++)
            {
                int id = opponentIds[i];
                string name = opponentNames[i];
                string label = !string.IsNullOrEmpty(name) ? name : (opponentIds.Count > 1 ? "PLAYER " + id : "OPPONENT");
                var bar = new ProgressBar(barsRoot, "Bar" + id, label, UiKit.Bad, width, height);
                UiKit.Place(bar.Root, new Vector2(0.5f, 1f), new Vector2(0f, y - height / 2f), bar.Root.sizeDelta);
                opponentBars[id] = bar;
                y -= height + 18f;
            }

            ownBar.Set(0f);
        }

        public void SetOwnProgress(float value) => ownBar?.Set(value);

        public void SetOpponentProgress(int id, float value)
        {
            if (opponentBars.TryGetValue(id, out var bar)) bar.Set(value);
        }

        public void SetSlots(IList<PowerType> queue)
        {
            for (int i = 0; i < MaxSlots; i++)
            {
                bool has = i < queue.Count;
                slotImages[i].gameObject.SetActive(has);
                if (!has) continue;

                var type = queue[i];
                float size = i == 0 ? 130f : 96f;
                slotImages[i].rectTransform.sizeDelta = new Vector2(size, size);
                slotImages[i].color = PowerInfo.Color(type);
                slotLabels[i].text = PowerInfo.Name(type);
                slotLabels[i].fontSize = i == 0 ? 26f : 20f;
            }

            int extra = queue.Count - MaxSlots;
            slotsMore.text = extra > 0 ? "+" + extra : "";
            slotsHint.text = queue.Count > 0 ? "POWERS  (Ctrl, then type the name)" : "POWERS  (finish words to earn them)";
        }

        public void SetCountdown(string text) => countdown.text = text;

        /// <summary>Mostra ou esconde a dica e os slots de poderes (o treino nao usa poderes); sem eles o painel desce e cresce.</summary>
        public void SetPowersVisible(bool visible)
        {
            slotsHint.gameObject.SetActive(visible);
            slotsMore.gameObject.SetActive(visible);
            slotsRoot.gameObject.SetActive(visible);

            TextPanel.anchoredPosition = visible ? new Vector2(0f, -10f) : new Vector2(0f, -85f);
            TextPanel.sizeDelta = visible ? new Vector2(1640f, 640f) : new Vector2(1640f, 790f);
        }

        /// <summary>Segundos restantes do treino; negativo esconde o timer.</summary>
        public void SetTrainingTimer(float seconds)
        {
            trainingTimer.text = seconds < 0f ? "" : "TIME  " + Mathf.CeilToInt(seconds);
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

        public void FlashScreen(float strength = 0.4f) => flashAlpha = strength;

        public void Tick(float dt)
        {
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
            var panel = UiKit.Box(root, "ResultPanel", new Color(0.05f, 0.06f, 0.08f, 0.97f), raycast: true);
            UiKit.Stretch(panel.rectTransform);
            resultPanel = panel.gameObject;

            resultTitle = UiKit.Label(panel.transform, "Title", "", 170f, UiKit.Good, style: FontStyles.Bold);
            UiKit.Place(resultTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 150f), new Vector2(1600f, 220f));

            resultSub = UiKit.Label(panel.transform, "Sub", "", 40f, UiKit.TextDim);
            UiKit.Place(resultSub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(1600f, 60f));

            PlayAgainButton = UiKit.MakeButton(panel.transform, "PlayAgain", "PLAY AGAIN", UiKit.Good, onPlayAgain, new Vector2(480f, 96f));
            UiKit.Place(PlayAgainButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0f, -100f), new Vector2(480f, 96f));

            resultWaiting = UiKit.Label(panel.transform, "Waiting", "Waiting for the host to start another match...", 36f, UiKit.TextDim);
            UiKit.Place(resultWaiting.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -100f), new Vector2(1600f, 60f));

            TrainAgainButton = UiKit.MakeButton(panel.transform, "TrainAgain", "TRAIN AGAIN", UiKit.Good, onTrainAgain, new Vector2(480f, 96f));
            UiKit.Place(TrainAgainButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0f, -100f), new Vector2(480f, 96f));

            ResultMenuButton = UiKit.MakeButton(panel.transform, "Menu", "MAIN MENU", UiKit.PanelLight, onMenu, new Vector2(360f, 80f));
            UiKit.Place(ResultMenuButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0f, -230f), new Vector2(360f, 80f));
        }

        // ------------------------------------------------------------ navegacao

        void Only(GameObject active)
        {
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
            resultPanel.SetActive(true);
            resultTitle.text = won ? "YOU WIN!" : "YOU LOSE";
            resultTitle.color = won ? UiKit.Good : UiKit.Bad;
            resultSub.text = subtitle;
            PlayAgainButton.gameObject.SetActive(isHost);
            resultWaiting.gameObject.SetActive(!isHost);
            TrainAgainButton.gameObject.SetActive(false);
        }

        public void ShowTrainingResult(float wpm, float accuracy)
        {
            resultPanel.SetActive(true);
            resultTitle.text = "TIME!";
            resultTitle.color = UiKit.Accent;
            resultSub.text = Mathf.RoundToInt(wpm) + " WPM   |   " + Mathf.RoundToInt(accuracy * 100f) + "% accuracy";
            PlayAgainButton.gameObject.SetActive(false);
            resultWaiting.gameObject.SetActive(false);
            TrainAgainButton.gameObject.SetActive(true);
        }

        public void HideResult() => resultPanel.SetActive(false);
    }
}
