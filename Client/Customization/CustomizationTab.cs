using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Comfort.Common;
using EFT;
using EFT.Customization;
using EFT.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ImprovedCustomizationUI.UI;

namespace ImprovedCustomizationUI.Customization
{
    public class CustomizationTab : MonoBehaviour, ITabController
    {
        public const EInventoryTab TAB_ID = (EInventoryTab)8;

        private const string TAB_NAME = "ImprovedCustomizationUICustomizationTab";

        private const float SAVE_DELAY = 0.45f;

        private Tab _tab;
        private RectTransform _page;
        private Profile _profile;
        private IEftSession _session;
        private bool _closing;

        private AppearanceView _view;
        private Task _loading = Task.CompletedTask;
        private Task<bool> _saving;

        private AppearanceChoice _observed;
        private float _changedAt;
        private bool _flushing;

        private Sprite _icon;
        private Texture2D _iconTexture;
        private Sprite _selectedIcon;
        private Texture2D _selectedIconTexture;

        private Tab _prestigeTab;
        private bool _prestigeWasActive;

        public void Initialize(InventoryScreen screen, InventoryScreen.InventoryScreenController controller)
        {
            ReleaseView();
            _profile = controller.Profile;
            _session = controller.Session;
            _closing = false;
            _saving = null;

            if (_tab == null)
            {
                CreateTab(screen);
            }

            _tab.Init(this);
            _tab.UpdateVisual(false);

            bool available = !controller.InRaid && !controller.IsInventoryBlocked && _profile.Side != EPlayerSide.Savage;
            _tab.SetInteractable(available);
            _page.gameObject.SetActive(false);

            if (!available && controller.LastSelectedTab == TAB_ID)
            {
                controller.LastSelectedTab = EInventoryTab.Gear;
            }
        }

        private void CreateTab(InventoryScreen screen)
        {
            Tab skills = screen._tabDictionary[EInventoryTab.Skills];
            _tab = Instantiate(skills, skills.transform.parent, false);
            _tab.name = TAB_NAME;

            foreach (LocalizedText localized in _tab.GetComponentsInChildren<LocalizedText>(true))
            {
                localized.enabled = false;
            }

            foreach (TextMeshProUGUI label in _tab.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                label.text = "CUSTOMIZATION";
            }

            SetIcon(_tab);

            Dictionary<EInventoryTab, Tab> tabs = new Dictionary<EInventoryTab, Tab>();
            foreach (KeyValuePair<EInventoryTab, Tab> pair in screen._tabDictionary)
            {
                if (pair.Key == EInventoryTab.Skills)
                {
                    tabs.Add(TAB_ID, _tab);
                }
                tabs.Add(pair.Key, pair.Value);
            }
            screen._tabDictionary = tabs;

            _prestigeTab = tabs.ContainsKey(EInventoryTab.Prestige) ? tabs[EInventoryTab.Prestige] : null;
            _prestigeWasActive = _prestigeTab != null && _prestigeTab.gameObject.activeSelf;
            FitTabRow(tabs);

            _page = UiHelpers.Rect("ImprovedCustomizationUICustomizationPage", screen._skillsAndMasteringScreen.transform.parent);
            _page.anchorMin = Vector2.zero;
            _page.anchorMax = Vector2.one;
            _page.offsetMin = new Vector2(0f, 35f);
            _page.offsetMax = new Vector2(0f, -45f);
        }

        private void SetIcon(Tab tab)
        {
            _icon = LoadIcon("tab_icon_customisation.png", out _iconTexture);
            _selectedIcon = LoadIcon("tab_icon_customisation_selected.png", out _selectedIconTexture);

            SetIconIn(tab._normalVersion, _icon);
            SetIconIn(tab._selectedVersion, _selectedIcon != null ? _selectedIcon : _icon);
        }

        private static void SetIconIn(GameObject version, Sprite sprite)
        {
            if (version == null || sprite == null)
            {
                return;
            }

            foreach (Image image in version.GetComponentsInChildren<Image>(true))
            {
                if (image.name == "Icon")
                {
                    image.sprite = sprite;
                    image.preserveAspect = true;
                }
            }
        }

