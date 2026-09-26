using System.Collections.Generic;
using System.Reflection;
using EFT;
using EFT.UI;
using HarmonyLib;

namespace ImprovedCustomizationUI.Ragman
{
    public static class RagmanFields
    {
        private static readonly FieldInfo _solver = AccessTools.Field(typeof(TacticalClothingView), "_solver");
        private static readonly FieldInfo _bodyParts = AccessTools.Field(typeof(TacticalClothingView), "_bodyParts");
        private static readonly FieldInfo _currentEnterIndex = AccessTools.Field(typeof(TacticalClothingView), "_currentEnterIndex");
        private static readonly FieldInfo _views = AccessTools.Field(typeof(TacticalClothingView), "_views");
        private static readonly FieldInfo _state = AccessTools.Field(typeof(ClothingItem), "_state");

        public static CustomizationSolver Solver(TacticalClothingView view)
        {
            return (CustomizationSolver)_solver.GetValue(view);
        }

        public static Dictionary<EBodyModelPart, ClothingItem.FullOffer[]> BodyParts(TacticalClothingView view)
        {
            return (Dictionary<EBodyModelPart, ClothingItem.FullOffer[]>)_bodyParts.GetValue(view);
        }

        public static int EnterIndex(TacticalClothingView view)
        {
            return (int)_currentEnterIndex.GetValue(view);
        }

        public static int NextEnterIndex(TacticalClothingView view)
        {
            int next = EnterIndex(view) + 1;
            _currentEnterIndex.SetValue(view, next);
            return next;
        }

        public static List<ClothingItem> Cards(TacticalClothingView view)
        {
            return (List<ClothingItem>)_views.GetValue(view);
        }

        public static ClothingItem.EClothingItemState State(ClothingItem card)
        {
            return (ClothingItem.EClothingItemState)_state.GetValue(card);
        }
    }
}
