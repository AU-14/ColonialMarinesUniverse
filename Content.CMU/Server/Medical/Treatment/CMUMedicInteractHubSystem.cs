using Content.Server.CMU14.Medical.Injuries.Wounds;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Injuries.Wounds.Events;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Interaction;
using Robust.Shared.GameObjects;

namespace Content.Server.CMU14.Medical.Treatment;

public sealed partial class CMUMedicInteractHubSystem : EntitySystem
{
    [Dependency] private CMUBandageInterceptionSystem _bandage = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUWoundTreaterInterceptEvent>(OnWoundTreaterIntercept);
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

}
