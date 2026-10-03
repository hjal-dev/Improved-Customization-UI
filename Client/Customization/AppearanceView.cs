using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Arena.UI;
using Comfort.Common;
using EFT;
using EFT.Customization;
using EFT.InventoryLogic;
using EFT.UI;
using PlayerIcons;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using ImprovedCustomizationUI.Customization.Preview;
using ImprovedCustomizationUI.Ragman;
using ImprovedCustomizationUI.UI;

namespace ImprovedCustomizationUI.Customization
{
    public class AppearanceView : IDisposable
    {
        private static readonly Dictionary<HeadSelectionState, AppearanceView> _owners = new Dictionary<HeadSelectionState, AppearanceView>();

        private const string CLONE_NAME = "ImprovedCustomizationUICustomization";

        private readonly Transform _host;
        private readonly Profile _profile;
        private readonly HashSet<Task> _pending = new HashSet<Task>();

        private EftAccountSideSelectionScreen _screen;
        private List<CustomizationHead> _heads;
        private List<CustomizationPlayerVoice> _voices;

        private List<UpperBodySuit> _upperSuits;
        private List<LowerBodySuit> _lowerSuits;
        private DropDownBox _upperPicker;
        private DropDownBox _lowerPicker;
        private GameObject _upperRow;
        private GameObject _lowerRow;

        private PreviewCamera _camera;

        private PreviewAnimations _animations;
        private PreviewControls _controls;
        private PreviewSounds _sounds;

        private int _shownHead = -1;
        private int _shownUpper = -1;
        private int _shownLower = -1;

        private int _previewGeneration;
        private bool _disposed;
        private bool _ready;
        private Task _load = Task.CompletedTask;

        public TextMeshProUGUI Status;

        public HeadSelectionState Head
        {
            get { return _screen != null ? _screen._headSelectionState : null; }
        }

        public bool Ready
        {
            get { return _ready && !_disposed && Head._preview.PlayerModelView.LoadingComplete; }
        }

        public bool SelectionReady
        {
            get { return _ready && !_disposed; }
        }

        public static bool TryGet(HeadSelectionState head, out AppearanceView view)
        {
            return _owners.TryGetValue(head, out view);
        }

        public AppearanceView(Transform host, Profile profile)
        {
            _host = host;
            _profile = profile;
        }

        public Task Load()
        {
            _load = Initialize();
            return _load;
        }

        private async Task Initialize()
        {
            EftAccountSideSelectionScreen source = FindCreationScreen();
            if (source == null)
            {
                throw new InvalidOperationException("EFT's appearance screen isn't loaded.");
            }

            await ClothingRules.SyncWithServer();
            if (_disposed)
            {
                return;
            }

            CollectChoices();

            _screen = UnityEngine.Object.Instantiate(source, _host, false);
            _screen.name = CLONE_NAME;
            _screen.gameObject.SetActive(false);
            _screen.enabled = false;
            RemoveInheritedPreviewModels(_screen);
            RemoveInheritedFaceCards(_screen._headSelectionState);

            RectTransform screenRect = (RectTransform)_screen.transform;
            screenRect.anchorMin = Vector2.zero;
            screenRect.anchorMax = Vector2.one;
            screenRect.offsetMin = Vector2.zero;
            screenRect.offsetMax = Vector2.zero;

            _screen.gameObject.SetActive(true);
            _screen._canvasGroup.alpha = 1f;
            _screen._canvasGroup.interactable = true;
            _screen._canvasGroup.blocksRaycasts = true;

            _screen._nextButton.gameObject.SetActive(false);
            _screen._backButton.gameObject.SetActive(false);
            _screen._sideSelectionState.StateCanvasGroup.gameObject.SetActive(false);
            HideChild(_screen.transform, "SideDescriptions");
            HideChild(_screen.transform, "FaceSelection/AuthorizationText");

            HeadSelectionState head = _screen._headSelectionState;
            _owners.Add(head, this);

            foreach (PlayerProfilePreview preview in _screen.GetComponentsInChildren<PlayerProfilePreview>(true))
            {
                if (preview != head._preview)
                {
                    preview.gameObject.SetActive(false);
                }
            }

            head.StateCanvasGroup.gameObject.SetActive(true);
            head.StateCanvasGroup.alpha = 0f;
            head.StateCanvasGroup.interactable = false;

            CreateProfileOperation.PreliminaryProfileData data = new CreateProfileOperation.PreliminaryProfileData();
            data.Side = _profile.Side;
            data.Nickname = _profile.Info.Nickname;
            data.HeadId = _profile.Customization[EBodyModelPart.Head];
            data.VoiceId = _profile.Customization[EBodyModelPart.Voice];

            Dictionary<EPlayerSide, Profile> previews = new Dictionary<EPlayerSide, Profile>();
            previews[_profile.Side] = _profile.Clone();

            head.Init(data, previews, new PlayerIconCreatorMaterialChanger(_screen._materialSettings), _profile.Info.Nickname);
            head._nicknameField.gameObject.SetActive(false);
            _camera = new PreviewCamera(head);
            _camera.Attach();
            HookVoicePreviewButton(head);
            CreateClothingPickers(head);
            CreatePreviewControls(head);
            CreateStatus(head);

            await head.ShowState();
            if (_disposed)
            {
                return;
            }

            head._nicknameField.gameObject.SetActive(false);
            RestoreVoice();
            ShowClothingPickers();

            RectTransform faceIcon = FindFaceIcon(head);
            if (faceIcon != null)
            {
                _iconColour = faceIcon.GetComponent<Image>().color;
            }
            CreateGearToggle(faceIcon);
            CreateLightsToggle(faceIcon);
            CreateWholeBodyToggle(faceIcon);
            CreatePhotoToggle(faceIcon);
            _shownHead = head._selectedHeadIndex;
            head.StateCanvasGroup.alpha = 1f;
            head.StateCanvasGroup.interactable = true;
            head.StateCanvasGroup.blocksRaycasts = true;
            Status.text = "";
            Status.transform.SetAsLastSibling();
            _ready = true;
        }

