using System.Numerics;
using Content.Shared._RMC14.Dropship.Fabricator;
using Content.Shared._RMC14.Marines.Announce;
using Content.Shared._RMC14.Requisitions;
using Content.Shared._RMC14.Requisitions.Components;
using Content.Shared._RMC14.Scaling;
using Content.Shared.AU14.Threats;
using Content.Shared.AU14.Util;
using Content.Shared.GameTicking;
using Content.Shared.UserInterface;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Intel.Tech;

public sealed class TechSystem : EntitySystem
{
    [Dependency] private readonly DropshipFabricatorSystem _dropshipFabricator = default!;
    [Dependency] private readonly SharedGameTicker _ticker = default!;
    [Dependency] private readonly IntelSystem _intel = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedMarineAnnounceSystem _marineAnnounce = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedRequisitionsSystem _requisitions = default!;
    [Dependency] private readonly ScalingSystem _scaling = default!;
    // NOTE: Do not depend on platform-specific AuThirdPartySystem here (shared) — use ExecuteTechPartySpawn helper
    // to let server code call the server-side spawn implementation.
    [Dependency] private readonly IPrototypeManager _proto = default!;
    public override void Initialize()
    {
        SubscribeLocalEvent<TechAnnounceEvent>(OnTechAnnounce);
        SubscribeLocalEvent<TechUnlockTierEvent>(OnTechUnlockTier);
        SubscribeLocalEvent<TechRequisitionsBudgetEvent>(OnTechRequisitionsBudget);
        SubscribeLocalEvent<TechDropshipBudgetEvent>(OnTechDropshipBudget);
        SubscribeLocalEvent<TechLogisticsDeliveryEvent>(OnTechLogisticsDelivery);

        SubscribeLocalEvent<TechControlConsoleComponent, BeforeActivatableUIOpenEvent>(OnControlConsoleBeforeOpen);

        Subs.BuiEvents<TechControlConsoleComponent>(TechControlConsoleUI.Key,
            subs =>
            {
                subs.Event<TechPurchaseOptionBuiMsg>(OnPurchaseOptionMsg);
            });
    }

    private void OnTechAnnounce(TechAnnounceEvent ev)
    {
        var msg = Loc.GetString("rmc-announcement-message-raw", ("author", ev.Author), ("message", ev.Message));
        _marineAnnounce.AnnounceToMarines(msg, ev.Sound);
    }

    private void OnTechUnlockTier(TechUnlockTierEvent ev)
    {
        var tree = _intel.EnsureTechTree(ev.Team);
        tree.Comp.Tree.Tier = ev.Tier;
        Dirty(tree);
        _intel.UpdateTree(tree);
    }

    private void OnTechRequisitionsBudget(TechRequisitionsBudgetEvent ev)
    {
        var scaling = _scaling.GetAliveHumanoids() / 50;
        scaling = Math.Max(1, scaling);
        // Apply budget to the specific team's requisitions account if provided
        var faction = string.IsNullOrEmpty(ev.Team) ? null : ev.Team;
        _requisitions.ChangeBudget(ev.Amount * scaling, faction);
    }

    private void OnTechDropshipBudget(TechDropshipBudgetEvent ev)
    {
        _dropshipFabricator.ChangeBudget(ev.Amount);
    }

    private void OnTechLogisticsDelivery(TechLogisticsDeliveryEvent ev)
    {
        _requisitions.CreateSpecialDelivery(ev.Object);
    }

    private void OnControlConsoleBeforeOpen(Entity<TechControlConsoleComponent> ent, ref BeforeActivatableUIOpenEvent args)
    {
        if (_net.IsClient)
            return;

        var team = !string.IsNullOrEmpty(ent.Comp.Team) && ent.Comp.Team != Team.None ? ent.Comp.Team : Team.None;
        var treeEntity = _intel.EnsureTechTree(team);
        ent.Comp.Tree = treeEntity.Comp.Tree;
        _intel.UpdateTree(treeEntity);
        Dirty(ent);
    }

