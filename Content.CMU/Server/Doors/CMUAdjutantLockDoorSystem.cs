using Content.Shared._CMU14.Doors;
using Content.Shared._RMC14.Roles;
using Content.Shared.Doors;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.GameTicking;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Roles;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._CMU14.Doors;

public sealed class CMUAdjutantLockDoorSystem : EntitySystem
{
    [Dependency] private readonly SharedDoorSystem _doors = default!;
    [Dependency] private readonly SharedGameTicker _gameTicker = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;

    private static readonly ProtoId<JobPrototype> AdjutantJob = "AU14JobGOVFORAdjutant";
    private static readonly ProtoId<JobPrototype> PlatCoJob = "AU14JobGOVFORPlatCo";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMUAdjutantLockDoorComponent, DoorStateChangedEvent>(OnDoorStateChanged);
        SubscribeLocalEvent<CMUAdjutantLockDoorComponent, BeforeDoorClosedEvent>(OnBeforeDoorClosed);
    }

    private void OnDoorStateChanged(Entity<CMUAdjutantLockDoorComponent> ent, ref DoorStateChangedEvent args)
    {
        if (args.State != DoorState.Open || ent.Comp.HasEverOpened)
            return;

        ent.Comp.HasEverOpened = true;
        Dirty(ent);
    }

    private void OnBeforeDoorClosed(Entity<CMUAdjutantLockDoorComponent> ent, ref BeforeDoorClosedEvent args)
    {
        if (ent.Comp.Locked)
            args.Cancel();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var duration = _gameTicker.RoundDuration();
        var awakeChecked = false;
        var awake = false;

        var query = EntityQueryEnumerator<CMUAdjutantLockDoorComponent, DoorComponent>();
        while (query.MoveNext(out var uid, out var lockComp, out var door))
        {
            if (lockComp.Locked)
                continue;

            if (!lockComp.CommandCheckDone && duration >= lockComp.CommandCheckTime)
            {
                lockComp.CommandCheckDone = true;
                Dirty(uid, lockComp);

                if (!awakeChecked)
                {
                    awake = IsGovforCommandAwake();
                    awakeChecked = true;
                }

                if (!awake)
                {
                    LockOpen((uid, lockComp), door);
                    continue;
                }
            }

            if (!lockComp.Locked && !lockComp.HasEverOpened && duration >= lockComp.FailsafeOpenTime)
                LockOpen((uid, lockComp), door);
        }
    }

    private bool IsGovforCommandAwake()
    {
        var query = EntityQueryEnumerator<OriginalRoleComponent, MobStateComponent, ActorComponent>();
        while (query.MoveNext(out var uid, out var role, out var mobState, out var actor))
        {
            if (role.Job is not { } job || (job != AdjutantJob && job != PlatCoJob))
                continue;

            if (actor.PlayerSession == null)
                continue;

            if (!_mobState.IsAlive(uid, mobState))
                continue;

            return true;
        }

        return false;
    }

    private void LockOpen(Entity<CMUAdjutantLockDoorComponent> ent, DoorComponent door)
    {
        ent.Comp.Locked = true;
        Dirty(ent);

        _doors.TryOpen(ent.Owner, door);
    }
}
