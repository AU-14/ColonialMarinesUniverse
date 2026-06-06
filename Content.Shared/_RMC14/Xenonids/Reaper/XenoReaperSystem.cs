using System.Numerics;
using Content.Shared._RMC14.Actions;
using Content.Shared._RMC14.Armor;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Medical.Unrevivable;
using Content.Shared._RMC14.Shields;
using Content.Shared._RMC14.Slow;
using Content.Shared._RMC14.Weapons.Melee;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared.Alert;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Coordinates;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Rounding;
using Content.Shared.StatusEffectNew;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.Xenonids.Reaper;

public sealed partial class XenoReaperSystem : EntitySystem
{
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private CMArmorSystem _armor = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private EntityLookupSystem _entityLookup = default!;
    [Dependency] private SharedXenoHiveSystem _hive = default!;
    [Dependency] private IMapManager _map = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private RMCUnrevivableSystem _unrevivable = default!;
    [Dependency] private SharedRMCActionsSystem _rmcActions = default!;
    [Dependency] private SharedRMCMeleeWeaponSystem _rmcMelee = default!;
    [Dependency] private XenoShieldSystem _shield = default!;
    [Dependency] private RMCSlowSystem _slow = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private XenoSystem _xeno = default!;

    private readonly HashSet<EntityUid> _nearbyTargets = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<XenoReaperComponent, MapInitEvent>(OnReaperMapInit);
        SubscribeLocalEvent<XenoReaperComponent, ComponentRemove>(OnReaperRemove);
        SubscribeLocalEvent<XenoReaperComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<XenoReaperComponent, XenoFleshHarvestActionEvent>(OnFleshHarvestAction);
        SubscribeLocalEvent<XenoReaperComponent, XenoFleshHarvestDoAfterEvent>(OnFleshHarvestDoAfter);
        SubscribeLocalEvent<XenoReaperComponent, XenoRaptureActionEvent>(OnRaptureAction);
        SubscribeLocalEvent<XenoReaperComponent, XenoFleshBloomActionEvent>(OnFleshBloomAction);
        SubscribeLocalEvent<XenoReaperComponent, XenoFleshBloomDoAfterEvent>(OnFleshBloomDoAfter);
        SubscribeLocalEvent<XenoReaperComponent, XenoReaperRedGasActionEvent>(OnRedGasAction);
        SubscribeLocalEvent<XenoReaperComponent, XenoReaperRedGasDoAfterEvent>(OnRedGasDoAfter);
        SubscribeLocalEvent<XenoReaperComponent, XenoCarrionMantleActionEvent>(OnCarrionMantleAction);

