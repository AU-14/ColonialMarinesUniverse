using Content.Server.Chat.Systems;
using Content.Server.Chat.Managers;
using Content.Server._RMC14.Xenonids.Leap;
using Content.Shared._CMU14.Yautja;
using Content.Shared._RMC14.Actions;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.Parasite;
using Content.Shared.Actions;
using Content.Shared.Chat;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.StatusIcon.Components;
using Content.Shared.Stunnable;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Maths;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._CMU14.Yautja;

public sealed partial class YautjaAbominationSystem : EntitySystem
{
    private static readonly TimeSpan GestationCheckEvery = TimeSpan.FromSeconds(1);
    private static readonly SpriteSpecifier.Rsi FrenzySingleIcon = new(new ResPath("_RMC14/Actions/xeno_actions.rsi"), "rav_eviscerate");
    private static readonly SpriteSpecifier.Rsi FrenzyAreaIcon = new(new ResPath("_RMC14/Actions/xeno_actions.rsi"), "spin_slash");

    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MovementSpeedModifierSystem _movement = default!;
    [Dependency] private XenoParasiteSystem _parasite = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedRMCActionsSystem _rmcActions = default!;
    [Dependency] private ISharedPlayerManager _players = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private YautjaCloakSystem _cloak = default!;
    [Dependency] private YautjaMarkSystem _marks = default!;
    [Dependency] private SharedXenoHiveSystem _hive = default!;
    [Dependency] private XenoSystem _xeno = default!;

    private TimeSpan _nextGestationCheck;

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaAbominationComponent, MapInitEvent>(OnAbominationMapInit);
        SubscribeLocalEvent<YautjaAbominationComponent, GetMeleeDamageEvent>(OnGetMeleeDamage);
        SubscribeLocalEvent<YautjaAbominationComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<YautjaAbominationComponent, YautjaAbominationRushActionEvent>(OnRush);
        SubscribeLocalEvent<YautjaAbominationComponent, YautjaAbominationRoarActionEvent>(OnRoar);
        SubscribeLocalEvent<YautjaAbominationComponent, YautjaAbominationToggleFrenzyModeActionEvent>(OnToggleFrenzyMode);
        SubscribeLocalEvent<YautjaAbominationComponent, YautjaAbominationSmashActionEvent>(OnSmash);
        SubscribeLocalEvent<YautjaAbominationComponent, YautjaAbominationFrenzyActionEvent>(OnFrenzy);

        SubscribeLocalEvent<YautjaComponent, DamageModifyEvent>(OnYautjaDamageModify);
        SubscribeLocalEvent<MobStateChangedEvent>(OnAnyMobStateChanged);