    private void OnPurchaseOptionMsg(Entity<TechControlConsoleComponent> ent, ref TechPurchaseOptionBuiMsg args)
    {
        if (_net.IsClient)
            return;

        var team = !string.IsNullOrEmpty(args.Team) && args.Team != Team.None ? args.Team : ent.Comp.Team;

        var tree = _intel.EnsureTechTree(team);
        if (tree.Comp.Tree.Tier < args.Tier ||
            !tree.Comp.Tree.Options.TryGetValue(args.Tier, out var tier))
        {
            Log.Warning($"{ToPrettyString(args.Actor)} tried to buy tech option with invalid tier {args.Tier}");
            return;
        }

        if (args.Index < 0 ||
            !tier.TryGetValue(args.Index, out var option))
        {
            Log.Warning($"{ToPrettyString(args.Actor)} tried to buy tech option with invalid index {args.Index}");
            return;
        }

        if (option.TimeLock  > _ticker.RoundDuration())
            return;

        if (option.Purchased && !option.Repurchasable)
            return;

        if (!_intel.TryUsePoints(team, option.CurrentCost))
            return;

        tier[args.Index] = option with
        {
            CurrentCost = option.CurrentCost + option.Increase,
            Purchased = true,
        };
        Dirty(ent);

        // Raise a shared event so the authoritative ObjectiveMaster/Objective system can deduct AU win points server-side.
        var auAmount =option.CurrentCost;
        var spendEv = new Content.Shared.AU14.Objectives.SpendWinPointsEvent { Team = team, Amount = auAmount };
        RaiseLocalEvent(spendEv);

        foreach (var ev in option.Events)
        {
            if (ev is TechUnlockTierEvent tierEv)
            {
                var newEv = tierEv with { Team = team };
                RaiseLocalEvent(newEv);
            }
            else if (ev is TechAnnounceEvent announceEv)
            {
                var newEv = announceEv with { Team = team };
                RaiseLocalEvent(newEv);
            }
            else if (ev is TechRequisitionsBudgetEvent requisitionsEv)
            {
                var newEv = requisitionsEv with { Team = team };
                RaiseLocalEvent(newEv);
            }
            else if (ev is TechDropshipBudgetEvent dropshipEv)
            {
                var newEv = dropshipEv with { Team = team };
                RaiseLocalEvent(newEv);
            }
            else if (ev is TechLogisticsDeliveryEvent logisticsEv)
            {
                var newEv = logisticsEv with { Team = team };
                RaiseLocalEvent(newEv);
            }
            else if (ev is TechPartySpawnEvent partySpawnEv)
            {
                if (string.IsNullOrEmpty(partySpawnEv.ThirdPartyId))
                {
                    Logger.Warning($"[TechSystem] TechPartySpawnEvent in tech option has empty ThirdPartyId; skipping (team={team}).");
                }
                else
                {
                    var newEv = partySpawnEv with { Team = team };
                    RaiseLocalEvent(newEv);
                }
            }
            else
            {
                // Unknown event type, raise as-is
                RaiseLocalEvent(ev);
            }
        }

        _intel.UpdateTree(tree);
    }

    /// <summary>
    /// Shared helper: execute a TechPartySpawn by resolving the prototype and invoking the provided spawn action
    /// for each requested amount. Returns true when the prototype was found and spawnAction invoked.
    /// </summary>
    public static bool ExecuteTechPartySpawn(IPrototypeManager proto, string thirdPartyId, Action<AuThirdPartyPrototype> spawnAction)
    {
        if (string.IsNullOrEmpty(thirdPartyId))
        {
            Logger.Warning("[TechSystem] ExecuteTechPartySpawn called with null/empty thirdPartyId.");
            return false;
        }
        if (!proto.TryIndex<AuThirdPartyPrototype>(thirdPartyId, out var partyProto))
        {
            proto.TryIndex(thirdPartyId, out var _); // keep for debug if needed
            Logger.Warning($"[TechSystem] Requested third party id '{thirdPartyId}' not found in prototypes.");
            return false;
        }


            spawnAction(partyProto);


        return true;
    }
}
