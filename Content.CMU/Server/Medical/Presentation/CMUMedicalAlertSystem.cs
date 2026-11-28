using Content.Shared.Alert;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Injuries.Shrapnel;
using Content.Shared.CMU14.Medical.Treatment.FirstAid;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Medical.Presentation;

public sealed partial class CMUMedicalAlertSystem : EntitySystem
{
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private CMUMedicalBodyIndexSystem _medicalIndex = default!;

    private static readonly ProtoId<AlertPrototype> Shrapnel = "CMUEmbeddedShrapnel";
    private static readonly ProtoId<AlertPrototype> Splinted = "CMUSplinted";
    private static readonly ProtoId<AlertPrototype> Cast = "CMUCast";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AlertsComponent, CMUMedicalChangedEvent>(OnMedicalChanged);
    }

    private void OnMedicalChanged(Entity<AlertsComponent> body, ref CMUMedicalChangedEvent args)
    {
        const CMUMedicalChangeFlags relevant = CMUMedicalChangeFlags.Topology |
            CMUMedicalChangeFlags.Wounds | CMUMedicalChangeFlags.Treatment;
        if ((args.Changes & relevant) == 0)
            return;

        var hasShrapnel = false;
        var hasSplint = false;
        var hasCast = false;
        foreach (var (part, _) in _medicalIndex.GetBodyParts(body.Owner))
        {
            hasShrapnel |= TryComp<CMUShrapnelComponent>(part, out var shrapnel) && shrapnel.Fragments > 0;
            hasSplint |= HasComp<CMUSplintedComponent>(part);
            hasCast |= HasComp<CMUCastComponent>(part);
        }

        SetAlert(body, Shrapnel, hasShrapnel);
        SetAlert(body, Splinted, hasSplint);
        SetAlert(body, Cast, hasCast);
    }

    private void SetAlert(Entity<AlertsComponent> body, ProtoId<AlertPrototype> alert, bool active)
    {
        if (active)
            _alerts.ShowAlert(body.AsNullable(), alert);
        else
            _alerts.ClearAlert(body.AsNullable(), alert);
    }
}
