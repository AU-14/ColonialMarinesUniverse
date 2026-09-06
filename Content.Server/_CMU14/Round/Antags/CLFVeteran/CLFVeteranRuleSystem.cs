// CMU14 file: wanted record/capture watch/bounty payout moved to ColonyBountySystem; the faxes remain
using Content.Server.AU14.Systems;
using Content.Server.Fax;
using Content.Server.GameTicking.Rules;
using Content.Shared.Fax.Components;
using Content.Shared.Paper;
using CLFFaxReceiverComponent = Content.Shared._CMU14.Threats.Mobs.CLF.CLFFaxReceiverComponent;
using Robust.Shared.Maths;
using Content.Server._CMU14.Round.Antags.ColonyBounty;

namespace Content.Server._CMU14.Round.Antags.CLFVeteran;

public sealed partial class CLFVeteranRuleSystem : GameRuleSystem<CLFVeteranRuleComponent>
{
    private const string ClfFaxGroup = "clf";
    private const string ClfPaperPrototype = "CMUPaperCLF";

    [Dependency] private readonly WantedSystem _wantedSystem = default!;
    [Dependency] private readonly FaxSystem _fax = default!;

    private EntityUid? _veteranUid = null;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CLFVeteranComponent, ComponentStartup>(OnVeteranSpawned);
        SubscribeLocalEvent<CLFFaxReceiverComponent, ComponentInit>(OnCLFFaxReceiverInit);
    }

    private void OnVeteranSpawned(EntityUid uid, CLFVeteranComponent component, ComponentStartup args)
    {
        _veteranUid = uid;

        _wantedSystem.SendPaperToGroup(ColonyCmbFax.MarshalBureauFaxGroup, "AUPaperCLFVeteran");

        var veteranName = EntityManager.GetComponentOrNull<MetaDataComponent>(uid)?.EntityName ?? "Unknown";
        _wantedSystem.SendFaxToGroup(
            ClfFaxGroup,
            "Encrypted Message",
            BuildVeteranFaxContent(veteranName),
            "paper_stamp-clf",
            new List<StampDisplayInfo>
            {
                new() { StampedColor = Color.FromHex("#2e5a1e"), StampedName = "CLF" },
            }, ClfPaperPrototype);
    }

    private void OnCLFFaxReceiverInit(EntityUid uid, CLFFaxReceiverComponent comp, ComponentInit args)
    {
        if (_veteranUid == null || !EntityManager.EntityExists(_veteranUid.Value))
            return;

        if (!TryComp(uid, out FaxMachineComponent? faxComp))
            return;

        var veteranName = EntityManager.GetComponentOrNull<MetaDataComponent>(_veteranUid.Value)?.EntityName ?? "Unknown";
        var printout = new FaxPrintout(
            BuildVeteranFaxContent(veteranName),
            "Encrypted Message",
            null,
            "CMPaper",
            "paper_stamp-clf",
            new List<StampDisplayInfo>
            {
                new() { StampedColor = Color.FromHex("#2e5a1e"), StampedName = "CLF" },
            });

        _fax.Receive(uid, printout, null, faxComp);
    }

    private static string BuildVeteranFaxContent(string veteranName)
    {
        return "[head=3][color=#2e5a1e]Colony Liberation Front[/color][/head]\n\n" +
            "[color=#2e5a1e]▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄[/color]\n\n" +
            "[bold]To:[/bold] [italic]Field Operatives[/italic]\n" +
            "[bold]From:[/bold] [bold]CLF Regional Command[/bold]\n" +
            "[color=#2e5a1e]‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾[/color]\n" +
            "Comrades,\n" +
            $"  A disavowed operative, [bold]{veteranName}[/bold], has been located in the colony. " +
            "Bring them back into the fold, or deal with them accordingly.\n\n" +
            "Freedom or death,\n" +
            "[color=#2e5a1e][bolditalic]CLF Command[/bolditalic][/color]\n" +
            "[color=#2e5a1e]‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾[/color]";
    }
}
