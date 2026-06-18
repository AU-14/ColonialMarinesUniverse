using Content.Shared._CMU14.Medical.Human.Components;
using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared._CMU14.Medical.Human.Systems;
using Robust.Shared.GameObjects;

namespace Content.Shared._CMU14.Medical.Human.Organs.Eyes;

public abstract partial class SharedEyesSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HumanMedicalLedgerChangedEvent>(OnLedgerChanged);
    }

    private void OnLedgerChanged(ref HumanMedicalLedgerChangedEvent args)
    {
        if (!HumanOrganLedgerUtility.OrgansChanged(args.Result))
            return;

        if (!TryComp(args.Body, out HumanMedicalComponent? medical))
            return;

        var status = HumanOrganLedgerUtility.EffectiveStatus(medical, OrganSlot.Eyes);
        UpdateVisionStatus(args.Body, status);
    }

    protected virtual void UpdateVisionStatus(EntityUid body, OrganDamageStatus status)
    {
    }
}