        private static Sprite LoadIcon(string file, out Texture2D texture)
        {
            texture = null;
            using (Stream resource = typeof(CustomizationTab).Assembly.GetManifestResourceStream("ImprovedCustomizationUI." + file))
            {
                if (resource == null)
                {
                    Plugin.Log.LogWarning("[ImprovedCustomizationUI] customization tab icon " + file + " missing, keeping the Skills icon");
                    return null;
                }

                MemoryStream bytes = new MemoryStream();
                resource.CopyTo(bytes);
                texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!texture.LoadImage(bytes.ToArray()))
                {
                    Plugin.Log.LogWarning("[ImprovedCustomizationUI] customization tab icon " + file + " unreadable, keeping the Skills icon");
                    return null;
                }
            }

            return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
        }

        private static void FitTabRow(Dictionary<EInventoryTab, Tab> tabs)
        {
            float left = float.MaxValue;
            float width = 0f;
            float y = 0f;
            bool first = true;
            foreach (Tab tab in tabs.Values)
            {
                if (tab.name == TAB_NAME)
                {
                    continue;
                }

                RectTransform rect = (RectTransform)tab.transform;
                if (first)
                {
                    y = rect.anchoredPosition.y;
                    first = false;
                }
                left = Mathf.Min(left, rect.anchoredPosition.x);
                width = Mathf.Max(width, rect.rect.width);
            }

            if (width <= 0f)
            {
                throw new InvalidOperationException("the Character screen's tab row has no size yet");
            }

            int index = 0;
            foreach (Tab tab in tabs.Values)
            {
                if (!tab.gameObject.activeSelf)
                {
                    continue;
                }

                TabElement element = tab.GetComponent<TabElement>();
                if (element != null)
                {
                    element.enabled = false;
                }

                RectTransform rect = (RectTransform)tab.transform;
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
                rect.anchoredPosition = new Vector2(left + index * (width - 26f), y);
                index++;

                foreach (TextMeshProUGUI label in tab.GetComponentsInChildren<TextMeshProUGUI>(true))
                {
                    ContentSizeFitter fitter = label.GetComponent<ContentSizeFitter>();
                    if (fitter != null)
                    {
                        fitter.enabled = false;
                    }

                    label.rectTransform.anchorMin = new Vector2(0f, 0.5f);
                    label.rectTransform.anchorMax = new Vector2(1f, 0.5f);
                    label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                    label.rectTransform.sizeDelta = new Vector2(-75f, 24f);
                    label.rectTransform.anchoredPosition = new Vector2(13.5f, 1f);
                    label.enableAutoSizing = true;
                    label.fontSizeMin = 11f;
                    label.fontSizeMax = label.fontSize;
                }
            }
        }

        private void LateUpdate()
        {
            if (_prestigeTab == null)
            {
                return;
            }

            bool active = _prestigeTab.gameObject.activeSelf;
            if (active == _prestigeWasActive)
            {
                return;
            }

            _prestigeWasActive = active;
            InventoryScreen screen = GetComponent<InventoryScreen>();
            if (screen != null)
            {
                FitTabRow(new Dictionary<EInventoryTab, Tab>(screen._tabDictionary));
            }
        }

        public void Show()
        {
            if (_closing || _profile == null || !_tab.Interactable)
            {
                return;
            }

            _page.gameObject.SetActive(true);
            if (_view == null)
            {
                _view = new AppearanceView(_page, _profile);
                _loading = Load(_view);
            }
        }

        private async Task Load(AppearanceView view)
        {
            try
            {
                await view.Load();
                if (_view == view)
                {
                    _observed = view.Selection();
                }
            }
            catch (Exception error)
            {
                Plugin.Log.LogError("[ImprovedCustomizationUI] customization page failed to load: " + error);
                Message(error.Message, true);
            }
        }

        private void Update()
        {
            if (_closing || _view == null || !_page.gameObject.activeInHierarchy)
            {
                return;
            }

            _view.Tick();

            if (_flushing || !_view.Ready)
            {
                return;
            }

            AppearanceChoice selected = _view.Selection();
            if (!selected.SameAs(_observed))
            {
                _observed = selected;
                _changedAt = Time.unscaledTime;
            }

            bool settled = Time.unscaledTime - _changedAt >= SAVE_DELAY;
            bool idle = _saving == null || _saving.IsCompleted;
            if (settled && idle && IsDirty())
            {
                _saving = Save();
            }
        }

        private bool IsDirty()
        {
            return _view != null && _view.SelectionReady && _profile != null && _view.Selection().NeedsSaving(_view.Confirmed());
        }

