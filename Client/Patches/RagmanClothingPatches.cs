using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Comfort.Common;
using EFT;
using EFT.Customization;
using EFT.Trading;
using EFT.UI;
using HarmonyLib;
using ImprovedCustomizationUI.Ragman;
using SPT.Reflection.Patching;

namespace ImprovedCustomizationUI.Patches
{
    public class RagmanClothingShowPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.DeclaredMethod(typeof(TacticalClothingView), nameof(TacticalClothingView.Show));
        }

        [PatchPrefix]
        public static void Prefix(TacticalClothingView __instance, Profile profile)
        {
            try
            {
                ClothingFilterBar bar = __instance.GetComponent<ClothingFilterBar>();
                if (bar == null)
                {
                    bar = __instance.gameObject.AddComponent<ClothingFilterBar>();
                }
                bar.Prepare(__instance, profile);
            }
            catch (Exception error)
            {
                Plugin.Log.LogWarning("[ImprovedCustomizationUI] couldn't add the Ragman faction toggles: " + error);
            }
        }
    }

    public class RagmanClothingOffersPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.DeclaredMethod(typeof(TacticalClothingView), nameof(TacticalClothingView.ShowOffers));
        }

        [PatchPrefix]
        public static bool Prefix(TacticalClothingView __instance, Trader trader)
        {
            ClothingFilterBar bar = __instance.GetComponent<ClothingFilterBar>();
            if (bar == null || !bar.Ready)
            {
                return true;
            }

            ShowOffers(__instance, trader, bar);
            return false;
        }

        private static async void ShowOffers(TacticalClothingView view, Trader trader, ClothingFilterBar bar)
        {
            try
            {
                int request = RagmanFields.NextEnterIndex(view);

                await ClothingRules.SyncWithServer();
                if (request != RagmanFields.EnterIndex(view))
                {
                    return;
                }
                bar.RefreshRule();

                List<CustomizationOffer> offers = new List<CustomizationOffer>();
                HashSet<string> seen = new HashSet<string>();
                foreach (EPlayerSide side in bar.SidesToLoad())
                {
                    CustomizationOffer[] sideOffers = await GetOffers(trader, side);
                    foreach (CustomizationOffer offer in sideOffers)
                    {
                        if (offer != null && seen.Add(offer.Id))
                        {
                            offers.Add(offer);
                        }
                    }
                }

                if (!view.gameObject.activeSelf || request != RagmanFields.EnterIndex(view))
                {
                    return;
                }

                CustomizationSolver solver = RagmanFields.Solver(view);
                ClothingItem.OfferComparer comparer = new ClothingItem.OfferComparer(solver);
                SortedSet<ClothingItem.FullOffer> upper = new SortedSet<ClothingItem.FullOffer>(comparer);
                SortedSet<ClothingItem.FullOffer> lower = new SortedSet<ClothingItem.FullOffer>(comparer);
                List<string> bundles = new List<string>();

                foreach (CustomizationOffer offer in offers)
                {
                    CustomizationSuite suite = solver.GetSuite(offer.SuiteId);
                    if (suite == null)
                    {
                        continue;
                    }

                    CustomizationClothing clothing = solver.GetItem(suite.MainBodyPartItem);
                    if (clothing == null)
                    {
                        continue;
                    }

                    ClothingItem.FullOffer full = new ClothingItem.FullOffer();
                    full.Offer = offer;
                    full.Suite = suite;
                    full.Clothing = clothing;

                    if (suite is LowerBodySuit)
                    {
                        if (lower.Add(full))
                        {
                            bundles.Add(clothing.Prefab.path);
                        }
                    }
                    else if (suite is UpperBodySuit)
                    {
                        if (upper.Add(full))
                        {
                            bundles.Add(clothing.Prefab.path);
                        }
                    }
                }

                Dictionary<EBodyModelPart, ClothingItem.FullOffer[]> bodyParts = RagmanFields.BodyParts(view);

                ClothingItem.FullOffer[] upperArray = new ClothingItem.FullOffer[upper.Count];
                upper.CopyTo(upperArray);
                bodyParts[EBodyModelPart.Body] = upperArray;

                ClothingItem.FullOffer[] lowerArray = new ClothingItem.FullOffer[lower.Count];
                lower.CopyTo(lowerArray);
                bodyParts[EBodyModelPart.Feet] = lowerArray;

                view.LoadBundles(bundles, request);
            }
            catch (Exception error)
            {
                Plugin.Log.LogError("[ImprovedCustomizationUI] Ragman clothing offers failed: " + error);
            }
        }

        private static Task<CustomizationOffer[]> GetOffers(Trader trader, EPlayerSide side)
        {
            TaskCompletionSource<CustomizationOffer[]> result = new TaskCompletionSource<CustomizationOffer[]>();
            trader._trading.GetOffers(trader.Id, side, delegate (Result<CustomizationOffer[]> offers)
            {
                if (offers.Failed || offers.Value == null)
                {
                    Plugin.Log.LogWarning("[ImprovedCustomizationUI] " + side + " clothing offers not loaded: " + offers.Error);
                    result.SetResult(new CustomizationOffer[0]);
                    return;
                }
                result.SetResult(offers.Value);
            });
            return result.Task;
        }
    }

    public class RagmanClothingCardPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.DeclaredMethod(typeof(TacticalClothingView), nameof(TacticalClothingView.InitClothBundle));
        }

        [PatchPostfix]
        public static void Postfix(TacticalClothingView __instance, ClothingItem bodyPartView)
        {
            ClothingFilterBar bar = __instance.GetComponent<ClothingFilterBar>();
            if (bar != null)
            {
                bar.Apply(bodyPartView);
            }
        }
    }

    public class RagmanClothingCardStatePatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.DeclaredMethod(typeof(TacticalClothingView), nameof(TacticalClothingView.UpdateView));
        }

        [PatchPostfix]
        public static void Postfix(TacticalClothingView __instance, ClothingItem view)
        {
            ClothingFilterBar bar = __instance.GetComponent<ClothingFilterBar>();
            if (bar != null)
            {
                bar.Apply(view);
            }
        }
    }
}
