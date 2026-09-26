using System;
using System.Collections.Generic;
using EFT;
using EFT.UI;
using EFT.Utilities;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using ImprovedCustomizationUI.UI;

namespace ImprovedCustomizationUI.Customization.Preview
{
    public class PreviewControls
    {
        private const float ICON_SLOT = 36f;
        private const float SPACING = 8f;

        private const string FREE_LOOK_OFF = "FREE LOOK";
        private const string FREE_LOOK_ON = "FREE LOOK: ON";

        private static readonly Color NORMAL = new Color(0.78f, 0.78f, 0.78f, 1f);
        private static readonly Color HOVER = Color.white;
        private static readonly Color DISABLED = new Color(1f, 1f, 1f, 0.25f);

        private readonly PreviewCamera _camera;
        private readonly PreviewAnimations _animations;

        private DefaultUIButton _freeLook;
        private bool _freeLookShownOn;
        private readonly Button[] _actionButtons = new Button[4];
        private readonly List<Button> _gestureButtons = new List<Button>();

        private static readonly PreviewAction[] ACTIONS = new PreviewAction[]
        {
            PreviewAction.Inspect, PreviewAction.CheckAmmo, PreviewAction.CheckChamber, PreviewAction.Reload
        };

        private static readonly string[][] ACTION_ICONS = new string[][]
        {
            new string[] { "Search", "Inspect" },
            new string[] { "icon_info_magsize", "CheckMagazine" },
            new string[] { "icon_info_caliber", "UnloadAmmo" },
            new string[] { "Reload", "LoadAmmo" }
        };

        private static readonly string[] ACTION_FILES = new string[] { "icon_inspect.png", "icon_check_ammo.png", "icon_check_chamber.png", "icon_reload.png" };

        private static readonly string[] ACTION_WORDS = new string[] { "INSPECT", "AMMO", "CHAMBER", "RELOAD" };
        private static readonly string[] ACTION_NAMES = new string[] { "ExamineWeapon", "CheckAmmo", "CheckChamber", "ReloadWeapon" };

        public PreviewControls(Transform parent, DefaultUIButton buttonTemplate, PreviewCamera camera, PreviewAnimations animations)
        {
            _camera = camera;
            _animations = animations;
            BuildFreeLook(parent, buttonTemplate);
            BuildIconRow(parent);
        }

        private void BuildFreeLook(Transform parent, DefaultUIButton template)
        {
            _freeLook = UnityEngine.Object.Instantiate(template, parent, false);
            _freeLook.name = "FreeLookButton";
            _freeLook.SetIcon(null, null);
            _freeLook.SetRawText(FREE_LOOK_OFF, template.HeaderSize);
            _freeLook.OnClick.AddListener(FreeLookClicked);
            _freeLook.gameObject.SetActive(true);
        }

        private void BuildIconRow(Transform parent)
        {
            RectTransform row = UiHelpers.Rect("PreviewControls", parent);
            row.sizeDelta = new Vector2(0f, ICON_SLOT);
            HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = SPACING;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            for (int i = 0; i < ACTIONS.Length; i++)
            {
                PreviewAction action = ACTIONS[i];
                Sprite icon = UiHelpers.LoadSprite(ACTION_FILES[i]);
                if (icon == null)
                {
                    icon = LoadGameIcon(ACTION_ICONS[i]);
                }
                _actionButtons[i] = CreateIconButton(row, action.ToString(), icon, ACTION_WORDS[i],
                    ACTION_NAMES[i].Localized(), delegate { _animations.Play(action); });
            }
            AddSeparator(row);

            foreach (EInteraction gesture in PreviewAnimations.GESTURES)
            {
                EInteraction picked = gesture;
                Button button = CreateIconButton(row, gesture.ToString(), GestureIcon(gesture), gesture.ToString().Localized().ToUpper(),
                    gesture.ToString().Localized(), delegate { _animations.PlayGesture(picked); });
                _gestureButtons.Add(button);
            }

            MatchActionIconsToGestures();
        }

        private void MatchActionIconsToGestures()
        {
            float gestureSize = 0f;
            foreach (Button button in _gestureButtons)
            {
                if (button.targetGraphic is Image)
                {
                    Vector2 shown = button.targetGraphic.rectTransform.sizeDelta;
                    gestureSize = Mathf.Max(gestureSize, Mathf.Max(shown.x, shown.y));
                }
            }
            if (gestureSize <= 0f)
            {
                return;
            }

            string sizes = "";
            foreach (Button button in _actionButtons)
            {
                Image image = button.targetGraphic as Image;
                if (image == null || image.sprite == null)
                {
                    continue;
                }

                Vector2 native = image.sprite.rect.size;
                Vector2 scaled = native * (gestureSize / Mathf.Max(native.x, native.y));
                image.rectTransform.sizeDelta = scaled;
                sizes += " " + button.name + " " + native.x.ToString("0") + "x" + native.y.ToString("0") + "->" + scaled.x.ToString("0") + "x" + scaled.y.ToString("0");
            }

            if (!_loggedSizes)
            {
                _loggedSizes = true;
                Plugin.Log.LogInfo("[ImprovedCustomizationUI] preview icons: gesture size " + gestureSize.ToString("0") + ", actions" + sizes);
            }
        }

