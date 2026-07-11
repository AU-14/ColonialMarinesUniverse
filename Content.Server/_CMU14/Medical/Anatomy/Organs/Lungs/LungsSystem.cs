using Content.Shared._CMU14.Medical.Anatomy.Organs.Lungs;
using Content.Shared._RMC14.Emote;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Popups;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server._CMU14.Medical.Anatomy.Organs.Lungs;

public sealed partial class LungsSystem : SharedLungsSystem
{
    [Dependency] private SharedBloodstreamSystem _bloodstream = default!;
    [Dependency] private SharedRMCEmoteSystem _emote = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    private static readonly ProtoId<DamageTypePrototype> Asphyxiation = "Asphyxiation";
    private static readonly ProtoId<EmotePrototype> Cough = "Cough";

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        UpdateServer(frameTime);
    }

    /// <summary>
    ///     Bypasses resistances so a marine drowning on damaged lungs cannot
    ///     be saved by armour.
    /// </summary>
    protected override void ApplyAsphyx(EntityUid body, EntityUid lung, FixedPoint2 amount)
    {
        if (!_proto.TryIndex(Asphyxiation, out _))
            return;

        var spec = new DamageSpecifier { DamageDict = { [Asphyxiation.Id] = amount } };
        Damageable.TryChangeDamage(body, spec, ignoreResistances: true, origin: lung);
    }

    protected override void ApplyBloodCough(EntityUid body, EntityUid lung, FixedPoint2 bloodLoss)
    {
        if (bloodLoss > FixedPoint2.Zero && TryComp<BloodstreamComponent>(body, out var bloodstream))
            _bloodstream.TryModifyBloodLevel((body, bloodstream), -bloodLoss);

        _emote.TryEmoteWithChat(body, Cough, forceEmote: true, cooldown: TimeSpan.Zero);
        _popup.PopupEntity(
            Loc.GetString("cmu-medical-lungs-cough-blood"),
            body,
            body,
            PopupType.MediumCaution);
    }
}
