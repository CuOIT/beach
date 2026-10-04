using Chess.AI;
using Chess.Game;
using Chess.Visual;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chess.UI
{
    /// <summary>Main menu, pause overlay and the settings screen.</summary>
    public sealed partial class UIManager
    {
        private UIFactory.OptionRow _difficultyRow;
        private UIFactory.OptionRow _sideRow;
        private UIFactory.OptionRow _themeRow;
        private UIFactory.SwitchRow _soundSwitch;
        private UIFactory.SwitchRow _legalMovesSwitch;
        private UIFactory.SwitchRow _lastMoveSwitch;
        private UIFactory.SliderRow _volumeSlider;
        private UIFactory.SliderRow _animationSlider;

        private void BuildMainMenu()
        {
            _mainMenu = CreateScreen("MainMenu", true);

            RectTransform column = UIFactory.CreateRect("Column", _mainMenu.transform);
            column.anchorMin = new Vector2(0.5f, 0.5f);
            column.anchorMax = new Vector2(0.5f, 0.5f);
            column.pivot = new Vector2(0.5f, 0.5f);
            column.sizeDelta = new Vector2(860f, 0f);

            var layout = column.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 24f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.MiddleCenter;

            var fitter = column.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            TextMeshProUGUI title = UIFactory.Label(column, "CHESS", 130f, UIPalette.Accent, TextAlignmentOptions.Center, FontWeight.Black);
            title.characterSpacing = 18f;
            UIFactory.SetHeight(title, 160f);

            TextMeshProUGUI subtitle = UIFactory.Label(column, "3D  -  play against the computer", 32f, UIPalette.TextMuted);
            UIFactory.SetHeight(subtitle, 48f);

            RectTransform spacer = UIFactory.CreateRect("Spacer", column);
            UIFactory.SetHeight(spacer, 40f);

            UIFactory.ButtonParts play = UIFactory.CreateButton(column, "PLAY", UIPalette.Accent, UIPalette.TextOnAccent, 128f, 46f);
            play.Button.onClick.AddListener(() => PlayRequested?.Invoke());

            UIFactory.ButtonParts settings = UIFactory.CreateButton(column, "Settings", UIPalette.CardRaised, UIPalette.Text);
            settings.Button.onClick.AddListener(() => SettingsOpened?.Invoke());

            UIFactory.ButtonParts quit = UIFactory.CreateButton(column, "Quit", UIPalette.CardRaised, UIPalette.TextMuted);
            quit.Button.onClick.AddListener(() => QuitRequested?.Invoke());

            RectTransform footerSpacer = UIFactory.CreateRect("Spacer2", column);
            UIFactory.SetHeight(footerSpacer, 24f);

            _menuFooter = UIFactory.Label(column, string.Empty, 28f, UIPalette.TextMuted);
            UIFactory.SetHeight(_menuFooter, 44f);
        }

        public void RefreshMenuFooter()
        {
            if (_menuFooter == null) return;

            string side;
            switch (GameSettings.PreferredSide)
            {
                case PlayerSide.Black: side = "Black"; break;
                case PlayerSide.Random: side = "a random colour"; break;
                default: side = "White"; break;
            }

            _menuFooter.text = string.Format(
                "{0} difficulty  -  you play {1}  -  {2} board",
                ChessAI.DisplayName(GameSettings.Difficulty),
                side,
                GameSettings.Theme.Name);
        }

        private void BuildPause()
        {
            _pause = CreateScreen("Pause", true);

            VerticalLayoutGroup card = CreateCard(_pause.transform, 860f);

            TextMeshProUGUI title = UIFactory.Label(card.transform, "Paused", 60f, UIPalette.Accent, TextAlignmentOptions.Center, FontWeight.Bold);
            UIFactory.SetHeight(title, 88f);

            UIFactory.ButtonParts resume = UIFactory.CreateButton(card.transform, "Resume", UIPalette.Accent, UIPalette.TextOnAccent);
            resume.Button.onClick.AddListener(() => ResumeRequested?.Invoke());

            UIFactory.ButtonParts restart = UIFactory.CreateButton(card.transform, "Restart game", UIPalette.CardRaised, UIPalette.Text);
            restart.Button.onClick.AddListener(() =>
            {
                _pause.SetActive(false);
                RestartRequested?.Invoke();
            });

            UIFactory.ButtonParts settings = UIFactory.CreateButton(card.transform, "Settings", UIPalette.CardRaised, UIPalette.Text);
            settings.Button.onClick.AddListener(() => SettingsOpened?.Invoke());

            UIFactory.ButtonParts menu = UIFactory.CreateButton(card.transform, "Main menu", UIPalette.CardRaised, UIPalette.TextMuted);
            menu.Button.onClick.AddListener(() =>
            {
                _pause.SetActive(false);
                MainMenuRequested?.Invoke();
            });
        }

        /// <summary>
        /// The settings card is anchored to the screen height with an internal scroll view, so
        /// the full list of options stays reachable on a short phone screen.
        /// </summary>
        private void BuildSettings()
        {
            _settings = CreateScreen("Settings", true);

            RectTransform holder = UIFactory.CreateRect("Card", _settings.transform);
            holder.anchorMin = new Vector2(0.5f, 0f);
            holder.anchorMax = new Vector2(0.5f, 1f);
            holder.pivot = new Vector2(0.5f, 0.5f);
            holder.sizeDelta = new Vector2(940f, -140f);
            holder.anchoredPosition = Vector2.zero;

            var background = holder.gameObject.AddComponent<Image>();
            background.color = UIPalette.Card;
            background.sprite = UIFactory.Rounded;
            background.type = Image.Type.Sliced;

            var outer = holder.gameObject.AddComponent<VerticalLayoutGroup>();
            outer.spacing = 18f;
            outer.padding = new RectOffset(36, 36, 36, 36);
            outer.childControlWidth = true;
            outer.childControlHeight = true;
            outer.childForceExpandWidth = true;
            outer.childForceExpandHeight = false;

            TextMeshProUGUI title = UIFactory.Label(holder, "Settings", 58f, UIPalette.Accent, TextAlignmentOptions.Center, FontWeight.Bold);
            UIFactory.SetHeight(title, 82f);

            RectTransform viewport = UIFactory.CreateRect("Viewport", holder);
            var viewportLayout = viewport.gameObject.AddComponent<LayoutElement>();
            viewportLayout.flexibleHeight = 1f;
            viewportLayout.minHeight = 300f;

            viewport.gameObject.AddComponent<RectMask2D>();

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;

            RectTransform content = UIFactory.CreateRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = new Vector2(8f, 0f);
            content.offsetMax = new Vector2(-8f, 0f);
            content.anchoredPosition = Vector2.zero;

            var contentLayout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            contentLayout.spacing = 26f;
            contentLayout.padding = new RectOffset(0, 12, 8, 24);
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;

            var contentFitter = content.gameObject.AddComponent<ContentSizeFitter>();
            contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewport;
            scroll.content = content;

            BuildSettingsRows(content);

            UIFactory.ButtonParts back = UIFactory.CreateButton(holder, "Back", UIPalette.Accent, UIPalette.TextOnAccent);
            back.Button.onClick.AddListener(CloseSettings);
        }

        private void BuildSettingsRows(Transform content)
        {
            _difficultyRow = UIFactory.CreateOptionRow(
                content, "Difficulty",
                new[] { "Easy", "Medium", "Hard", "Expert" },
                (int)GameSettings.Difficulty,
                index =>
                {
                    GameSettings.Difficulty = (Difficulty)index;
                    ApplySettings();
                });

            _sideRow = UIFactory.CreateOptionRow(
                content, "Play as",
                new[] { "White", "Black", "Random" },
                (int)GameSettings.PreferredSide,
                index =>
                {
                    GameSettings.PreferredSide = (PlayerSide)index;
                    ApplySettings();
                });

            var themeNames = new string[BoardTheme.All.Length];
            for (int i = 0; i < themeNames.Length; i++) themeNames[i] = BoardTheme.All[i].Name;

            _themeRow = UIFactory.CreateOptionRow(
                content, "Board theme",
                themeNames,
                GameSettings.ThemeIndex,
                index =>
                {
                    GameSettings.ThemeIndex = index;
                    ApplySettings();
                });

            _soundSwitch = UIFactory.CreateSwitch(content, "Sound effects", GameSettings.SoundEnabled, value =>
            {
                GameSettings.SoundEnabled = value;
                ApplySettings();
            });

            _volumeSlider = UIFactory.CreateSlider(
                content, "Volume", GameSettings.Volume, 0f, 1f,
                v => Mathf.RoundToInt(v * 100f) + "%",
                v =>
                {
                    GameSettings.Volume = v;
                    ApplySettings();
                });

            _legalMovesSwitch = UIFactory.CreateSwitch(content, "Show legal moves", GameSettings.ShowLegalMoves, value =>
            {
                GameSettings.ShowLegalMoves = value;
                ApplySettings();
            });

            _lastMoveSwitch = UIFactory.CreateSwitch(content, "Highlight last move", GameSettings.ShowLastMove, value =>
            {
                GameSettings.ShowLastMove = value;
                ApplySettings();
            });

            _animationSlider = UIFactory.CreateSlider(
                content, "Animation speed", GameSettings.AnimationSpeed, 0.5f, 3f,
                v => v.ToString("0.0") + "x",
                v =>
                {
                    GameSettings.AnimationSpeed = v;
                    ApplySettings();
                });

            TextMeshProUGUI note = UIFactory.Label(
                content,
                "Difficulty and colour take effect from the next new game.",
                26f, UIPalette.TextMuted, TextAlignmentOptions.Left);
            UIFactory.SetHeight(note, 70f);
        }

        private void ApplySettings()
        {
            GameSettings.Save();
            RefreshMenuFooter();
            SettingsApplied?.Invoke();
        }

        public void OpenSettings(bool fromGame)
        {
            _settingsCameFromGame = fromGame;
            SyncSettingsControls();
            _settings.SetActive(true);
        }

        private void CloseSettings()
        {
            _settings.SetActive(false);
            SettingsClosed?.Invoke();
        }

        /// <summary>Pushes the stored values back into the widgets, so the screen never shows stale state.</summary>
        private void SyncSettingsControls()
        {
            _difficultyRow?.Select((int)GameSettings.Difficulty);
            _sideRow?.Select((int)GameSettings.PreferredSide);
            _themeRow?.Select(GameSettings.ThemeIndex);
            _soundSwitch?.Set(GameSettings.SoundEnabled);
            _legalMovesSwitch?.Set(GameSettings.ShowLegalMoves);
            _lastMoveSwitch?.Set(GameSettings.ShowLastMove);

            if (_volumeSlider != null)
            {
                _volumeSlider.Slider.SetValueWithoutNotify(GameSettings.Volume);
                _volumeSlider.Value.text = Mathf.RoundToInt(GameSettings.Volume * 100f) + "%";
            }

            if (_animationSlider != null)
            {
                _animationSlider.Slider.SetValueWithoutNotify(GameSettings.AnimationSpeed);
                _animationSlider.Value.text = GameSettings.AnimationSpeed.ToString("0.0") + "x";
            }
        }

        public bool SettingsCameFromGame => _settingsCameFromGame;
    }
}