        private async Task<bool> Save()
        {
            if (!IsDirty())
            {
                return true;
            }

            AppearanceView view = _view;
            Profile profile = _profile;
            AppearanceChoice choice = view.Selection();
            AppearanceChoice confirmed = view.Confirmed();
            UpperBodySuit upper = view.PickedUpper();
            LowerBodySuit lower = view.PickedLower();

            try
            {
                if (_session == null || _session.Profile != profile)
                {
                    throw new InvalidOperationException("The active character changed. Reopen Customization.");
                }

                Message("SAVING...", false);

                MongoID headId = new MongoID(choice.Head);
                MongoID voiceId = new MongoID(choice.Voice);
                List<AvailableCustomization> changes = new List<AvailableCustomization>();
                changes.Add(new AvailableCustomization(headId, ECustomizationType.Head));
                changes.Add(new AvailableCustomization(voiceId, ECustomizationType.Voice));

                bool sendUpper = upper != null && choice.Upper != confirmed.Upper;
                bool sendLower = lower != null && choice.Lower != confirmed.Lower;
                if (sendUpper)
                {
                    changes.Add(new AvailableCustomization(upper.Id, ECustomizationType.Suite));
                }
                if (sendLower)
                {
                    changes.Add(new AvailableCustomization(lower.Id, ECustomizationType.Suite));
                }

                IResult result = await _session.ApplyCustomization(changes);
                if (result == null || result.Failed)
                {
                    string reason = result != null ? result.Error : "no answer";
                    throw new InvalidOperationException(reason);
                }

                profile.Customization[EBodyModelPart.Head] = headId;
                profile.Customization[EBodyModelPart.Voice] = voiceId;
                if (sendUpper)
                {
                    upper.SetClothingsToProfile(profile.Customization);
                }
                if (sendLower)
                {
                    lower.SetClothingsToProfile(profile.Customization);
                }

                if (_view == view)
                {
                    Message("", false);
                }
                return true;
            }
            catch (Exception error)
            {
                Plugin.Log.LogWarning("[ImprovedCustomizationUI] appearance not saved: " + error.Message);
                if (_view == view)
                {
                    view.Restore();
                    _observed = view.Confirmed();
                    Message("Appearance was not saved. " + error.Message, true);
                }
                return false;
            }
        }

        public async Task<bool> Flush()
        {
            _flushing = true;
            AppearanceView view = _view;
            try
            {
                await _loading;

                if (view != null)
                {
                    view.SetInteractable(false);
                }

                if (_saving != null && !await _saving)
                {
                    _saving = null;
                    return false;
                }

                if (IsDirty())
                {
                    _saving = Save();
                    return await _saving;
                }

                return true;
            }
            finally
            {
                _flushing = false;
                if (_view == view && view != null)
                {
                    view.SetInteractable(true);
                }
            }
        }

        public async Task<bool> TryHide()
        {
            if (!await Flush())
            {
                return false;
            }

            if (_page != null)
            {
                _page.gameObject.SetActive(false);
            }
            ReleaseView();
            return true;
        }

        private void Message(string text, bool isError)
        {
            if (_view != null && _view.Status != null)
            {
                _view.Status.text = text;
                _view.Status.color = isError ? new Color(0.9f, 0.3f, 0.3f) : new Color(0.85f, 0.85f, 0.85f);
            }
            else if (isError)
            {
                PreloaderUI.Instance.ShowErrorScreen("Customization", text);
            }
        }

        public void Close()
        {
            _closing = true;
            ReleaseView();
            if (_page != null)
            {
                _page.gameObject.SetActive(false);
            }
        }

        private void ReleaseView()
        {
            if (_view != null)
            {
                _view.Dispose();
                _view = null;
            }
        }

        private void OnDestroy()
        {
            Close();
            if (_page != null)
            {
                Destroy(_page.gameObject);
            }
            if (_tab != null)
            {
                Destroy(_tab.gameObject);
            }
            if (_icon != null)
            {
                Destroy(_icon);
            }
            if (_iconTexture != null)
            {
                Destroy(_iconTexture);
            }
            if (_selectedIcon != null)
            {
                Destroy(_selectedIcon);
            }
            if (_selectedIconTexture != null)
            {
                Destroy(_selectedIconTexture);
            }
        }
    }
}
