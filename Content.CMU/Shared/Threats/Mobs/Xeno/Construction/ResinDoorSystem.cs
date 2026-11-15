/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
/// reason: Because I, (MACMAN2003), the initial coder of this specific file disagree with the AGPL's copyleft approach to
/// free software and would prefer this code be shared freely without restrictions.

using Content.Shared._RMC14.Xenonids.Construction.ResinWhisper;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared.Doors;
using Robust.Shared.Network;
using Robust.Shared.Timing;
using CultistComponent = Content.Shared.CMU14.Threats.Mobs.Cultist.CultistComponent;

namespace Content.Shared.CMU14.Threats.Mobs.Xeno.Construction;

public sealed partial class ResinDoorSystem : EntitySystem
{
    [Dependency] private SharedXenoHiveSystem _hive = default!;
    [Dependency] private INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ResinDoorComponent, BeforeDoorOpenedEvent>(OnBeforeDoorOpen);
        SubscribeLocalEvent<ResinDoorComponent, BeforeDoorClosedEvent>(OnBeforeDoorClose);
        SubscribeLocalEvent<ResinDoorComponent, MapInitEvent>(OnDoorMapInit);
    }

    private void OnBeforeDoorOpen(Entity<ResinDoorComponent> ent, ref BeforeDoorOpenedEvent args)
    {
        if (args.User is null) return;

        if (!CanAccess(args.User.Value, ent.Owner))
            args.Cancel();
    }

    private void OnBeforeDoorClose(Entity<ResinDoorComponent> ent, ref BeforeDoorClosedEvent args)
    {
        if (args.User is null) return;

        if (!CanAccess(args.User.Value, ent.Owner))
            args.Cancel();
    }

    // Map-placed ruin doors never pass through a construction path, so nothing stamps their hive.
    // Without one the access gate fails open and anyone can open them.
    private void OnDoorMapInit(Entity<ResinDoorComponent> ent, ref MapInitEvent args)
    {
        TryAssignHive(ent.Owner);
    }

    private void TryAssignHive(EntityUid door)
    {
        if (_net.IsClient || TerminatingOrDeleted(door))
            return;

        // a built door is assigned by its builder right after spawn, so never steal it
        if (_hive.GetHive(door) is not null)
            return;

        var hives = EntityQueryEnumerator<HiveComponent, MetaDataComponent>();
        while (hives.MoveNext(out var hiveUid, out _, out var meta))
        {
            if (meta.EntityPrototype?.ID != "CMXenoHive")
                continue;

            _hive.SetHive(door, hiveUid);
            return;
        }

        // the hive may not exist yet at map load; keep retrying, just slowly
        Timer.Spawn(TimeSpan.FromSeconds(1), () => TryAssignHive(door));
    }

    private bool CanAccess(EntityUid user, EntityUid door)
    {
        // cultists are xeno admirers (non-hostiles)
        if (HasComp<CultistComponent>(user))
            return true;

        Entity<HiveComponent>? hive = _hive.GetHive(door);
        if (hive is null)
        {
            EntityUid? doorHive = TryComp(door, out HiveMemberComponent? doorComp)
                ? doorComp.Hive
                : null;
            EntityUid? userHive = TryComp(user, out HiveMemberComponent? userComp)
                ? userComp.Hive
                : null;

            // if (doorHive == null || doorHive == EntityUid.Invalid || userHive == null || userHive == EntityUid.Invalid)
            // {
            //     Logger.GetSawmill("hive").Debug(
            //         $"Resin door ({door}) is missing a Hive. Door hive: {doorHive}, User ({user}) hive: {userHive}. Permitting access.");
            // }

            return true; // IsAllyOfHive early-returns false when Hive is null
        }

        return _hive.IsAllyOfHive(user, hive);
    }
}
