using System.Reflection;
using HarmonyLib;
using SPT.Reflection.Patching;
using ImprovedCustomizationUI.Customization.Preview;

namespace ImprovedCustomizationUI.Patches
{
    public class PreviewSoundPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(MenuPlayerPoser), "IEventsConsumer.OnSound");
        }

        [PatchPostfix]
        public static void Postfix(MenuPlayerPoser __instance, string StringParam)
        {
            PreviewSounds.TryPlay(__instance, StringParam);
        }
    }

    public class PreviewSoundAtPointPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(MenuPlayerPoser), "IEventsConsumer.OnSoundAtPoint");
        }

        [PatchPostfix]
        public static void Postfix(MenuPlayerPoser __instance, string StringParam)
        {
            PreviewSounds.TryPlay(__instance, StringParam);
        }
    }
}
