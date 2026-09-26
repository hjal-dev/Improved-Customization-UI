using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Utils;

namespace ImprovedCustomizationUI.Server.Clothing;

public record ClothingRuleRequest : IRequestData
{
    [JsonPropertyName("rule")]
    public string Rule { get; set; }
}

[Injectable]
public class ClothingRuleRouter(JsonUtil jsonUtil) : StaticRouter(jsonUtil, new List<RouteAction>
{
    new RouteAction<ClothingRuleRequest>("/improvedcustomizationui/clothingrule",
        async (string url, ClothingRuleRequest info, MongoId sessionID, string output, CancellationToken cancellationToken) =>
        {
            OtherFactionClothing clothing = OtherFactionClothing.Instance;
            if (clothing == null)
            {
                return "{}";
            }
            return await new ValueTask<string>(clothing.SetClientRule(sessionID, info.Rule));
        })
})
{
}
