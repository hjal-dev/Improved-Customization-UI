using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using ImprovedCustomizationUI.Config;
using ImprovedCustomizationUI.Ragman;

namespace ImprovedCustomizationUI
{
    [BepInPlugin("com.hj.improvedcustomizationui", "Hj's Improved Customization UI", "1.1.0")]
    public class Plugin : BaseUnityPlugin
    {
        public static ManualLogSource Log;

        private const string TAB_SECTION = "1. Customization tab";
        private const string RAGMAN_SECTION = "2. Ragman clothing";
        private const string LIGHTING_SECTION = "3. Preview lighting";
        private const string PHOTO_SECTION = "4. Photo mode";

        public static ConfigEntry<bool> EnableCustomizationTab;
        public static ConfigEntry<bool> PreviewWeaponSounds;
        public static ConfigEntry<float> PreviewWeaponSoundsVolume;

        public static ConfigEntry<bool> EnableRagmanFilters;
        public static ConfigEntry<OtherFactionClothing> OtherFaction;

        public static ConfigEntry<bool> PreviewLights;
        public static ConfigEntry<float> ExtraLightBrightness;
        public static ConfigEntry<float> ExtraLightRotationX;
        public static ConfigEntry<float> ExtraLightRotationY;
        public static ConfigEntry<float> GameLightsBrightness;
        public static ConfigEntry<float> RimLightStrength;
        public static ConfigEntry<bool> GameLightShadows;
        public static ConfigEntry<string> PhotoFolder;

        public static event Action LightingChanged;

