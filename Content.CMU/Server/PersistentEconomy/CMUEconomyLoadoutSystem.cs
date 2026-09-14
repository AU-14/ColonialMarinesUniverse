using System.Linq;
using Content.Shared.Clothing;
using Content.Shared.CMU14.PersistentEconomy;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Roles;
using Content.Shared.Station;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.PersistentEconomy;

/// <summary>
/// Bridges the existing character loadout system into the persistent economy.
/// The normal loadout UI remains authoritative; CMU economy prototypes only add
/// a money price and purchase mode to matching loadout items.
/// </summary>
public sealed class CMUEconomyLoadoutSystem : EntitySystem
{
    [Dependency] private CMUPersistentEconomySystem _economy = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private LoadoutSystem _loadouts = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BeforeRoleLoadoutEquipEvent>(OnBeforeRoleLoadoutEquip);
    }

    private void OnBeforeRoleLoadoutEquip(ref BeforeRoleLoadoutEquipEvent args)
    {
        if (!_economy.Enabled || _economy.RoundId <= 0 ||
            args.PlayerUserId == null || args.Job == null)
            return;

        var playerUserId = args.PlayerUserId.Value;
        var jobId = args.Job.Value;
        var loadoutId = args.Loadout;
        if (!_prototypes.TryIndex<JobPrototype>(jobId, out var job) || !job.CmuEconomyEnabled ||
            !_prototypes.TryIndex<LoadoutPrototype>(loadoutId, out var loadout))
            return;

        var entity = _loadouts.GetFirstOrNull(loadout);
        if (entity == null)
            return;

        var priced = _prototypes.EnumeratePrototypes<CMULoadoutItemPrototype>()
            .FirstOrDefault(item => item.Entity == entity.Value && item.Jobs.Contains(job.ID));
        if (priced == null || priced.Price <= 0)
            return;

        var user = playerUserId.UserId;
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

        var key = $"loadout:{_economy.RoundId}:{user}:{loadoutId}";
        var success = _economy.Store.Mutate(user, _economy.RoundId, key, op =>
        {
            if (priced.PurchaseMode == CMUPurchaseMode.Permanent)
                return op.Buy(priced.ID, priced.Price);

            return op.Change(-priced.Price, "LoadoutDeployment", $"Loadout preset {loadoutId}");
        });

        if (!success)
            args.Cancelled = true;
    }
}
