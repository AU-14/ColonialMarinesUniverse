using Content.Shared._RMC14.Attachable.Components;
using Content.Shared._RMC14.Deafness;
using Content.Shared._RMC14.Synth;
using Content.Shared.CMU14.Hearing;
using Content.Shared.Examine;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Radio.Components;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Hearing;

public sealed class CMUCombatHearingSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedDeafnessSystem _deafness = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(1);
    private const float GunLookupRange = 12f;

    private readonly HashSet<Entity<CMUCombatHearingComponent>> _listeners = new();
    private TimeSpan _nextUpdate;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUGunFiredEvent>(OnGunFired);
        SubscribeLocalEvent<CMUExplosionSpawnedEvent>(OnExplosion);
        SubscribeLocalEvent<RMCEarProtectionComponent, ExaminedEvent>(OnProtectionExamined);
        SubscribeLocalEvent<CMUCombatEarProtectionComponent, ExaminedEvent>(OnProtectionExamined);
    }

    private void OnProtectionExamined<T>(Entity<T> ent, ref ExaminedEvent args) where T : IComponent
    {
        // An item with both components should only say it once.
        if (typeof(T) == typeof(CMUCombatEarProtectionComponent) && HasComp<RMCEarProtectionComponent>(ent))
            return;

        PushProtectionExamine(args);
    }

    /// <summary>
    ///     Headsets get this from the callsign system, which owns their examine handler.
    /// </summary>
    public void PushProtectionExamine(ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("cmu-combat-hearing-protection-examine"));
    }

    private void OnGunFired(ref CMUGunFiredEvent args)
    {
        var from = _transform.ToMapCoordinates(args.From);
        var suppressed = IsSuppressed(args.Gun);

        _listeners.Clear();
        _lookup.GetEntitiesInRange(from, GunLookupRange, _listeners);
        foreach (var listener in _listeners)
        {
            var comp = listener.Comp;
            var falloff = Falloff(listener, from, comp.GunRange);
            if (falloff <= 0f || !CanBeHurt(listener))
                continue;

            var amount = comp.GunExposure * falloff;
            if (listener.Owner == args.User)
                amount *= comp.ShooterMultiplier;
            if (suppressed)
                amount *= comp.SuppressedMultiplier;

            AddExposure(listener, amount);

            if (comp.Tier >= 3 && !suppressed && _random.Prob(comp.GunDeafChanceTier3))
                _deafness.TryDeafen(listener, comp.GunDeafTier3, true, ignoreProtection: true);
        }
    }

    private void OnExplosion(ref CMUExplosionSpawnedEvent args)
    {
        _listeners.Clear();
        _lookup.GetEntitiesInRange(args.Epicenter, 40f, _listeners);
        foreach (var listener in _listeners)
        {
            var comp = listener.Comp;
            var range = MathF.Min(comp.ExplosionRangeBase + args.Radius * comp.ExplosionRangePerTile, comp.ExplosionRangeMax);
            var falloff = Falloff(listener, args.Epicenter, range);
            if (falloff <= 0f || !CanBeHurt(listener))
                continue;

            var size = Math.Clamp(args.Radius / 3f, 1f, 3f);
            AddExposure(listener, comp.ExplosionExposure * falloff * size);

            var deaf = comp.Tier switch
            {
                >= 3 => comp.ExplosionDeafTier3,
                2 => comp.ExplosionDeafTier2,
                _ => TimeSpan.Zero,
            };

            if (deaf > TimeSpan.Zero)
                _deafness.TryDeafen(listener, deaf * falloff, true, ignoreProtection: true);
        }
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        if (now < _nextUpdate)
            return;

        var elapsed = (float) UpdateInterval.TotalSeconds;
        _nextUpdate = now + UpdateInterval;

        var query = EntityQueryEnumerator<CMUCombatHearingComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Exposure <= 0f)
                continue;

            if (HasComp<SynthComponent>(uid))
            {
                comp.Exposure = 0f;
                comp.Tier = 0;
                continue;
            }

            comp.Exposure = MathF.Max(0f, comp.Exposure - comp.DecayPerSecond * elapsed);
            UpdateTier((uid, comp));

            if (comp.Tier < 3 || now < comp.NextEpisodeAt || !_mobState.IsAlive(uid))
                continue;

            ScheduleEpisode(comp, now);
            var duration = TimeSpan.FromSeconds(_random.NextDouble(
                comp.EpisodeDurationMin.TotalSeconds,
                comp.EpisodeDurationMax.TotalSeconds));
            _deafness.TryDeafen(uid, duration, true, ignoreProtection: true);
        }
    }

    private void AddExposure(Entity<CMUCombatHearingComponent> ent, float amount)
    {
        if (amount <= 0f)
            return;

        if (TryComp<CMUHyperacusisComponent>(ent, out var hyperacusis))
            amount *= hyperacusis.ExposureMultiplier;

        ent.Comp.Exposure = MathF.Min(ent.Comp.MaxExposure, ent.Comp.Exposure + amount);
        UpdateTier(ent);
    }

    private void UpdateTier(Entity<CMUCombatHearingComponent> ent)
    {
        var comp = ent.Comp;
        var tier = 0;
        foreach (var threshold in comp.Thresholds)
        {
            if (comp.Exposure >= threshold)
                tier++;
        }

        if (tier == comp.Tier)
            return;

        var old = comp.Tier;
        comp.Tier = tier;

        if (tier > old && tier - 1 < comp.WarningMessages.Count)
        {
            _popup.PopupEntity(Loc.GetString(comp.WarningMessages[tier - 1]), ent, ent, PopupType.MediumCaution);
            if (tier >= 3 && old < 3)
                ScheduleEpisode(comp, _timing.CurTime);
        }
        else if (tier == 0)
        {
            _popup.PopupEntity(Loc.GetString(comp.RecoveredMessage), ent, ent, PopupType.Medium);
        }
    }

    private void ScheduleEpisode(CMUCombatHearingComponent comp, TimeSpan now)
    {
        comp.NextEpisodeAt = now + TimeSpan.FromSeconds(_random.NextDouble(
            comp.EpisodeIntervalMin.TotalSeconds,
            comp.EpisodeIntervalMax.TotalSeconds));
    }

    private float Falloff(EntityUid listener, MapCoordinates source, float range)
    {
        var pos = _transform.GetMapCoordinates(listener);
        if (pos.MapId != source.MapId || range <= 0f)
            return 0f;

        var distance = (pos.Position - source.Position).Length();
        return distance >= range ? 0f : 1f - distance / range;
    }

    private bool CanBeHurt(EntityUid uid)
    {
        return _mobState.IsAlive(uid) && !HasComp<SynthComponent>(uid) && !IsProtected(uid);
    }

    private bool IsProtected(EntityUid uid)
    {
        if (_deafness.HasEarProtection(uid))
            return true;

        // Pens, tools and cigarettes tucked behind an ear don't count, only real headsets.
        if (_inventory.TryGetSlotEntity(uid, "ears", out var ears) && HasComp<HeadsetComponent>(ears))
            return true;

        if (_inventory.TryGetContainerSlotEnumerator(uid, out var slots))
        {
            while (slots.NextItem(out var item, out var slot))
            {
                if (slot.Name is not ("ears" or "head"))
                    continue;

                if (HasComp<CMUCombatEarProtectionComponent>(item))
                    return true;

                // Ear gear clipped onto a worn helmet, like a headset helmet accessory.
                foreach (var container in _container.GetAllContainers(item))
                {
                    foreach (var attached in container.ContainedEntities)
                    {
                        // Hearing protection clipped onto or stored in a worn helmet still counts.
                        if (HasComp<CMUCombatEarProtectionComponent>(attached) ||
                            HasComp<HeadsetComponent>(attached) ||
                            HasComp<RMCEarProtectionComponent>(attached))
                        {
                            return true;
                        }
                    }
                }
            }
        }

        return HasComp<CMUCombatEarProtectionComponent>(uid);
    }

    private bool IsSuppressed(EntityUid gun)
    {
        foreach (var container in _container.GetAllContainers(gun))
        {
            foreach (var contained in container.ContainedEntities)
            {
                if (HasComp<AttachableSilencerComponent>(contained))
                    return true;
            }
        }

        return false;
    }
}
