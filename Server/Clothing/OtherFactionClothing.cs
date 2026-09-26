using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.Callbacks;
using SPTarkov.Server.Core.Controllers;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Customization;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Utils;
using SPTarkov.Server.Core.Utils.Json;

namespace ImprovedCustomizationUI.Server.Clothing;

[Injectable(InjectionType.Singleton, TypePriority = OnLoadOrder.PostLoad)]
public class OtherFactionClothing(
    TemplateTable templateTable,
    TradersTable tradersTable,
    LocaleTable localeTable,
    ProfileHelper profileHelper,
    HttpResponseUtil httpResponseUtil,
    ISptLogger<OtherFactionClothing> logger)
    : IOnLoad
{
    public static OtherFactionClothing Instance;

    public const string OFF = "Off";
    public const string UNHEARD_ONLY = "UnheardOnly";
    public const string ANY_EDITION = "AnyEdition";

    private const string UNHEARD_EDITION = "unheard_edition";

    private const string CONFIG_FILE = "config.json";

    private string _cap = ANY_EDITION;

    private readonly Dictionary<MongoId, string> _clientRules = new Dictionary<MongoId, string>();
    private readonly object _clientRulesLock = new object();

    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        Instance = this;
        LoadConfig();
        AddLocales();
        new TraderSuitsPatch().Enable();
        new AllTraderSuitsPatch().Enable();
        new SetCustomisationSuitePatch().Enable();
        return Task.CompletedTask;
    }

    public class ServerConfig
    {
        [JsonPropertyName("_help")]
        public string Help { get; set; } = "Each player picks who may see, buy and wear the other faction's clothing in their own BepInEx menu "
            + "(Hj's Improved Customization UI > Ragman clothing > Other faction clothing). MaxOtherFactionClothing caps that for everyone on this server: "
            + ANY_EDITION + " (no cap), " + UNHEARD_ONLY + " (at most Unheard accounts), " + OFF + " (nobody).";

        [JsonPropertyName("MaxOtherFactionClothing")]
        public string MaxOtherFactionClothing { get; set; }
    }

    private void LoadConfig()
    {
        string folder = System.IO.Path.GetDirectoryName(typeof(OtherFactionClothing).Assembly.Location) ?? ".";
        string path = System.IO.Path.Combine(folder, CONFIG_FILE);
        JsonSerializerOptions options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        try
        {
            ServerConfig config = null;
            if (File.Exists(path))
            {
                config = JsonSerializer.Deserialize<ServerConfig>(File.ReadAllText(path), options);
            }

            if (config == null || config.MaxOtherFactionClothing == null)
            {
                config = new ServerConfig();
                config.MaxOtherFactionClothing = ANY_EDITION;
                File.WriteAllText(path, JsonSerializer.Serialize(config, options));
                logger.Info("[ImprovedCustomizationUI] config.json written with the defaults (no cap - each player's BepInEx setting decides)");
            }

            _cap = ParseRule(config.MaxOtherFactionClothing, "config.json MaxOtherFactionClothing");
        }
        catch (Exception error)
        {
            logger.Warning("[ImprovedCustomizationUI] config.json unreadable (" + error.Message + "), no cap");
            _cap = ANY_EDITION;
        }

        logger.Info("[ImprovedCustomizationUI] other-faction clothing cap: " + _cap);
    }

    private string ParseRule(string value, string from)
    {
        if (value != null && value.Equals(ANY_EDITION, StringComparison.OrdinalIgnoreCase))
        {
            return ANY_EDITION;
        }
        if (value != null && value.Equals(OFF, StringComparison.OrdinalIgnoreCase))
        {
            return OFF;
        }
        if (value == null || !value.Equals(UNHEARD_ONLY, StringComparison.OrdinalIgnoreCase))
        {
            logger.Warning("[ImprovedCustomizationUI] " + from + ": unknown value '" + value + "', using " + UNHEARD_ONLY);
        }
        return UNHEARD_ONLY;
    }

    private static int Openness(string rule)
    {
        if (rule == ANY_EDITION)
        {
            return 2;
        }
        if (rule == UNHEARD_ONLY)
        {
            return 1;
        }
        return 0;
    }

    private static string Stricter(string a, string b)
    {
        return Openness(a) <= Openness(b) ? a : b;
    }

    public class ClothingRuleAnswer
    {
        [JsonPropertyName("requested")]
        public string Requested { get; set; } = UNHEARD_ONLY;

        [JsonPropertyName("cap")]
        public string Cap { get; set; } = ANY_EDITION;

        [JsonPropertyName("enforced")]
        public string Enforced { get; set; } = UNHEARD_ONLY;
    }

    public string SetClientRule(MongoId sessionId, string rule)
    {
        string requested = ParseRule(rule, "the game's setting");
        lock (_clientRulesLock)
        {
            _clientRules[sessionId] = requested;
        }

        ClothingRuleAnswer answer = new ClothingRuleAnswer();
        answer.Requested = requested;
        answer.Cap = _cap;
        answer.Enforced = RuleFor(sessionId);
        return httpResponseUtil.NoBody(answer);
    }

    private string RuleFor(MongoId sessionId)
    {
        string requested = UNHEARD_ONLY;
        lock (_clientRulesLock)
        {
            if (_clientRules.TryGetValue(sessionId, out string stored))
            {
                requested = stored;
            }
        }
        return Stricter(requested, _cap);
    }

    public bool MayUseSide(MongoId sessionId, PmcData pmcData, string side)
    {
        string ownSide = pmcData?.Info?.Side;
        if (ownSide != null && ownSide.Equals(side, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        string rule = RuleFor(sessionId);
        if (rule == ANY_EDITION)
        {
            return true;
        }
        if (rule == UNHEARD_ONLY)
        {
            return pmcData?.Info?.GameVersion == UNHEARD_EDITION;
        }
        return false;
    }

    private string RuleNote(MongoId sessionId)
    {
        return "rule " + RuleFor(sessionId) + ", cap " + _cap;
    }

    public static string OtherSide(string side)
    {
        if (side == null)
        {
            return null;
        }
        if (side.Equals("Usec", StringComparison.OrdinalIgnoreCase))
        {
            return "Bear";
        }
        if (side.Equals("Bear", StringComparison.OrdinalIgnoreCase))
        {
            return "Usec";
        }
        return null;
    }

    public List<Suit> SuitsFor(MongoId traderId, string side)
    {
        List<Suit> result = new List<Suit>();
        Trader trader = tradersTable.GetTrader(traderId);
        if (trader?.Suits == null)
        {
            return result;
        }

        foreach (Suit suit in trader.Suits)
        {
            if (!templateTable.Customization.TryGetValue(suit.SuiteId, out CustomizationItem clothing) || clothing?.Properties?.Side == null)
            {
                continue;
            }

            foreach (string suitSide in clothing.Properties.Side)
            {
                if (suitSide.Equals(side, StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(suit);
                    break;
                }
            }
        }
        return result;
    }

    public bool TryAnswerOffers(string url, MongoId sessionId, out string body)
    {
        body = "";
        string[] parts = url.Split('/');
        if (parts.Length < 4 || !MongoId.IsValidMongoId(parts[^3]))
        {
            return false;
        }

        MongoId traderId = new MongoId(parts[^3]);
        string side = parts[^2];
        if (!side.Equals("bear", StringComparison.OrdinalIgnoreCase) && !side.Equals("usec", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        PmcData pmcData = profileHelper.GetPmcProfile(sessionId);
        List<Suit> suits;
        if (MayUseSide(sessionId, pmcData, side))
        {
            suits = SuitsFor(traderId, side);
        }
        else
        {
            suits = new List<Suit>();
            logger.Warning("[ImprovedCustomizationUI] " + side + " clothing offers refused for " + pmcData?.Info?.Nickname
                + " (" + pmcData?.Info?.GameVersion + ", " + RuleNote(sessionId) + ")");
        }

        body = httpResponseUtil.GetBody(suits);
        return true;
    }

    public IEnumerable<Suit> AddOtherSideSuits(IEnumerable<Suit> ownSuits, MongoId sessionId)
    {
        PmcData pmcData = profileHelper.GetPmcProfile(sessionId);
        string otherSide = OtherSide(pmcData?.Info?.Side);
        if (otherSide == null || !MayUseSide(sessionId, pmcData, otherSide))
        {
            return ownSuits;
        }

        List<Suit> all = new List<Suit>(ownSuits);
        foreach (KeyValuePair<MongoId, Trader> trader in tradersTable)
        {
            if (trader.Value.Base.CustomizationSeller == true)
            {
                all.AddRange(SuitsFor(trader.Key, otherSide));
            }
        }
        return all;
    }

    private const string UPPER_PARENT = "5cd944ca1388ce03a44dc2a4";
    private const string LOWER_PARENT = "5cd944d01388ce000a659df9";

    public void RemoveForbiddenSuits(MongoId sessionId, CustomizationSetRequest request, PmcData pmcData)
    {
        if (request.Customizations == null)
        {
            return;
        }

        List<CustomizationSetOption> refused = new List<CustomizationSetOption>();
        foreach (CustomizationSetOption option in request.Customizations)
        {
            if (option.Type != "suite")
            {
                continue;
            }

            string problem = WhySuitNotAllowed(sessionId, option.Id, pmcData);
            if (problem != null)
            {
                refused.Add(option);
                logger.Warning("[ImprovedCustomizationUI] suit " + option.Id + " not applied for " + pmcData.Info?.Nickname + ": " + problem);
            }
        }

        foreach (CustomizationSetOption option in refused)
        {
            request.Customizations.Remove(option);
        }
    }

    private string WhySuitNotAllowed(MongoId sessionId, MongoId suitId, PmcData pmcData)
    {
        if (!templateTable.Customization.TryGetValue(suitId, out CustomizationItem suit) || suit?.Properties?.Side == null)
        {
            return null;
        }

        if (suit.Parent == UPPER_PARENT && pmcData.Customization?.Body == suit.Properties.Body)
        {
            return null;
        }
        if (suit.Parent == LOWER_PARENT && pmcData.Customization?.Feet == suit.Properties.Feet)
        {
            return null;
        }

        foreach (string side in suit.Properties.Side)
        {
            if (MayUseSide(sessionId, pmcData, side))
            {
                return null;
            }
        }

        return "it's for " + string.Join("/", suit.Properties.Side) + " (" + pmcData.Info?.GameVersion + ", " + RuleNote(sessionId) + ")";
    }

    private void AddLocales()
    {
        Dictionary<string, Dictionary<string, string>> texts = null;
        try
        {
            using Stream resource = typeof(OtherFactionClothing).Assembly.GetManifestResourceStream("ImprovedCustomizationUI.Server.clothing_locales.json");
            if (resource != null)
            {
                texts = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(resource);
            }
        }
        catch (Exception error)
        {
            logger.Warning("[ImprovedCustomizationUI] clothing texts unreadable: " + error.Message);
        }

        if (texts == null || !texts.ContainsKey("en"))
        {
            logger.Warning("[ImprovedCustomizationUI] clothing texts missing, the filter will show raw keys");
            return;
        }

        foreach (KeyValuePair<string, LazyLoad<GlobalLocaleDictionary>> language in localeTable.Global)
        {
            Dictionary<string, string> wanted = texts.ContainsKey(language.Key) ? texts[language.Key] : texts["en"];
            language.Value.AddTransformer(delegate (GlobalLocaleDictionary locale)
            {
                if (locale != null)
                {
                    foreach (KeyValuePair<string, string> text in wanted)
                    {
                        locale.TryAdd(text.Key, text.Value);
                    }
                }
                return locale;
            });
        }
    }
}

public class TraderSuitsPatch : AbstractPatch
{
    protected override MethodBase GetTargetMethod()
    {
        return typeof(CustomizationCallbacks).GetMethod(nameof(CustomizationCallbacks.GetTraderSuits))!;
    }

    [PatchPrefix]
    public static bool Prefix(string url, MongoId sessionID, ref ValueTask<string> __result)
    {
        OtherFactionClothing clothing = OtherFactionClothing.Instance;
        if (clothing == null)
        {
            return true;
        }

        try
        {
            if (clothing.TryAnswerOffers(url, sessionID, out string body))
            {
                __result = new ValueTask<string>(body);
                return false;
            }
        }
        catch (Exception error)
        {
            Console.WriteLine("[ImprovedCustomizationUI] clothing offers failed, using 4.1's own: " + error);
        }
        return true;
    }
}

public class SetCustomisationSuitePatch : AbstractPatch
{
    protected override MethodBase GetTargetMethod()
    {
        return typeof(CustomizationController).GetMethod(nameof(CustomizationController.SetCustomisation))!;
    }

    [PatchPrefix]
    public static void Prefix(MongoId sessionId, CustomizationSetRequest request, PmcData pmcData)
    {
        OtherFactionClothing clothing = OtherFactionClothing.Instance;
        if (clothing == null)
        {
            return;
        }

        try
        {
            clothing.RemoveForbiddenSuits(sessionId, request, pmcData);
        }
        catch (Exception error)
        {
            Console.WriteLine("[ImprovedCustomizationUI] suit check failed, leaving the request to 4.1: " + error);
        }
    }
}

public class AllTraderSuitsPatch : AbstractPatch
{
    protected override MethodBase GetTargetMethod()
    {
        return typeof(CustomizationController).GetMethod("GetAllTraderSuits", BindingFlags.Instance | BindingFlags.NonPublic)!;
    }

    [PatchPostfix]
    public static void Postfix(MongoId sessionId, ref IEnumerable<Suit> __result)
    {
        OtherFactionClothing clothing = OtherFactionClothing.Instance;
        if (clothing == null)
        {
            return;
        }

        try
        {
            __result = clothing.AddOtherSideSuits(__result, sessionId);
        }
        catch (Exception error)
        {
            Console.WriteLine("[ImprovedCustomizationUI] other-faction suits for buying failed: " + error);
        }
    }
}
