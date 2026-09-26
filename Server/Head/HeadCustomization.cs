using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.Controllers;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Customization;
using SPTarkov.Server.Core.Models.Eft.Profile;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace ImprovedCustomizationUI.Server.Head;

[Injectable(InjectionType.Singleton, TypePriority = OnLoadOrder.PostLoad)]
public class HeadCustomization(TemplateTable templateTable, ProfileHelper profileHelper, ISptLogger<HeadCustomization> logger)
    : IOnLoad
{
    public static HeadCustomization Instance;

    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        Instance = this;
        new SetCustomisationHeadPatch().Enable();
        return Task.CompletedTask;
    }

    public void ApplyHeads(MongoId sessionId, CustomizationSetRequest request, PmcData pmcData)
    {
        if (request.Customizations == null)
        {
            return;
        }

        List<CustomizationSetOption> heads = new List<CustomizationSetOption>();
        foreach (CustomizationSetOption option in request.Customizations)
        {
            if (option.Type == "head")
            {
                heads.Add(option);
            }
        }

        foreach (CustomizationSetOption head in heads)
        {
            request.Customizations.Remove(head);

            string problem = WhyNotAllowed(sessionId, head.Id, pmcData);
            if (problem != null)
            {
                logger.Warning("[ImprovedCustomizationUI] head " + head.Id + " not applied: " + problem);
                continue;
            }

            pmcData.Customization ??= new SPTarkov.Server.Core.Models.Eft.Common.Tables.Customization();
            pmcData.Customization.Head = head.Id;
        }
    }

    private string WhyNotAllowed(MongoId sessionId, MongoId headId, PmcData pmcData)
    {
        if (pmcData.Customization?.Head == headId)
        {
            return null;
        }

        if (!templateTable.Customization.TryGetValue(headId, out CustomizationItem item) || item == null)
        {
            return "not in the database";
        }

        if (item.Parent != CustomisationTypeId.HEAD)
        {
            return "not a head";
        }

        string side = pmcData.Info?.Side;
        if (side == null || item.Properties?.Side == null || !item.Properties.Side.Contains(side))
        {
            return "not for this side";
        }

        if (!IsOwned(sessionId, headId))
        {
            return "not owned";
        }

        return null;
    }

    private bool IsOwned(MongoId sessionId, MongoId headId)
    {
        foreach (CustomisationStorage stored in templateTable.CustomisationStorage)
        {
            if (stored.Id == headId)
            {
                return true;
            }
        }

        SptProfile profile = profileHelper.GetFullProfile(sessionId);
        if (profile?.CustomisationUnlocks != null)
        {
            foreach (CustomisationStorage unlock in profile.CustomisationUnlocks)
            {
                if (unlock.Id == headId)
                {
                    return true;
                }
            }
        }

        return false;
    }
}

public class SetCustomisationHeadPatch : AbstractPatch
{
    protected override MethodBase GetTargetMethod()
    {
        return typeof(CustomizationController).GetMethod(nameof(CustomizationController.SetCustomisation))!;
    }

    [PatchPrefix]
    public static void Prefix(MongoId sessionId, CustomizationSetRequest request, PmcData pmcData)
    {
        HeadCustomization heads = HeadCustomization.Instance;
        if (heads == null)
        {
            return;
        }

        try
        {
            heads.ApplyHeads(sessionId, request, pmcData);
        }
        catch (Exception error)
        {
            Console.WriteLine("[ImprovedCustomizationUI] head customization failed: " + error);
        }
    }
}
