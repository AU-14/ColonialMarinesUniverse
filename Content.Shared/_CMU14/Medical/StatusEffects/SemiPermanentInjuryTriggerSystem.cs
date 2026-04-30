using Content.Shared._CMU14.Medical.BodyPart;
using Content.Shared._CMU14.Medical.BodyPart.Events;
using Content.Shared._CMU14.Medical.Organs.Brain;
using Content.Shared._CMU14.Medical.Organs.Events;
using Content.Shared.Body.Part;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Medical.StatusEffects;

public sealed class SemiPermanentInjuryTriggerSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly SharedStatusEffectsSystem _status = default!;

    private static readonly EntProtoId Whiplash = "StatusEffectCMUWhiplash";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUBrainComponent, OrganDamagedEvent>(OnBrainHit);
        SubscribeLocalEvent<BodyPartHealthComponent, BodyPartHealedEvent>(OnPartHealed);
    }

    private bool IsEnabled()
    {
        return _cfg.GetCVar(CMUMedicalCCVars.Enabled)
            && _cfg.GetCVar(CMUMedicalCCVars.WoundsEnabled);
    }

    /// <summary>
    ///     Whiplash from a brute hit on the brain. Only fires for direct
    ///     part-distribution hits (i.e. a head impact damaged the brain via
    ///     the BoneShieldsOrgans-let-through path) — surgery / reagent / rib
    ///     burst paths route through other sources and don't whiplash.
    /// </summary>
    private void OnBrainHit(Entity<CMUBrainComponent> ent, ref OrganDamagedEvent args)
    {
        if (!IsEnabled())
            return;
        if (args.Source != OrganDamageSource.PartDistribution)
            return;
        if (args.Damage.GetTotal() < 5)
            return;
        _status.TrySetStatusEffectDuration(args.Body, Whiplash, TimeSpan.FromMinutes(5));
    }

    /// <summary>
    ///     Nerve-damage triggers when a limb crosses up through the 10% HP
    ///     threshold — i.e. the limb was almost destroyed and is now healing
    ///     back.
    /// </summary>
    private void OnPartHealed(Entity<BodyPartHealthComponent> ent, ref BodyPartHealedEvent args)
    {
        if (!IsEnabled())
            return;
        var statusId = ResolveNerveStatus(args.Type);
        if (statusId is null)
            return;
        _status.TrySetStatusEffectDuration(args.Body, statusId.Value, TimeSpan.FromMinutes(30));
    }

    private static EntProtoId? ResolveNerveStatus(BodyPartType type) => type switch
    {
        BodyPartType.Arm => "StatusEffectCMUNerveDamageArm",
        BodyPartType.Hand => "StatusEffectCMUNerveDamageHand",
        BodyPartType.Leg => "StatusEffectCMUNerveDamageLeg",
        BodyPartType.Foot => "StatusEffectCMUNerveDamageFoot",
        _ => null,
    };
}
