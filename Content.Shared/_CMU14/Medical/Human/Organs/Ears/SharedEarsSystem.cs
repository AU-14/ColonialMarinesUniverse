using Content.Shared._CMU14.Medical.Human.Components;
using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared._CMU14.Medical.Human.Systems;
using Content.Shared.StatusEffectNew;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Medical.Human.Organs.Ears;

public abstract partial class SharedEarsSystem : EntitySystem
{
    [Dependency] protected SharedStatusEffectsSystem Status = default!;

    private static readonly EntProtoId Tinnitus = "StatusEffectCMUTinnitus";
    private static readonly EntProtoId Deafened = "StatusEffectCMUDeafened";

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

        var status = HumanOrganLedgerUtility.EffectiveStatus(medical, OrganSlot.Ears);
        ApplyHearingStatus(args.Body, status);
    }

    private void ApplyHearingStatus(EntityUid body, OrganDamageStatus status)
    {
        Status.TryRemoveStatusEffect(body, Tinnitus);
        Status.TryRemoveStatusEffect(body, Deafened);

        switch (status)
        {
            case OrganDamageStatus.Bruised:
                Status.TrySetStatusEffectDuration(body, Tinnitus, duration: null);
                break;
            case OrganDamageStatus.Broken:
                Status.TrySetStatusEffectDuration(body, Deafened, duration: null);
                break;
        }
    }
}
