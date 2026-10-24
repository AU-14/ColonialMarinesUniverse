using System.Linq;
using Content.Shared._RMC14.Explosion;
using Content.Shared._RMC14.Map;
using Content.Shared._RMC14.Xenonids.Fruit;
using Content.Shared._RMC14.Xenonids.Fruit.Components;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared.Actions;
using Content.Shared.Actions.Events;
using Content.Shared.CMU14.Threats.Mobs.Xeno.Gambler;
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
    [Dependency] private RMCMapSystem _map = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUXenoGamblerComponent, CMUXenoGambleActionEvent>(OnGamble);
        SubscribeLocalEvent<CMUXenoGamblerComponent, CMUXenoGambleReelActionEvent>(OnReel);
        SubscribeLocalEvent<CMUXenoGamblerComponent, CMUXenoGambleExplodeActionEvent>(OnExplode);
        SubscribeLocalEvent<CMUXenoGamblerComponent, CMUXenoGambleFruitRainActionEvent>(OnFruitRain);

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

        ClearGrants(ent);
        ClearReels(ent);

        ent.Comp.WinnerIcons.Clear();
        ent.Comp.WinnerOverlays.Clear();
        ent.Comp.WinnerTiers.Clear();
        foreach (var roll in rolls)
        {
            if (_actions.AddAction(ent, ent.Comp.ReelAction) is { } reel)
                ent.Comp.Reels.Add(reel);

            ent.Comp.WinnerIcons.Add(GetReward(pool, roll).Icon);
            ent.Comp.WinnerOverlays.Add(pool.Tiers[roll.Tier].Overlay ?? ent.Comp.DefaultOverlay);
            ent.Comp.WinnerTiers.Add(roll.Tier);
        }

        ent.Comp.PendingRolls = rolls;
        ent.Comp.GambleAction = args.Action;
        ent.Comp.SpinStart = _timing.CurTime;
        ent.Comp.LastTickStep = 0;
        ent.Comp.ReelsStopped = 0;
        Dirty(ent);

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
            var target = origin.Offset(offset);
            if (_map.IsTileBlocked(target))
                continue;

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

    private void OnGrantedActionPerformed(Entity<CMUXenoGambleGrantedActionComponent> ent, ref ActionPerformedEvent args)
    {
        if (!TryComp(ent.Comp.Gambler, out CMUXenoGamblerComponent? gambler))
            return;

        foreach (var grant in gambler.Grants)
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

    private static CMUXenoGambleReward GetReward(CMUXenoGamblePoolPrototype pool, CMUXenoGambleRoll roll)
    {
        return pool.Tiers[roll.Tier].Rewards[roll.Reward];
    }

    private static bool SameRoll(CMUXenoGambleRoll a, CMUXenoGambleRoll b)
    {
        return a.Tier == b.Tier && a.Reward == b.Reward;
    }

    private void FinishSpin(Entity<CMUXenoGamblerComponent> ent, CMUXenoGamblePoolPrototype pool)
    {
        ClearReels(ent);

        var time = _timing.CurTime;
        var names = new List<string>();
        CMUXenoGambleTier? bestTier = null;
        foreach (var roll in ent.Comp.PendingRolls)
        {
            var tier = pool.Tiers[roll.Tier];
            if (bestTier == null || tier.Priority > bestTier.Priority)
                bestTier = tier;

            var reward = tier.Rewards[roll.Reward];
            GrantComponents(ent, roll, reward);

            if (_actions.AddAction(ent, reward.Action) is not { } action)
                continue;

            EnsureComp<CMUXenoGambleGrantedActionComponent>(action).Gambler = ent;
            ent.Comp.Grants.Add(new CMUXenoGambleGrant
            {
                Action = action,
                Roll = roll,
                GrantedAt = time,
            });

            names.Add(Name(action));
        }

        ent.Comp.PendingRolls.Clear();
        ent.Comp.WinnerIcons.Clear();
        ent.Comp.WinnerOverlays.Clear();
        ent.Comp.WinnerTiers.Clear();
        ent.Comp.SpinStart = null;
        Dirty(ent);

        if (ent.Comp.GambleAction is { } gamble)
            _actions.SetCooldown(gamble, ent.Comp.SpinCooldown);

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

    private void GrantComponents(Entity<CMUXenoGamblerComponent> ent, CMUXenoGambleRoll roll, CMUXenoGambleReward reward)
    {
        foreach (var (name, entry) in reward.Components)
        {
            var registry = new ComponentRegistry { [name] = entry };
            if (ent.Comp.GrantedComponents.TryGetValue(name, out var granted))
            {
                granted.RemoveAt = null;

                if (SameRoll(granted.Source, roll))
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

    private void RemoveGrant(Entity<CMUXenoGamblerComponent> ent, int index)
    {
        var grant = ent.Comp.Grants[index];
        ent.Comp.Grants.RemoveAt(index);

        if (Exists(grant.Action))
            _actions.RemoveAction(grant.Action);

        var removeAt = _timing.CurTime + ent.Comp.ComponentLinger;
        foreach (var granted in ent.Comp.GrantedComponents.Values)
        {
            if (granted.RemoveAt == null && SameRoll(granted.Source, grant.Roll))
                granted.RemoveAt = removeAt;
        }
    }

    private void ClearGrants(Entity<CMUXenoGamblerComponent> ent)
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

        if (pool == null || !GetReward(pool, grant.Roll).ConsumeOnCooldown)
            return false;

        return _actions.GetAction(grant.Action) is { Comp.Cooldown: { } cooldown } &&
               cooldown.Start >= grant.GrantedAt &&
               cooldown.End > time;
    }

    private void UpdateGambler(Entity<CMUXenoGamblerComponent> ent, TimeSpan time)
    {
        _prototypes.TryIndex(ent.Comp.Pool, out var pool);

        if (ent.Comp.SpinStart is { } start)
        {
            var reels = ent.Comp.PendingRolls.Count;
            var stopped = 0;
            while (stopped < reels && time >= CMUXenoGambleSpin.GetStopTime(ent.Comp, start, stopped))
            {
                stopped++;
            }

            if (stopped > ent.Comp.ReelsStopped)
            {
                ent.Comp.ReelsStopped = stopped;
                ent.Comp.LastStepSound = time;
                _audio.PlayPvs(ent.Comp.ReelStopSound, ent);
            }
            else if (stopped < reels)
            {
                var step = (int) CMUXenoGambleSpin.GetReelPosition(ent.Comp, start, time, stopped);
                if (step > ent.Comp.LastTickStep && time >= ent.Comp.LastStepSound + ent.Comp.MinStepSoundInterval)
                {
                    ent.Comp.LastTickStep = step;
                    ent.Comp.LastStepSound = time;
                    _audio.PlayPvs(ent.Comp.StepSound, ent);
                }
            }

            if (pool == null)
            {
                ent.Comp.PendingRolls.Clear();
                ent.Comp.WinnerIcons.Clear();
                ent.Comp.WinnerOverlays.Clear();
                ent.Comp.WinnerTiers.Clear();
                ent.Comp.SpinStart = null;
                ClearReels(ent);
                Dirty(ent);
            }
            else if (time >= start + CMUXenoGambleSpin.GetDuration(ent.Comp, reels) + ent.Comp.ResultHold)
            {
                FinishSpin(ent, pool);
            }
        }

        for (var i = ent.Comp.Grants.Count - 1; i >= 0; i--)
        {
            if (IsSpent(ent.Comp.Grants[i], pool, time))
                RemoveGrant(ent, i);
        }

        if (ent.Comp.GrantedComponents.Count == 0)
            return;

        foreach (var (name, granted) in ent.Comp.GrantedComponents.ToArray())
        {
            if (granted.RemoveAt is not { } removeAt || removeAt > time)
                continue;

            ent.Comp.GrantedComponents.Remove(name);
            RemCompDeferred(ent, _componentFactory.GetRegistration(name).Type);
        }
    }

    public override void Update(float frameTime)
    {
        var time = _timing.CurTime;

        var gamblers = EntityQueryEnumerator<CMUXenoGamblerComponent>();
        while (gamblers.MoveNext(out var uid, out var gambler))
        {
            if (gambler.SpinStart == null && gambler.Grants.Count == 0 && gambler.GrantedComponents.Count == 0)
                continue;

            UpdateGambler((uid, gambler), time);
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