        private void Awake()
        {
            Log = Logger;

            EnableCustomizationTab = Config.Bind(TAB_SECTION, "Enable Customization tab", true, Describe(
                "Adds 5.0's CUSTOMIZATION tab to the Character screen (heads, voices, clothing). Needs the server mod. Takes effect after a game restart.", 30));

            PreviewWeaponSounds = Config.Bind(TAB_SECTION, "Weapon sounds in preview", false, Describe(
                "Plays the weapon's own sounds (mag out, mag in, bolt...) when the preview model inspects, checks or reloads. Off by default, like 4.1's silent menu model. Works right away, no restart.", 20));

            PreviewWeaponSoundsVolume = Config.Bind(TAB_SECTION, "Weapon sounds volume", 0.7f, Describe(
                "How loud the preview's weapon sounds are, on top of the game's own volume settings.", 10, new AcceptableValueRange<float>(0f, 1f)));

            EnableRagmanFilters = Config.Bind(RAGMAN_SECTION, "Enable faction toggles and filter", true, Describe(
                "Adds 5.0's BEAR / USEC toggles and the All / Obtained / Purchasable / Unavailable filter to Ragman > Services > Tactical clothing. Takes effect after a game restart.", 20));

            OtherFaction = Config.Bind(RAGMAN_SECTION, "Other faction clothing", OtherFactionClothing.UnheardOnly, Describe(
                "Who may look at, buy and wear the other faction's clothing (Ragman and the CUSTOMIZATION tab). UnheardOnly = Unheard accounts, like 5.0. "
                + "The game tells the server, which enforces it. A server can cap it for everyone with MaxOtherFactionClothing in user\\mods\\ImprovedCustomizationUI\\config.json.", 10));

            OtherFaction.SettingChanged += delegate { SyncClothingRule(); };

            PreviewLights = Config.Bind(LIGHTING_SECTION, "Extra light", true, Describe(
                "An extra light on the CUSTOMIZATION preview, on top of the game's own preview lights. Same as the power icon on the page.", 70));

            ExtraLightBrightness = Config.Bind(LIGHTING_SECTION, "Extra light brightness", 0.85f, Describe(
                "How bright the extra light is.", 60, new AcceptableValueRange<float>(0f, 5f)));

            ExtraLightRotationX = Config.Bind(LIGHTING_SECTION, "Extra light rotation X", 25f, Describe(
                "Tilts the extra light up and down (degrees). 0 = level, positive = from above.", 50, new AcceptableValueRange<float>(-180f, 180f)));

            ExtraLightRotationY = Config.Bind(LIGHTING_SECTION, "Extra light rotation Y", -30f, Describe(
                "Turns the extra light around the model (degrees). 0 = from the front, 180 = from behind (a rim light). "
                + "It stays put when you orbit with Free Look, so you see the lit and shaded sides.", 40, new AcceptableValueRange<float>(-180f, 180f)));

            GameLightsBrightness = Config.Bind(LIGHTING_SECTION, "Game lights brightness", 1f, Describe(
                "The game's own preview lights (main, fill, hair, down and rim lights) - 1 = as the game has them.", 30, new AcceptableValueRange<float>(0f, 3f)));

            RimLightStrength = Config.Bind(LIGHTING_SECTION, "Rim light strength", 1f, Describe(
                "Just the game's two rim lights (the edge glow on the outline), on top of the brightness above - 1 = as the game has them.", 20, new AcceptableValueRange<float>(0f, 3f)));

            GameLightShadows = Config.Bind(LIGHTING_SECTION, "Game light shadows", true, Describe(
                "Off = the game's preview lights cast no shadows (a flatter, brighter look). On = as the game has them.", 10));

            PhotoFolder = Config.Bind(PHOTO_SECTION, "Photo folder", "", Describe(
                "Where photo mode saves its PNGs. Empty = SPT\\user\\mods\\ImprovedCustomizationUI\\Photos.", 10));

            PreviewLights.SettingChanged += OnLightingChanged;
            ExtraLightBrightness.SettingChanged += OnLightingChanged;
            ExtraLightRotationX.SettingChanged += OnLightingChanged;
            ExtraLightRotationY.SettingChanged += OnLightingChanged;
            GameLightsBrightness.SettingChanged += OnLightingChanged;
            RimLightStrength.SettingChanged += OnLightingChanged;
            GameLightShadows.SettingChanged += OnLightingChanged;

            if (EnableCustomizationTab.Value)
            {
                TryEnable("customization tab", delegate { new Patches.CustomizationTabShowPatch().Enable(); });
                TryEnable("customization tab close", delegate { new Patches.CustomizationTabClosePatch().Enable(); });
                TryEnable("customization tab cleanup", delegate { new Patches.CustomizationTabCleanupPatch().Enable(); });
                TryEnable("customization choices", delegate { new Patches.CustomizationPrepareSelectorsPatch().Enable(); });
                TryEnable("customization preview", delegate { new Patches.CustomizationUpdatePreviewPatch().Enable(); });
                TryEnable("customization voice", delegate { new Patches.CustomizationPlayVoicePatch().Enable(); });
                TryEnable("preview weapon sounds", delegate { new Patches.PreviewSoundPatch().Enable(); });
                TryEnable("preview weapon sounds at point", delegate { new Patches.PreviewSoundAtPointPatch().Enable(); });
            }
            else
            {
                Log.LogInfo("[ImprovedCustomizationUI] Customization tab switched off in the config");
            }

            if (EnableRagmanFilters.Value)
            {
                TryEnable("Ragman clothing header", delegate { new Patches.RagmanClothingShowPatch().Enable(); });
                TryEnable("Ragman clothing offers", delegate { new Patches.RagmanClothingOffersPatch().Enable(); });
                TryEnable("Ragman clothing cards", delegate { new Patches.RagmanClothingCardPatch().Enable(); });
                TryEnable("Ragman clothing card states", delegate { new Patches.RagmanClothingCardStatePatch().Enable(); });
            }
            else
            {
                Log.LogInfo("[ImprovedCustomizationUI] Ragman faction toggles switched off in the config");
            }
        }

        private static ConfigDescription Describe(string text, int order, AcceptableValueBase range = null)
        {
            ConfigurationManagerAttributes attributes = new ConfigurationManagerAttributes();
            attributes.Order = order;
            return new ConfigDescription(text, range, attributes);
        }

        private static void OnLightingChanged(object sender, EventArgs args)
        {
            if (LightingChanged != null)
            {
                LightingChanged();
            }
        }

        private static async void SyncClothingRule()
        {
            try
            {
                await ClothingRules.SyncWithServer();
            }
            catch (Exception error)
            {
                Log.LogWarning("[ImprovedCustomizationUI] other-faction clothing setting not sent: " + error.Message);
            }
        }

        private void TryEnable(string what, Action enable)
        {
            try
            {
                enable();
            }
            catch (Exception error)
            {
                Log.LogWarning("[ImprovedCustomizationUI] '" + what + "' patch failed, that part is off: " + error.Message);
            }
        }
    }
}
