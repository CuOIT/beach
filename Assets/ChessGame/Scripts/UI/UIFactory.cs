using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chess.UI
{
    /// <summary>Shared colours so every screen looks like part of the same app.</summary>
    public static class UIPalette
    {
        public static readonly Color Scrim = new Color32(0x0B, 0x0E, 0x13, 0xD8);
        public static readonly Color Card = new Color32(0x1B, 0x20, 0x29, 0xFF);
        public static readonly Color CardRaised = new Color32(0x25, 0x2C, 0x38, 0xFF);
        public static readonly Color Bar = new Color32(0x16, 0x1A, 0x22, 0xE0);

        public static readonly Color Accent = new Color32(0xE8, 0xB2, 0x5C, 0xFF);
        public static readonly Color AccentDim = new Color32(0x6B, 0x54, 0x2C, 0xFF);
        public static readonly Color Info = new Color32(0x4C, 0x9A, 0xFF, 0xFF);

        public static readonly Color Text = new Color32(0xF0, 0xF2, 0xF6, 0xFF);
        public static readonly Color TextMuted = new Color32(0x97, 0xA1, 0xB2, 0xFF);
        public static readonly Color TextOnAccent = new Color32(0x1A, 0x14, 0x08, 0xFF);

        public static readonly Color Success = new Color32(0x4E, 0xD8, 0x8A, 0xFF);
        public static readonly Color Danger = new Color32(0xFF, 0x6B, 0x6B, 0xFF);
    }

    /// <summary>
    /// Builds the interface in code. Everything is uGUI driven by layout groups, which keeps the
    /// same construction working across phone and desktop aspect ratios without hand-tuned rects.
    /// </summary>
    public static class UIFactory
    {
        private static TMP_FontAsset _font;
        private static Sprite _roundedSprite;
        private static Sprite _circleSprite;

        public static Sprite Rounded
        {
            get
            {
                if (_roundedSprite == null) _roundedSprite = Visual.MaterialLibrary.CreateRoundedSprite(64, 18);
                return _roundedSprite;
            }
        }

        public static Sprite Circle
        {
            get
            {
                if (_circleSprite == null) _circleSprite = Visual.MaterialLibrary.CreateCircleSprite(64);
                return _circleSprite;
            }
        }

        /// <summary>
        /// Finds a usable TextMeshPro font. Falls back to wrapping the built-in OS font when the
        /// TMP essential resources were never imported, so text never silently disappears.
        /// </summary>
        public static TMP_FontAsset ResolveFont()
        {
            if (_font != null) return _font;

            try
            {
                _font = TMP_Settings.defaultFontAsset;
            }
            catch
            {
                _font = null;
            }

            if (_font == null)
            {
                Font builtin = null;
                try { builtin = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
                catch { builtin = null; }

                if (builtin == null)
                {
                    try { builtin = Resources.GetBuiltinResource<Font>("Arial.ttf"); }
                    catch { builtin = null; }
                }

                if (builtin != null) _font = TMP_FontAsset.CreateFontAsset(builtin);
            }

            return _font;
        }

        public static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        /// <summary>Stretches a rect to fill its parent, optionally inset by <paramref name="padding"/>.</summary>
        public static RectTransform Stretch(RectTransform rect, float padding = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(padding, padding);
            rect.offsetMax = new Vector2(-padding, -padding);
            return rect;
        }

        public static Image Panel(Transform parent, string name, Color color, bool rounded = true)
        {
            RectTransform rect = CreateRect(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;

            if (rounded)
            {
                image.sprite = Rounded;
                image.type = Image.Type.Sliced;
            }

            return image;
        }

        public static TextMeshProUGUI Label(
            Transform parent,
            string text,
            float size,
            Color color,
            TextAlignmentOptions alignment = TextAlignmentOptions.Center,
            FontWeight weight = FontWeight.Regular)
        {
            RectTransform rect = CreateRect("Label", parent);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();

            label.font = ResolveFont();
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = alignment;
            label.fontWeight = weight;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.raycastTarget = false;

            return label;
        }

        public struct ButtonParts
        {
            public Button Button;
            public Image Background;
            public TextMeshProUGUI Label;
            public RectTransform Rect;
        }

        public static ButtonParts CreateButton(Transform parent, string text, Color background, Color foreground, float height = 110f, float fontSize = 40f)
        {
            Image image = Panel(parent, "Button " + text, background);
            RectTransform rect = image.rectTransform;

            var layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = height;
            layout.preferredHeight = height;
            layout.flexibleWidth = 1f;

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(1f, 1f, 1f, 0.35f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            TextMeshProUGUI label = Label(rect, text, fontSize, foreground, TextAlignmentOptions.Center, FontWeight.SemiBold);
            Stretch(label.rectTransform, 12f);

            return new ButtonParts { Button = button, Background = image, Label = label, Rect = rect };
        }

        public static VerticalLayoutGroup VerticalGroup(Transform parent, string name, float spacing, RectOffset padding = null)
        {
            RectTransform rect = CreateRect(name, parent);
            var group = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            group.spacing = spacing;
            group.padding = padding ?? new RectOffset(0, 0, 0, 0);
            group.childControlWidth = true;
            group.childControlHeight = true;
            group.childForceExpandWidth = true;
            group.childForceExpandHeight = false;
            group.childAlignment = TextAnchor.UpperCenter;
            return group;
        }

        public static HorizontalLayoutGroup HorizontalGroup(Transform parent, string name, float spacing, RectOffset padding = null)
        {
            RectTransform rect = CreateRect(name, parent);
            var group = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            group.spacing = spacing;
            group.padding = padding ?? new RectOffset(0, 0, 0, 0);
            group.childControlWidth = true;
            group.childControlHeight = true;
            group.childForceExpandWidth = true;
            group.childForceExpandHeight = false;
            group.childAlignment = TextAnchor.MiddleCenter;
            return group;
        }

        public static LayoutElement SetHeight(Component target, float height)
        {
            LayoutElement element = target.gameObject.GetComponent<LayoutElement>();
            if (element == null) element = target.gameObject.AddComponent<LayoutElement>();
            element.minHeight = height;
            element.preferredHeight = height;
            return element;
        }

        /// <summary>A labelled row of mutually exclusive choices, e.g. difficulty or board theme.</summary>
        public class OptionRow
        {
            public RectTransform Rect;
            public Button[] Buttons;
            public Image[] Backgrounds;
            public TextMeshProUGUI[] Labels;
            private int _selected;

            public int Selected => _selected;

            public void Select(int index)
            {
                _selected = index;
                for (int i = 0; i < Backgrounds.Length; i++)
                {
                    bool on = i == index;
                    Backgrounds[i].color = on ? UIPalette.Accent : UIPalette.CardRaised;
                    Labels[i].color = on ? UIPalette.TextOnAccent : UIPalette.TextMuted;
                }
            }
        }

        public static OptionRow CreateOptionRow(Transform parent, string title, string[] options, int selected, System.Action<int> onChanged)
        {
            VerticalLayoutGroup column = VerticalGroup(parent, "Option " + title, 12f);

            TextMeshProUGUI caption = Label(column.transform, title, 32f, UIPalette.TextMuted, TextAlignmentOptions.Left);
            SetHeight(caption, 40f);

            HorizontalLayoutGroup row = HorizontalGroup(column.transform, "Choices", 12f);
            SetHeight(row, 92f);

            var result = new OptionRow
            {
                Rect = column.GetComponent<RectTransform>(),
                Buttons = new Button[options.Length],
                Backgrounds = new Image[options.Length],
                Labels = new TextMeshProUGUI[options.Length]
            };

            for (int i = 0; i < options.Length; i++)
            {
                ButtonParts parts = CreateButton(row.transform, options[i], UIPalette.CardRaised, UIPalette.TextMuted, 92f, 32f);
                int index = i;

                parts.Button.onClick.AddListener(() =>
                {
                    result.Select(index);
                    onChanged(index);
                });

                result.Buttons[i] = parts.Button;
                result.Backgrounds[i] = parts.Background;
                result.Labels[i] = parts.Label;
            }

            result.Select(selected);
            return result;
        }

        /// <summary>A switch row: caption on the left, on/off pill on the right.</summary>
        public class SwitchRow
        {
            public Button Button;
            public Image Pill;
            public TextMeshProUGUI State;
            private bool _on;

            public bool IsOn => _on;

            public void Set(bool on)
            {
                _on = on;
                Pill.color = on ? UIPalette.Success : UIPalette.CardRaised;
                State.text = on ? "ON" : "OFF";
                State.color = on ? UIPalette.TextOnAccent : UIPalette.TextMuted;
            }
        }

        public static SwitchRow CreateSwitch(Transform parent, string title, bool value, System.Action<bool> onChanged)
        {
            HorizontalLayoutGroup row = HorizontalGroup(parent, "Switch " + title, 16f);
            SetHeight(row, 84f);

            TextMeshProUGUI caption = Label(row.transform, title, 32f, UIPalette.Text, TextAlignmentOptions.Left);
            caption.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            Image pill = Panel(row.transform, "Pill", UIPalette.CardRaised);
            var pillLayout = pill.gameObject.AddComponent<LayoutElement>();
            pillLayout.minWidth = 150f;
            pillLayout.preferredWidth = 150f;
            pillLayout.flexibleWidth = 0f;

            var button = pill.gameObject.AddComponent<Button>();
            button.targetGraphic = pill;

            TextMeshProUGUI state = Label(pill.transform, "ON", 30f, UIPalette.TextOnAccent, TextAlignmentOptions.Center, FontWeight.Bold);
            Stretch(state.rectTransform);

            var result = new SwitchRow { Button = button, Pill = pill, State = state };
            result.Set(value);

            button.onClick.AddListener(() =>
            {
                result.Set(!result.IsOn);
                onChanged(result.IsOn);
            });

            return result;
        }

        /// <summary>A caption, a value readout and a slider, laid out as one settings row.</summary>
        public class SliderRow
        {
            public Slider Slider;
            public TextMeshProUGUI Value;
        }

        public static SliderRow CreateSlider(Transform parent, string title, float value, float min, float max, System.Func<float, string> format, System.Action<float> onChanged)
        {
            VerticalLayoutGroup column = VerticalGroup(parent, "Slider " + title, 8f);

            HorizontalLayoutGroup header = HorizontalGroup(column.transform, "Header", 8f);
            SetHeight(header, 40f);

            TextMeshProUGUI caption = Label(header.transform, title, 32f, UIPalette.TextMuted, TextAlignmentOptions.Left);
            caption.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            TextMeshProUGUI readout = Label(header.transform, format(value), 32f, UIPalette.Accent, TextAlignmentOptions.Right, FontWeight.SemiBold);
            readout.gameObject.AddComponent<LayoutElement>().minWidth = 140f;

            RectTransform sliderRect = CreateRect("Slider", column.transform);
            SetHeight(sliderRect, 56f);

            var slider = sliderRect.gameObject.AddComponent<Slider>();

            Image background = Panel(sliderRect, "Track", UIPalette.CardRaised);
            RectTransform backgroundRect = background.rectTransform;
            backgroundRect.anchorMin = new Vector2(0f, 0.5f);
            backgroundRect.anchorMax = new Vector2(1f, 0.5f);
            backgroundRect.sizeDelta = new Vector2(0f, 16f);
            backgroundRect.anchoredPosition = Vector2.zero;

            RectTransform fillArea = CreateRect("Fill Area", sliderRect);
            fillArea.anchorMin = new Vector2(0f, 0.5f);
            fillArea.anchorMax = new Vector2(1f, 0.5f);
            fillArea.sizeDelta = new Vector2(-30f, 16f);
            fillArea.anchoredPosition = Vector2.zero;

            Image fill = Panel(fillArea, "Fill", UIPalette.Accent);
            fill.rectTransform.sizeDelta = new Vector2(30f, 0f);

            // Slider rewrites both of the handle's anchors every update - anchorMin.y to 0 and
            // anchorMax.y to 1 - so the handle always stretches to its parent's height. The only
            // way to keep the knob round is to give the slide area exactly the wanted height and
            // leave the handle's own sizeDelta.y at zero. The horizontal inset is half the knob,
            // so its centre lines up with the ends of the track.
            const float handleSize = 46f;

            RectTransform handleArea = CreateRect("Handle Slide Area", sliderRect);
            handleArea.anchorMin = new Vector2(0f, 0.5f);
            handleArea.anchorMax = new Vector2(1f, 0.5f);
            handleArea.sizeDelta = new Vector2(-handleSize, handleSize);
            handleArea.anchoredPosition = Vector2.zero;

            Image handle = Panel(handleArea, "Handle", UIPalette.Text);
            handle.sprite = Circle;
            handle.type = Image.Type.Simple;
            handle.rectTransform.sizeDelta = new Vector2(handleSize, 0f);

            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.SetValueWithoutNotify(value);

            var result = new SliderRow { Slider = slider, Value = readout };

            slider.onValueChanged.AddListener(v =>
            {
                readout.text = format(v);
                onChanged(v);
            });

            return result;
        }
    }
}
