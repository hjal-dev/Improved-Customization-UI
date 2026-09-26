using System;
using System.Threading.Tasks;
using EFT;
using Newtonsoft.Json.Linq;
using SPT.Common.Http;

namespace ImprovedCustomizationUI.Ragman
{
    public enum OtherFactionClothing
    {
        UnheardOnly,
        AnyEdition,
        Off
    }

    public static class ClothingRules
    {
        public const string UNHEARD_EDITION = "unheard_edition";

        private const string RULE_ROUTE = "/improvedcustomizationui/clothingrule";

        private static OtherFactionClothing? _serverCap;
        private static bool _loggedNoServer;

        public static bool IsUnheard(Profile profile)
        {
            return profile != null && profile.Info != null && profile.Info.GameVersion == UNHEARD_EDITION;
        }

        private static OtherFactionClothing ClientRule()
        {
            return Plugin.OtherFaction != null ? Plugin.OtherFaction.Value : OtherFactionClothing.UnheardOnly;
        }

        private static OtherFactionClothing Rule()
        {
            OtherFactionClothing rule = ClientRule();
            if (_serverCap.HasValue && Openness(_serverCap.Value) < Openness(rule))
            {
                return _serverCap.Value;
            }
            return rule;
        }

        private static int Openness(OtherFactionClothing rule)
        {
            if (rule == OtherFactionClothing.AnyEdition)
            {
                return 2;
            }
            if (rule == OtherFactionClothing.UnheardOnly)
            {
                return 1;
            }
            return 0;
        }

        private static bool Allows(OtherFactionClothing rule, Profile profile)
        {
            if (rule == OtherFactionClothing.AnyEdition)
            {
                return true;
            }
            if (rule == OtherFactionClothing.UnheardOnly)
            {
                return IsUnheard(profile);
            }
            return false;
        }

        public static bool MayUseSide(Profile profile, EPlayerSide side)
        {
            if (profile == null)
            {
                return false;
            }
            if (profile.Side == side)
            {
                return true;
            }
            return Allows(Rule(), profile);
        }

        public static bool BlockedByServer(Profile profile)
        {
            return profile != null && Allows(ClientRule(), profile) && !Allows(Rule(), profile);
        }

        public static EPlayerSide OtherSide(EPlayerSide side)
        {
            return side == EPlayerSide.Bear ? EPlayerSide.Usec : EPlayerSide.Bear;
        }

        public static async Task SyncWithServer()
        {
            try
            {
                string request = "{\"rule\":\"" + ClientRule() + "\"}";
                string answer = await RequestHandler.PostJsonAsync(RULE_ROUTE, request);
                JObject json = JObject.Parse(answer);
                string cap = (string)json["cap"];

                OtherFactionClothing parsed;
                if (cap != null && Enum.TryParse(cap, out parsed))
                {
                    _serverCap = parsed;
                }
            }
            catch (Exception error)
            {
                if (!_loggedNoServer)
                {
                    _loggedNoServer = true;
                    Plugin.Log.LogWarning("[ImprovedCustomizationUI] couldn't tell the server the other-faction clothing setting (server mod missing or old?): " + error.Message);
                }
            }
        }
    }
}