        SubscribeLocalEvent<YautjaAbominationRushComponent, RefreshMovementSpeedModifiersEvent>(OnRushRefreshSpeed);
        SubscribeLocalEvent<YautjaAbominationRoarBuffComponent, RefreshMovementSpeedModifiersEvent>(OnRoarBuffRefreshSpeed);
        SubscribeLocalEvent<YautjaAbominationRoarBuffComponent, GetMeleeDamageEvent>(OnRoarBuffGetMeleeDamage);
    }

    private void ConfigureAbominationGestation(Entity<VictimInfectedComponent> ent)
    {
        if (!TryComp(ent, out YautjaAbominationHostComponent? host))
        {
            if (!HasComp<YautjaComponent>(ent))
                return;

            host = EnsureComp<YautjaAbominationHostComponent>(ent);
        }

        if (ent.Comp.BurstSpawn == host.LarvaPrototype)
            return;

        _parasite.SetBurstSpawn(ent, host.LarvaPrototype);
        _popup.PopupEntity(Loc.GetString("cmu-yautja-abomination-gestating"), ent, ent, PopupType.LargeCaution);
    }

    private void OnAbominationMapInit(Entity<YautjaAbominationComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.AnnounceAt = _timing.CurTime + ent.Comp.AnnounceDelay;
        Dirty(ent);

        _marks.ForceMark(ent.Owner, ent.Owner, YautjaMarkKind.Dishonored);

        _popup.PopupEntity(Loc.GetString("cmu-yautja-abomination-awaken"), ent, ent, PopupType.LargeCaution);
    }

    private void OnGetMeleeDamage(Entity<YautjaAbominationComponent> ent, ref GetMeleeDamageEvent args)
    {
        if (ent.Comp.Kills <= 0)
            return;

        args.Damage += MakeSlash(ent.Comp.DamagePerKill * ent.Comp.Kills);
    }

    private void OnMeleeHit(Entity<YautjaAbominationComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit)
            return;

        foreach (var hit in args.HitEntities)
            MarkAbominationStrike(ent, hit);
    }

    private void OnYautjaDamageModify(Entity<YautjaComponent> ent, ref DamageModifyEvent args)
    {
        if (args.Origin is not { } origin ||
            !TryComp(origin, out YautjaAbominationComponent? abomination))
        {
            return;
        }

        args.Damage *= abomination.YautjaDamageMultiplier;
    }

    private void OnAnyMobStateChanged(MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead ||
            args.Origin is not { } origin ||
            !TryComp(origin, out YautjaAbominationComponent? abomination) ||
            origin == args.Target ||
            abomination.Kills >= abomination.MaxKills)
        {
            return;
        }

        abomination.Kills = Math.Min(abomination.MaxKills, abomination.Kills + 1);
        Dirty(origin, abomination);
    }

    private void OnRush(Entity<YautjaAbominationComponent> ent, ref YautjaAbominationRushActionEvent args)
    {
        if (args.Handled || !_rmcActions.TryUseAction(args))
            return;

        args.Handled = true;
        var rush = EnsureComp<YautjaAbominationRushComponent>(ent);
        rush.SpeedMultiplier = ent.Comp.RushSpeedMultiplier;
        rush.ExpiresAt = _timing.CurTime + ent.Comp.RushDuration;
        Dirty(ent.Owner, rush);
        _movement.RefreshMovementSpeedModifiers(ent);
        _audio.PlayPvs(ent.Comp.RushSound, ent);
        _popup.PopupEntity(Loc.GetString("cmu-yautja-abomination-rush-start"), ent, ent);
    }

    private void OnRoar(Entity<YautjaAbominationComponent> ent, ref YautjaAbominationRoarActionEvent args)
    {
        if (args.Handled || !_rmcActions.TryUseAction(args))
            return;

        args.Handled = true;
        _audio.PlayPvs(ent.Comp.RoarSound, ent);
        _popup.PopupEntity(Loc.GetString("cmu-yautja-abomination-roar-others", ("user", ent.Owner)), ent, ent, PopupType.Large);

        var coords = _transform.GetMapCoordinates(ent);
        foreach (var yautja in _lookup.GetEntitiesInRange<YautjaComponent>(coords, ent.Comp.RoarRange))
        {
            if (yautja.Owner == ent.Owner)
                continue;

            _cloak.ForceDecloak(yautja);
        }

        foreach (var xeno in _lookup.GetEntitiesInRange<XenoComponent>(coords, ent.Comp.RoarRange))
        {
            if (xeno.Owner == ent.Owner)
                continue;

            var buff = EnsureComp<YautjaAbominationRoarBuffComponent>(xeno);
            buff.DamageBonus = ent.Comp.RoarDamagePerKill * ent.Comp.Kills;
            buff.SpeedMultiplier = 1f + ent.Comp.RoarSpeedPerKill * ent.Comp.Kills;
            buff.ExpiresAt = _timing.CurTime + ent.Comp.RoarBuffBaseDuration + ent.Comp.RoarBuffDurationPerKill * ent.Comp.Kills;
            Dirty(xeno.Owner, buff);
            _movement.RefreshMovementSpeedModifiers(xeno);
        }
    }

    private void OnToggleFrenzyMode(Entity<YautjaAbominationComponent> ent, ref YautjaAbominationToggleFrenzyModeActionEvent args)
    {
        if (args.Handled || !_rmcActions.TryUseAction(args))
            return;

        args.Handled = true;
        ent.Comp.FrenzyAreaMode = !ent.Comp.FrenzyAreaMode;
        Dirty(ent);
        RefreshFrenzyActions(ent);

        var loc = ent.Comp.FrenzyAreaMode
            ? "cmu-yautja-abomination-frenzy-mode-area"
            : "cmu-yautja-abomination-frenzy-mode-single";
        _popup.PopupEntity(Loc.GetString(loc), ent, ent);
    }

    private void OnSmash(Entity<YautjaAbominationComponent> ent, ref YautjaAbominationSmashActionEvent args)
    {
        if (args.Handled ||
            !CanAttackTargetInRange(ent, args.Target, ent.Comp.SmashRange) ||
            !_rmcActions.TryUseAction(args))
        {
            return;
        }

        args.Handled = true;
        var damage = ent.Comp.SmashBaseDamage + ent.Comp.SmashDamagePerKill * ent.Comp.Kills;
        Strike(ent, args.Target, MakeSlash(damage), ent.Comp.SmashSound);
        _stun.TryParalyze(args.Target, ent.Comp.SmashParalyze, true);
    }

    private void OnFrenzy(Entity<YautjaAbominationComponent> ent, ref YautjaAbominationFrenzyActionEvent args)
    {
        if (args.Handled ||
            !CanAttackTargetInRange(ent, args.Target, ent.Comp.FrenzyRange) ||
            !_rmcActions.TryUseAction(args))
        {
            return;
        }

        args.Handled = true;
        _audio.PlayPvs(ent.Comp.FrenzySound, ent);

        if (!ent.Comp.FrenzyAreaMode)
        {
            if (!CanAttackTarget(ent.Owner, args.Target))
                return;

            var damage = ent.Comp.FrenzySingleBaseDamage + ent.Comp.FrenzyDamagePerKill * ent.Comp.Kills;
            Strike(ent, args.Target, MakeSlash(damage), ent.Comp.FrenzySound);
            return;
        }

        var damageArea = MakeSlash(ent.Comp.FrenzyAreaBaseDamage + ent.Comp.FrenzyDamagePerKill * ent.Comp.Kills);
        var coords = _transform.GetMapCoordinates(ent);
        foreach (var target in _lookup.GetEntitiesInRange<MobStateComponent>(coords, ent.Comp.FrenzyRange))
        {
            if (!CanAttackTarget(ent.Owner, target))
                continue;

            Strike(ent, target, damageArea, null);
        }
    }

    private void OnRushRefreshSpeed(Entity<YautjaAbominationRushComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        args.ModifySpeed(ent.Comp.SpeedMultiplier, ent.Comp.SpeedMultiplier);
    }

    private void OnRoarBuffRefreshSpeed(Entity<YautjaAbominationRoarBuffComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        args.ModifySpeed(ent.Comp.SpeedMultiplier, ent.Comp.SpeedMultiplier);
    }

    private void OnRoarBuffGetMeleeDamage(Entity<YautjaAbominationRoarBuffComponent> ent, ref GetMeleeDamageEvent args)
    {
        if (ent.Comp.DamageBonus <= FixedPoint2.Zero)
            return;

        args.Damage += MakeSlash(ent.Comp.DamageBonus);
    }

    public override void Update(float frameTime)
    {
        var time = _timing.CurTime;

        if (time >= _nextGestationCheck)
        {
            _nextGestationCheck = time + GestationCheckEvery;
            UpdateAbominationGestation();
        }

        var abominationQuery = EntityQueryEnumerator<YautjaAbominationComponent>();
        while (abominationQuery.MoveNext(out var uid, out var abomination))
        {
            if (abomination.Announced || abomination.AnnounceAt > time)
                continue;

            abomination.Announced = true;
            Dirty(uid, abomination);
            _audio.PlayPvs(abomination.RoarSound, uid);
            AnnounceToYautja(uid, Loc.GetString("cmu-yautja-abomination-announcement"));
        }

        var rushQuery = EntityQueryEnumerator<YautjaAbominationRushComponent>();
        while (rushQuery.MoveNext(out var uid, out var rush))
        {
            if (rush.ExpiresAt > time)
                continue;

            RemCompDeferred<YautjaAbominationRushComponent>(uid);
            _movement.RefreshMovementSpeedModifiers(uid);
        }

        var buffQuery = EntityQueryEnumerator<YautjaAbominationRoarBuffComponent>();
        while (buffQuery.MoveNext(out var uid, out var buff))
        {
            if (buff.ExpiresAt > time)
                continue;

            RemCompDeferred<YautjaAbominationRoarBuffComponent>(uid);
            _movement.RefreshMovementSpeedModifiers(uid);
        }
    }

    private void UpdateAbominationGestation()
    {
        var query = EntityQueryEnumerator<VictimInfectedComponent>();
        while (query.MoveNext(out var uid, out var infected))
        {
            ConfigureAbominationGestation((uid, infected));
        }
    }

    private void AnnounceToYautja(EntityUid source, string message)
    {
        var wrapped = Loc.GetString("chat-manager-server-wrap-message", ("message", message));
        var query = EntityQueryEnumerator<YautjaComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (!_players.TryGetSessionByEntity(uid, out var session))
                continue;

            _chat.ChatMessageToOne(ChatChannel.Radio, message, wrapped, source, false, session.Channel, Color.Red);
        }
    }

    private bool CanAttackTarget(EntityUid abomination, EntityUid target)
    {
        if (abomination == target || _mobState.IsDead(target))
            return false;

        if (HasComp<YautjaComponent>(target))
            return true;

        if (!HasComp<XenoComponent>(target))
            return HasComp<MobStateComponent>(target);

        return !_hive.FromSameHive(abomination, target);
    }

    private bool CanAttackTargetInRange(Entity<YautjaAbominationComponent> abomination, EntityUid target, float range)
    {
        if (!CanAttackTarget(abomination.Owner, target))
            return false;

        return _transform.InRange(_transform.GetMoverCoordinates(abomination.Owner), _transform.GetMoverCoordinates(target), range);
    }

    private void RefreshFrenzyActions(Entity<YautjaAbominationComponent> ent)
    {
        foreach (var frenzyAction in _rmcActions.GetActionsWithEvent<YautjaAbominationFrenzyActionEvent>(ent.Owner))
        {
            _actions.SetIcon(frenzyAction.AsNullable(), ent.Comp.FrenzyAreaMode ? FrenzyAreaIcon : FrenzySingleIcon);
        }

        foreach (var toggleAction in _rmcActions.GetActionsWithEvent<YautjaAbominationToggleFrenzyModeActionEvent>(ent.Owner))
        {
            _actions.SetToggled(toggleAction.AsNullable(), ent.Comp.FrenzyAreaMode);
        }
    }

    private void Strike(Entity<YautjaAbominationComponent> abomination, EntityUid target, DamageSpecifier damage, Robust.Shared.Audio.SoundSpecifier? sound)
    {
        if (sound != null)
            _audio.PlayPvs(sound, abomination);

        MarkAbominationStrike(abomination, target);
        var finalDamage = _xeno.TryApplyXenoSlashDamageMultiplier(target, damage);
        _damage.TryChangeDamage(target, finalDamage, origin: abomination.Owner, tool: abomination.Owner);
    }

    private void MarkAbominationStrike(Entity<YautjaAbominationComponent> abomination, EntityUid target)
    {
        if (abomination.Owner == target || !TryComp(target, out MobStateComponent? mobState) || mobState.CurrentState == MobState.Dead)
            return;

        // The actual kill credit is taken from MobStateChangedEvent. This keeps ability and melee paths consistent.
    }

    private static DamageSpecifier MakeSlash(FixedPoint2 amount)
    {
        var damage = new DamageSpecifier();
        damage.DamageDict["Slash"] = amount;
        return damage;
    }
}
