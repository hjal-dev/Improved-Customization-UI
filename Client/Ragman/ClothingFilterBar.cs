using System;
using System.Collections.Generic;
using EFT;
using EFT.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ImprovedCustomizationUI.UI;

namespace ImprovedCustomizationUI.Ragman
{
    public class ClothingFilterBar : MonoBehaviour
    {
        private const string TICKS_KEY = "Customization/SideToggles";

        private const int FILTER_ALL = 0;
        private const int FILTER_OBTAINED = 1;
        private const int FILTER_PURCHASABLE = 2;
        private const int FILTER_UNAVAILABLE = 3;
        private static readonly string[] FILTER_KEYS = new string[]
        {
            "ClothingItem/All", "ClothingItem/Obtained", "ClothingItem/CanBuy", "ClothingItem/Unavailable"
        };

        private const float SPACING = 10f;
        private const float BAR_WIDTH = 80f + SPACING + 80f + SPACING + 2f + SPACING + 200f;

        private TacticalClothingView _view;
        private Profile _profile;
        private FactionToggle _bear;
        private FactionToggle _usec;
        private DropDownBox _filter;
        private int _shownFilter = -1;
        private bool _built;

        public bool Ready
        {
            get { return _built && _profile != null; }
        }

        public void Prepare(TacticalClothingView view, Profile profile)
        {
            _view = view;
            _profile = profile;

            if (!_built)
            {
                Build();
                _built = true;
            }

            RebuildTogglesIfNeeded(ClothingRules.IsUnheard(profile));
            RefreshRule();

            _shownFilter = FILTER_ALL;
            _resetFilter = true;
        }

        public void RefreshRule()
        {
            EPlayerSide own = _profile.Side;
            EPlayerSide other = ClothingRules.OtherSide(own);
            bool otherAllowed = ClothingRules.MayUseSide(_profile, other);

            HashSet<EPlayerSide> saved = LoadTicks();
            bool ownOn = saved.Count == 0 || saved.Contains(own);
            bool otherOn = otherAllowed && saved.Contains(other);
            if (!ownOn && !otherOn)
            {
                ownOn = true;
            }

            ToggleFor(own).SetState(ownOn, true, false);
            ToggleFor(other).SetState(otherOn, otherAllowed, ClothingRules.BlockedByServer(_profile));
            ApplyAll();
        }

        private bool _resetFilter;
        private bool _filterFilled;

        public List<EPlayerSide> SidesToLoad()
        {
            List<EPlayerSide> sides = new List<EPlayerSide>();
            sides.Add(_profile.Side);
            EPlayerSide other = ClothingRules.OtherSide(_profile.Side);
            if (ClothingRules.MayUseSide(_profile, other))
            {
                sides.Add(other);
            }
            return sides;
        }

        public void Apply(ClothingItem card)
        {
            if (card == null || !Ready)
            {
                return;
            }

            bool shown = MatchesSides(card) && MatchesFilter(card);
            if (card.gameObject.activeSelf != shown)
            {
                card.gameObject.SetActive(shown);
            }
        }

        public void ApplyAll()
        {
            if (_view == null)
            {
                return;
            }

            List<ClothingItem> cards = RagmanFields.Cards(_view);
            if (cards == null)
            {
                return;
            }

            foreach (ClothingItem card in cards)
            {
                Apply(card);
            }
        }

        private bool MatchesSides(ClothingItem card)
        {
            if (card.Offer == null || card.Offer.Suite == null || card.Offer.Suite.Side == null)
            {
                return true;
            }

            foreach (EPlayerSide side in card.Offer.Suite.Side)
            {
                if ((side == EPlayerSide.Bear && _bear.IsOn) || (side == EPlayerSide.Usec && _usec.IsOn))
                {
                    return true;
                }
            }
            return false;
        }

        private bool MatchesFilter(ClothingItem card)
        {
            int filter = _shownFilter;
            ClothingItem.EClothingItemState state = RagmanFields.State(card);

            if (filter == FILTER_OBTAINED)
            {
                return state == ClothingItem.EClothingItemState.Purchased || state == ClothingItem.EClothingItemState.Selected;
            }
            if (filter == FILTER_PURCHASABLE)
            {
                return state == ClothingItem.EClothingItemState.PurchaseAvailable;
            }
            if (filter == FILTER_UNAVAILABLE)
            {
                return state == ClothingItem.EClothingItemState.Locked;
            }
            return true;
        }

