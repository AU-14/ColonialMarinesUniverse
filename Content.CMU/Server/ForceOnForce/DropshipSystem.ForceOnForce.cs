using Content.Server.GameTicking;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Marines;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.CMU14;
using Content.Shared.CMU14.ForceOnForce;
using Content.Shared.CMU14.Round;
using Content.Shared._RMC14.Xenonids;

namespace Content.Server._RMC14.Dropship;

public sealed partial class DropshipSystem
{
    private void AnnounceForceOnForceBoarders(Entity<DropshipComponent> dropship)
    {
        if (EntityManager.System<GameTicker>().CurrentPreset?.ID.Equals("ForceOnForce", StringComparison.OrdinalIgnoreCase) != true ||
            !TryGetDropshipNavigationComputer(dropship, out var computer) ||
            !TryComp<WhitelistedShuttleComponent>(computer.Owner, out var shuttle) ||
            ForceOnForceSystem.Opponent(shuttle.Faction) is not { } enemy)
            return;

        var count = 0;
        var query = EntityQueryEnumerator<MarineComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out var marine, out var mob))
        {
            if (mob.CurrentState != MobState.Dead && string.Equals(marine.Faction, enemy, StringComparison.OrdinalIgnoreCase) &&
                TryGetGridDropship(uid, out var boarded) && boarded.Owner == dropship.Owner)
                count++;
        }

        if (count == 0)
            return;

        _marineAnnounce.AnnounceARES(dropship,
            Loc.GetString("cmu-fof-hostile-boarders", ("name", Name(dropship)), ("count", count)),
            dropship.Comp.UnidentifledlifesignsSound, shuttle.Faction);
    }

    protected override bool IsForceOnForceHijacker(EntityUid computer, EntityUid user)
    {
        var faction = EntityManager.System<ForceOnForceSystem>();
        return IsForceOnForceHuman(user) &&
            faction.CanCommand(user, includeSquadLeaders: true) &&
            TryComp<WhitelistedShuttleComponent>(computer, out var shuttle) &&
            ForceOnForceSystem.Opponent(faction.GetFaction(user)) == shuttle.Faction?.ToLowerInvariant();
    }

    protected override bool IsForceOnForceHuman(EntityUid user) =>
        EntityManager.System<GameTicker>().CurrentPreset?.ID.Equals("ForceOnForce", StringComparison.OrdinalIgnoreCase) == true &&
        !HasComp<XenoComponent>(user) && EntityManager.System<ForceOnForceSystem>().GetFaction(user) != null;

    private bool CanLandAt(EntityUid computer, EntityUid destination)
    {
        if (!TryComp<WhitelistedShuttleComponent>(computer, out var shuttle))
            return GetCarrierFaction(destination) == null;

        var carrierFaction = GetCarrierFaction(destination);
        var controller = TryComp<DropshipDestinationComponent>(destination, out var landing)
            ? landing.FactionController
            : null;
        return (string.IsNullOrWhiteSpace(carrierFaction) || string.Equals(carrierFaction, shuttle.Faction, StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrWhiteSpace(controller) || string.Equals(controller, shuttle.Faction, StringComparison.OrdinalIgnoreCase));
    }
}
