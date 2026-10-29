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
        public TMP_Text MenuStatus { get; private set; }
        public Button HostButton { get; private set; }
        public Button JoinButton { get; private set; }

        // Lobby
        GameObject lobbyPanel;
        public TMP_Text LobbyInfo { get; private set; }
        public TMP_Text LobbyIp { get; private set; }
        public TMP_Text LobbyPlayers { get; private set; }
        public Button StartButton { get; private set; }
        public Button LeaveButton { get; private set; }

        // Partida
        GameObject gamePanel;
        RectTransform barsRoot;
        ProgressBar ownBar;
        readonly Dictionary<int, ProgressBar> opponentBars = new Dictionary<int, ProgressBar>();
        public RectTransform TextPanel { get; private set; }
        public TypingView Typing { get; private set; }
        TMP_Text glitchBanner;
        TMP_Text toast;
        TMP_Text countdown;
        Image flash;
        public Image BombImage { get; private set; }
        public Image ExplosionImage { get; private set; }
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

        public void Build(Action onHost, Action onJoin, Action onStart, Action onLeave, Action onPlayAgain)
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

            BuildMenu(root, onHost, onJoin);
            BuildLobby(root, onStart, onLeave);
            BuildGame(root);
            BuildResult(root, onPlayAgain, onLeave);
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

        void BuildMenu(RectTransform root, Action onHost, Action onJoin)
        {
            var panel = UiKit.NewRect("MenuPanel", root);
            UiKit.Stretch(panel);
            menuPanel = panel.gameObject;

            var title = UiKit.Label(panel, "Title", "TYPE DUEL", 150f, UiKit.TextMain, style: FontStyles.Bold);
            UiKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -190f), new Vector2(1400f, 180f));

            var sub = UiKit.Label(panel, "Subtitle", "Type 100 words before your opponent does", 38f, UiKit.TextDim);
            UiKit.Place(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -310f), new Vector2(1400f, 60f));

            HostButton = UiKit.MakeButton(panel, "HostButton", "HOST GAME", UiKit.Accent, onHost, new Vector2(480f, 96f));
            UiKit.Place(HostButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(480f, 96f));

            var ipBox = UiKit.Box(panel, "IpBox", UiKit.Panel);
            UiKit.Place(ipBox.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -90f), new Vector2(640f, 90f));
            IpText = UiKit.Label(ipBox.transform, "IpText", "", 40f, UiKit.TextMain);
            UiKit.Stretch(IpText.rectTransform);

            JoinButton = UiKit.MakeButton(panel, "JoinButton", "JOIN GAME", UiKit.Good, onJoin, new Vector2(480f, 96f));
            UiKit.Place(JoinButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0f, -210f), new Vector2(480f, 96f));

            MenuStatus = UiKit.Label(panel, "Status", "", 34f, UiKit.Bad);
            UiKit.Place(MenuStatus.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -330f), new Vector2(1400f, 60f));

            var hint = UiKit.Label(panel, "Hint", "Both players on the same Wi-Fi / LAN. Type the host's IP above (Ctrl/Cmd+V pastes).", 28f, UiKit.TextDim);
            UiKit.Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(1700f, 50f));
        }

        public void SetIpText(string ip, bool showCaret)
        {
            IpText.text = "Host IP:  " + ip + (showCaret ? "_" : " ");
        }

        // ------------------------------------------------------------ lobby

        void BuildLobby(RectTransform root, Action onStart, Action onLeave)
        {
            var panel = UiKit.NewRect("LobbyPanel", root);
            UiKit.Stretch(panel);
            lobbyPanel = panel.gameObject;

            var title = UiKit.Label(panel, "Title", "LOBBY", 120f, UiKit.TextMain, style: FontStyles.Bold);
            UiKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -190f), new Vector2(1400f, 150f));

            LobbyIp = UiKit.Label(panel, "Ip", "", 44f, UiKit.Accent);
            UiKit.Place(LobbyIp.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 150f), new Vector2(1600f, 70f));

            LobbyPlayers = UiKit.Label(panel, "Players", "Players: 1/2", 54f, UiKit.TextMain, style: FontStyles.Bold);
            UiKit.Place(LobbyPlayers.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(1400f, 80f));

            LobbyInfo = UiKit.Label(panel, "Info", "", 36f, UiKit.TextDim);
            UiKit.Place(LobbyInfo.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(1600f, 60f));

            StartButton = UiKit.MakeButton(panel, "StartButton", "START MATCH", UiKit.Good, onStart, new Vector2(480f, 96f));
            UiKit.Place(StartButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0f, -150f), new Vector2(480f, 96f));

            LeaveButton = UiKit.MakeButton(panel, "LeaveButton", "LEAVE", UiKit.PanelLight, onLeave, new Vector2(320f, 80f));
            UiKit.Place(LeaveButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0f, -270f), new Vector2(320f, 80f));
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
            UiKit.Place(TextPanel, new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), new Vector2(1640f, 640f));
            Typing = new TypingView(TextPanel);

            glitchBanner = UiKit.Label(panel, "GlitchBanner", "", 40f, PowerInfo.Color(PowerType.Glitch), style: FontStyles.Bold);
            UiKit.Place(glitchBanner.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 330f), new Vector2(1400f, 60f));

            toast = UiKit.Label(panel, "Toast", "", 46f, UiKit.TextMain, style: FontStyles.Bold);
            UiKit.Place(toast.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 395f), new Vector2(1400f, 70f));

            slotsHint = UiKit.Label(panel, "SlotsHint", "POWERS  (left click to use)", 28f, UiKit.TextDim);
            UiKit.Place(slotsHint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 190f), new Vector2(1000f, 40f));

            var slotsRoot = UiKit.NewRect("Slots", panel);
            UiKit.Place(slotsRoot, new Vector2(0.5f, 0f), new Vector2(0f, 90f), new Vector2(1200f, 140f));
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
            UiKit.Place(slotsMore.rectTransform, new Vector2(0.5f, 0f), new Vector2(560f, 90f), new Vector2(200f, 60f));

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
        }

        /// <summary>Cria as barras de progresso: a sua e uma por oponente.</summary>
        public void SetupBars(IList<int> opponentIds)
        {
            foreach (Transform child in barsRoot) UnityEngine.Object.Destroy(child.gameObject);
            opponentBars.Clear();

            const float width = 900f;
            const float height = 40f;
            float y = 0f;

            ownBar = new ProgressBar(barsRoot, "OwnBar", "YOU", UiKit.Good, width, height);
            UiKit.Place(ownBar.Root, new Vector2(0.5f, 1f), new Vector2(0f, y - height / 2f), ownBar.Root.sizeDelta);
            y -= height + 18f;

            foreach (var id in opponentIds)
            {
                var bar = new ProgressBar(barsRoot, "Bar" + id, opponentIds.Count > 1 ? "PLAYER " + id : "OPPONENT", UiKit.Bad, width, height);
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
            slotsHint.text = queue.Count > 0 ? "POWERS  (left click uses the first one)" : "POWERS  (finish words to earn them)";
        }

        public void SetCountdown(string text) => countdown.text = text;

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
            TextPanel.anchoredPosition = new Vector2(0f, -30f);
            glitchBanner.text = "";
            toast.text = "";
            toastTimer = 0f;
            flashAlpha = 0f;
            var c = flash.color;
            c.a = 0f;
            flash.color = c;
        }

        // ------------------------------------------------------------ resultado

        void BuildResult(RectTransform root, Action onPlayAgain, Action onMenu)
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
        }

        public void HideResult() => resultPanel.SetActive(false);
    }
}