        private void Update()
        {
            if (_filter == null)
            {
                return;
            }

            if (_resetFilter)
            {
                _resetFilter = false;
                if (!_filterFilled)
                {
                    List<string> names = new List<string>();
                    foreach (string key in FILTER_KEYS)
                    {
                        names.Add(key.Localized());
                    }
                    _filter.Show(names);
                    _filterFilled = true;
                }
                _filter.UpdateValue(FILTER_ALL, false);
                _shownFilter = FILTER_ALL;
                return;
            }

            if (_filterFilled && _filter.CurrentIndex != _shownFilter)
            {
                _shownFilter = _filter.CurrentIndex;
                ApplyAll();
            }
        }

        private void ToggleClicked(FactionToggle toggle)
        {
            FactionToggle other = toggle == _bear ? _usec : _bear;
            if (toggle.IsOn && !other.IsOn)
            {
                return;
            }

            toggle.SetState(!toggle.IsOn, true, false);
            SaveTicks();
            ApplyAll();
        }

        private void Build()
        {
            TextMeshProUGUI sideTitle = _view._sideTitle;
            Transform header = sideTitle.transform.parent;
            LogHeader(header);

            sideTitle.gameObject.SetActive(false);

            RectTransform bar = UiHelpers.Rect("ImprovedCustomizationUIFilterBar", header);
            bar.SetSiblingIndex(sideTitle.transform.GetSiblingIndex() + 1);

            HorizontalLayoutGroup layout = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = SPACING;
            layout.childAlignment = TextAnchor.MiddleRight;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            LayoutElement size = bar.gameObject.AddComponent<LayoutElement>();
            size.minWidth = BAR_WIDTH;
            size.preferredWidth = BAR_WIDTH;
            size.minHeight = 40f;
            size.preferredHeight = 40f;
            bar.sizeDelta = new Vector2(BAR_WIDTH, 40f);

            if (header.GetComponent<LayoutGroup>() == null)
            {
                bar.anchorMin = new Vector2(1f, 0.5f);
                bar.anchorMax = new Vector2(1f, 0.5f);
                bar.pivot = new Vector2(1f, 0.5f);
                bar.anchoredPosition = new Vector2(-10f, 0f);
                bar.sizeDelta = new Vector2(BAR_WIDTH, 40f);
            }

            _bear = FactionToggle.Create(bar, EPlayerSide.Bear, false);
            _usec = FactionToggle.Create(bar, EPlayerSide.Usec, false);
            _bear.Clicked = ToggleClicked;
            _usec.Clicked = ToggleClicked;

            RectTransform separator = UiHelpers.Rect("Separator", bar);
            Image line = separator.gameObject.AddComponent<Image>();
            line.sprite = UiHelpers.LoadSprite("SeparatorVertical.png");
            line.raycastTarget = false;
            LayoutElement lineSize = separator.gameObject.AddComponent<LayoutElement>();
            lineSize.preferredWidth = 2f;
            lineSize.preferredHeight = 32f;

            _filter = CreateFilter(bar);
        }

        private bool _unheardTicks;

        private void RebuildTogglesIfNeeded(bool unheard)
        {
            if (unheard == _unheardTicks)
            {
                return;
            }
            _unheardTicks = unheard;

            Transform bar = _bear.transform.parent;
            int bearIndex = _bear.transform.GetSiblingIndex();
            int usecIndex = _usec.transform.GetSiblingIndex();
            Destroy(_bear.gameObject);
            Destroy(_usec.gameObject);

            _bear = FactionToggle.Create(bar, EPlayerSide.Bear, unheard);
            _usec = FactionToggle.Create(bar, EPlayerSide.Usec, unheard);
            _bear.transform.SetSiblingIndex(bearIndex);
            _usec.transform.SetSiblingIndex(usecIndex);
            _bear.Clicked = ToggleClicked;
            _usec.Clicked = ToggleClicked;
        }