        private static EftAccountSideSelectionScreen FindCreationScreen()
        {
            foreach (EftAccountSideSelectionScreen screen in Resources.FindObjectsOfTypeAll<EftAccountSideSelectionScreen>())
            {
                if (screen.gameObject.scene.IsValid() && screen.name != CLONE_NAME && screen.name != "CampaignCustomization")
                {
                    return screen;
                }
            }
            return null;
        }

        private void CollectChoices()
        {
            CustomizationSolver solver = Singleton<CustomizationSolver>.Instance;
            _heads = new List<CustomizationHead>();
            _voices = new List<CustomizationPlayerVoice>();

            foreach (CustomizationHead owned in solver.GetAvailableHeads(_profile.Side))
            {
                _heads.Add(owned);
            }
            foreach (CustomizationPlayerVoice owned in solver.GetAvailableVoices(_profile.Side))
            {
                _voices.Add(owned);
            }

            MongoID currentHead = _profile.Customization[EBodyModelPart.Head];
            if (!ContainsHead(currentHead) && solver._headTemplates.TryGetValue(currentHead, out CustomizationHead wornHead))
            {
                _heads.Insert(0, wornHead);
            }

            MongoID currentVoice = _profile.Customization[EBodyModelPart.Voice];
            if (!ContainsVoice(currentVoice) && solver._voices.TryGetValue(currentVoice, out CustomizationPlayerVoice wornVoice))
            {
                _voices.Insert(0, wornVoice);
            }

            if (_heads.Count == 0 || _voices.Count == 0)
            {
                throw new InvalidOperationException("No heads or voices are available for this faction.");
            }

            _upperSuits = new List<UpperBodySuit>();
            _lowerSuits = new List<LowerBodySuit>();
            AddOwnedSuits(solver, _profile.Side);

            EPlayerSide otherSide = ClothingRules.OtherSide(_profile.Side);
            if (ClothingRules.MayUseSide(_profile, otherSide))
            {
                AddOwnedSuits(solver, otherSide);
            }

            if (FindWornUpper() < 0)
            {
                _upperSuits.Insert(0, null);
            }
            if (FindWornLower() < 0)
            {
                _lowerSuits.Insert(0, null);
            }
        }

        private void AddOwnedSuits(CustomizationSolver solver, EPlayerSide side)
        {
            foreach (CustomizationSuite suite in solver.GetAvailableSuites(side))
            {
                if (suite is UpperBodySuit upper && !_upperSuits.Contains(upper))
                {
                    _upperSuits.Add(upper);
                }
                else if (suite is LowerBodySuit lower && !_lowerSuits.Contains(lower))
                {
                    _lowerSuits.Add(lower);
                }
            }
        }

        private int FindWornUpper()
        {
            for (int i = 0; i < _upperSuits.Count; i++)
            {
                UpperBodySuit suite = _upperSuits[i];
                if (suite != null && suite.Body == _profile.Customization[EBodyModelPart.Body]
                    && suite.Hands == _profile.Customization[EBodyModelPart.Hands])
                {
                    return i;
                }
            }
            return -1;
        }

        private int FindWornLower()
        {
            for (int i = 0; i < _lowerSuits.Count; i++)
            {
                LowerBodySuit suite = _lowerSuits[i];
                if (suite != null && suite.Feet == _profile.Customization[EBodyModelPart.Feet])
                {
                    return i;
                }
            }
            return -1;
        }

        private int WornUpperIndex()
        {
            int index = FindWornUpper();
            return index >= 0 ? index : 0;
        }

        private int WornLowerIndex()
        {
            int index = FindWornLower();
            return index >= 0 ? index : 0;
        }

        private bool ContainsHead(MongoID id)
        {
            foreach (CustomizationHead head in _heads)
            {
                if (head.Id == id)
                {
                    return true;
                }
            }
            return false;
        }

        private bool ContainsVoice(MongoID id)
        {
            foreach (CustomizationPlayerVoice voice in _voices)
            {
                if (voice.Id == id)
                {
                    return true;
                }
            }
            return false;
        }

        public void PrepareSelectors()
        {
            HeadSelectionState head = Head;
            head._headTemplates = new List<KeyValuePair<MongoID, CustomizationHead>>();
            head._voiceTemplates = new List<KeyValuePair<MongoID, CustomizationPlayerVoice>>();

            foreach (CustomizationHead choice in _heads)
            {
                head._headTemplates.Add(new KeyValuePair<MongoID, CustomizationHead>(choice.Id, choice));
            }
            foreach (CustomizationPlayerVoice choice in _voices)
            {
                head._voiceTemplates.Add(new KeyValuePair<MongoID, CustomizationPlayerVoice>(choice.Id, choice));
            }

            head._voices.Clear();
            head.PrepareFaceSelector();
            head.PrepareVoiceSelector();
        }

