using Content.Shared.Access.Systems;
using Content.Shared.Doors;
using Content.Shared.Doors.Components;
using Content.Shared.Prying.Components;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Doors;

/// <summary>
///     Keeps a sealed <see cref="CMUFirelockSealComponent"/> firelock shut against anyone without access unless they use
///     a crowbar, and starts its re-close timer whenever it's opened while sealed. The server decides when it seals.
/// </summary>
public sealed partial class CMUFirelockSealSystem : EntitySystem
{
    [Dependency] private AccessReaderSystem _access = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUFirelockSealComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CMUFirelockSealComponent, BeforeDoorOpenedEvent>(OnBeforeOpened);
        SubscribeLocalEvent<CMUFirelockSealComponent, BeforePryEvent>(OnBeforePry);
        SubscribeLocalEvent<CMUFirelockSealComponent, DoorStateChangedEvent>(OnStateChanged);
    }

    private void OnMapInit(Entity<CMUFirelockSealComponent> ent, ref MapInitEvent args)
    {
        if (!ent.Comp.RequiresPower)
            KeepPowered(ent);
    }

    /// <summary>
    ///     These firelocks run without power, so the upstream firelock logic always treats them as powered.
    /// </summary>
    public void KeepPowered(EntityUid uid)
    {
        if (!TryComp(uid, out FirelockComponent? firelock) || firelock.Powered)
            return;

        firelock.Powered = true;
        Dirty(uid, firelock);
    }

    private void OnBeforeOpened(EntityUid uid, CMUFirelockSealComponent comp, BeforeDoorOpenedEvent args)
    {
        // Opening by hand or remote. Prying skips this event entirely.
        if (!comp.Sealed)
            return;

        if (args.User is not { } user || !_access.IsAllowed(user, uid))
            args.Cancel();
    }

    private void OnBeforePry(Entity<CMUFirelockSealComponent> ent, ref BeforePryEvent args)
    {
        // Bare hands can't force a sealed firelock; a crowbar can.
        if (ent.Comp.Sealed && !args.StrongPry)
            args.Cancelled = true;
    }

    private void OnStateChanged(EntityUid uid, CMUFirelockSealComponent comp, DoorStateChangedEvent args)
    {
        // However it got opened while sealed, it only stays that way for a few seconds.
        if (!comp.Sealed || args.State != DoorState.Opening || comp.CloseAt != null)
            return;

        comp.CloseAt = _timing.CurTime + comp.PriedOpenDuration;
        Dirty(uid, comp);
    }
}
