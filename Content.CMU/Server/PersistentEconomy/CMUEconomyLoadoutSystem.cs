using System.Linq;
using Content.Shared.Clothing;
using Content.Shared.CMU14.PersistentEconomy;
using Content.Shared.Mind;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Roles.Jobs;
using Content.Shared.Station;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.PersistentEconomy;

/// <summary>
/// Bridges the existing character loadout system into the persistent economy.
/// The normal loadout UI remains authoritative; CMU economy prototypes only add
/// a money price and purchase mode to matching loadout items.
/// </summary>
public sealed class CMUEconomyLoadoutSystem : EntitySystem
{
    [Dependency] private readonly CMUPersistentEconomySystem _economy = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly LoadoutSystem _loadouts = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedJobSystem _jobs = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BeforeRoleLoadoutEquipEvent>(OnBeforeRoleLoadoutEquip);
    }

    private void OnBeforeRoleLoadoutEquip(ref BeforeRoleLoadoutEquipEvent args)
    {
        if (!_economy.Enabled || _economy.RoundId <= 0 ||
            !TryComp<ActorComponent>(args.Entity, out var actor) ||
            !_mind.TryGetMind(actor.PlayerSession.UserId, out var mindId, out _) ||
            !_jobs.MindTryGetJob(mindId, out var job) || !job.CmuEconomyEnabled ||
            !_prototypes.TryIndex<LoadoutPrototype>(args.Loadout, out var loadout))
            return;

        var entity = _loadouts.GetFirstOrNull(loadout);
        if (entity == null)
            return;

        var priced = _prototypes.EnumeratePrototypes<CMULoadoutItemPrototype>()
            .FirstOrDefault(item => item.Entity == entity.Value && item.Jobs.Contains(job.ID));
        if (priced == null || priced.Price <= 0)
            return;

        var user = actor.PlayerSession.UserId.UserId;
        var round = _economy.Store.ReadRound(user, _economy.RoundId);

        // Paid preset gear is issued only once per deployment. Respawns do not mint
        // another copy merely because the character still has the preset selected.
        if (round.DeploymentIssued)
        {
            args.Cancelled = true;
            return;
        }

        var account = _economy.Store.ReadAccount(user);
        if (priced.PurchaseMode == CMUPurchaseMode.Permanent && account.Purchases.ContainsKey(priced.ID))
            return;

        var key = $"loadout:{_economy.RoundId}:{user}:{args.Loadout}";
        var success = _economy.Store.Mutate(user, _economy.RoundId, key, op =>
        {
            if (priced.PurchaseMode == CMUPurchaseMode.Permanent)
                return op.Buy(priced.ID, priced.Price);

            return op.Change(-priced.Price, "LoadoutDeployment", $"Loadout preset {args.Loadout}");
        });

        if (!success)
            args.Cancelled = true;
    }
}