        private void HookVoicePreviewButton(HeadSelectionState head)
        {
            Transform voice = head._voiceSelector.transform.parent;
            Transform speakerIcon = voice != null ? voice.Find("Icon") : null;
            if (speakerIcon != null)
            {
                Button play = speakerIcon.GetComponent<Button>();
                if (play == null)
                {
                    play = speakerIcon.gameObject.AddComponent<Button>();
                }
                play.targetGraphic = speakerIcon.GetComponent<Image>();
                play.onClick.RemoveAllListeners();
                play.onClick.AddListener(PreviewVoiceClicked);
            }
        }

        private void CreateClothingPickers(HeadSelectionState head)
        {
            RectTransform voiceRow = (RectTransform)head._voiceSelector.transform.parent;
            LogRow(voiceRow);

            float step = ((RectTransform)head._voiceSelector.transform).rect.height + ROW_GAP;
            Sprite shirt = UiHelpers.LoadSprite("icon_upper_body.png");
            Sprite trousers = UiHelpers.LoadSprite("icon_lower_body.png");
            if (shirt == null || trousers == null)
            {
                InventoryClothingSelectionPanel gearPanel = FindGearClothingPanel();
                if (shirt == null && gearPanel != null)
                {
                    shirt = FindDropDownIcon(gearPanel._upperButtonDropDown);
                }
                if (trousers == null && gearPanel != null)
                {
                    trousers = FindDropDownIcon(gearPanel._lowerButtonDropDown);
                }
            }

            _upperRow = CopyPickerRow(voiceRow, "UpperBodyPicker", "UPPER", shirt, step, UpperIconClicked, out _upperPicker);
            _lowerRow = CopyPickerRow(voiceRow, "LowerBodyPicker", "LOWER", trousers, step * 2f, LowerIconClicked, out _lowerPicker);

            _voiceRow = voiceRow;

            if (voiceRow.parent.GetComponent<LayoutGroup>() == null)
            {
                _lowerRow.transform.SetAsLastSibling();
                _upperRow.transform.SetAsLastSibling();
                voiceRow.SetAsLastSibling();
            }
        }

        private const float ROW_GAP = 20f;

        private GameObject CopyPickerRow(RectTransform voiceRow, string name, string caption, Sprite iconSprite, float drop, UnityAction iconClicked, out DropDownBox picker)
        {
            GameObject row = UnityEngine.Object.Instantiate(voiceRow.gameObject, voiceRow.parent, false);
            row.name = name;

            picker = row.GetComponentInChildren<DropDownBox>(true);
            if (picker == null)
            {
                throw new InvalidOperationException("The voice picker has no dropdown to copy.");
            }

            if (voiceRow.parent.GetComponent<LayoutGroup>() != null)
            {
                LayoutElement ignore = row.GetComponent<LayoutElement>();
                if (ignore == null)
                {
                    ignore = row.AddComponent<LayoutElement>();
                }
                ignore.ignoreLayout = true;
            }

            RectTransform rect = (RectTransform)row.transform;
            rect.anchorMin = voiceRow.anchorMin;
            rect.anchorMax = voiceRow.anchorMax;
            rect.pivot = voiceRow.pivot;
            rect.sizeDelta = voiceRow.sizeDelta;
            rect.anchoredPosition = voiceRow.anchoredPosition - new Vector2(0f, drop);

            Transform icon = row.transform.Find("Icon");
            if (icon != null)
            {
                Image image = icon.GetComponent<Image>();

                if (iconSprite != null && image != null)
                {
                    image.sprite = iconSprite;
                    image.preserveAspect = true;
                    image.raycastTarget = true;

                    Button iconButton = icon.GetComponent<Button>();
                    if (iconButton == null)
                    {
                        iconButton = icon.gameObject.AddComponent<Button>();
                    }
                    iconButton.enabled = true;
                    iconButton.targetGraphic = image;
                    iconButton.onClick.RemoveAllListeners();
                    iconButton.onClick.AddListener(iconClicked);
                    UiButtonSounds.AddTo(icon.gameObject);
                }
                else
                {
                    icon.gameObject.SetActive(false);
                    TextMeshProUGUI captionText = AddCaption(row.transform, (RectTransform)icon, caption, picker);
                    captionText.raycastTarget = true;
                    Button captionButton = captionText.gameObject.AddComponent<Button>();
                    captionButton.targetGraphic = captionText;
                    captionButton.onClick.AddListener(iconClicked);
                    UiButtonSounds.AddTo(captionText.gameObject);
                }
            }

            return row;
        }

        private const string GEAR_CLOTHING_DROPDOWN = "ClothingSelector";

        public static InventoryClothingSelectionPanel FindGearClothingPanel()
        {
            foreach (InventoryClothingSelectionPanel panel in Resources.FindObjectsOfTypeAll<InventoryClothingSelectionPanel>())
            {
                if (!panel.gameObject.scene.IsValid() || panel._upperButtonDropDown == null || panel._lowerButtonDropDown == null)
                {
                    continue;
                }

                if (panel._upperButtonDropDown.name == GEAR_CLOTHING_DROPDOWN && panel._lowerButtonDropDown.name == GEAR_CLOTHING_DROPDOWN)
                {
                    return panel;
                }
            }

            Plugin.Log.LogWarning("[ImprovedCustomizationUI] Gear tab clothing panel not found, clothing rows get word captions");
            return null;
        }

