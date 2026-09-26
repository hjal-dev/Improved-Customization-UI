using System;
using System.Reflection;
using System.Threading.Tasks;
using EFT.UI;
using HarmonyLib;
using SPT.Reflection.Patching;
using ImprovedCustomizationUI.Customization;

namespace ImprovedCustomizationUI.Patches
{
    public class CustomizationTabShowPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(InventoryScreen), nameof(InventoryScreen.Show),
                new Type[] { typeof(InventoryScreen.InventoryScreenController) });
        }

        [PatchPrefix]
        public static void Prefix(InventoryScreen __instance, InventoryScreen.InventoryScreenController controller)
        {
            try
            {
                CustomizationTab tab = __instance.GetComponent<CustomizationTab>();
                if (tab == null)
                {
                    tab = __instance.gameObject.AddComponent<CustomizationTab>();
                }

                tab.Initialize(__instance, controller);
            }
            catch (Exception error)
            {
                Plugin.Log.LogWarning("[ImprovedCustomizationUI] couldn't add the CUSTOMIZATION tab: " + error);
            }
        }
    }

    public class CustomizationTabClosePatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(InventoryScreen.InventoryScreenController),
                nameof(InventoryScreen.InventoryScreenController.CloseScreenInterruption));
        }

        [PatchPostfix]
        public static void Postfix(InventoryScreen.InventoryScreenController __instance, ref Task<bool> __result)
        {
            CustomizationTab tab = null;
            if (__instance.Screen != null)
            {
                tab = __instance.Screen.GetComponent<CustomizationTab>();
            }

            __result = CloseAfterSave(__result, tab);
        }

        private static async Task<bool> CloseAfterSave(Task<bool> original, CustomizationTab tab)
        {
            if (!await original)
            {
                return false;
            }

            if (tab == null)
            {
                return true;
            }

            return await tab.Flush();
        }
    }

    public class CustomizationPrepareSelectorsPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(HeadSelectionState), nameof(HeadSelectionState.PrepareSelectors));
        }

        [PatchPrefix]
        public static bool Prefix(HeadSelectionState __instance)
        {
            if (!AppearanceView.TryGet(__instance, out AppearanceView view))
            {
                return true;
            }

            view.PrepareSelectors();
            return false;
        }
    }

    public class CustomizationUpdatePreviewPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(HeadSelectionState), nameof(HeadSelectionState.UpdatePreview));
        }

        [PatchPrefix]
        public static bool Prefix(HeadSelectionState __instance, ref Task __result)
        {
            if (!AppearanceView.TryGet(__instance, out AppearanceView view))
            {
                return true;
            }

            __result = view.UpdatePreview();
            return false;
        }
    }

    public class CustomizationPlayVoicePatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(HeadSelectionState), nameof(HeadSelectionState.PlayVoice));
        }

        [PatchPrefix]
        public static bool Prefix(HeadSelectionState __instance, int selectedIndex, ref Task __result)
        {
            if (!AppearanceView.TryGet(__instance, out AppearanceView view))
            {
                return true;
            }

            __result = view.PlayVoice(selectedIndex);
            return false;
        }
    }

    public class CustomizationTabCleanupPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(InventoryScreen), nameof(InventoryScreen.Close));
        }

        [PatchPrefix]
        public static void Prefix(InventoryScreen __instance)
        {
            CustomizationTab tab = __instance.GetComponent<CustomizationTab>();
            if (tab != null)
            {
                tab.Close();
            }
        }
    }
}
