using Content.Shared._CMU14.Medical.Metabolism.Events;
using Content.Shared._CMU14.Medical.Organs.Kidneys;
using Content.Shared._CMU14.Medical.Organs.Liver;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.Shared._CMU14.Medical.Metabolism;

/// <summary>
///     Owns the single
///     <c>&lt;CMUHumanMedicalComponent, MetabolismGroupRateModifyEvent&gt;</c>
///     subscription on the body and dispatches to the per-organ subsystems.
///     Centralising the body-level handler avoids duplicate-event subscription conflicts.
/// </summary>
public abstract class SharedMetabolismHubSystem : EntitySystem
{
    [Dependency] protected readonly IConfigurationManager Cfg = default!;
    [Dependency] protected readonly IGameTiming Timing = default!;
    [Dependency] protected readonly SharedLiverSystem Liver = default!;
    [Dependency] protected readonly SharedKidneysSystem Kidneys = default!;

    private readonly Dictionary<EntityUid, float> _clearanceCache = new();
    private TimeSpan _clearanceCacheTime;

    private bool _medicalEnabled;
    private bool _organEnabled;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUHumanMedicalComponent, MetabolismGroupRateModifyEvent>(OnRate);

        Cfg.OnValueChanged(CMUMedicalCCVars.Enabled, v => _medicalEnabled = v, true);
        Cfg.OnValueChanged(CMUMedicalCCVars.OrganEnabled, v => _organEnabled = v, true);
    }

    private void OnRate(Entity<CMUHumanMedicalComponent> ent, ref MetabolismGroupRateModifyEvent args)
    {
        if (!_medicalEnabled || !_organEnabled)
            return;

        args.Multiplier *= GetClearanceMultiplier(ent);
        Liver.ApplyBloodstreamDirectDamage(ent, args.Group);
    }

    private float GetClearanceMultiplier(EntityUid body)
    {
        var now = Timing.CurTime;
        if (_clearanceCacheTime != now)
        {
            _clearanceCacheTime = now;
            _clearanceCache.Clear();
        }

        if (_clearanceCache.TryGetValue(body, out var cached))
            return cached;

        var multiplier = Liver.GetClearanceMultiplier(body) * Kidneys.GetClearanceMultiplier(body);
        _clearanceCache[body] = multiplier;
        return multiplier;
    }
}
