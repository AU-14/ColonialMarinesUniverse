using System.Numerics;
using Content.Server.Explosion.EntitySystems;
using Content.Shared._RMC14.Aura;
using Content.Shared._RMC14.Explosion;
using Content.Shared._RMC14.Weapons.Common;
using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared._RMC14.Weapons.Ranged.Sharp;
using Content.Shared.Eye;
using Content.Shared.Examine;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.StepTrigger.Components;
using Content.Shared.StepTrigger.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._RMC14.Weapons.Ranged.Sharp;

public sealed partial class SharpSystem : EntitySystem
{
    private static readonly Color SharpExplosiveMarkColor = Color.FromHex("#FF2020");
    private static readonly Color SharpIncendiaryMarkColor = Color.FromHex("#FFD21A");
    private static readonly TimeSpan MinMarkDuration = TimeSpan.FromSeconds(1);
    private const float SharpMarkFlashFrequency = 5;
    private const float SharpMarkMinAlpha = 0.15f;
    private const float SharpMarkOutlineWidth = 3;

    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedAuraSystem _aura = default!;
    [Dependency] private GunIFFSystem _gunIff = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private StepTriggerSystem _stepTrigger = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TriggerSystem _trigger = default!;
    [Dependency] private VisibilitySystem _visibility = default!;

    private readonly List<PendingSharpEffect> _pendingEffects = new();

    private EntityQuery<ProjectileComponent> _projectileQuery;
    private EntityQuery<ProjectileIFFComponent> _projectileIffQuery;
    private EntityQuery<RMCLandmineComponent> _landmineQuery;
    private EntityQuery<StepTriggerComponent> _stepTriggerQuery;

    public override void Initialize()
    {
        _projectileQuery = GetEntityQuery<ProjectileComponent>();
        _projectileIffQuery = GetEntityQuery<ProjectileIFFComponent>();
        _landmineQuery = GetEntityQuery<RMCLandmineComponent>();
        _stepTriggerQuery = GetEntityQuery<StepTriggerComponent>();

        SubscribeLocalEvent<SharpGunComponent, ExaminedEvent>(OnGunExamined);
        SubscribeLocalEvent<SharpGunComponent, UniqueActionEvent>(OnGunUniqueAction);

        SubscribeLocalEvent<SharpProjectileComponent, ProjectileHitEvent>(OnProjectileHit);
        SubscribeLocalEvent<SharpProjectileComponent, ProjectileFixedDistanceStopEvent>(OnProjectileFixedDistanceStop);

        SubscribeLocalEvent<SharpMineComponent, MapInitEvent>(OnMineMapInit);
        SubscribeLocalEvent<SharpMineComponent, RMCTriggerEvent>(OnMineTriggered);
        SubscribeLocalEvent<SharpMineComponent, ClaymoreDisarmDoafterEvent>(OnMineDisarmed);
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        UpdatePendingEffects(now);
        UpdateMines(now);
    }

    private void OnGunExamined(Entity<SharpGunComponent> ent, ref ExaminedEvent args)
    {
        using (args.PushGroup(nameof(SharpGunComponent)))
        {
            args.PushMarkup(Loc.GetString(
                "rmc-sharp-examine",
                ("seconds", ent.Comp.CurrentDelay.TotalSeconds)));
        }
    }

    private void OnGunUniqueAction(Entity<SharpGunComponent> ent, ref UniqueActionEvent args)
    {
        if (args.Handled)
            return;

        ent.Comp.Delayed = !ent.Comp.Delayed;
        Dirty(ent);

        _audio.PlayPredicted(ent.Comp.ToggleSound, ent, args.UserUid);

        var msg = Loc.GetString(
            "rmc-sharp-toggle-delay",
            ("gun", ent.Owner),
            ("seconds", ent.Comp.CurrentDelay.TotalSeconds));
        _popup.PopupClient(msg, args.UserUid, args.UserUid, PopupType.Medium);

        args.Handled = true;
    }

