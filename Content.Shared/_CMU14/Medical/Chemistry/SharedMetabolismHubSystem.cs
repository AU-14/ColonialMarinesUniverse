using Content.Shared._CMU14.Medical.Foundation;
using Content.Shared._CMU14.Medical.Human.Components;
using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared._CMU14.Medical.Human.Systems;
using Content.Shared._CMU14.Medical.Chemistry.Events;
using Content.Shared.FixedPoint;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.Shared._CMU14.Medical.Chemistry;

/// <summary>
///     Owns the single
///     <c>&lt;HumanMedicalComponent, MetabolismGroupRateModifyEvent&gt;</c>
///     subscription on the body and dispatches ledger organ effects.
///     Centralising the body-level handler avoids duplicate-event subscription conflicts.
/// </summary>
public abstract partial class SharedMetabolismHubSystem : EntitySystem
{
    private static readonly FixedPoint2 BloodstreamDirectOrganDamage = (FixedPoint2) 0.05f;

    [Dependency] protected IConfigurationManager Cfg = default!;
    [Dependency] protected SharedHumanMedicalSystem Medical = default!;
    [Dependency] protected IGameTiming Timing = default!;

    private readonly Dictionary<EntityUid, float> _clearanceCache = new();
    private TimeSpan _clearanceCacheTime;

    private bool _medicalEnabled;
    private bool _organEnabled;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HumanMedicalComponent, MetabolismGroupRateModifyEvent>(OnRate);

        Cfg.OnValueChanged(CMUMedicalCCVars.Enabled, v => _medicalEnabled = v, true);
        Cfg.OnValueChanged(CMUMedicalCCVars.OrganEnabled, v => _organEnabled = v, true);
    }

    private void OnRate(Entity<HumanMedicalComponent> ent, ref MetabolismGroupRateModifyEvent args)
    {
        if (!_medicalEnabled || !_organEnabled)
            return;

        args.Multiplier *= GetClearanceMultiplier(ent.Owner, ent.Comp);
        ApplyBloodstreamDirectDamage(ent, args.Group);
    }

    private float GetClearanceMultiplier(EntityUid body, HumanMedicalComponent medical)
    {
        var now = Timing.CurTime;
        if (_clearanceCacheTime != now)
        {
            _clearanceCacheTime = now;
            _clearanceCache.Clear();
        }

        if (_clearanceCache.TryGetValue(body, out var cached))
            return cached;

        var liver = HumanMedicalLedger.GetOrgan(medical, OrganSlot.Liver);
        var kidneys = HumanMedicalLedger.GetOrgan(medical, OrganSlot.Kidneys);
        var multiplier = GetLiverClearanceMultiplier(liver) * GetKidneyClearanceMultiplier(kidneys);
        _clearanceCache[body] = multiplier;
        return multiplier;
    }

    private void ApplyBloodstreamDirectDamage(Entity<HumanMedicalComponent> body, string group)
    {
        if (group != "Poison" && group != "Alcohol")
            return;

        var transaction = new MedicalTransaction(BodyRegion.Chest);
        transaction.Add(MedicalEffect.AddOrganDamage(
            OrganSlot.Liver,
            BloodstreamDirectOrganDamage));

        Medical.ApplyTransaction(body, transaction);
    }

    private static float GetLiverClearanceMultiplier(OrganState liver)
    {
        if (liver.Missing)
            return 0f;

        return liver.Status switch
        {
            OrganDamageStatus.LittleBruised => 0.8f,
            OrganDamageStatus.Bruised => 0.5f,
            OrganDamageStatus.Broken => 0.2f,
            _ => 1.0f,
        };
    }

    private static float GetKidneyClearanceMultiplier(OrganState kidneys)
    {
        if (kidneys.Missing)
            return 0f;

        return kidneys.Status switch
        {
            OrganDamageStatus.LittleBruised => 0.85f,
            OrganDamageStatus.Bruised => 0.6f,
            OrganDamageStatus.Broken => 0.3f,
            _ => 1.0f,
        };
    }
}
