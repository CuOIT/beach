using System;
using System.Text;
using Chess.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Chess.UI
{
    /// <summary>
    /// Owns every screen in the game and the transitions between them. The whole interface is
    /// constructed once in <see cref="Build"/>; afterwards screens are only shown and hidden.
    /// </summary>
    public sealed partial class UIManager : MonoBehaviour
    {
        public event Action PlayRequested;
        public event Action SettingsOpened;
        public event Action SettingsClosed;
        public event Action QuitRequested;
        public event Action PauseRequested;
        public event Action ResumeRequested;
        public event Action RestartRequested;
        public event Action MainMenuRequested;
        public event Action UndoRequested;
        public event Action<PieceType> PromotionChosen;

        /// <summary>Raised when a setting changed that the rest of the game must react to.</summary>
        public event Action SettingsApplied;

        private Canvas _canvas;
        private RectTransform _canvasRect;
        private RectTransform _root;

        private float _topHudExtent = 300f;
        private float _bottomHudExtent = 280f;

        private GameObject _mainMenu;
        private GameObject _hud;
        private GameObject _pause;
        private GameObject _settings;
        private GameObject _promotion;
        private GameObject _gameOver;
        private GameObject _moveList;

        private TextMeshProUGUI _statusLabel;
        private TextMeshProUGUI _opponentLabel;
        private TextMeshProUGUI _opponentCaptures;
        private TextMeshProUGUI _playerLabel;
        private TextMeshProUGUI _playerCaptures;
        private TextMeshProUGUI _moveListText;
        private TextMeshProUGUI _gameOverTitle;
        private TextMeshProUGUI _gameOverDetail;
        private TextMeshProUGUI _menuFooter;
        private TextMeshProUGUI _promotionTitle;

        private Button _undoButton;
        private ScrollRect _moveScroll;

        private string _statusText = string.Empty;
        private Color _statusColor = UIPalette.Accent;
        private string _thinkingText = "Thinking...";
        private bool _thinking;

        private bool _settingsCameFromGame;

        public bool IsModalOpen =>
            (_pause != null && _pause.activeSelf) ||
            (_settings != null && _settings.activeSelf) ||
            (_promotion != null && _promotion.activeSelf) ||
            (_gameOver != null && _gameOver.activeSelf) ||
            (_moveList != null && _moveList.activeSelf);

        public bool IsPromotionOpen => _promotion != null && _promotion.activeSelf;

        public void Build()
        {
            BuildCanvas();
            BuildMainMenu();
            BuildHud();
            BuildMoveList();
            BuildPause();
            BuildSettings();
            BuildPromotion();
            BuildGameOver();

            ShowMainMenu();
        }

        private void BuildCanvas()
        {
            var canvasObject = new GameObject("UI", typeof(RectTransform));
            canvasObject.transform.SetParent(transform, false);

            _canvas = canvasObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            // Balanced between width and height so the layout survives landscape too.
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            canvasObject.AddComponent<GraphicRaycaster>();
            _canvasRect = (RectTransform)canvasObject.transform;

            if (EventSystem.current == null)
            {
                var eventSystem = new GameObject("EventSystem", typeof(EventSystem));
                eventSystem.transform.SetParent(transform, false);
                eventSystem.AddComponent<StandaloneInputModule>();
            }

            _root = UIFactory.CreateRect("SafeArea", canvasObject.transform);
            UIFactory.Stretch(_root);
            _root.gameObject.AddComponent<SafeAreaFitter>();
        }

        private GameObject CreateScreen(string name, bool opaqueScrim)
        {
            RectTransform rect = UIFactory.CreateRect(name, _root);
            UIFactory.Stretch(rect);

            var image = rect.gameObject.AddComponent<Image>();
            image.color = opaqueScrim ? UIPalette.Scrim : new Color(0f, 0f, 0f, 0f);
            // A fully transparent scrim must still swallow clicks aimed at the board behind it.
            image.raycastTarget = opaqueScrim;

            rect.gameObject.SetActive(false);
            return rect.gameObject;
        }

        /// <summary>Centred card used by every modal.</summary>
        private VerticalLayoutGroup CreateCard(Transform parent, float width, float topPadding = 48f)
        {
            RectTransform holder = UIFactory.CreateRect("Card", parent);
            holder.anchorMin = new Vector2(0.5f, 0.5f);
            holder.anchorMax = new Vector2(0.5f, 0.5f);
            holder.pivot = new Vector2(0.5f, 0.5f);
            holder.sizeDelta = new Vector2(width, 0f);

            var background = holder.gameObject.AddComponent<Image>();
            background.color = UIPalette.Card;
            background.sprite = UIFactory.Rounded;
            background.type = Image.Type.Sliced;

            // The layout group has to live on the same object as the fitter, otherwise the
            // fitter has no preferred height to read and the card collapses to nothing.
            var layout = holder.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 20f;
            layout.padding = new RectOffset(44, 44, (int)topPadding, 44);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.UpperCenter;

            var fitter = holder.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            return layout;
        }

        // ---------------------------------------------------------------- HUD

        private void BuildHud()
        {
            _hud = CreateScreen("HUD", false);

            RectTransform topBar = UIFactory.CreateRect("TopBar", _hud.transform);
            topBar.anchorMin = new Vector2(0f, 1f);
            topBar.anchorMax = new Vector2(1f, 1f);
            topBar.pivot = new Vector2(0.5f, 1f);
            topBar.sizeDelta = new Vector2(-32f, 132f);
            topBar.anchoredPosition = new Vector2(0f, -16f);

            Image topPanel = UIFactory.Panel(topBar, "Panel", UIPalette.Bar);
            UIFactory.Stretch(topPanel.rectTransform);

            HorizontalLayoutGroup topRow = UIFactory.HorizontalGroup(topBar, "Row", 16f, new RectOffset(28, 28, 16, 16));
            UIFactory.Stretch(topRow.GetComponent<RectTransform>());

            _opponentLabel = UIFactory.Label(topRow.transform, "Computer", 34f, UIPalette.Text, TextAlignmentOptions.Left, FontWeight.SemiBold);
            _opponentLabel.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            _opponentCaptures = UIFactory.Label(topRow.transform, string.Empty, 30f, UIPalette.TextMuted, TextAlignmentOptions.Right);
            _opponentCaptures.gameObject.AddComponent<LayoutElement>().minWidth = 260f;

            // Status sits just under the top bar, where the eye lands between moves.
            RectTransform statusRect = UIFactory.CreateRect("Status", _hud.transform);
            statusRect.anchorMin = new Vector2(0f, 1f);
            statusRect.anchorMax = new Vector2(1f, 1f);
            statusRect.pivot = new Vector2(0.5f, 1f);
            statusRect.sizeDelta = new Vector2(-32f, 70f);
            statusRect.anchoredPosition = new Vector2(0f, -160f);

            _statusLabel = UIFactory.Label(statusRect, "Your turn", 38f, UIPalette.Accent, TextAlignmentOptions.Center, FontWeight.Bold);
            UIFactory.Stretch(_statusLabel.rectTransform);

            _topHudExtent = 160f + 70f + 20f;
            BuildBottomBar();
        }

        private void BuildBottomBar()
        {
            RectTransform bottom = UIFactory.CreateRect("BottomBar", _hud.transform);
            bottom.anchorMin = new Vector2(0f, 0f);
            bottom.anchorMax = new Vector2(1f, 0f);
            bottom.pivot = new Vector2(0.5f, 0f);
            bottom.sizeDelta = new Vector2(-32f, 208f);
            bottom.anchoredPosition = new Vector2(0f, 16f);

            _bottomHudExtent = 208f + 16f + 20f;

            Image panel = UIFactory.Panel(bottom, "Panel", UIPalette.Bar);
            UIFactory.Stretch(panel.rectTransform);

            VerticalLayoutGroup column = UIFactory.VerticalGroup(bottom, "Column", 14f, new RectOffset(24, 24, 18, 18));
            UIFactory.Stretch(column.GetComponent<RectTransform>());

            HorizontalLayoutGroup infoRow = UIFactory.HorizontalGroup(column.transform, "Info", 16f);
            UIFactory.SetHeight(infoRow, 48f);

            _playerLabel = UIFactory.Label(infoRow.transform, "You", 34f, UIPalette.Text, TextAlignmentOptions.Left, FontWeight.SemiBold);
            _playerLabel.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            _playerCaptures = UIFactory.Label(infoRow.transform, string.Empty, 30f, UIPalette.TextMuted, TextAlignmentOptions.Right);
            _playerCaptures.gameObject.AddComponent<LayoutElement>().minWidth = 260f;

            HorizontalLayoutGroup buttonRow = UIFactory.HorizontalGroup(column.transform, "Buttons", 16f);
            UIFactory.SetHeight(buttonRow, 96f);

            UIFactory.ButtonParts menu = UIFactory.CreateButton(buttonRow.transform, "Menu", UIPalette.CardRaised, UIPalette.Text, 96f, 32f);
            menu.Button.onClick.AddListener(() => PauseRequested?.Invoke());

            UIFactory.ButtonParts undo = UIFactory.CreateButton(buttonRow.transform, "Undo", UIPalette.CardRaised, UIPalette.Text, 96f, 32f);
            undo.Button.onClick.AddListener(() => UndoRequested?.Invoke());
            _undoButton = undo.Button;

            UIFactory.ButtonParts moves = UIFactory.CreateButton(buttonRow.transform, "Moves", UIPalette.CardRaised, UIPalette.Text, 96f, 32f);
            moves.Button.onClick.AddListener(ShowMoveList);
        }

        private void BuildMoveList()
        {
            _moveList = CreateScreen("MoveList", true);

            VerticalLayoutGroup card = CreateCard(_moveList.transform, 880f);

            TextMeshProUGUI title = UIFactory.Label(card.transform, "Moves", 48f, UIPalette.Accent, TextAlignmentOptions.Center, FontWeight.Bold);
            UIFactory.SetHeight(title, 64f);

            RectTransform viewport = UIFactory.CreateRect("Viewport", card.transform);
            UIFactory.SetHeight(viewport, 780f);

            var viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0.25f);
            viewportImage.sprite = UIFactory.Rounded;
            viewportImage.type = Image.Type.Sliced;

            // RectMask2D clips without needing the extra material pass a Mask would add.
            viewport.gameObject.AddComponent<RectMask2D>();

            _moveScroll = viewport.gameObject.AddComponent<ScrollRect>();
            _moveScroll.horizontal = false;
            _moveScroll.vertical = true;
            _moveScroll.movementType = ScrollRect.MovementType.Clamped;
            _moveScroll.scrollSensitivity = 40f;

            // The text itself is the scroll content: TMP reports a preferred height, so the
            // fitter grows it as moves are appended and the ScrollRect scrolls it.
            _moveListText = UIFactory.Label(viewport, "No moves yet.", 32f, UIPalette.Text, TextAlignmentOptions.TopLeft);

            RectTransform content = _moveListText.rectTransform;
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = new Vector2(20f, 0f);
            content.offsetMax = new Vector2(-20f, 0f);
            content.anchoredPosition = Vector2.zero;

            var textFitter = content.gameObject.AddComponent<ContentSizeFitter>();
            textFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            textFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _moveScroll.viewport = viewport;
            _moveScroll.content = content;

            UIFactory.ButtonParts close = UIFactory.CreateButton(card.transform, "Close", UIPalette.Accent, UIPalette.TextOnAccent);
            close.Button.onClick.AddListener(() => _moveList.SetActive(false));
        }

        // ---------------------------------------------------------- Promotion

        private void BuildPromotion()
        {
            _promotion = CreateScreen("Promotion", true);

            VerticalLayoutGroup card = CreateCard(_promotion.transform, 880f);

            _promotionTitle = UIFactory.Label(card.transform, "Promote to", 48f, UIPalette.Accent, TextAlignmentOptions.Center, FontWeight.Bold);
            UIFactory.SetHeight(_promotionTitle, 70f);

            TextMeshProUGUI hint = UIFactory.Label(card.transform, "Choose the piece your pawn becomes.", 28f, UIPalette.TextMuted);
            UIFactory.SetHeight(hint, 44f);

            AddPromotionButton(card.transform, "Queen", PieceType.Queen);
            AddPromotionButton(card.transform, "Rook", PieceType.Rook);
            AddPromotionButton(card.transform, "Bishop", PieceType.Bishop);
            AddPromotionButton(card.transform, "Knight", PieceType.Knight);
        }

        private void AddPromotionButton(Transform parent, string text, PieceType type)
        {
            Color background = type == PieceType.Queen ? UIPalette.Accent : UIPalette.CardRaised;
            Color foreground = type == PieceType.Queen ? UIPalette.TextOnAccent : UIPalette.Text;

            UIFactory.ButtonParts parts = UIFactory.CreateButton(parent, text, background, foreground);
            parts.Button.onClick.AddListener(() =>
            {
                _promotion.SetActive(false);
                PromotionChosen?.Invoke(type);
            });
        }

        // ----------------------------------------------------------- Game over

        private void BuildGameOver()
        {
            _gameOver = CreateScreen("GameOver", true);

            VerticalLayoutGroup card = CreateCard(_gameOver.transform, 900f);

            _gameOverTitle = UIFactory.Label(card.transform, "Checkmate", 64f, UIPalette.Accent, TextAlignmentOptions.Center, FontWeight.Bold);
            UIFactory.SetHeight(_gameOverTitle, 92f);

            _gameOverDetail = UIFactory.Label(card.transform, string.Empty, 32f, UIPalette.TextMuted);
            UIFactory.SetHeight(_gameOverDetail, 90f);

            UIFactory.ButtonParts rematch = UIFactory.CreateButton(card.transform, "Play again", UIPalette.Accent, UIPalette.TextOnAccent);
            rematch.Button.onClick.AddListener(() =>
            {
                _gameOver.SetActive(false);
                RestartRequested?.Invoke();
            });

            UIFactory.ButtonParts review = UIFactory.CreateButton(card.transform, "Review moves", UIPalette.CardRaised, UIPalette.Text);
            review.Button.onClick.AddListener(ShowMoveList);

            UIFactory.ButtonParts menu = UIFactory.CreateButton(card.transform, "Main menu", UIPalette.CardRaised, UIPalette.Text);
            menu.Button.onClick.AddListener(() =>
            {
                _gameOver.SetActive(false);
                MainMenuRequested?.Invoke();
            });
        }

        // ------------------------------------------------------------- Showing

        public void ShowMainMenu()
        {
            HideAll();
            _mainMenu.SetActive(true);
            RefreshMenuFooter();
        }

        public void ShowHud()
        {
            HideAll();
            _hud.SetActive(true);
        }

        public void ShowPause()
        {
            _pause.SetActive(true);
        }

        public void HidePause()
        {
            _pause.SetActive(false);
        }

        public void ShowMoveList()
        {
            _moveList.SetActive(true);
            Canvas.ForceUpdateCanvases();
            if (_moveScroll != null) _moveScroll.verticalNormalizedPosition = 0f;
        }

        public void ShowPromotion(PieceColor color)
        {
            _promotionTitle.text = color == PieceColor.White ? "Promote (White)" : "Promote (Black)";
            _promotion.SetActive(true);
        }

        public void ShowGameOver(string title, string detail, Color titleColor)
        {
            _gameOverTitle.text = title;
            _gameOverTitle.color = titleColor;
            _gameOverDetail.text = detail;
            _gameOver.SetActive(true);
        }

        private void HideAll()
        {
            _mainMenu.SetActive(false);
            _hud.SetActive(false);
            _pause.SetActive(false);
            _settings.SetActive(false);
            _promotion.SetActive(false);
            _gameOver.SetActive(false);
            _moveList.SetActive(false);
        }

        // ------------------------------------------------------- HUD updates

        /// <summary>
        /// Share of the screen height covered by the top and bottom HUD, so the camera can frame
        /// the board in what is left. Measured in canvas units against the canvas's own rect,
        /// which keeps it correct whatever the resolution or render mode.
        /// </summary>
        public float TopHudFraction => HudFraction(_topHudExtent);

        public float BottomHudFraction => HudFraction(_bottomHudExtent);

        private float HudFraction(float canvasUnits)
        {
            if (_canvasRect == null) return 0.16f;

            float height = _canvasRect.rect.height;
            if (height <= 1f) return 0.16f;

            return Mathf.Clamp(canvasUnits / height, 0.04f, 0.45f);
        }

        public void SetStatus(string text, Color color)
        {
            _statusText = text;
            _statusColor = color;
            RefreshStatus();
        }

        /// <summary>
        /// While the opponent searches, its notice takes over the status line rather than
        /// occupying a row of its own.
        /// </summary>
        public void SetThinking(bool thinking, string text = "Thinking...")
        {
            _thinking = thinking;
            _thinkingText = text;
            RefreshStatus();
        }

        private void RefreshStatus()
        {
            if (_statusLabel == null) return;

            _statusLabel.text = _thinking ? _thinkingText : _statusText;
            _statusLabel.color = _thinking ? UIPalette.Info : _statusColor;
        }

        public void SetPlayers(string playerText, string opponentText)
        {
            if (_playerLabel != null) _playerLabel.text = playerText;
            if (_opponentLabel != null) _opponentLabel.text = opponentText;
        }

        public void SetCaptures(string playerCaptures, string opponentCaptures)
        {
            if (_playerCaptures != null) _playerCaptures.text = playerCaptures;
            if (_opponentCaptures != null) _opponentCaptures.text = opponentCaptures;
        }

        public void SetUndoEnabled(bool enabled)
        {
            if (_undoButton != null) _undoButton.interactable = enabled;
        }

        public void SetMoveList(StringBuilder text)
        {
            if (_moveListText == null) return;
            _moveListText.text = text.Length == 0 ? "No moves yet." : text.ToString();
        }
    }
}