    private void OnProjectileHit(Entity<SharpProjectileComponent> projectile, ref ProjectileHitEvent args)
    {
        if (args.Target == args.Shooter)
            return;

        if (projectile.Comp.Kind == SharpProjectileKind.Flechette)
            return;

        if (HasComp<MobStateComponent>(args.Target))
        {
            if (IsFriendly(projectile, args.Target))
                return;

            if (!TryResolveProjectile(projectile))
                return;

            var delay = GetDirectDelay(projectile, projectile.Comp);
            ApplyDirectHitMark(args.Target, projectile.Comp.Kind, delay);
            _pendingEffects.Add(new PendingSharpEffect(
                args.Target,
                args.Shooter,
                projectile.Comp.DirectEffect,
                _timing.CurTime + delay));
            QueueDel(projectile.Owner);
            return;
        }

        if (!TryResolveProjectile(projectile))
            return;

        SpawnDartMine(projectile, args.Shooter);
    }

    private void OnProjectileFixedDistanceStop(Entity<SharpProjectileComponent> projectile, ref ProjectileFixedDistanceStopEvent args)
    {
        if (projectile.Comp.Kind == SharpProjectileKind.Flechette)
            return;

        if (!TryResolveProjectile(projectile))
        {
            QueueDel(projectile.Owner);
            return;
        }

        var shooter = GetProjectileShooter(projectile.Owner);
        SpawnDartMine(projectile, shooter);
    }

    private void OnMineMapInit(Entity<SharpMineComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.Level = Math.Clamp(ent.Comp.Level, 1, ent.Comp.MaxLevel);
        ent.Comp.Armed = false;
        ent.Comp.Disarmed = false;
        ent.Comp.ArmAt = _timing.CurTime + ent.Comp.ArmDelay;
        ent.Comp.DespawnAt = _timing.CurTime + ent.Comp.Lifetime;
        ent.Comp.NextUpgradeAt = ent.Comp.ArmAt + ent.Comp.UpgradeEvery;

        if (_landmineQuery.TryComp(ent, out var landmine))
        {
            landmine.Armed = false;
            Dirty(ent, landmine);
        }

        if (_stepTriggerQuery.TryComp(ent, out var stepTrigger))
            _stepTrigger.SetActive(ent, false, stepTrigger);

        SetMineState(ent, SharpMineState.Inactive);
        Dirty(ent);
    }

    private void OnMineTriggered(Entity<SharpMineComponent> ent, ref RMCTriggerEvent args)
    {
        if (args.Handled || ent.Comp.Disarmed)
            return;

        var effect = GetMineEffect(ent.Comp);
        if (effect != null)
            SpawnAndTrigger(effect.Value, Transform(ent).Coordinates, args.User);

        args.Handled = true;
        QueueDel(ent.Owner);
    }

    private void OnMineDisarmed(Entity<SharpMineComponent> ent, ref ClaymoreDisarmDoafterEvent args)
    {
        if (args.Cancelled)
            return;

        ent.Comp.Disarmed = true;
        ent.Comp.Armed = false;

        if (_landmineQuery.TryComp(ent, out var landmine))
        {
            landmine.Armed = false;
            Dirty(ent, landmine);
        }

        if (_stepTriggerQuery.TryComp(ent, out var stepTrigger))
            _stepTrigger.SetActive(ent, false, stepTrigger);

        SetMineState(ent, SharpMineState.Disarmed);
        Dirty(ent);
    }

    private void UpdatePendingEffects(TimeSpan now)
    {
        for (var i = _pendingEffects.Count - 1; i >= 0; i--)
        {
            var pending = _pendingEffects[i];
            if (pending.TriggerAt > now)
                continue;

            _pendingEffects.RemoveAt(i);

            if (TerminatingOrDeleted(pending.Target))
                continue;

            SpawnAndTrigger(pending.Effect, Transform(pending.Target).Coordinates, pending.User);
        }
    }

