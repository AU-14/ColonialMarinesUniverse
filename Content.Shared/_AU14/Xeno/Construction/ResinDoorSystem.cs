/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
/// reason: Because I, (MACMAN2003), the initial coder of this specific file disagree with the AGPL's copyleft approach to
/// free software and would prefer this code be shared freely without restrictions.
using Content.Shared._RMC14.Xenonids.Construction.ResinWhisper;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared.AU14;
using Content.Shared.Doors;

namespace Content.Shared._AU14.Xeno.Construction;

public sealed partial class ResinDoorSystem : EntitySystem
{
    [Dependency] private SharedXenoHiveSystem _hive = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ResinDoorComponent, BeforeDoorOpenedEvent>(OnBeforeDoorOpen);
        SubscribeLocalEvent<ResinDoorComponent, BeforeDoorClosedEvent>(OnBeforeDoorClose);

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

    private bool CanAccess(EntityUid user, EntityUid door)
    {
        // cultists are xeno admirers (non-hostiles)
        if (HasComp<CultistComponent>(user))
            return true;

        var hive = _hive.GetHive(door);
        if (hive is null)
        {
            Logger.GetSawmill("hive").Warning($"Resin door ({door}) is missing a Hive, permitting access");
            return true; // IsAllyOfHive early-returns false when Hive is null
        }

        return _hive.IsAllyOfHive(user, hive);
    }
}
