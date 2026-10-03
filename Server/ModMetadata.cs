using System.Collections.Generic;
using SPTarkov.Server.Core.Models.Spt.Mod;

using Version = SemanticVersioning.Version;
using Range = SemanticVersioning.Range;

namespace ImprovedCustomizationUI.Server;

public record ModMetadata : IModMetadata
{
    public string ModGuid { get; init; } = "com.hj.improvedcustomizationui.server";
    public string Name { get; init; } = "Hj's Improved Customization UI";
    public string Author { get; init; } = "hj";
    public List<string> Contributors { get; init; } = null;
    public Version Version { get; init; } = new Version("1.1.0");
    public Range SptVersion { get; init; } = new Range("~4.1.0");
    public bool HasPrepatcher { get; init; } = false;
    public List<string> Incompatibilities { get; init; } = null;
    public Dictionary<string, Range> ModDependencies { get; init; } = null;
    public string Url { get; init; } = null;
    public string License { get; init; } = "MIT";
}