        private static Sprite FindDropDownIcon(DropDownBox dropDown)
        {
            if (dropDown == null || dropDown.transform.parent == null)
            {
                return null;
            }

            Image fallback = null;
            foreach (Image image in dropDown.transform.parent.GetComponentsInChildren<Image>(true))
            {
                if (image.sprite == null || image.transform.IsChildOf(dropDown.transform))
                {
                    continue;
                }

                if (image.name.IndexOf("icon", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Plugin.Log.LogInfo("[ImprovedCustomizationUI] clothing icon for " + dropDown.name + ": " + image.name + " (" + image.sprite.name + ")");
                    return image.sprite;
                }

                if (fallback == null)
                {
                    fallback = image;
                }
            }

            if (fallback != null)
            {
                Plugin.Log.LogInfo("[ImprovedCustomizationUI] clothing icon for " + dropDown.name + ": " + fallback.name + " (" + fallback.sprite.name + ")");
                return fallback.sprite;
            }
            return null;
        }

        private static TextMeshProUGUI AddCaption(Transform row, RectTransform icon, string caption, DropDownBox picker)
        {
            RectTransform rect = UiHelpers.Rect("Caption", row);
            rect.anchorMin = icon.anchorMin;
            rect.anchorMax = icon.anchorMax;
            rect.pivot = new Vector2(1f, 0.5f);
            float iconRight = icon.anchoredPosition.x + icon.rect.width * (1f - icon.pivot.x);
            float iconMiddle = icon.anchoredPosition.y + icon.rect.height * (0.5f - icon.pivot.y);
            rect.sizeDelta = new Vector2(70f, icon.rect.height);
            rect.anchoredPosition = new Vector2(iconRight, iconMiddle);

            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (picker._currentValueText != null)
            {
                text.font = picker._currentValueText.font;
                text.color = picker._currentValueText.color;
            }
            text.fontSize = 15f;
            text.alignment = TextAlignmentOptions.MidlineRight;
            text.raycastTarget = false;
            text.text = caption;
            return text;
        }

        private void UpperIconClicked()
        {
            if (_camera != null && _ready && !_disposed)
            {
                _camera.GoTo(PlayerProfilePreview.ECameraViewType.FullBody, 0f);
            }
        }

        private void LowerIconClicked()
        {
            if (_camera != null && _ready && !_disposed)
            {
                _camera.GoTo(PlayerProfilePreview.ECameraViewType.FullBody, PreviewCamera.LEGS_DROP);
            }
        }

        private void CreatePreviewControls(HeadSelectionState head)
        {
            _animations = new PreviewAnimations(head._preview);
            _sounds = new PreviewSounds(head._preview, _host);
            Transform spot = _screen._backButton.transform.parent;
            _controls = new PreviewControls(spot, _screen._backButton, _camera, _animations);

            RectTransform spotRect = (RectTransform)spot;
            Plugin.Log.LogInfo("[ImprovedCustomizationUI] preview controls under '" + spot.name + "' at " + spotRect.anchoredPosition.ToString("F0")
                + " size " + spotRect.rect.size.ToString("F0") + ", layout " + (spot.GetComponent<LayoutGroup>() != null ? spot.GetComponent<LayoutGroup>().GetType().Name : "none"));
        }

        private static void LogRow(RectTransform row)
        {
            string children = "";
            for (int i = 0; i < row.childCount; i++)
            {
                children += (i > 0 ? ", " : "") + row.GetChild(i).name;
            }
            Plugin.Log.LogInfo("[ImprovedCustomizationUI] voice row '" + row.name + "' " + row.rect.width.ToString("0") + "x"
                + row.rect.height.ToString("0") + ", children: " + children);
        }

        private void ShowClothingPickers()
        {
            List<string> upperNames = new List<string>();
            foreach (UpperBodySuit suite in _upperSuits)
            {
                upperNames.Add(SuiteName(suite));
            }

            List<string> lowerNames = new List<string>();
            foreach (LowerBodySuit suite in _lowerSuits)
            {
                lowerNames.Add(SuiteName(suite));
            }

            _upperPicker.Show(upperNames);
            _lowerPicker.Show(lowerNames);
            _upperPicker.UpdateValue(WornUpperIndex(), false);
            _lowerPicker.UpdateValue(WornLowerIndex(), false);
            _shownUpper = _upperPicker.CurrentIndex;
            _shownLower = _lowerPicker.CurrentIndex;
        }

        private static string SuiteName(CustomizationSuite suite)
        {
            if (suite == null)
            {
                return "Current outfit";
            }
            return suite.NameLocalizationKey.Localized();
        }

        public void Tick()
        {
            if (!_ready || _disposed)
            {
                return;
            }

            HeadSelectionState head = Head;

            if (head._selectedHeadIndex != _shownHead)
            {
                _shownHead = head._selectedHeadIndex;
                _camera.MoveTo(PlayerProfilePreview.ECameraViewType.Head, 0f);
            }

            bool upperChanged = _upperPicker.CurrentIndex != _shownUpper;
            bool lowerChanged = _lowerPicker.CurrentIndex != _shownLower;
            _shownUpper = _upperPicker.CurrentIndex;
            _shownLower = _lowerPicker.CurrentIndex;

            if (upperChanged || lowerChanged)
            {
                DressPreview();
                _camera.MoveTo(PlayerProfilePreview.ECameraViewType.FullBody, lowerChanged ? PreviewCamera.LEGS_DROP : 0f);
                UpdatePreview();
            }

            _camera.Tick();
            _controls.Tick();
        }

        private void DressPreview()
        {
            BodyCustomization preview = Head._previewProfile.Customization;
            UpperBodySuit upper = PickedUpper();
            LowerBodySuit lower = PickedLower();

            if (upper != null)
            {
                upper.SetClothingsToProfile(preview);
            }
            else
            {
                preview[EBodyModelPart.Body] = _profile.Customization[EBodyModelPart.Body];
                preview[EBodyModelPart.Hands] = _profile.Customization[EBodyModelPart.Hands];
            }

            if (lower != null)
            {
                lower.SetClothingsToProfile(preview);
            }
            else
            {
                preview[EBodyModelPart.Feet] = _profile.Customization[EBodyModelPart.Feet];
            }
        }

        public UpperBodySuit PickedUpper()
        {
            int index = _upperPicker.CurrentIndex;
            return index >= 0 && index < _upperSuits.Count ? _upperSuits[index] : null;
        }

        public LowerBodySuit PickedLower()
        {
            int index = _lowerPicker.CurrentIndex;
            return index >= 0 && index < _lowerSuits.Count ? _lowerSuits[index] : null;
        }

        private static bool _showGear;

        private readonly List<GameObject> _gearModels = new List<GameObject>();

        private Image _gearToggleImage;

        private void CollectGearModels()
        {
            _gearModels.Clear();
            PlayerBody body = Head._preview.PlayerModelView.PlayerBody;
            if (body == null || body.SlotViews == null)
            {
                return;
            }

            foreach (EquipmentSlot slot in (EquipmentSlot[])Enum.GetValues(typeof(EquipmentSlot)))
            {
                if (!body.SlotViews.ContainsKey(slot))
                {
                    continue;
                }

                GameObject model = body.SlotViews.GetByKey(slot).ParentedModel.Value;
                if (model == null)
                {
                    continue;
                }

                if (model.activeSelf || Head._hiddenSlots.Contains(slot))
                {
                    _gearModels.Add(model);
                }
            }
        }

        private void ApplyGearVisibility()
        {
            foreach (GameObject model in _gearModels)
            {
                if (model != null && model.activeSelf != _showGear)
                {
                    model.SetActive(_showGear);
                }
            }

            if (_gearToggleImage != null)
            {
                _gearToggleImage.color = _showGear ? _iconColour : Dimmed(_iconColour);
            }
        }

        private Color _iconColour = Color.white;

        private static Color Dimmed(Color colour)
        {
            return new Color(colour.r, colour.g, colour.b, colour.a * 0.35f);
        }

        private void CreateGearToggle(RectTransform faceIcon)
        {
            Sprite sprite = UiHelpers.LoadSprite("gear_toggle.png");
            if (faceIcon == null || sprite == null)
            {
                Plugin.Log.LogWarning("[ImprovedCustomizationUI] armour toggle left out (face icon " + (faceIcon != null ? "found" : "not found")
                    + ", image " + (sprite != null ? "found" : "missing") + ")");
                return;
            }

            _gearToggleImage = CopyFaceIcon(faceIcon, 1, "GearToggle", sprite, "Show equipped gear", GearToggleClicked);
            ApplyGearVisibility();
            Plugin.Log.LogInfo("[ImprovedCustomizationUI] armour toggle under face icon '" + faceIcon.parent.name + "/" + faceIcon.name + "' "
                + faceIcon.rect.size.ToString("F0") + " at " + faceIcon.anchoredPosition.ToString("F0"));
        }

        private const float GEAR_TOGGLE_GAP = 24f;

        private RectTransform _voiceRow;

        private static GameObject FindFaceGrid(HeadSelectionState head)
        {
            Transform level = head._faceCardsViewPort;
            while (level != null)
            {
                if (level.GetComponent<ScrollRect>() != null)
                {
                    return level.gameObject;
                }
                level = level.parent;
            }
            return head._faceCardsViewPort != null && head._faceCardsViewPort.parent != null ? head._faceCardsViewPort.parent.gameObject : null;
        }

        private void GearToggleClicked()
        {
            _showGear = !_showGear;
            ApplyGearVisibility();
        }

        private static Image CopyFaceIcon(RectTransform faceIcon, int slot, string name, Sprite sprite, string tooltip, UnityAction clicked)
        {
            GameObject copy = UnityEngine.Object.Instantiate(faceIcon.gameObject, faceIcon.parent, false);
            copy.name = name;
            copy.SetActive(true);
            foreach (Transform child in copy.transform)
            {
                UnityEngine.Object.Destroy(child.gameObject);
            }

            RectTransform rect = (RectTransform)copy.transform;
            rect.anchoredPosition = faceIcon.anchoredPosition - new Vector2(0f, (faceIcon.rect.height + GEAR_TOGGLE_GAP) * slot);

            Image image = copy.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = true;

            Button button = copy.GetComponent<Button>();
            if (button == null)
            {
                button = copy.AddComponent<Button>();
            }
            button.enabled = true;
            button.targetGraphic = image;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(clicked);
            UiButtonSounds.AddTo(copy);

            try
            {
                HoverTooltipArea hover = copy.GetComponent<HoverTooltipArea>();
                if (hover == null)
                {
                    hover = copy.AddComponent<HoverTooltipArea>();
                }
                hover.SetMessageText(tooltip, true);
            }
            catch (Exception error)
            {
                Plugin.Log.LogWarning("[ImprovedCustomizationUI] " + name + " tooltip: " + error.Message);
            }

            return image;
        }

        private Image _lightsToggleImage;
        private PreviewLighting _lighting;

        private void CreateLightsToggle(RectTransform faceIcon)
        {
            HeadSelectionState head = Head;
            if (head != null && head._preview != null && head._preview._camera != null)
            {
                _lighting = new PreviewLighting(head._preview);
            }

            Sprite sprite = UiHelpers.LoadSprite("icon_lights.png");
            if (faceIcon != null && sprite != null)
            {
                _lightsToggleImage = CopyFaceIcon(faceIcon, 2, "LightsToggle", sprite, "Extra light", LightsToggleClicked);
            }

            Plugin.LightingChanged += LightingChanged;
            ApplyLights();
        }

        private void LightsToggleClicked()
        {
            Plugin.PreviewLights.Value = !Plugin.PreviewLights.Value;
        }

        private void LightingChanged()
        {
            if (!_disposed)
            {
                ApplyLights();
            }
        }

        private void ApplyLights()
        {
            if (_lighting != null)
            {
                _lighting.Apply();
            }
            if (_lightsToggleImage != null)
            {
                _lightsToggleImage.color = Plugin.PreviewLights.Value ? _iconColour : Dimmed(_iconColour);
            }
        }

        private static bool _wholeBody;
        private Image _wholeBodyImage;
        private RawImage _previewImage;
        private Material _previewMask;
        private Vector2[] _viewportSizes;
        private PhotoMode _photo;
        private Image _photoImage;

        private void CreateWholeBodyToggle(RectTransform faceIcon)
        {
            Sprite sprite = UiHelpers.LoadSprite("icon_full_body_view.png");
            if (faceIcon == null || sprite == null)
            {
                return;
            }

            PlayerProfilePreview preview = Head._preview;
            _previewImage = preview._transform.GetComponent<RawImage>();
            _previewMask = _previewImage != null ? _previewImage.material : null;
            _viewportSizes = new Vector2[preview._viewPorts.Count];
            for (int i = 0; i < preview._viewPorts.Count; i++)
            {
                RectTransform port = preview._viewPorts[i].ViewportPosition;
                _viewportSizes[i] = port != null ? port.sizeDelta : Vector2.zero;
            }

            _wholeBodyImage = CopyFaceIcon(faceIcon, 3, "WholeBodyToggle", sprite, "Full body view", WholeBodyClicked);
            ApplyWholeBody();
        }

        private void WholeBodyClicked()
        {
            _wholeBody = !_wholeBody;
            ApplyWholeBody();
        }

        private void ApplyWholeBody()
        {
            PlayerProfilePreview preview = Head._preview;
            if (_previewImage != null)
            {
                _previewImage.material = _wholeBody ? null : _previewMask;
            }

            RectTransform parent = preview._transform.parent as RectTransform;
            for (int i = 0; i < preview._viewPorts.Count; i++)
            {
                RectTransform port = preview._viewPorts[i].ViewportPosition;
                if (port == null)
                {
                    continue;
                }
                Vector2 original = _viewportSizes[i];
                if (!_wholeBody || parent == null)
                {
                    port.sizeDelta = original;
                    continue;
                }
                float fullHeight = parent.rect.height;
                float shownHeight = fullHeight + original.y;
                float width = shownHeight > 1f ? original.x * fullHeight / shownHeight : original.x;
                port.sizeDelta = new Vector2(width, 0f);
            }

            if (_wholeBodyImage != null)
            {
                _wholeBodyImage.color = _wholeBody ? _iconColour : Dimmed(_iconColour);
            }
            if (_camera != null)
            {
                _camera.SetWholeBody(_wholeBody);
            }
        }

        private void CreatePhotoToggle(RectTransform faceIcon)
        {
            Sprite sprite = UiHelpers.LoadSprite("icon_photo_mode.png");
            if (faceIcon == null || sprite == null)
            {
                return;
            }

            _photoImage = CopyFaceIcon(faceIcon, 4, "PhotoModeToggle", sprite, "Photo mode", PhotoClicked);
            GameObject grid = FindFaceGrid(Head);
            RectTransform spot = grid != null ? (RectTransform)grid.transform : (RectTransform)_photoImage.transform;
            _photo = new PhotoMode(Head._preview, _profile.Info.Nickname, spot, _screen._backButton);
            _photoImage.color = Dimmed(_iconColour);
        }

        private void PhotoClicked()
        {
            if (_photo == null)
            {
                return;
            }
            _photo.Toggle();
            bool open = _photo.Open;
            GameObject faces = FindFaceGrid(Head);
            if (faces != null)
            {
                faces.SetActive(!open);
            }
            foreach (GameObject row in new GameObject[] { _upperRow, _lowerRow, _voiceRow != null ? _voiceRow.gameObject : null })
            {
                if (row != null)
                {
                    row.SetActive(!open);
                }
            }
            _photoImage.color = _photo.Open ? _iconColour : Dimmed(_iconColour);
        }

        private RectTransform FindFaceIcon(HeadSelectionState head)
        {
            GameObject grid = FindFaceGrid(head);
            if (grid == null)
            {
                return null;
            }

            Vector3[] corners = new Vector3[4];
            ((RectTransform)grid.transform).GetWorldCorners(corners);
            Vector3 gridTopLeft = corners[1];
            float gridHeight = corners[1].y - corners[0].y;
            float gridLeft = corners[0].x;

            RectTransform best = null;
            float bestDistance = float.MaxValue;
            foreach (Image image in _screen.GetComponentsInChildren<Image>(true))
            {
                RectTransform rect = image.rectTransform;
                if (image.sprite == null || !image.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (rect.rect.width > 100f || rect.rect.height > 100f)
                {
                    continue;
                }

                if (rect.IsChildOf(grid.transform) || (_voiceRow != null && rect.IsChildOf(_voiceRow))
                    || image == head._usecIcon || image == head._bearIcon)
                {
                    continue;
                }

                rect.GetWorldCorners(corners);
                Vector3 centre = (corners[0] + corners[2]) * 0.5f;

                if (centre.x >= gridLeft || centre.y < gridTopLeft.y - gridHeight * 0.3f || centre.y > gridTopLeft.y + gridHeight * 0.15f)
                {
                    continue;
                }

                float distance = Vector3.Distance(centre, gridTopLeft);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = rect;
                }
            }

            if (best != null)
            {
                Plugin.Log.LogInfo("[ImprovedCustomizationUI] face icon is '" + best.parent.name + "/" + best.name + "' (sprite "
                    + best.GetComponent<Image>().sprite.name + ", " + best.rect.size.ToString("F0") + ")");
            }
            return best;
        }

        public void SetInteractable(bool interactable)
        {
            if (Head != null)
            {
                Head.StateCanvasGroup.interactable = interactable;
            }
            if (_upperPicker != null)
            {
                _upperPicker.Interactable = interactable;
            }
            if (_lowerPicker != null)
            {
                _lowerPicker.Interactable = interactable;
            }
        }

        private void CreateStatus(HeadSelectionState head)
        {
            RectTransform rect = UiHelpers.Rect("AppearanceStatus", _host);
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.sizeDelta = new Vector2(650f, 32f);
            rect.anchoredPosition = new Vector2(-32f, 16f);

            Status = rect.gameObject.AddComponent<TextMeshProUGUI>();
            TextMeshProUGUI nicknameText = head._nicknameField.GetComponentInChildren<TextMeshProUGUI>(true);
            if (nicknameText != null)
            {
                Status.font = nicknameText.font;
            }
            Status.fontSize = 18f;
            Status.alignment = TextAlignmentOptions.BottomRight;
            Status.raycastTarget = false;
            Status.text = "LOADING APPEARANCE...";
        }

        private async void PreviewVoiceClicked()
        {
            try
            {
                HeadSelectionState head = Head;
                if (head != null)
                {
                    await head.PlayVoice(head._voiceSelector.CurrentIndex);
                }
            }
            catch (Exception error)
            {
                Plugin.Log.LogWarning("[ImprovedCustomizationUI] voice preview failed: " + error.Message);
            }
        }

        public AppearanceChoice Selection()
        {
            HeadSelectionState head = Head;
            string headId = head._headTemplates[head._selectedHeadIndex].Key.ToString();
            string voiceId = head._voiceTemplates[head._voiceSelector.CurrentIndex].Key.ToString();
            UpperBodySuit upper = PickedUpper();
            LowerBodySuit lower = PickedLower();
            string upperId = upper != null ? upper.Id.ToString() : "";
            string lowerId = lower != null ? lower.Id.ToString() : "";
            return new AppearanceChoice(headId, voiceId, upperId, lowerId);
        }

        public AppearanceChoice Confirmed()
        {
            int upper = FindWornUpper();
            int lower = FindWornLower();
            return new AppearanceChoice(
                _profile.Customization[EBodyModelPart.Head].ToString(),
                _profile.Customization[EBodyModelPart.Voice].ToString(),
                upper >= 0 ? _upperSuits[upper].Id.ToString() : "",
                lower >= 0 ? _lowerSuits[lower].Id.ToString() : "");
        }

        public void Restore()
        {
            if (!_ready || _disposed)
            {
                return;
            }

            HeadSelectionState head = Head;
            int index = head._headTemplates.FindIndex(pair => pair.Key == _profile.Customization[EBodyModelPart.Head]);
            if (index >= 0)
            {
                head.HeadValueChangedHandler(index, true);
                for (int i = 0; i < head._faceCards.Count; i++)
                {
                    bool selected = i == index;
                    head._faceCards[i]._toggle.SetIsOnWithoutNotify(selected);
                    head._faceCards[i].SetSelected(selected);
                }
            }

            RestoreVoice();

            _upperPicker.UpdateValue(WornUpperIndex(), false);
            _lowerPicker.UpdateValue(WornLowerIndex(), false);
        }

        private void RestoreVoice()
        {
            HeadSelectionState head = Head;
            int index = head._voiceTemplates.FindIndex(pair => pair.Key == _profile.Customization[EBodyModelPart.Voice]);
            if (index < 0)
            {
                return;
            }

            head._selectedVoiceIndex = index;
            head._voiceSelector.UpdateValue(index, false);
            head._profileData.VoiceId = _profile.Customization[EBodyModelPart.Voice];
        }

        public Task UpdatePreview()
        {
            _previewGeneration++;
            return Track(Render(_previewGeneration));
        }

        private async Task Render(int generation)
        {
            HeadSelectionState head = Head;
            await head._preview.Show(head._previewProfile);

            if (_disposed || generation != _previewGeneration || !head._preview.PlayerModelView.LoadingComplete)
            {
                return;
            }

            head._materialChanger.ChangeMaterials(head._preview.PlayerModelView.PlayerBody);
            head.HideSlots();
            CollectGearModels();
            ApplyGearVisibility();
        }

        public Task PlayVoice(int index)
        {
            return Track(Play(index));
        }

        private async Task Play(int index)
        {
            HeadSelectionState head = Head;
            if (_disposed || index < 0 || index >= head._voiceTemplates.Count)
            {
                return;
            }

            KeyValuePair<MongoID, CustomizationPlayerVoice> template = head._voiceTemplates[index];
            if (!head._voices.TryGetValue(index, out TagBank bank))
            {
                bank = await Singleton<PlayerVoiceLoader>.Instance.TakeVoice(template.Value.Name);
                if (_disposed || bank == null)
                {
                    return;
                }
                head._voices[index] = bank;
            }

            if (_disposed || index != head._voiceSelector.CurrentIndex || bank.Clips.Length == 0)
            {
                return;
            }

            head._profileData.VoiceId = template.Key;
            TaggedClip clip = bank.Clips[UnityEngine.Random.Range(0, bank.Clips.Length)];
            await Singleton<GUISounds>.Instance.ForcePlaySound(clip.Clip);
        }

        private async Task Track(Task task)
        {
            _pending.Add(task);
            try
            {
                await task;
            }
            finally
            {
                _pending.Remove(task);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (_camera != null)
            {
                _camera.Dispose();
            }
            if (_sounds != null)
            {
                _sounds.Dispose();
            }
            Plugin.LightingChanged -= LightingChanged;
            if (_lighting != null)
            {
                _lighting.Dispose();
            }
            if (_photo != null)
            {
                _photo.Dispose();
            }
            if (_screen != null)
            {
                _screen.transform.SetParent(null, false);
                _screen.gameObject.SetActive(false);
            }
            Release();
        }

        private async void Release()
        {
            try
            {
                try
                {
                    await _load;
                }
                catch (Exception error)
                {
                    Plugin.Log.LogWarning("[ImprovedCustomizationUI] appearance load ended with: " + error.Message);
                }

                try
                {
                    await Task.WhenAll(new List<Task>(_pending));
                }
                catch (Exception error)
                {
                    Plugin.Log.LogWarning("[ImprovedCustomizationUI] appearance preview ended with: " + error.Message);
                }

                if (_screen != null)
                {
                    _screen._headSelectionState.Close();
                    foreach (PlayerProfilePreview preview in _screen.GetComponentsInChildren<PlayerProfilePreview>(true))
                    {
                        preview.Close();
                    }
                }
            }
            catch (Exception error)
            {
                Plugin.Log.LogWarning("[ImprovedCustomizationUI] appearance clean-up: " + error.Message);
            }
            finally
            {
                if (_screen != null)
                {
                    _owners.Remove(_screen._headSelectionState);
                    UnityEngine.Object.Destroy(_screen.gameObject);
                }
                if (Status != null)
                {
                    UnityEngine.Object.Destroy(Status.gameObject);
                }
                if (_upperRow != null)
                {
                    UnityEngine.Object.Destroy(_upperRow);
                }
                if (_lowerRow != null)
                {
                    UnityEngine.Object.Destroy(_lowerRow);
                }
            }
        }

        private static void RemoveInheritedFaceCards(HeadSelectionState head)
        {
            foreach (FaceCardView card in head._faceCardsViewPort.GetComponentsInChildren<FaceCardView>(true))
            {
                if (card == head._faceCardPrefab)
                {
                    continue;
                }

                card.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(card.gameObject);
            }
            head._faceCards.Clear();
        }

        private static void RemoveInheritedPreviewModels(EftAccountSideSelectionScreen screen)
        {
            foreach (PlayerModelView view in screen.GetComponentsInChildren<PlayerModelView>(true))
            {
                foreach (MenuPlayerPoser model in view.GetComponentsInChildren<MenuPlayerPoser>(true))
                {
                    model.gameObject.SetActive(false);
                    PlayerBody body = model.GetComponent<PlayerBody>();
                    if (body != null)
                    {
                        body.Dispose();
                    }
                    UnityEngine.Object.Destroy(model.gameObject);
                }
            }
        }

        private static void HideChild(Transform root, string path)
        {
            Transform child = root.Find(path);
            if (child != null)
            {
                child.gameObject.SetActive(false);
            }
        }
    }
}
