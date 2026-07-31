using Content.Shared._CMU14.Medical.Injuries.Pain;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Robust.Shared.GameObjects;

namespace Content.Server._CMU14.Medical.Injuries.Pain;

public sealed partial class PainShockSystem : SharedPainShockSystem
{
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private SharedStunSystem _stun = default!;

    private static readonly string[] MildPainReflections =
    {
        "cmu-medical-pain-reflection-mild-1",
        "cmu-medical-pain-reflection-mild-2",
        "cmu-medical-pain-reflection-mild-3",
    };

    private static readonly string[] ModeratePainReflections =
    {
        "cmu-medical-pain-reflection-moderate-1",
        "cmu-medical-pain-reflection-moderate-2",
        "cmu-medical-pain-reflection-moderate-3",
    };

    private static readonly string[] SeverePainReflections =
    {
        "cmu-medical-pain-reflection-severe-1",
        "cmu-medical-pain-reflection-severe-2",
        "cmu-medical-pain-reflection-severe-3",
    };

    private static readonly string[] ShockPainReflections =
    {
        "cmu-medical-pain-reflection-shock-1",
        "cmu-medical-pain-reflection-shock-2",
        "cmu-medical-pain-reflection-shock-3",
    };

    private static readonly string[] PainReliefReflections =
    {
        "cmu-medical-pain-relief-1",
        "cmu-medical-pain-relief-2",
        "cmu-medical-pain-relief-3",
    };

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        UpdateServer(frameTime);
    }

    protected override void ApplyShockEntryEffect(EntityUid body)
    {
        TryKnockdownStandingPatient(body);
    }

    protected override void ApplyPeriodicShockKnockdown(EntityUid body)
    {
        TryKnockdownStandingPatient(body);
        _popup.PopupEntity(Loc.GetString("cmu-medical-pain-shock-pulse"), body, body, PopupType.LargeCaution);
    }

    private void TryKnockdownStandingPatient(EntityUid body)
    {
        if (_standing.IsDown(body))
            return;

        _stun.TryKnockdown(body, TimeSpan.FromSeconds(1), refresh: false);
    }

    protected override void ApplyPainReflection(EntityUid body, PainTier tier)
    {
        var keys = tier switch
        {
            PainTier.Mild => MildPainReflections,
            PainTier.Moderate => ModeratePainReflections,
            PainTier.Severe => SeverePainReflections,
            PainTier.Shock => ShockPainReflections,
            _ => null,
        };

        if (keys is null || keys.Length == 0)
            return;

        var popupType = tier switch
        {
            PainTier.Severe => PopupType.MediumCaution,
            PainTier.Shock => PopupType.LargeCaution,
            _ => PopupType.SmallCaution,
        };
        _popup.PopupEntity(Loc.GetString(keys[Random.Next(keys.Length)]), body, body, popupType);
    }

    protected override void ApplyPainRelief(EntityUid body, PainTier tier)
    {
        _popup.PopupEntity(
            Loc.GetString(PainReliefReflections[Random.Next(PainReliefReflections.Length)]),
            body,
            body,
            PopupType.Medium);
    }
}
