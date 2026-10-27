using System.Linq;
using Content.Shared._RMC14.Explosion;
using Content.Shared._RMC14.Map;
using Content.Shared._RMC14.Xenonids.Fruit;
using Content.Shared._RMC14.Xenonids.Fruit.Components;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared.Actions;
using Content.Shared.Actions.Events;
using Content.Shared.CMU14.Threats.Mobs.Xeno.Gambler;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Popups;
using Robust.Server.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Threats.Mobs.Xeno.Gambler;

public sealed partial class CMUXenoGamblerSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private IComponentFactory _componentFactory = default!;
    [Dependency] private SharedRMCExplosionSystem _explosion = default!;
    [Dependency] private SharedXenoFruitSystem _fruit = default!;
    [Dependency] private SharedXenoHiveSystem _hive = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private RMCMapSystem _map = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TurfSystem _turf = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUXenoGamblerComponent, CMUXenoGambleActionEvent>(OnGamble);
        SubscribeLocalEvent<CMUXenoGamblerComponent, CMUXenoGambleReelActionEvent>(OnReel);
        SubscribeLocalEvent<CMUXenoGamblerComponent, CMUXenoGambleExplodeActionEvent>(OnExplode);
        SubscribeLocalEvent<CMUXenoGamblerComponent, CMUXenoGambleFruitRainActionEvent>(OnFruitRain);

        SubscribeLocalEvent<CMUXenoGambleStateComponent, EntityUnpausedEvent>(OnStateUnpaused);
        SubscribeLocalEvent<CMUXenoGambleGrantedActionComponent, ActionPerformedEvent>(OnGrantedActionPerformed);
    }

    private void OnGamble(Entity<CMUXenoGamblerComponent> ent, ref CMUXenoGambleActionEvent args)
    {
        if (args.Handled || ent.Comp.SpinStart != null)
            return;

        if (!_prototypes.TryIndex(ent.Comp.Pool, out var pool))
            return;

        var rolls = Roll(ent, pool);
        if (rolls.Count == 0)
            return;

        // make the best roll the last one for ultimate retention & dopamine
        rolls.Sort((a, b) => pool.Tiers[a.Tier].Priority.CompareTo(pool.Tiers[b.Tier].Priority));

        args.Handled = true;

        var state = EnsureComp<CMUXenoGambleStateComponent>(ent);
        ClearGrants((ent, state));
        ClearReels(ent);

        foreach (var _ in rolls)
        {
            if (_actions.AddAction(ent, ent.Comp.ReelAction) is { } reel)
                ent.Comp.Reels.Add(reel);
        }

        ent.Comp.SpinRolls = rolls;
        ent.Comp.SpinStart = _timing.CurTime;
        Dirty(ent);

        state.GambleAction = args.Action;
        state.LastTickStep = 0;
        state.ReelsStopped = 0;

        _audio.PlayPvs(ent.Comp.SpinSound, ent);
    }

    private void OnReel(Entity<CMUXenoGamblerComponent> ent, ref CMUXenoGambleReelActionEvent args)
    {
        _popup.PopupEntity("The reels are still spinning!", ent, ent, PopupType.SmallCaution);
    }

    private void OnFruitRain(Entity<CMUXenoGamblerComponent> ent, ref CMUXenoGambleFruitRainActionEvent args)
    {
        if (args.Handled || args.Fruits.Count == 0 || args.Amount <= 0)
            return;

        args.Handled = true;

        var rain = EnsureComp<CMUXenoGambleFruitRainComponent>(ent);
        rain.Fruits = args.Fruits;
        rain.Remaining += args.Amount;
        rain.Radius = args.Radius;
        rain.Interval = args.Interval;
        rain.DropSound = args.DropSound;
        rain.NextDrop = _timing.CurTime;
    }

    private void DropFruit(Entity<CMUXenoGambleFruitRainComponent> ent)
    {
        var origin = Transform(ent).Coordinates;
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var offset = _random.NextVector2(ent.Comp.Radius);
            if (!_turf.TryGetTileRef(origin.Offset(offset), out var tile) || _turf.IsSpace(tile.Value))
                continue;

            var target = _turf.GetTileCenter(tile.Value);
            if (_map.IsTileBlocked(target) ||
                !_interaction.InRangeUnobstructed(ent.Owner, target, ent.Comp.Radius + 1))
            {
                continue;
            }

            var fruit = SpawnAtPosition(_random.Pick(ent.Comp.Fruits), target);

            _hive.SetSameHive(ent.Owner, fruit);

            if (TryComp(fruit, out XenoFruitComponent? fruitComp))
                _fruit.MakeHarvested((fruit, fruitComp));

            _audio.PlayPvs(ent.Comp.DropSound, fruit);
            return;
        }
    }

    private void OnExplode(Entity<CMUXenoGamblerComponent> ent, ref CMUXenoGambleExplodeActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        _explosion.QueueExplosion(_transform.GetMapCoordinates(ent),
            args.ExplosionType,
            args.TotalIntensity,
            args.IntensitySlope,
            args.MaxIntensity,
            ent,
            canCreateVacuum: false);
    }

    private void OnStateUnpaused(Entity<CMUXenoGambleStateComponent> ent, ref EntityUnpausedEvent args)
    {
        ent.Comp.LastStepSound += args.PausedTime;
        foreach (var grant in ent.Comp.Grants)
        {
            grant.GrantedAt += args.PausedTime;
        }

        foreach (var granted in ent.Comp.GrantedComponents.Values)
        {
            if (granted.RemoveAt is { } removeAt)
                granted.RemoveAt = removeAt + args.PausedTime;
        }
    }

    private void OnGrantedActionPerformed(Entity<CMUXenoGambleGrantedActionComponent> ent, ref ActionPerformedEvent args)
    {
        if (!TryComp(ent.Comp.Gambler, out CMUXenoGambleStateComponent? state))
            return;

        foreach (var grant in state.Grants)
        {
            if (grant.Action == ent.Owner)
                grant.Used = true;
        }
    }

    private List<CMUXenoGambleRoll> Roll(Entity<CMUXenoGamblerComponent> ent, CMUXenoGamblePoolPrototype pool)
    {
        var rolls = new List<CMUXenoGambleRoll>();
        var totalWeight = pool.Tiers.Sum(t => t.Weight);
        if (totalWeight <= 0)
            return rolls;

        // account for shared abilities using same component
        var usedActions = new HashSet<EntProtoId>();
        var usedComponents = new HashSet<string>();
        var attempts = ent.Comp.MaxRollAttempts * ent.Comp.Rolls;
        while (rolls.Count < ent.Comp.Rolls && attempts-- > 0)
        {
            var tier = PickTier(pool, totalWeight);
            var rewards = pool.Tiers[tier].Rewards;
            if (rewards.Count == 0)
                continue;

            var rewardIndex = _random.Next(rewards.Count);
            var reward = rewards[rewardIndex];
            if (usedActions.Contains(reward.Action) ||
                reward.Components.Keys.Any(usedComponents.Contains))
            {
                continue;
            }

            usedActions.Add(reward.Action);
            usedComponents.UnionWith(reward.Components.Keys);
            rolls.Add(new CMUXenoGambleRoll { Tier = tier, Reward = rewardIndex });
        }

        return rolls;
    }

    private int PickTier(CMUXenoGamblePoolPrototype pool, float totalWeight)
    {
        var pick = _random.NextFloat(totalWeight);
        for (var i = 0; i < pool.Tiers.Count; i++)
        {
            pick -= pool.Tiers[i].Weight;
            if (pick < 0)
                return i;
        }

        return pool.Tiers.Count - 1;
    }

    private void FinishSpin(Entity<CMUXenoGamblerComponent, CMUXenoGambleStateComponent> ent, CMUXenoGamblePoolPrototype pool)
    {
        ClearReels(ent);

        var time = _timing.CurTime;
        var names = new List<string>();
        CMUXenoGambleTier? bestTier = null;
        foreach (var roll in ent.Comp1.SpinRolls)
        {
            if (!roll.TryGet(pool, out var tier, out var reward))
                continue;

            if (bestTier == null || tier.Priority > bestTier.Priority)
                bestTier = tier;

            if (_actions.AddAction(ent, reward.Action) is not { } action)
                continue;

            GrantComponents((ent, ent.Comp2), roll, reward);

            EnsureComp<CMUXenoGambleGrantedActionComponent>(action).Gambler = ent;
            ent.Comp2.Grants.Add(new CMUXenoGambleGrant
            {
                Action = action,
                Roll = roll,
                GrantedAt = time,
            });

            names.Add(Name(action));
        }

        ent.Comp1.SpinRolls.Clear();
        ent.Comp1.SpinStart = null;
        Dirty(ent, ent.Comp1);

        if (ent.Comp2.GambleAction is { } gamble)
            _actions.SetCooldown(gamble, ent.Comp1.SpinCooldown);

        if (bestTier != null)
        {
            foreach (var sound in bestTier.Sounds)
            {
                _audio.PlayPvs(sound, ent);
            }
        }

        if (names.Count > 0)
        {
            var msg = $"You rolled: {string.Join(", ", names)}!";
            _popup.PopupEntity(msg, ent, ent, PopupType.Large);
        }
    }

    private void GrantComponents(Entity<CMUXenoGambleStateComponent> ent, CMUXenoGambleRoll roll, CMUXenoGambleReward reward)
    {
        foreach (var (name, entry) in reward.Components)
        {
            var registry = new ComponentRegistry { [name] = entry };
            if (ent.Comp.GrantedComponents.TryGetValue(name, out var granted))
            {
                granted.RemoveAt = null;

                if (granted.Source.Matches(roll))
                    continue;

                granted.Source = roll;
                EntityManager.AddComponents(ent, registry, removeExisting: true);
                continue;
            }

            if (HasComp(ent, _componentFactory.GetRegistration(name).Type))
                continue;

            EntityManager.AddComponents(ent, registry, removeExisting: false);
            ent.Comp.GrantedComponents[name] = new CMUXenoGambleGrantedComponent { Source = roll };
        }
    }

    private void RemoveGrant(Entity<CMUXenoGambleStateComponent> ent, int index)
    {
        var grant = ent.Comp.Grants[index];
        ent.Comp.Grants.RemoveAt(index);

        if (Exists(grant.Action))
            _actions.RemoveAction(grant.Action);

        var removeAt = _timing.CurTime + (TryComp(ent, out CMUXenoGamblerComponent? gambler)
            ? gambler.ComponentLinger
            : TimeSpan.Zero);

        foreach (var granted in ent.Comp.GrantedComponents.Values)
        {
            if (granted.RemoveAt == null && granted.Source.Matches(grant.Roll))
                granted.RemoveAt = removeAt;
        }
    }

    private void ClearGrants(Entity<CMUXenoGambleStateComponent> ent)
    {
        for (var i = ent.Comp.Grants.Count - 1; i >= 0; i--)
        {
            RemoveGrant(ent, i);
        }
    }

    private void ClearReels(Entity<CMUXenoGamblerComponent> ent)
    {
        foreach (var reel in ent.Comp.Reels)
        {
            if (Exists(reel))
                _actions.RemoveAction(reel);
        }

        ent.Comp.Reels.Clear();
    }

    private bool IsSpent(CMUXenoGambleGrant grant, CMUXenoGamblePoolPrototype? pool, TimeSpan time)
    {
        if (grant.Used || !Exists(grant.Action))
            return true;

        if (pool == null || !grant.Roll.TryGet(pool, out _, out var reward) || !reward.ConsumeOnCooldown)
            return false;

        return _actions.GetAction(grant.Action) is { Comp.Cooldown: { } cooldown } &&
               cooldown.Start >= grant.GrantedAt &&
               cooldown.End > time;
    }

    private void UpdateSpin(Entity<CMUXenoGamblerComponent, CMUXenoGambleStateComponent> ent,
        CMUXenoGamblePoolPrototype? pool,
        TimeSpan start,
        TimeSpan time)
    {
        var (gambler, state) = (ent.Comp1, ent.Comp2);
        var reels = gambler.SpinRolls.Count;
        var stopped = 0;
        while (stopped < reels && time >= CMUXenoGambleSpin.GetStopTime(gambler, start, stopped))
        {
            stopped++;
        }

        if (stopped > state.ReelsStopped)
        {
            state.ReelsStopped = stopped;
            state.LastStepSound = time;
            _audio.PlayPvs(gambler.ReelStopSound, ent);
        }
        else if (stopped < reels)
        {
            var step = (int) CMUXenoGambleSpin.GetReelPosition(gambler, start, time, stopped);
            if (step > state.LastTickStep && time >= state.LastStepSound + gambler.MinStepSoundInterval)
            {
                state.LastTickStep = step;
                state.LastStepSound = time;
                _audio.PlayPvs(gambler.StepSound, ent);
            }
        }

        if (pool == null)
        {
            gambler.SpinRolls.Clear();
            gambler.SpinStart = null;
            ClearReels((ent, gambler));
            Dirty(ent, gambler);
        }
        else if (time >= start + CMUXenoGambleSpin.GetDuration(gambler, reels) + gambler.ResultHold)
        {
            FinishSpin(ent, pool);
        }
    }

    private void UpdateGambler(Entity<CMUXenoGamblerComponent, CMUXenoGambleStateComponent> ent, TimeSpan time)
    {
        _prototypes.TryIndex(ent.Comp1.Pool, out var pool);

        if (ent.Comp1.SpinStart is { } start)
            UpdateSpin(ent, pool, start, time);

        var state = ent.Comp2;
        for (var i = state.Grants.Count - 1; i >= 0; i--)
        {
            if (IsSpent(state.Grants[i], pool, time))
                RemoveGrant((ent, state), i);
        }

        if (state.GrantedComponents.Count == 0)
            return;

        foreach (var (name, granted) in state.GrantedComponents.ToArray())
        {
            if (granted.RemoveAt is not { } removeAt || removeAt > time)
                continue;

            state.GrantedComponents.Remove(name);
            RemCompDeferred(ent, _componentFactory.GetRegistration(name).Type);
        }
    }

    public override void Update(float frameTime)
    {
        var time = _timing.CurTime;

        var gamblers = EntityQueryEnumerator<CMUXenoGamblerComponent, CMUXenoGambleStateComponent>();
        while (gamblers.MoveNext(out var uid, out var gambler, out var state))
        {
            if (gambler.SpinStart == null && state.Grants.Count == 0 && state.GrantedComponents.Count == 0)
                continue;

            UpdateGambler((uid, gambler, state), time);
        }

        var rains = EntityQueryEnumerator<CMUXenoGambleFruitRainComponent>();
        while (rains.MoveNext(out var uid, out var rain))
        {
            if (rain.NextDrop > time)
                continue;

            rain.NextDrop = time + rain.Interval;
            DropFruit((uid, rain));

            if (--rain.Remaining <= 0)
                RemCompDeferred<CMUXenoGambleFruitRainComponent>(uid);
        }
    }
}
