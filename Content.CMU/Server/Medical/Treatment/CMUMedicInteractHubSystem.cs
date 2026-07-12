using Content.Server._CMU14.Medical.Diagnostics;
using Content.Server._CMU14.Medical.Injuries.Wounds;
using Content.Shared._CMU14.Medical.Core;
using Content.Shared._CMU14.Medical.Injuries.Wounds.Events;
using Content.Shared._CMU14.Yautja;
using Content.Shared._RMC14.Medical.Scanner;
using Content.Shared.Interaction;
using Robust.Shared.GameObjects;

namespace Content.Server._CMU14.Medical.Treatment;

public sealed partial class CMUMedicInteractHubSystem : EntitySystem
{
    [Dependency] private CMUBandageInterceptionSystem _bandage = default!;
    [Dependency] private CMUStethoscopeSystem _stethoscope = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUWoundTreaterInterceptEvent>(OnWoundTreaterIntercept);
        SubscribeLocalEvent<CMUHumanMedicalComponent, AfterInteractEvent>(OnMedicAfterInteract);
    }

    private void OnWoundTreaterIntercept(ref CMUWoundTreaterInterceptEvent args)
    {
        if (args.Handled)
            return;
        if (!HasComp<CMUHumanMedicalComponent>(args.User) &&
            !HasComp<YautjaMedicalItemComponent>(args.Treater))
        {
            return;
        }

        var fakeArgs = new AfterInteractEvent(args.User, args.Treater, args.Patient, default, true);
        _bandage.HandleAfterInteract(args.User, ref fakeArgs);
        if (fakeArgs.Handled)
            args.Handled = true;
    }

    private void OnMedicAfterInteract(Entity<CMUHumanMedicalComponent> medic, ref AfterInteractEvent args)
    {
        if (args.Handled)
            return;
        if (HasComp<RMCStethoscopeComponent>(args.Used))
            _stethoscope.HandleAfterInteract(medic, ref args);
    }
}
