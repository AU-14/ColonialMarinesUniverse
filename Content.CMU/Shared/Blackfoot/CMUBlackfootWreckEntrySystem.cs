using Content.Shared._RMC14.Vehicle;
using Content.Shared.Popups;
using Robust.Shared.Network;

namespace Content.Shared.CMU14.Blackfoot;

public sealed partial class CMUBlackfootWreckEntrySystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUBlackfootDestructionComponent, VehicleEntryAttemptEvent>(OnEntryAttempt);
    }

    private void OnEntryAttempt(Entity<CMUBlackfootDestructionComponent> ent, ref VehicleEntryAttemptEvent args)
    {
        if (!TryComp(ent, out HardpointIntegrityComponent? frame) || !frame.DestroyedBeyondRepair)
            return;

        args.Cancelled = true;
        if (_net.IsServer)
            _popup.PopupEntity(Loc.GetString("cmu-blackfoot-wreck-entry-blocked"), ent.Owner, args.User, PopupType.SmallCaution);
    }
}