        SubscribeLocalEvent<XenoCarrionMantleComponent, CMGetArmorEvent>(OnCarrionMantleGetArmor);
        SubscribeLocalEvent<XenoCarrionMantleComponent, RefreshMovementSpeedModifiersEvent>(OnCarrionMantleRefreshSpeed);
        SubscribeLocalEvent<XenoCarrionMantleComponent, BeforeStatusEffectAddedEvent>(OnCarrionMantleBeforeStatus);
    }

    private void OnReaperMapInit(Entity<XenoReaperComponent> xeno, ref MapInitEvent args)
    {
        xeno.Comp.NextPassiveGainAt = _timing.CurTime + xeno.Comp.PassiveGainEvery;
        Dirty(xeno);
        UpdateFleshAlert(xeno);
    }

    private void OnReaperRemove(Entity<XenoReaperComponent> xeno, ref ComponentRemove args)
    {
        _alerts.ClearAlert(xeno, xeno.Comp.Alert);
    }

    private void OnMeleeHit(Entity<XenoReaperComponent> xeno, ref MeleeHitEvent args)
    {
        if (!args.IsHit)
            return;

        foreach (var hit in args.HitEntities)
        {
            if (!_xeno.CanAbilityAttackTarget(xeno, hit))
                continue;

            AddFleshResin(xeno, xeno.Comp.MeleeGain);
            break;
        }
    }

    private void OnFleshHarvestAction(Entity<XenoReaperComponent> xeno, ref XenoFleshHarvestActionEvent args)
    {
        if (args.Handled)
            return;

        if (!CanFleshHarvestTarget(xeno, args.Target, true))
            return;

        if (!_rmcActions.TryUseAction(args))
            return;

        args.Handled = true;

        var doAfter = new DoAfterArgs(EntityManager, xeno.Owner, xeno.Comp.FleshHarvestDelay, new XenoFleshHarvestDoAfterEvent(), xeno.Owner, args.Target)
        {
            BreakOnMove = true,
            DuplicateCondition = DuplicateConditions.SameEvent,
            CancelDuplicate = true,
        };

        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnFleshHarvestDoAfter(Entity<XenoReaperComponent> xeno, ref XenoFleshHarvestDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Target is not { } target)
            return;

        if (!CanFleshHarvestTarget(xeno, target, false))
            return;

        args.Handled = true;
        EnsureComp<XenoFleshHarvestedComponent>(target);
        RipLimbsFromCorpse(target);
        AddFleshResin(xeno, xeno.Comp.FleshHarvestGain);
    }

    private void OnRaptureAction(Entity<XenoReaperComponent> xeno, ref XenoRaptureActionEvent args)
    {
        if (args.Handled || !_xeno.CanAbilityAttackTarget(xeno, args.Target) || !_rmcActions.TryUseAction(args))
            return;

        args.Handled = true;
        _damageable.TryChangeDamage(args.Target, new DamageSpecifier { DamageDict = { ["Blunt"] = 20, ["Cellular"] = 15 } }, origin: xeno, tool: xeno);
        _slow.TrySlowdown(args.Target, TimeSpan.FromSeconds(3), ignoreDurationModifier: true);
        _audio.PlayPredicted(xeno.Comp.RaptureSound, xeno, xeno);
        _rmcMelee.DoLunge(xeno.Owner, args.Target);

        if (_net.IsServer)
            SpawnAttachedTo(xeno.Comp.RaptureEffect, args.Target.ToCoordinates());

        AddFleshResin(xeno, xeno.Comp.RaptureGain);
    }

    private void OnFleshBloomAction(Entity<XenoReaperComponent> xeno, ref XenoFleshBloomActionEvent args)
    {
        if (args.Handled || !HasFleshResinPopup(xeno, xeno.Comp.FleshBloomCost))
            return;

        var coordinates = args.Target.SnapToGrid(EntityManager, _map);
        if (!coordinates.IsValid(EntityManager))
            return;

        if (!_rmcActions.TryUseAction(args))
            return;

        var doAfter = new DoAfterArgs(
            EntityManager,
            xeno.Owner,
            xeno.Comp.FleshBloomDelay,
            new XenoFleshBloomDoAfterEvent(GetNetCoordinates(coordinates)),
            xeno.Owner)
        {
            BreakOnMove = true,
            DuplicateCondition = DuplicateConditions.SameEvent,
            CancelDuplicate = true,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        args.Handled = true;
        _audio.PlayPredicted(xeno.Comp.FleshBloomSound, coordinates, xeno);
        SpawnFleshBloomTelegraph(xeno, coordinates);
    }

    private void OnFleshBloomDoAfter(Entity<XenoReaperComponent> xeno, ref XenoFleshBloomDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        if (!TryRemoveFleshResin(xeno, xeno.Comp.FleshBloomCost))
            return;

        args.Handled = true;

        if (_net.IsClient)
            return;

        SpawnFleshBlooms(xeno, GetCoordinates(args.Coordinates));
    }

    private void OnRedGasAction(Entity<XenoReaperComponent> xeno, ref XenoReaperRedGasActionEvent args)
    {
        if (args.Handled || !HasFleshResinPopup(xeno, xeno.Comp.RedGasCost))
            return;

        var start = Transform(xeno).Coordinates.SnapToGrid(EntityManager, _map);
        var target = args.Target.SnapToGrid(EntityManager, _map);
        if (!start.IsValid(EntityManager) || !target.IsValid(EntityManager))
            return;

        var path = BuildRedGasPath(start, target, xeno.Comp.RedGasRange);
        if (path.Count == 0)
            return;

        if (!_rmcActions.TryUseAction(args))
            return;

        args.Handled = true;

        if (_net.IsServer)
            TryStartRedGasStepDoAfter(xeno, ToNetPath(path), 0);
    }

    private void OnRedGasDoAfter(Entity<XenoReaperComponent> xeno, ref XenoReaperRedGasDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        args.Handled = true;
        if (_net.IsClient)
            return;

        var path = ToEntityPath(args.Path);
        if (!AdvanceRedGasStep(xeno, path, args.Step))
            return;

        TryStartRedGasStepDoAfter(xeno, args.Path, args.Step + 1);
    }

    private void OnCarrionMantleAction(Entity<XenoReaperComponent> xeno, ref XenoCarrionMantleActionEvent args)
    {
        if (args.Handled)
            return;

        if (args.Target != xeno.Owner && !_hive.FromSameHive(xeno.Owner, args.Target))
            return;

        if (!HasFleshResinPopup(xeno, xeno.Comp.CarrionMantleCost) || !_rmcActions.TryUseAction(args))
            return;

        if (!TryRemoveFleshResin(xeno, xeno.Comp.CarrionMantleCost))
            return;

        args.Handled = true;
        var mantle = EnsureComp<XenoCarrionMantleComponent>(args.Target);
        mantle.ExpiresAt = _timing.CurTime + xeno.Comp.CarrionMantleDuration;
        Dirty(args.Target, mantle);
        EnsureComp<KingShieldComponent>(args.Target);
        _shield.ApplyShield(
            args.Target,
            XenoShieldSystem.ShieldType.King,
            xeno.Comp.CarrionMantleShieldAmount,
            duration: xeno.Comp.CarrionMantleDuration,
            decay: xeno.Comp.CarrionMantleShieldDecay,
            visualState: xeno.Comp.CarrionMantleShieldVisualState);
        _movementSpeed.RefreshMovementSpeedModifiers(args.Target);
        _armor.UpdateArmorValue((args.Target, null));
        _popup.PopupClient(Loc.GetString("cm-xeno-reaper-carrion-mantle"), args.Target, xeno);
    }

    private void OnCarrionMantleGetArmor(Entity<XenoCarrionMantleComponent> ent, ref CMGetArmorEvent args)
    {
        args.XenoArmor += ent.Comp.Armor;
    }

    private void OnCarrionMantleRefreshSpeed(Entity<XenoCarrionMantleComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        args.ModifySpeed(ent.Comp.SpeedMultiplier, ent.Comp.SpeedMultiplier);
    }

    private void OnCarrionMantleBeforeStatus(Entity<XenoCarrionMantleComponent> ent, ref BeforeStatusEffectAddedEvent args)
    {
        if (ent.Comp.ImmuneToStatuses.Contains(args.Effect.Id))
            args.Cancelled = true;
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var time = _timing.CurTime;
        var query = EntityQueryEnumerator<XenoReaperComponent>();
        while (query.MoveNext(out var uid, out var reaper))
        {
            if (time < reaper.NextPassiveGainAt)
                continue;

            reaper.NextPassiveGainAt = time + reaper.PassiveGainEvery;
            var passiveCap = Math.Min(reaper.MaxFleshResin, reaper.PassiveGainMaxFleshResin);
            if (passiveCap <= 0 || reaper.FleshResin >= passiveCap)
                continue;

            var gain = Math.Max(0, reaper.PassiveGain);
            if (gain == 0)
                continue;

            reaper.FleshResin = Math.Min(passiveCap, reaper.FleshResin + gain);
            Dirty(uid, reaper);
            UpdateFleshAlert((uid, reaper));
        }

        var bloomQuery = EntityQueryEnumerator<XenoFleshBloomComponent>();
        while (bloomQuery.MoveNext(out var uid, out var bloom))
        {
            if (time < bloom.NextPulseAt)
                continue;

            bloom.NextPulseAt = time + bloom.PulseEvery;
            Dirty(uid, bloom);
            PulseFleshBloom((uid, bloom));
        }

        var redGasQuery = EntityQueryEnumerator<XenoReaperRedGasComponent>();
        while (redGasQuery.MoveNext(out var uid, out var gas))
        {
            if (time < gas.NextPulseAt)
                continue;

            gas.NextPulseAt = time + gas.PulseEvery;
            PulseRedGas((uid, gas));
        }

        var mantleQuery = EntityQueryEnumerator<XenoCarrionMantleComponent>();
        while (mantleQuery.MoveNext(out var uid, out var mantle))
        {
            if (time < mantle.ExpiresAt)
                continue;

            RemCompDeferred<XenoCarrionMantleComponent>(uid);
            _movementSpeed.RefreshMovementSpeedModifiers(uid);
            _armor.UpdateArmorValue((uid, null));
        }
    }

    private bool CanFleshHarvestTarget(Entity<XenoReaperComponent> xeno, EntityUid target, bool popup)
    {
        if (!HasComp<MarineComponent>(target) ||
            HasComp<XenoComponent>(target) ||
            !_mobState.IsDead(target) ||
            !_unrevivable.IsUnrevivable(target))
        {
            if (popup)
                _popup.PopupClient(Loc.GetString("cm-xeno-reaper-harvest-permadead-marine"), xeno, xeno);

            return false;
        }

        if (HasComp<XenoFleshHarvestedComponent>(target))
        {
            if (popup)
                _popup.PopupClient(Loc.GetString("cm-xeno-reaper-harvest-spent"), xeno, xeno);

            return false;
        }

        return true;
    }

    private void RipLimbsFromCorpse(EntityUid corpse)
    {
        if (_net.IsClient || !TryComp<BodyComponent>(corpse, out var body))
            return;

        var limbs = new HashSet<EntityUid>();
        foreach (var type in new[] { BodyPartType.Arm, BodyPartType.Hand, BodyPartType.Leg, BodyPartType.Foot })
        {
            foreach (var limb in _body.GetBodyChildrenOfType(corpse, type, body))
            {
                limbs.Add(limb.Id);
            }
        }

        if (limbs.Count == 0)
            return;

        var origin = Transform(corpse).Coordinates;
        var index = 0;
        foreach (var limb in limbs)
        {
            if (!_containers.TryGetContainingContainer((limb, null, null), out var container))
                continue;

            if (!_containers.Remove(limb, container))
                continue;

            _transform.SetCoordinates(limb, origin);
            _transform.AttachToGridOrMap(limb);

            var angle = MathF.Tau * index / limbs.Count;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 1.5f;
            _throwing.TryThrow(limb, direction, baseThrowSpeed: 4f, doSpin: true, compensateFriction: true);
            index++;
        }
    }

    private List<EntityCoordinates> BuildRedGasPath(EntityCoordinates start, EntityCoordinates target, int maxRange)
    {
        var path = new List<EntityCoordinates>();
        if (target.EntityId != start.EntityId)
            target = _transform.WithEntityId(target, start.EntityId);

        var delta = target.Position - start.Position;
        if (delta.LengthSquared() <= 0.001f)
        {
            path.Add(start);
            return path;
        }

        var distance = MathF.Min(delta.Length(), maxRange);
        var direction = Vector2.Normalize(delta);
        var steps = Math.Max(0, (int) MathF.Round(distance));

        for (var i = 0; i <= steps; i++)
        {
            var position = start.Position + direction * i;
            var coordinates = new EntityCoordinates(start.EntityId, position).SnapToGrid(EntityManager, _map);
            if (path.Count == 0 || path[^1].Position != coordinates.Position)
                path.Add(coordinates);
        }

        return path;
    }

    private NetCoordinates[] ToNetPath(List<EntityCoordinates> path)
    {
        var netPath = new NetCoordinates[path.Count];
        for (var i = 0; i < path.Count; i++)
        {
            netPath[i] = GetNetCoordinates(path[i]);
        }

        return netPath;
    }

    private List<EntityCoordinates> ToEntityPath(NetCoordinates[] path)
    {
        var entityPath = new List<EntityCoordinates>(path.Length);
        foreach (var coordinates in path)
        {
            entityPath.Add(GetCoordinates(coordinates));
        }

        return entityPath;
    }

    private bool TryStartRedGasStepDoAfter(Entity<XenoReaperComponent> xeno, NetCoordinates[] path, int step)
    {
        if (step >= path.Length)
            return false;

        var doAfter = new DoAfterArgs(
            EntityManager,
            xeno.Owner,
            xeno.Comp.RedGasStepEvery,
            new XenoReaperRedGasDoAfterEvent(path, step),
            xeno.Owner)
        {
            BreakOnMove = false,
        };

        return _doAfter.TryStartDoAfter(doAfter);
    }

    private bool AdvanceRedGasStep(Entity<XenoReaperComponent> reaper, List<EntityCoordinates> path, int step)
    {
        if (step >= path.Count)
            return false;

        var center = path[step];
        var width = GetRedGasWidth(step, path.Count);
        var perpendicular = GetRedGasPerpendicular(path, step);
        var spawned = false;

        foreach (var offset in GetRedGasOffsets(width, perpendicular))
        {
            var coordinates = center.Offset(offset).SnapToGrid(EntityManager, _map);
            if (!coordinates.IsValid(EntityManager))
                continue;

            if (!TryRemoveFleshResin(reaper, reaper.Comp.RedGasCost))
                return false;

            SpawnRedGas(reaper, coordinates);
            spawned = true;
        }

        if (spawned)
            _audio.PlayPvs(reaper.Comp.RedGasSound, center);

        return step + 1 < path.Count;
    }

    private static int GetRedGasWidth(int index, int count)
    {
        if (count <= 1)
            return 1;

        var progress = index / (float) (count - 1);
        if (progress >= 2f / 3f)
            return 3;

        if (progress >= 1f / 3f)
            return 2;

        return 1;
    }

    private static Vector2 GetRedGasPerpendicular(List<EntityCoordinates> path, int index)
    {
        Vector2 direction;
        if (index + 1 < path.Count)
            direction = path[index + 1].Position - path[index].Position;
        else if (index > 0)
            direction = path[index].Position - path[index - 1].Position;
        else
            return Vector2.UnitY;

        if (direction.LengthSquared() <= 0.001f)
            return Vector2.UnitY;

        direction = Vector2.Normalize(direction);
        var perpendicular = new Vector2(-direction.Y, direction.X);
        if (MathF.Abs(perpendicular.X) >= MathF.Abs(perpendicular.Y))
            return new Vector2(MathF.Sign(perpendicular.X), 0);

        return new Vector2(0, MathF.Sign(perpendicular.Y));
    }

    private static IEnumerable<Vector2> GetRedGasOffsets(int width, Vector2 perpendicular)
    {
        yield return Vector2.Zero;

        if (width >= 2)
            yield return perpendicular;

        if (width >= 3)
            yield return -perpendicular;
    }

    private void SpawnRedGas(Entity<XenoReaperComponent> reaper, EntityCoordinates coordinates)
    {
        var gas = SpawnAtPosition(reaper.Comp.RedGasPrototype, coordinates);
        var gasComp = EnsureComp<XenoReaperRedGasComponent>(gas);
        gasComp.Reaper = reaper;
        gasComp.NextPulseAt = _timing.CurTime;
        gasComp.PulseEvery = reaper.Comp.RedGasPulseEvery;
        gasComp.Radius = reaper.Comp.RedGasRadius;
        gasComp.Damage = new DamageSpecifier(reaper.Comp.RedGasDamage);
    }

    private void PulseRedGas(Entity<XenoReaperRedGasComponent> gas)
    {
        if (gas.Comp.Reaper is not { } reaper || !Exists(reaper))
        {
            QueueDel(gas);
            return;
        }

        _nearbyTargets.Clear();
        _entityLookup.GetEntitiesInRange(Transform(gas).Coordinates, gas.Comp.Radius, _nearbyTargets);

        foreach (var target in _nearbyTargets)
        {
            if (target == gas.Owner || _mobState.IsDead(target))
                continue;

            if (HasComp<XenoComponent>(target) && _hive.FromSameHive(reaper, target))
                continue;

            if (!_xeno.CanAbilityAttackTarget(reaper, target))
                continue;

            _damageable.TryChangeDamage(target, gas.Comp.Damage, armorPiercing: 10, origin: reaper, tool: gas);
        }
    }

    private void SpawnFleshBloomTelegraph(Entity<XenoReaperComponent> xeno, EntityCoordinates center)
    {
        if (_net.IsClient)
            return;

        for (var x = -1; x <= 1; x++)
        {
            for (var y = -1; y <= 1; y++)
            {
                SpawnAtPosition(xeno.Comp.FleshBloomTelegraphPrototype, center.Offset(new Vector2(x, y)));
            }
        }
    }

    private void SpawnFleshBlooms(Entity<XenoReaperComponent> xeno, EntityCoordinates center)
    {
        for (var x = -1; x <= 1; x++)
        {
            for (var y = -1; y <= 1; y++)
            {
                var bloom = SpawnAtPosition(xeno.Comp.FleshBloomPrototype, center.Offset(new Vector2(x, y)));
                var bloomComp = EnsureComp<XenoFleshBloomComponent>(bloom);
                bloomComp.Reaper = xeno;
                bloomComp.NextPulseAt = _timing.CurTime;
                Dirty(bloom, bloomComp);
            }
        }
    }

    private void PulseFleshBloom(Entity<XenoFleshBloomComponent> bloom)
    {
        if (bloom.Comp.Reaper is not { } reaper || !Exists(reaper))
        {
            QueueDel(bloom);
            return;
        }

        _nearbyTargets.Clear();
        _entityLookup.GetEntitiesInRange(Transform(bloom).Coordinates, bloom.Comp.Range, _nearbyTargets);

        foreach (var target in _nearbyTargets)
        {
            if (target == bloom.Owner || _mobState.IsDead(target))
                continue;

            if (HasComp<XenoComponent>(target) && _hive.FromSameHive(reaper, target))
            {
                _damageable.TryChangeDamage(target, bloom.Comp.Heal, ignoreResistances: true, origin: reaper, tool: bloom);
                continue;
            }

            if (!_xeno.CanAbilityAttackTarget(reaper, target))
                continue;

            _damageable.TryChangeDamage(target, bloom.Comp.Damage, armorPiercing: 10, origin: reaper, tool: bloom);
            _slow.TrySlowdown(target, bloom.Comp.SlowDuration, ignoreDurationModifier: true);
        }
    }

    private void AddFleshResin(Entity<XenoReaperComponent> reaper, int amount)
    {
        reaper.Comp.FleshResin = Math.Min(reaper.Comp.MaxFleshResin, reaper.Comp.FleshResin + amount);
        Dirty(reaper);
        UpdateFleshAlert(reaper);
    }

    private bool HasFleshResinPopup(Entity<XenoReaperComponent> reaper, int amount)
    {
        if (reaper.Comp.FleshResin >= amount)
            return true;

        _popup.PopupClient(Loc.GetString("cm-xeno-reaper-not-enough-flesh"), reaper, reaper);
        return false;
    }

    private bool TryRemoveFleshResin(Entity<XenoReaperComponent> reaper, int amount)
    {
        if (!HasFleshResinPopup(reaper, amount))
            return false;

        reaper.Comp.FleshResin -= amount;
        Dirty(reaper);
        UpdateFleshAlert(reaper);
        return true;
    }

    private void UpdateFleshAlert(Entity<XenoReaperComponent> reaper)
    {
        if (reaper.Comp.MaxFleshResin <= 0)
            return;

        var level = MathF.Max(0f, reaper.Comp.FleshResin);
        var max = _alerts.GetMaxSeverity(reaper.Comp.Alert);
        var severity = max - ContentHelpers.RoundToLevels(level, reaper.Comp.MaxFleshResin, max + 1);
        var message = $"{reaper.Comp.FleshResin} / {reaper.Comp.MaxFleshResin}";
        _alerts.ShowAlert(reaper, reaper.Comp.Alert, (short) severity, dynamicMessage: message);
    }
}