        private static bool _loggedSizes;

        public void Tick()
        {
            if (_camera.MouseOverModel)
            {
                _animations.CheckKeys();
            }
            _animations.UpdateGestureLayer();

            for (int i = 0; i < ACTIONS.Length; i++)
            {
                SetUsable(_actionButtons[i], _animations.CanPlay(ACTIONS[i]));
            }

            bool canGesture = _animations.CanGesture();
            foreach (Button button in _gestureButtons)
            {
                SetUsable(button, canGesture);
            }

            if (_camera.FreeLook != _freeLookShownOn)
            {
                _freeLookShownOn = _camera.FreeLook;
                _freeLook.SetRawText(_freeLookShownOn ? FREE_LOOK_ON : FREE_LOOK_OFF, _freeLook.HeaderSize);
            }
        }

        private static void SetUsable(Button button, bool usable)
        {
            if (button.interactable != usable)
            {
                button.interactable = usable;
            }
        }

        private void FreeLookClicked()
        {
            _camera.SetFreeLook(!_camera.FreeLook);
        }

        private static Button CreateIconButton(Transform row, string name, Sprite icon, string word, string tooltip, UnityAction clicked)
        {
            RectTransform slot = UiHelpers.Rect(name, row);
            LayoutElement size = slot.gameObject.AddComponent<LayoutElement>();
            size.preferredHeight = ICON_SLOT;
            size.minHeight = ICON_SLOT;

            Image hitArea = slot.gameObject.AddComponent<Image>();
            hitArea.color = new Color(0f, 0f, 0f, 0f);

            RectTransform content = UiHelpers.Rect("Icon", slot);
            content.anchorMin = new Vector2(0.5f, 0.5f);
            content.anchorMax = new Vector2(0.5f, 0.5f);

            Graphic graphic;
            if (icon != null)
            {
                Image image = content.gameObject.AddComponent<Image>();
                image.sprite = icon;
                image.preserveAspect = true;
                image.raycastTarget = false;
                content.sizeDelta = NativeSizeWithin(icon, ICON_SLOT);
                graphic = image;
                size.preferredWidth = ICON_SLOT;
                size.minWidth = ICON_SLOT;
            }
            else
            {
                TextMeshProUGUI text = content.gameObject.AddComponent<TextMeshProUGUI>();
                text.text = word;
                text.fontSize = 16f;
                text.alignment = TextAlignmentOptions.Center;
                text.enableWordWrapping = false;
                text.raycastTarget = false;
                content.sizeDelta = new Vector2(text.preferredWidth, ICON_SLOT);
                graphic = text;
                size.preferredWidth = text.preferredWidth;
            }

            Button button = slot.gameObject.AddComponent<Button>();
            button.targetGraphic = graphic;
            ColorBlock colors = button.colors;
            colors.normalColor = NORMAL;
            colors.highlightedColor = HOVER;
            colors.pressedColor = new Color(0.6f, 0.6f, 0.6f, 1f);
            colors.selectedColor = NORMAL;
            colors.disabledColor = DISABLED;
            colors.fadeDuration = 0.1f;
            button.colors = colors;
            button.onClick.AddListener(clicked);
            UiButtonSounds.AddTo(slot.gameObject);

            try
            {
                HoverTooltipArea hover = slot.gameObject.AddComponent<HoverTooltipArea>();
                hover.SetMessageText(tooltip, true);
            }
            catch (Exception error)
            {
                Plugin.Log.LogWarning("[ImprovedCustomizationUI] preview control tooltip: " + error.Message);
            }

            return button;
        }

        private static Vector2 NativeSizeWithin(Sprite sprite, float limit)
        {
            Vector2 native = sprite.rect.size;
            float biggest = Mathf.Max(native.x, native.y);
            if (biggest <= limit || biggest <= 0f)
            {
                return native;
            }
            return native * (limit / biggest);
        }

        private static void AddSeparator(Transform row)
        {
            RectTransform rect = UiHelpers.Rect("Separator", row);
            Image line = rect.gameObject.AddComponent<Image>();
            line.sprite = UiHelpers.LoadSprite("SeparatorVertical.png");
            line.raycastTarget = false;
            LayoutElement size = rect.gameObject.AddComponent<LayoutElement>();
            size.preferredWidth = 2f;
            size.preferredHeight = ICON_SLOT - 8f;
        }

        private static Sprite LoadGameIcon(string[] names)
        {
            foreach (string name in names)
            {
                Sprite sprite = null;
                try
                {
                    sprite = ResourcesCache.Pop<Sprite>("Characteristics/Icons/" + name);
                }
                catch (Exception)
                {
                }
                if (sprite != null)
                {
                    return sprite;
                }
            }
            return null;
        }

        private static Sprite GestureIcon(EInteraction gesture)
        {
            try
            {
                Sprite sprite;
                if (EFTHardSettings.Instance.StaticIcons.GestureSprites.TryGetValue(gesture, out sprite))
                {
                    return sprite;
                }
            }
            catch (Exception)
            {
            }
            return null;
        }
    }
}
