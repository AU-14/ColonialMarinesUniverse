using Content.Shared._CMU14.Medical.Human.Diagnostics;
using Content.Shared._CMU14.Medical.Human.Effects;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Robust.Shared.GameObjects;

namespace Content.Server._CMU14.Medical.Human.Effects;

public sealed partial class PainShockSystem : SharedPainShockSystem
{
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStunSystem _stun = default!;

    private static readonly string[] PainReliefReflections =
    {
        "cmu-medical-pain-relief-1",
        "cmu-medical-pain-relief-2",
        "cmu-medical-pain-relief-3",
    };

    private readonly List<string> _handsToDrop = new();

    protected override void ApplyShockEntryEffect(EntityUid body)
    {
        _stun.TryKnockdown(body, TimeSpan.FromSeconds(1), refresh: false);
    }

    protected override void ApplyPeriodicShockKnockdown(EntityUid body)
    {
        _stun.TryKnockdown(body, TimeSpan.FromSeconds(1), refresh: false);
        _popup.PopupEntity(Loc.GetString("cmu-medical-pain-shock-pulse"), body, body, PopupType.LargeCaution);
    }

    protected override void ApplyPainReflection(EntityUid body, PainTier tier, PainReflectionContext context)
    {
        var key = GetPainReflectionKey(context);
        if (key is null)
            return;

        _popup.PopupEntity(
            Loc.GetString(
                key,
                ("part", Loc.GetString(HumanMedicalScannerBuiSystem.GetRegionLocKey(context.Region)))),
            body,
            body,
            GetPainReflectionPopup(context));

        TryApplyHighPainFumble(body, context);
    }

    protected override void ApplyCustomPain(
        EntityUid body,
        string locKey,
        int flashStrength,
        (string, object)[] locArgs)
    {
        _popup.PopupEntity(
            Loc.GetString(locKey, locArgs),
            body,
            body,
            flashStrength > 0 ? PopupType.LargeCaution : PopupType.MediumCaution);
    }

    protected override void ApplyPainRelief(EntityUid body, PainTier tier)
    {
        _popup.PopupEntity(
            Loc.GetString(PainReliefReflections[Random.Next(PainReliefReflections.Length)]),
            body,
            body,
            PopupType.Medium);
    }

    private static string? GetPainReflectionKey(PainReflectionContext context)
    {
        return (context.Burning, context.Severity) switch
        {
            (true, PainReflectionSeverity.Low) => "cmu-medical-pain-burn-low",
            (true, PainReflectionSeverity.Medium) => "cmu-medical-pain-burn-medium",
            (true, PainReflectionSeverity.High) => "cmu-medical-pain-burn-high",
            (false, PainReflectionSeverity.Low) => "cmu-medical-pain-brute-low",
            (false, PainReflectionSeverity.Medium) => "cmu-medical-pain-brute-medium",
            (false, PainReflectionSeverity.High) => "cmu-medical-pain-brute-high",
            _ => null,
        };
    }

    private static PopupType GetPainReflectionPopup(PainReflectionContext context)
    {
        return context.Severity switch
        {
            PainReflectionSeverity.High => PopupType.LargeCaution,
            PainReflectionSeverity.Medium => PopupType.MediumCaution,
            _ => PopupType.SmallCaution,
        };
    }

    private void TryApplyHighPainFumble(EntityUid body, PainReflectionContext context)
    {
        if (!context.CanFumble ||
            context.FumbleChance <= 0f ||
            Random.NextFloat() >= context.FumbleChance)
        {
            return;
        }

        if (!TryComp<HandsComponent>(body, out var hands))
            return;

        _handsToDrop.Clear();
        for (var i = 0; i < hands.SortedHands.Count; i++)
        {
            var hand = hands.SortedHands[i];
            if (_hands.TryGetHeldItem((body, hands), hand, out _))
                _handsToDrop.Add(hand);
        }

        var dropped = false;
        for (var i = 0; i < _handsToDrop.Count; i++)
        {
            if (_hands.TryDrop((body, hands), _handsToDrop[i], checkActionBlocker: false))
                dropped = true;
        }

        _handsToDrop.Clear();
        if (!dropped)
            return;

        _popup.PopupEntity(
            Loc.GetString("cmu-medical-pain-fumble"),
            body,
            body,
            PopupType.MediumCaution);
    }
}