    private void UpdateMines(TimeSpan now)
    {
        var query = EntityQueryEnumerator<SharpMineComponent>();
        while (query.MoveNext(out var uid, out var mine))
        {
            if (mine.Disarmed)
                continue;

            if (mine.DespawnAt != TimeSpan.Zero && now >= mine.DespawnAt)
            {
                QueueDel(uid);
                continue;
            }

            if (!mine.Armed && now >= mine.ArmAt)
                ArmMine((uid, mine));

            if (mine.Armed && TryTriggerNearbyHostile((uid, mine)))
                continue;

            if (!mine.Armed || mine.Level >= mine.MaxLevel || now < mine.NextUpgradeAt)
                continue;

            while (mine.Level < mine.MaxLevel && now >= mine.NextUpgradeAt)
            {
                mine.Level++;
                mine.NextUpgradeAt += mine.UpgradeEvery;
            }

            SetMineState((uid, mine), GetActiveMineState(mine.Level));
            Dirty(uid, mine);
        }
    }

    private void ArmMine(Entity<SharpMineComponent> ent)
    {
        ent.Comp.Armed = true;

        if (_landmineQuery.TryComp(ent, out var landmine))
        {
            landmine.Armed = true;
            Dirty(ent, landmine);
        }

        if (_stepTriggerQuery.TryComp(ent, out var stepTrigger))
            _stepTrigger.SetActive(ent, true, stepTrigger);

        SetMineState(ent, GetActiveMineState(ent.Comp.Level));
        Dirty(ent);
    }

    private bool TryTriggerNearbyHostile(Entity<SharpMineComponent> ent)
    {
        if (!ent.Comp.Armed || ent.Comp.Disarmed || ent.Comp.TriggerRadius <= 0)
            return false;

        var coordinates = Transform(ent).Coordinates;
        foreach (var target in _lookup.GetEntitiesInRange<MobStateComponent>(coordinates, ent.Comp.TriggerRadius))
        {
            if (_mobState.IsDead(target) || IsFriendlyMine(ent.Owner, target))
                continue;

            _trigger.Trigger(ent.Owner, target);
            return true;
        }

        return false;
    }

    private EntProtoId? GetMineEffect(SharpMineComponent mine)
    {
        if (mine.Effects.Count == 0)
            return null;

        var index = Math.Clamp(mine.Level - 1, 0, mine.Effects.Count - 1);
        return mine.Effects[index];
    }

    private void SpawnDartMine(Entity<SharpProjectileComponent> projectile, EntityUid? user)
    {
        var coordinates = GetCenteredCoordinates(projectile.Owner);
        if (TryTriggerExistingMine(coordinates, user))
        {
            HideAndQueueDeleteDart(projectile.Owner);
            return;
        }

        var mine = Spawn(projectile.Comp.Mine, coordinates);
        var xform = Transform(mine);
        _transform.AnchorEntity(mine, xform);
        _physics.SetBodyType(mine, BodyType.Static);

        if (_projectileIffQuery.TryComp(projectile, out var projectileIff) &&
            _landmineQuery.TryComp(mine, out var landmine))
        {
            landmine.Factions.Clear();
            landmine.Factions.UnionWith(projectileIff.Factions);
            Dirty(mine, landmine);
        }

        if (_stepTriggerQuery.TryComp(mine, out var stepTrigger))
            _stepTrigger.SetActive(mine, false, stepTrigger);

        HideAndQueueDeleteDart(projectile.Owner);
    }

    private void HideAndQueueDeleteDart(EntityUid projectile)
    {
        if (TryComp(projectile, out PhysicsComponent? physics))
            _physics.SetCanCollide(projectile, false, body: physics);

        var visibility = EnsureComp<VisibilityComponent>(projectile);
        _visibility.RemoveLayer((projectile, visibility), (int) VisibilityFlags.Normal, false);
        _visibility.RefreshVisibility(projectile, visibility);

        QueueDel(projectile);
    }

