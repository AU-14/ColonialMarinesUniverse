using Content.Server.AU14.Systems;
using Content.Server.Communications;
using Content.Server.Power.Components;
using Content.Server._CMU14.Round.Antags.ColonyBounty;
using Content.Shared._CMU14.Round.Antags.ColonyBounty;
using Content.Shared._CMU14.Round.Antags.CLFSaboteur;
using Content.Shared.Paper;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.Server._CMU14.Round.Antags.CLFSaboteur;

/// <summary>
/// Counts colony infrastructure destroyed while a CLF saboteur is active. Destruction is
/// counted regardless of who caused it; the sabotage claim is propaganda either way.
/// </summary>
public sealed partial class CLFSaboteurSystem : EntitySystem
{
    private const string ClfFaxGroup = "clf";
    private const string ClfPaperPrototype = "CMUPaperCLF";

    [Dependency] private readonly WantedSystem _wanted = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ApcComponent, EntityTerminatingEvent>(OnApcTerminating);
        SubscribeLocalEvent<CommunicationsConsoleComponent, EntityTerminatingEvent>(OnCommsTerminating);
    }

    private void OnApcTerminating(EntityUid uid, ApcComponent comp, ref EntityTerminatingEvent args)
        => CountSabotage();

    private void OnCommsTerminating(EntityUid uid, CommunicationsConsoleComponent comp, ref EntityTerminatingEvent args)
        => CountSabotage();

    private void CountSabotage()
    {
        var enumerator = EntityManager.AllEntityQueryEnumerator<CLFSaboteurComponent>();
        while (enumerator.MoveNext(out var uid, out var saboteur))
        {
            saboteur.Count++;

            if (!saboteur.BountyPosted)
            {
                saboteur.BountyPosted = true;
                var bounty = EnsureComp<ColonyBountyComponent>(uid);
                bounty.Bounty = 1200;
                bounty.Reason = "CLF sabotage - infrastructure attack";
                bounty.RecordName = "The Saboteur (Unknown)";
                bounty.CapturedFaxPaper = "CMUPaperColonyAntagCaptured";
            }

            if (!saboteur.AnnouncedComplete && saboteur.Count >= saboteur.SabotageGoal)
            {
                saboteur.AnnouncedComplete = true;
                _wanted.SendFaxToGroup(
                    ClfFaxGroup,
                    "Sabotage Report",
                    "[head=3][color=#2e5a1e]Colony Liberation Front[/color][/head]\n\n" +
                    "Comrades,\n" +
                    $"  Primary sabotage complete: {saboteur.Count} enemy infrastructure assets destroyed. " +
                    "The occupiers are blind and bleeding. Freedom or death.\n\n" +
                    "[color=#2e5a1e][bolditalic]CLF Command[/bolditalic][/color]",
                    "paper_stamp-clf",
                    new List<StampDisplayInfo>
                    {
                        new() { StampedColor = Color.FromHex("#2e5a1e"), StampedName = "CLF" },
                    }, ClfPaperPrototype);
            }
        }
    }
}