        private DropDownBox CreateFilter(Transform bar)
        {
            DropDownBox source = FindDropDownToCopy();
            if (source == null)
            {
                Plugin.Log.LogWarning("[ImprovedCustomizationUI] no dropdown found to copy, Ragman filter left out");
                return null;
            }

            GameObject copy = Instantiate(source.gameObject, bar, false);
            copy.name = "Dropdown";
            copy.SetActive(true);

            RectTransform rect = (RectTransform)copy.transform;
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(200f, 30f);

            LayoutElement size = copy.GetComponent<LayoutElement>();
            if (size == null)
            {
                size = copy.AddComponent<LayoutElement>();
            }
            size.ignoreLayout = false;
            size.minHeight = 30f;
            size.preferredWidth = 200f;
            size.preferredHeight = 30f;

            DropDownBox filter = copy.GetComponent<DropDownBox>();
            filter.Interactable = true;

            if (filter._currentValueText != null)
            {
                filter._currentValueText.enableAutoSizing = true;
                filter._currentValueText.fontSizeMin = 10f;
                filter._currentValueText.fontSizeMax = 15f;
                filter._currentValueText.color = new Color32(0xBD, 0xBA, 0xAD, 0xFF);
            }

            LogDropDown(source, copy.transform);
            return filter;
        }

        private static DropDownBox FindDropDownToCopy()
        {
            InventoryClothingSelectionPanel gearPanel = ImprovedCustomizationUI.Customization.AppearanceView.FindGearClothingPanel();
            if (gearPanel != null)
            {
                return gearPanel._upperButtonDropDown;
            }

            foreach (EftAccountSideSelectionScreen screen in Resources.FindObjectsOfTypeAll<EftAccountSideSelectionScreen>())
            {
                if (screen.gameObject.scene.IsValid() && screen._headSelectionState != null && screen._headSelectionState._voiceSelector != null)
                {
                    return screen._headSelectionState._voiceSelector;
                }
            }
            return null;
        }

        private FactionToggle ToggleFor(EPlayerSide side)
        {
            return side == EPlayerSide.Bear ? _bear : _usec;
        }

        private static HashSet<EPlayerSide> LoadTicks()
        {
            HashSet<EPlayerSide> ticks = new HashSet<EPlayerSide>();
            try
            {
                string saved = PlayerPrefs.GetString(TICKS_KEY, "");
                foreach (string part in saved.Split(','))
                {
                    if (part == "Bear")
                    {
                        ticks.Add(EPlayerSide.Bear);
                    }
                    else if (part == "Usec")
                    {
                        ticks.Add(EPlayerSide.Usec);
                    }
                }
            }
            catch (Exception error)
            {
                Plugin.Log.LogWarning("[ImprovedCustomizationUI] couldn't read the saved faction ticks: " + error.Message);
            }
            return ticks;
        }

        private void SaveTicks()
        {
            try
            {
                string value = (_bear.IsOn ? "Bear" : "") + (_bear.IsOn && _usec.IsOn ? "," : "") + (_usec.IsOn ? "Usec" : "");
                PlayerPrefs.SetString(TICKS_KEY, value);
                PlayerPrefs.Save();
            }
            catch (Exception error)
            {
                Plugin.Log.LogWarning("[ImprovedCustomizationUI] couldn't save the faction ticks: " + error.Message);
            }
        }

        private static void LogHeader(Transform header)
        {
            string children = "";
            for (int i = 0; i < header.childCount; i++)
            {
                Transform child = header.GetChild(i);
                RectTransform rect = child as RectTransform;
                children += (i > 0 ? ", " : "") + child.name + (child.gameObject.activeSelf ? "" : " [off]")
                    + (rect != null ? " " + rect.rect.width.ToString("0") + "x" + rect.rect.height.ToString("0") : "");
            }

            LayoutGroup layout = header.GetComponent<LayoutGroup>();
            RectTransform headerRect = header as RectTransform;
            Plugin.Log.LogInfo("[ImprovedCustomizationUI] Ragman clothing header '" + header.name + "' "
                + (headerRect != null ? headerRect.rect.width.ToString("0") + "x" + headerRect.rect.height.ToString("0") : "")
                + ", layout " + (layout != null ? layout.GetType().Name : "none") + ", children: " + children);
        }

        private static void LogDropDown(DropDownBox source, Transform copy)
        {
            string children = "";
            for (int i = 0; i < copy.childCount; i++)
            {
                children += (i > 0 ? ", " : "") + copy.GetChild(i).name;
            }
            Plugin.Log.LogInfo("[ImprovedCustomizationUI] Ragman filter copied from '" + source.name + "' (" + source.transform.parent.name
                + "), children: " + children);
        }
    }
}