    private bool TryTriggerExistingMine(EntityCoordinates coordinates, EntityUid? user)
    {
        var mapCoordinates = _transform.ToMapCoordinates(coordinates);
        foreach (var mine in _lookup.GetEntitiesInRange<RMCLandmineComponent>(mapCoordinates, 0.45f))
        {
            _trigger.Trigger(mine.Owner, user);
            return true;
        }

        return false;
    }

    private bool IsFriendlyMine(EntityUid mine, EntityUid target)
    {
        if (!_landmineQuery.TryComp(mine, out var landmine))
            return false;

        foreach (var faction in landmine.Factions)
        {
            if (_gunIff.IsInFaction(target, faction))
                return true;
        }

        return false;
    }

    private bool TryResolveProjectile(Entity<SharpProjectileComponent> projectile)
    {
        if (projectile.Comp.Resolved)
            return false;

        projectile.Comp.Resolved = true;
        return true;
    }

    private void ApplyDirectHitMark(EntityUid target, SharpProjectileKind kind, TimeSpan delay)
    {
        var color = kind switch
        {
            SharpProjectileKind.Explosive => SharpExplosiveMarkColor,
            SharpProjectileKind.Incendiary => SharpIncendiaryMarkColor,
            _ => (Color?) null,
        };

        if (color == null)
            return;

        var duration = delay > MinMarkDuration ? delay : MinMarkDuration;
        _aura.GiveAura(
            target,
            color.Value,
            duration,
            SharpMarkOutlineWidth,
            flash: true,
            flashFrequency: SharpMarkFlashFrequency,
            flashMinAlpha: SharpMarkMinAlpha);
    }

    private TimeSpan GetDirectDelay(Entity<SharpProjectileComponent> projectile, SharpProjectileComponent sharp)
    {
        if (_projectileQuery.TryComp(projectile, out var projectileComp) &&
            projectileComp.Weapon is { } weapon &&
            TryComp(weapon, out SharpGunComponent? gun))
        {
            return gun.CurrentDelay;
        }

        return sharp.DirectDelay;
    }

    private EntityUid? GetProjectileShooter(EntityUid projectile)
    {
        if (_projectileQuery.TryComp(projectile, out var projectileComp))
            return projectileComp.Shooter;

        return null;
    }

    private bool IsFriendly(Entity<SharpProjectileComponent> projectile, EntityUid target)
    {
        if (!_projectileIffQuery.TryComp(projectile, out var iff))
            return false;

        foreach (var faction in iff.Factions)
        {
            if (_gunIff.IsInFaction(target, faction))
                return true;
        }

        return false;
    }

    private EntityCoordinates GetCenteredCoordinates(EntityUid uid)
    {
        var mapCoordinates = _transform.GetMapCoordinates(uid);
        var centered = new MapCoordinates(
            new Vector2(
                MathF.Floor(mapCoordinates.Position.X) + 0.5f,
                MathF.Floor(mapCoordinates.Position.Y) + 0.5f),
            mapCoordinates.MapId);

        return _transform.ToCoordinates(centered);
    }

    private void SpawnAndTrigger(EntProtoId effect, EntityCoordinates coordinates, EntityUid? user)
    {
        var uid = Spawn(effect, coordinates);
        _trigger.Trigger(uid, user);
    }

    private void SetMineState(Entity<SharpMineComponent> ent, SharpMineState state)
    {
        _appearance.SetData(ent.Owner, SharpMineVisuals.State, state);
    }

    private static SharpMineState GetActiveMineState(int level)
    {
        return level switch
        {
            <= 1 => SharpMineState.Active1,
            2 => SharpMineState.Active2,
            3 => SharpMineState.Active3,
            _ => SharpMineState.Active4,
        };
    }

    private readonly record struct PendingSharpEffect(EntityUid Target, EntityUid? User, EntProtoId Effect, TimeSpan TriggerAt);
}
