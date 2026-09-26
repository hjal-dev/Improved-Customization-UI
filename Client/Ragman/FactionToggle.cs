using System;
using EFT;
using EFT.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using ImprovedCustomizationUI.UI;

namespace ImprovedCustomizationUI.Ragman
{
    public class FactionToggle : MonoBehaviour, IPointerClickHandler
    {
        public EPlayerSide Side;
        public bool IsOn;
        public bool IsInteractive;
        public Action<FactionToggle> Clicked;

        private Image _background;
        private Image _logo;
        private GameObject _tick;
        private HoverTooltipArea _tooltip;

        private Sprite _logoActive;
        private Sprite _logoInactive;
        private Sprite _logoDisabled;

        private const string BACKGROUND_ACTIVE = "EFT_Services_Outfit_Filter_Button_Backgrounds_1_0.png";
        private const string BACKGROUND_INACTIVE = "EFT_Services_Outfit_Filter_Button_Backgrounds_1_1.png";
        private const string BACKGROUND_DISABLED = "EFT_Services_Outfit_Filter_Button_Backgrounds_1_2.png";
        private const string TICK_UNHEARD = "EFT_Services_Outfit_Filter_Button_Toggle_1_0.png";
        private const string TICK_COMMON = "EFT_Services_Outfit_Filter_Button_Toggle_1_1.png";
        private const string BOX = "EFT_Services_Outfit_Filter_Button_Toggle_1_2.png";

        private const float LOGO_SCALE = 42f / 84f;

        public static FactionToggle Create(Transform parent, EPlayerSide side, bool unheardTick)
        {
            RectTransform root = UiHelpers.Rect(side == EPlayerSide.Bear ? "BearSideToggle" : "UsecSideToggle", parent);
            root.sizeDelta = new Vector2(80f, 40f);
            LayoutElement slot = root.gameObject.AddComponent<LayoutElement>();
            slot.minWidth = 80f;
            slot.minHeight = 40f;
            slot.preferredWidth = 80f;
            slot.preferredHeight = 40f;

            FactionToggle toggle = root.gameObject.AddComponent<FactionToggle>();
            toggle.Side = side;

            int first = side == EPlayerSide.Bear ? 0 : 1;
            toggle._logoActive = UiHelpers.LoadSprite("EFT_Services_Outfit_Filter_Icons_Faction_" + first + ".png");
            toggle._logoInactive = UiHelpers.LoadSprite("EFT_Services_Outfit_Filter_Icons_Faction_" + (first + 2) + ".png");
            toggle._logoDisabled = UiHelpers.LoadSprite("EFT_Services_Outfit_Filter_Icons_Faction_" + (first + 4) + ".png");

            RectTransform background = UiHelpers.Rect("Background", root);
            background.anchorMin = new Vector2(0.5f, 0.5f);
            background.anchorMax = new Vector2(0.5f, 0.5f);
            background.sizeDelta = new Vector2(88f, 48f);
            toggle._background = background.gameObject.AddComponent<Image>();

            RectTransform logo = UiHelpers.Rect("SideIcon", root);
            logo.anchorMin = new Vector2(0f, 0.5f);
            logo.anchorMax = new Vector2(0f, 0.5f);
            logo.pivot = new Vector2(0.5f, 0.5f);
            logo.anchoredPosition = new Vector2(5f + 21f, 0f);
            logo.sizeDelta = new Vector2(42f, 42f);
            toggle._logo = logo.gameObject.AddComponent<Image>();
            toggle._logo.preserveAspect = true;
            toggle._logo.raycastTarget = false;

            RectTransform box = UiHelpers.Rect("Checkmark", root);
            box.anchorMin = new Vector2(1f, 0.5f);
            box.anchorMax = new Vector2(1f, 0.5f);
            box.pivot = new Vector2(1f, 0.5f);
            box.anchoredPosition = new Vector2(-12f, 0f);
            box.sizeDelta = new Vector2(18f, 18f);
            Image boxImage = box.gameObject.AddComponent<Image>();
            boxImage.sprite = UiHelpers.LoadSprite(BOX);
            boxImage.raycastTarget = false;

            RectTransform tick = UiHelpers.Rect("Checkmark", box);
            tick.anchorMin = new Vector2(0.5f, 0.5f);
            tick.anchorMax = new Vector2(0.5f, 0.5f);
            tick.sizeDelta = new Vector2(48f, 48f);
            Image tickImage = tick.gameObject.AddComponent<Image>();
            tickImage.sprite = UiHelpers.LoadSprite(unheardTick ? TICK_UNHEARD : TICK_COMMON);
            tickImage.raycastTarget = false;
            toggle._tick = tick.gameObject;

            try
            {
                toggle._tooltip = root.gameObject.AddComponent<HoverTooltipArea>();
                toggle._tooltip.SetMessageText("", true);
            }
            catch (Exception error)
            {
                Plugin.Log.LogWarning("[ImprovedCustomizationUI] faction toggle tooltip: " + error.Message);
            }

            toggle.SetState(false, true, false);
            return toggle;
        }

        public void SetState(bool isOn, bool interactive, bool blockedByServer)
        {
            IsOn = isOn;
            IsInteractive = interactive;

            if (!interactive)
            {
                _background.sprite = UiHelpers.LoadSprite(BACKGROUND_DISABLED);
                _logo.sprite = _logoDisabled;
            }
            else if (isOn)
            {
                _background.sprite = UiHelpers.LoadSprite(BACKGROUND_ACTIVE);
                _logo.sprite = _logoActive;
            }
            else
            {
                _background.sprite = UiHelpers.LoadSprite(BACKGROUND_INACTIVE);
                _logo.sprite = _logoInactive;
            }

            if (_logo.sprite != null)
            {
                _logo.rectTransform.sizeDelta = _logo.sprite.rect.size * LOGO_SCALE;
            }

            _tick.SetActive(isOn);

            if (_tooltip != null)
            {
                if (interactive)
                {
                    _tooltip.SetMessageText("", true);
                }
                else if (blockedByServer)
                {
                    _tooltip.SetMessageText("Blocked by the server's settings", true);
                }
                else
                {
                    _tooltip.SetMessageText("ClothingItem/LockedForCurrentGameVersion", false);
                }
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || !IsInteractive)
            {
                return;
            }

            if (Clicked != null)
            {
                Clicked(this);
            }
        }
    }
}
