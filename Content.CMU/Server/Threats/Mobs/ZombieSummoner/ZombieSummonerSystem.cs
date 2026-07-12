using System.Numerics;
using Content.Server.Actions;
using Content.Server.Chat;
using Content.Server.Chat.Systems;
using Content.Server.Humanoid.Systems;
using Content.Server.Mobs;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Server.Prayer;
using Content.Server.Zombies;
using Content.Shared._RMC14.NightVision;
using Content.Shared._CMU14.Medical.Anatomy.BodyParts.Events;
using Content.Shared.Actions.Components;
using Content.Shared._CMU14.Threats.Mobs.ZombieSummoner;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Dataset;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.NameModifier.EntitySystems;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.Pointing;
using Content.Shared.Popups;
using Content.Shared.Random.Helpers;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Zombies;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._CMU14.Threats.Mobs.ZombieSummoner;

public sealed partial class ZombieSummonerSystem : EntitySystem
{
    private const string ZombiePassiveGroan = "ZombieGroan";
    private const string HumanScream = "Scream";
    private const string ZombieFaction = "Zombie";
    private const string ZombieSummonerBuiClientType = "ZombieSummonerBui";
    private const string TargetKey = "Target";
    private const string TargetCoordinatesKey = "TargetCoordinates";
    private const float UpdateInterval = 1f;

    private float _updateAccumulator;

    private static readonly Color[] RealisticEyeColors =
    {
        Color.Brown,
        Color.Gray,
        Color.Azure,
        Color.SteelBlue,
        Color.Black,
    };

    private static readonly string[] InsanityStageOneMessages =
    {
        "cmu-zombie-summoner-insanity-stage1-1",
        "cmu-zombie-summoner-insanity-stage1-2",
        "cmu-zombie-summoner-insanity-stage1-3",
        "cmu-zombie-summoner-insanity-stage1-4",
    };

    private static readonly string[] InsanityStageTwoMessages =
    {
        "cmu-zombie-summoner-insanity-stage2-1",
        "cmu-zombie-summoner-insanity-stage2-2",
        "cmu-zombie-summoner-insanity-stage2-3",
        "cmu-zombie-summoner-insanity-stage2-4",
        "cmu-zombie-summoner-insanity-stage2-5",
        "cmu-zombie-summoner-insanity-stage2-6",
    };

    private static readonly string[] InsanityStageThreeMessages =
    {
        "cmu-zombie-summoner-insanity-stage3-1",
        "cmu-zombie-summoner-insanity-stage3-2",
        "cmu-zombie-summoner-insanity-stage3-3",
        "cmu-zombie-summoner-insanity-stage3-4",
        "cmu-zombie-summoner-insanity-stage3-5",
        "cmu-zombie-summoner-insanity-stage3-6",
        "cmu-zombie-summoner-insanity-stage3-7",
        "cmu-zombie-summoner-insanity-stage3-8",
        "cmu-zombie-summoner-insanity-stage3-9",
    };

    [Dependency] private ActionsSystem _actions = default!;
    [Dependency] private AutoEmoteSystem _autoEmote = default!;
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private EmoteOnDamageSystem _emoteOnDamage = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private HTNSystem _htn = default!;
    [Dependency] private SharedHumanoidAppearanceSystem _humanoid = default!;
    [Dependency] private MetaDataSystem _meta = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private NPCSystem _npc = default!;
    [Dependency] private NpcFactionSystem _faction = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private PrayerSystem _prayer = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private ZombieSystem _zombie = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ZombieSummonerComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<ZombieSummonerComponent, ComponentStartup>(OnComponentStartup);
        SubscribeLocalEvent<ZombieSummonerComponent, ComponentShutdown>(OnComponentShutdown);
        SubscribeLocalEvent<ZombieSummonerComponent, MobStateChangedEvent>(OnSummonerMobStateChanged,
            after: [typeof(ZombieSystem)]);
        SubscribeLocalEvent<ZombieSummonerComponent, MapInitEvent>(OnMapInit,
            after: [typeof(RandomHumanoidAppearanceSystem)]);
        SubscribeLocalEvent<ZombieSummonerComponent, ZombieSummonerOpenActionEvent>(OnOpenAction);
        SubscribeLocalEvent<ZombieSummonerComponent, ZombieSummonerOrderActionEvent>(OnOrderAction);
        SubscribeLocalEvent<ZombieSummonerComponent, AfterPointedAtEvent>(OnPointedAt);
        SubscribeLocalEvent<ZombieSummonerMinionComponent, ComponentShutdown>(OnMinionShutdown);
        SubscribeLocalEvent<ZombieSummonerMinionComponent, DamageChangedEvent>(OnMinionDamageChanged);
        SubscribeLocalEvent<ZombieSummonerMinionComponent, MobStateChangedEvent>(OnMinionMobStateChanged,
            after: [typeof(ZombieSystem)]);
        SubscribeLocalEvent<ZombieSummonerZombieLabelComponent, RefreshNameModifiersEvent>(OnRefreshZombieLabel);
        SubscribeLocalEvent<MeleeWeaponComponent, MeleeHitEvent>(OnMeleeHit);

        Subs.BuiEvents<ZombieSummonerComponent>(ZombieSummonerUiKey.Key, subs =>
        {
            subs.Event<ZombieSummonerSpawnMessage>(OnSpawnMessage);
        });
    }

    public override void Update(float frameTime)
    {
        _updateAccumulator += frameTime;
        if (_updateAccumulator < UpdateInterval)
            return;

        var elapsed = _updateAccumulator;
        _updateAccumulator = 0f;

        var query = EntityQueryEnumerator<ZombieSummonerComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            UpdatePoints(uid, comp, elapsed);
        }

        UpdateInsanityCurses(elapsed);
        UpdateDelayedFaceMarks();
    }

    private void UpdatePoints(EntityUid uid, ZombieSummonerComponent comp, float frameTime)
    {
        if (comp.Points >= comp.MaxPoints)
        {
            comp.Points = comp.MaxPoints;
            comp.PointAccumulator = 0;
            return;
        }

        comp.PointAccumulator += frameTime * comp.PointsPerSecond;
        var gained = (int) MathF.Floor(comp.PointAccumulator);
        if (gained <= 0)
            return;

        comp.PointAccumulator -= gained;
        comp.Points = Math.Min(comp.Points + gained, comp.MaxPoints);

        if (_ui.IsUiOpen(uid, ZombieSummonerUiKey.Key))
            PushBuiState(uid, comp);
    }

    private void OnComponentInit(Entity<ZombieSummonerComponent> ent, ref ComponentInit args)
    {
        ent.Comp.MaxPoints = Math.Max(1, ent.Comp.MaxPoints);
        ent.Comp.Points = Math.Clamp(ent.Comp.Points, 0, ent.Comp.MaxPoints);
        ent.Comp.ZombieCost = Math.Max(1, ent.Comp.ZombieCost);
        ent.Comp.MilitaryZombieCost = Math.Max(1, ent.Comp.MilitaryZombieCost);
        ent.Comp.PointsPerSecond = Math.Max(0, ent.Comp.PointsPerSecond);
        ent.Comp.MaxControlledZombies = Math.Max(1, ent.Comp.MaxControlledZombies);
        ent.Comp.SpawnRadius = Math.Max(0, ent.Comp.SpawnRadius);
        ent.Comp.ZombieMovementSpeedModifier = Math.Clamp(ent.Comp.ZombieMovementSpeedModifier, 0.1f, 2f);
        ent.Comp.MilitaryZombieMovementSpeedModifier = Math.Clamp(ent.Comp.MilitaryZombieMovementSpeedModifier, 0.1f, 2f);
        ent.Comp.ZombieDelimbChance = Math.Clamp(ent.Comp.ZombieDelimbChance, 0f, 1f);
        ent.Comp.ZombieHitScreamChance = Math.Clamp(ent.Comp.ZombieHitScreamChance, 0f, 1f);
    }

    private void OnComponentStartup(Entity<ZombieSummonerComponent> ent, ref ComponentStartup args)
    {
        RefreshSummonerActions(ent);
    }

    private void OnSummonerMobStateChanged(Entity<ZombieSummonerComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead)
            PerishSummonedZombies(ent);

        RefreshSummonerActions(ent);
    }

    private void RefreshSummonerActions(Entity<ZombieSummonerComponent> ent)
    {
        if (!_mobState.IsAlive(ent.Owner))
        {
            RemoveSummonerActions(ent);
            return;
        }

        var actions = EnsureComp<ActionsComponent>(ent.Owner);
        _actions.AddAction(ent.Owner, ref ent.Comp.ActionOpenEntity, ent.Comp.ActionOpen, component: actions);
        _actions.AddAction(ent.Owner, ref ent.Comp.ActionOrderHaltEntity, ent.Comp.ActionOrderHalt, component: actions);
        _actions.AddAction(ent.Owner, ref ent.Comp.ActionOrderAttackEntity, ent.Comp.ActionOrderAttack, component: actions);
        _actions.AddAction(ent.Owner, ref ent.Comp.ActionOrderFollowEntity, ent.Comp.ActionOrderFollow, component: actions);
        _actions.AddAction(ent.Owner, ref ent.Comp.ActionOrderCheeseEmEntity, ent.Comp.ActionOrderCheeseEm, component: actions);

        UpdateOrderActions(ent);
    }

    private void OnComponentShutdown(Entity<ZombieSummonerComponent> ent, ref ComponentShutdown args)
    {
        PerishSummonedZombies(ent);
        RemoveSummonerActions(ent);
    }

    private void PerishSummonedZombies(Entity<ZombieSummonerComponent> ent)
    {
        if (ent.Comp.Zombies.Count == 0)
            return;

        var zombies = new List<EntityUid>(ent.Comp.Zombies);
        ent.Comp.Zombies.Clear();

        foreach (var zombie in zombies)
        {
            if (Deleted(zombie))
                continue;

            if (TryComp(zombie, out ZombieSummonerMinionComponent? minion))
                DeleteSpawnedWeapon(minion);

            QueueDel(zombie);
        }
    }

    private void RemoveSummonerActions(Entity<ZombieSummonerComponent> ent)
    {
        if (!TryComp(ent.Owner, out ActionsComponent? actions))
            return;

        var actionsEnt = new Entity<ActionsComponent?>(ent.Owner, actions);
        _actions.RemoveAction(actionsEnt, ent.Comp.ActionOpenEntity);
        _actions.RemoveAction(actionsEnt, ent.Comp.ActionOrderHaltEntity);
        _actions.RemoveAction(actionsEnt, ent.Comp.ActionOrderAttackEntity);
        _actions.RemoveAction(actionsEnt, ent.Comp.ActionOrderFollowEntity);
        _actions.RemoveAction(actionsEnt, ent.Comp.ActionOrderCheeseEmEntity);
    }

    private void OnMapInit(Entity<ZombieSummonerComponent> ent, ref MapInitEvent args)
    {
        if (!TryComp(ent, out HumanoidAppearanceComponent? humanoid))
            return;

        if (ent.Comp.SkinColors.Count > 0)
            _humanoid.SetSkinColor(ent.Owner, _random.Pick(ent.Comp.SkinColors), false, false, humanoid);

        EnsureRealisticEyeColor(ent.Owner, humanoid, ent.Comp.EyeColors);
        if (ent.Comp.StartsWithFaceMark)
            _humanoid.AddMarking(ent.Owner, ent.Comp.ZombieFaceMarking, ent.Comp.ZombieFaceMarkingColor, false, true, humanoid);

        Dirty(ent.Owner, humanoid);
    }

    private void OnRefreshZombieLabel(Entity<ZombieSummonerZombieLabelComponent> ent, ref RefreshNameModifiersEvent args)
    {
        args.AddModifier(ent.Comp.Name, 100);
    }

    private void OnOpenAction(Entity<ZombieSummonerComponent> ent, ref ZombieSummonerOpenActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        _ui.TryOpenUi(ent.Owner, ZombieSummonerUiKey.Key, args.Performer);
        PushBuiState(ent.Owner, ent.Comp);
    }

    private void OnOrderAction(Entity<ZombieSummonerComponent> ent, ref ZombieSummonerOrderActionEvent args)
    {
        if (ent.Comp.CurrentOrder == args.Type)
            return;

        args.Handled = true;
        ent.Comp.CurrentOrder = args.Type;
        ent.Comp.OrderedTarget = null;
        Dirty(ent);

        DoCommandCallout(ent);
        UpdateOrderActions(ent);
        UpdateAllZombies(ent);
    }

    private void OnPointedAt(Entity<ZombieSummonerComponent> ent, ref AfterPointedAtEvent args)
    {
        if (ent.Comp.CurrentOrder != ZombieSummonerOrderType.CheeseEm)
            return;

        if (!CanCheeseTarget(args.Pointed))
            return;

        ent.Comp.OrderedTarget = args.Pointed;
        Dirty(ent);

        foreach (var zombie in ent.Comp.Zombies)
        {
            if (!TryComp(zombie, out HTNComponent? htn))
                continue;

            SetZombieCheeseTarget(zombie, htn, args.Pointed);
            if (htn.Plan != null)
                _htn.ShutdownPlan(htn);

            _htn.Replan(htn);
        }
    }

    private bool CanCheeseTarget(EntityUid target)
    {
        return HasComp<DamageableComponent>(target) &&
               !HasComp<ZombieComponent>(target) &&
               !HasComp<ZombieSummonerComponent>(target);
    }

    private bool CanCursedZombieDamageTarget(EntityUid target)
    {
        return HasComp<DamageableComponent>(target) &&
               !HasComp<ZombieComponent>(target) &&
               !HasComp<ZombieSummonerComponent>(target);
    }

    private bool CanStartInsanityCurse(EntityUid target)
    {
        return CanCursedZombieDamageTarget(target) &&
               !HasComp<ZombieSummonerInsanityComponent>(target) &&
               _mobState.IsAlive(target);
    }

    private void TrySeverRandomLimb(EntityUid body)
    {
        var arms = new List<(EntityUid Id, BodyPartComponent Part)>();
        var legs = new List<(EntityUid Id, BodyPartComponent Part)>();

        foreach (var (partUid, part) in _body.GetBodyChildren(body))
        {
            if (part.PartType is BodyPartType.Arm)
                arms.Add((partUid, part));
            else if (part.PartType is BodyPartType.Leg)
                legs.Add((partUid, part));
        }

        if (arms.Count == 0 && legs.Count == 0)
            return;

        List<(EntityUid Id, BodyPartComponent Part)> chosen;

        if (arms.Count > 0 && legs.Count > 0)
            chosen = _random.Prob(0.4f) ? arms : legs;
        else if (arms.Count > 0)
            chosen = arms;
        else
            chosen = legs;

        var (severedPartUid, severedPart) = _random.Pick(chosen);
        var ev = new BodyPartSeveredEvent(body, severedPartUid, severedPart.PartType);
        RaiseLocalEvent(severedPartUid, ref ev);
    }

    private void OnMinionShutdown(Entity<ZombieSummonerMinionComponent> ent, ref ComponentShutdown args)
    {
        DeleteSpawnedWeapon(ent.Comp);

        if (ent.Comp.Summoner is not { } summoner ||
            !TryComp(summoner, out ZombieSummonerComponent? summonerComp))
        {
            return;
        }

        summonerComp.Zombies.Remove(ent.Owner);
        Dirty(summoner, summonerComp);

        if (_ui.IsUiOpen(summoner, ZombieSummonerUiKey.Key))
            PushBuiState(summoner, summonerComp);
    }

    private void OnMinionMobStateChanged(Entity<ZombieSummonerMinionComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead)
        {
            DeleteSpawnedWeapon(ent.Comp);
            QueueDel(ent.Owner);
            return;
        }

        if (args.NewMobState != MobState.Alive)
            return;

        UseHumanScreamAudio(ent.Owner);
        SuppressZombiePassiveGroan(ent.Owner);
        SuppressAutomaticDamageScream(ent.Owner);
    }

    private void DeleteSpawnedWeapon(ZombieSummonerMinionComponent minion)
    {
        if (minion.SpawnedWeapon is not { } weapon)
            return;

        minion.SpawnedWeapon = null;

        if (!Deleted(weapon))
            QueueDel(weapon);
    }

    private void OnMinionDamageChanged(Entity<ZombieSummonerMinionComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased ||
            ent.Comp.HitScreamChance <= 0 ||
            !_random.Prob(ent.Comp.HitScreamChance))
        {
            return;
        }

        _chat.TryEmoteWithoutChat(ent.Owner, HumanScream);
    }

    private void OnMeleeHit(Entity<MeleeWeaponComponent> weapon, ref MeleeHitEvent args)
    {
        if (!args.IsHit ||
            args.HitEntities.Count == 0 ||
            !TryComp(args.User, out ZombieSummonerMinionComponent? minion))
        {
            return;
        }

        var hitValidTarget = false;
        foreach (var target in args.HitEntities)
        {
            if (!CanCursedZombieDamageTarget(target))
                continue;

            hitValidTarget = true;

            if (CanStartInsanityCurse(target))
                StartInsanityCurse(target);

            if (_mobState.IsAlive(target) &&
                HasComp<BodyComponent>(target) &&
                _random.Prob(minion.DelimbChance))
            {
                TrySeverRandomLimb(target);
            }
        }

        if (hitValidTarget && !minion.BonusMeleeDamage.Empty)
            args.BonusDamage += minion.BonusMeleeDamage;
    }

    private void StartInsanityCurse(EntityUid target)
    {
        var insanity = EnsureComp<ZombieSummonerInsanityComponent>(target);
        insanity.Elapsed = 0;
        insanity.TransformAfter = _random.NextFloat(30f * 60f, 35f * 60f);
        insanity.MessageStage = GetInsanityStage(insanity.Elapsed);
        insanity.NextMessageAt = insanity.Elapsed + GetNextInsanityMessageDelay(insanity);
        Dirty(target, insanity);
    }

    private void UpdateInsanityCurses(float frameTime)
    {
        var query = EntityQueryEnumerator<ZombieSummonerInsanityComponent>();
        while (query.MoveNext(out var uid, out var insanity))
        {
            if (!CanCursedZombieDamageTarget(uid) ||
                !_mobState.IsAlive(uid))
            {
                RemCompDeferred<ZombieSummonerInsanityComponent>(uid);
                continue;
            }

            if (insanity.TransformAfter <= 0)
                insanity.TransformAfter = _random.NextFloat(30f * 60f, 35f * 60f);

            insanity.Elapsed += frameTime;

            if (insanity.Elapsed >= insanity.TransformAfter)
            {
                TransformInsanityVictim(uid, insanity);
                continue;
            }

            var currentStage = GetInsanityStage(insanity.Elapsed);
            if (currentStage != insanity.MessageStage)
            {
                insanity.MessageStage = currentStage;
                insanity.NextMessageAt = insanity.Elapsed + GetNextInsanityMessageDelay(insanity);
            }

            if (insanity.NextMessageAt <= 0)
                insanity.NextMessageAt = insanity.Elapsed + GetNextInsanityMessageDelay(insanity);

            if (insanity.Elapsed >= insanity.NextMessageAt)
            {
                SendInsanityMessage(uid, insanity.Elapsed);
                insanity.NextMessageAt = insanity.Elapsed + GetNextInsanityMessageDelay(insanity);
            }

            Dirty(uid, insanity);
        }
    }

    private int GetInsanityStage(float elapsed)
    {
        return elapsed switch
        {
            < 10f * 60f => 1,
            < 20f * 60f => 2,
            _ => 3,
        };
    }

    private float GetNextInsanityMessageDelay(ZombieSummonerInsanityComponent insanity)
    {
        var delay = insanity.MessageStage switch
        {
            1 => insanity.StageOneMessageInterval,
            2 => insanity.StageTwoMessageInterval,
            _ => insanity.StageThreeMessageInterval,
        };

        return Math.Max(1f, delay);
    }

    private void SendInsanityMessage(EntityUid target, float elapsed)
    {
        var messages = GetInsanityStage(elapsed) switch
        {
            1 => InsanityStageOneMessages,
            2 => InsanityStageTwoMessages,
            _ => InsanityStageThreeMessages,
        };

        var message = Loc.GetString(_random.Pick(messages));
        SendInsanityAlert(target, message);
    }

    private void SendInsanityAlert(EntityUid target, string message)
    {
        if (TryComp(target, out ActorComponent? actor))
        {
            _prayer.SendSubtleMessage(
                actor.PlayerSession,
                Loc.GetString("cmu-zombie-summoner-insanity-source"),
                message,
                Loc.GetString("prayer-popup-subtle-default"));
            return;
        }

        _popup.PopupEntity(Loc.GetString("prayer-popup-subtle-default"), target, target, PopupType.Large);
    }

    private void TransformInsanityVictim(EntityUid victim, ZombieSummonerInsanityComponent insanity)
    {
        if (Deleted(victim) ||
            !_mobState.IsAlive(victim))
        {
            return;
        }

        EnsureComp<ActionsComponent>(victim);
        _ui.SetUi(victim,
            ZombieSummonerUiKey.Key,
            new InterfaceData(ZombieSummonerBuiClientType, interactionRange: 0f, requireInputValidation: false));

        var summonerComp = EnsureComp<ZombieSummonerComponent>(victim);
        summonerComp.StartsWithFaceMark = false;
        summonerComp.Points = Math.Clamp(insanity.TransformedStartingPoints, 0, summonerComp.MaxPoints);
        summonerComp.PointAccumulator = 0;
        summonerComp.PointsPerSecond = Math.Max(0, insanity.TransformedPointsPerSecond);
        Dirty(victim, summonerComp);

        EnsureSummonerAbilities(victim, summonerComp);
        RefreshSummonerActions((victim, summonerComp));

        var delayedMark = EnsureComp<ZombieSummonerDelayedFaceMarkComponent>(victim);
        delayedMark.TimeRemaining = Math.Max(0, insanity.DelayedFaceMarkTime);
        delayedMark.ApplyAt = _timing.CurTime + TimeSpan.FromSeconds(delayedMark.TimeRemaining);
        delayedMark.FaceMarking = insanity.FaceMarking;
        delayedMark.FaceMarkingColor = insanity.FaceMarkingColor;
        Dirty(victim, delayedMark);

        RemCompDeferred<ZombieSummonerInsanityComponent>(victim);
    }

    private void EnsureSummonerAbilities(EntityUid uid, ZombieSummonerComponent summoner)
    {
        EnsureComp<ZombieImmuneComponent>(uid);

        EnsureComp<NightVisionComponent>(uid);

        _faction.AddFaction((uid, CompOrNull<NpcFactionMemberComponent>(uid)), ZombieFaction);

        if (TryComp(uid, out HumanoidAppearanceComponent? humanoid))
            EnsureRealisticEyeColor(uid, humanoid, summoner.EyeColors);
    }

    private void EnsureRealisticEyeColor(
        EntityUid uid,
        HumanoidAppearanceComponent humanoid,
        IReadOnlyList<Color> eyeColors)
    {
        var palette = eyeColors.Count > 0 ? eyeColors : RealisticEyeColors;
        if (IsRealisticEyeColor(humanoid.EyeColor, palette))
            return;

        humanoid.EyeColor = palette[_random.Next(palette.Count)];
        Dirty(uid, humanoid);
    }

    private bool IsRealisticEyeColor(Color color, IReadOnlyList<Color> eyeColors)
    {
        foreach (var eyeColor in eyeColors)
        {
            if (color.Equals(eyeColor))
                return true;
        }

        return false;
    }

    private void UpdateDelayedFaceMarks()
    {
        var query = EntityQueryEnumerator<ZombieSummonerDelayedFaceMarkComponent>();
        while (query.MoveNext(out var uid, out var delayedMark))
        {
            if (delayedMark.ApplyAt == TimeSpan.Zero)
            {
                delayedMark.ApplyAt = _timing.CurTime + TimeSpan.FromSeconds(Math.Max(0, delayedMark.TimeRemaining));
                Dirty(uid, delayedMark);
            }

            if (_timing.CurTime < delayedMark.ApplyAt)
            {
                continue;
            }

            ApplyDelayedFaceMark(uid, delayedMark);
            RemCompDeferred<ZombieSummonerDelayedFaceMarkComponent>(uid);
        }
    }

    private void ApplyDelayedFaceMark(EntityUid uid, ZombieSummonerDelayedFaceMarkComponent delayedMark)
    {
        if (!TryComp(uid, out HumanoidAppearanceComponent? humanoid))
            return;

        _humanoid.AddMarking(uid, delayedMark.FaceMarking, delayedMark.FaceMarkingColor, false, true, humanoid);
        Dirty(uid, humanoid);
    }

    private void OnSpawnMessage(Entity<ZombieSummonerComponent> ent, ref ZombieSummonerSpawnMessage args)
    {
        if (args.Count <= 0)
            return;

        var costPerZombie = GetZombieCost(ent.Comp, args.Type);
        var controlledZombies = PruneControlledZombies(ent.Owner, ent.Comp);
        var openSlots = Math.Max(0, ent.Comp.MaxControlledZombies - controlledZombies);
        if (openSlots <= 0)
        {
            _popup.PopupEntity(
                Loc.GetString("cmu-zombie-summoner-too-many-zombies", ("max", ent.Comp.MaxControlledZombies)),
                ent.Owner,
                args.Actor,
                PopupType.SmallCaution);
            PushBuiState(ent.Owner, ent.Comp);
            return;
        }

        var maxSummonable = Math.Min(ent.Comp.Points / costPerZombie, openSlots);
        if (args.Count > maxSummonable)
        {
            var popup = args.Count > openSlots
                ? Loc.GetString("cmu-zombie-summoner-too-many-zombies", ("max", ent.Comp.MaxControlledZombies))
                : Loc.GetString("cmu-zombie-summoner-not-enough-points");

            _popup.PopupEntity(popup, ent.Owner, args.Actor, PopupType.SmallCaution);
            PushBuiState(ent.Owner, ent.Comp);
            return;
        }

        var cost = args.Count * costPerZombie;
        ent.Comp.Points -= cost;

        for (var i = 0; i < args.Count; i++)
        {
            var zombie = Spawn(GetZombiePrototype(ent.Comp, args.Type), GetSpawnCoordinates(ent.Owner, i, args.Count, ent.Comp));
            var skinColor = Color.Transparent;
            var eyeColor = Color.Transparent;
            var hasHumanoidAppearance = false;
            if (TryComp(zombie, out HumanoidAppearanceComponent? humanoid))
            {
                hasHumanoidAppearance = true;
                skinColor = humanoid.SkinColor;
                eyeColor = humanoid.EyeColor;
            }

            _zombie.ZombifyEntity(zombie);
            var minion = EnsureComp<ZombieSummonerMinionComponent>(zombie);
            minion.Summoner = ent.Owner;
            minion.BonusMeleeDamage = new DamageSpecifier(ent.Comp.ZombieBonusMeleeDamage);
            minion.DelimbChance = ent.Comp.ZombieDelimbChance;
            minion.HitScreamChance = ent.Comp.ZombieHitScreamChance;
            ConfigureCursedZombie(zombie, ent.Comp, skinColor, eyeColor, hasHumanoidAppearance);
            minion.SpawnedWeapon = GiveZombieWeapon(zombie, ent.Comp, args.Type);
            Dirty(zombie, minion);

            MakeZombieMove(zombie, GetZombieMovementSpeedModifier(ent.Comp, args.Type));

            ent.Comp.Zombies.Add(zombie);
            UpdateZombieNpc(ent.Owner, zombie, ent.Comp);
            SetZombieName(zombie);
        }

        var message = args.Type == ZombieSummonerSpawnType.Military
            ? "cmu-zombie-summoner-summoned-military"
            : "cmu-zombie-summoner-summoned-civilian";

        _popup.PopupEntity(Loc.GetString(message, ("count", args.Count)), ent.Owner, args.Actor);
        PushBuiState(ent.Owner, ent.Comp);
    }

    private void ConfigureCursedZombie(
        EntityUid zombie,
        ZombieSummonerComponent comp,
        Color skinColor,
        Color eyeColor,
        bool restoreHumanoidAppearance)
    {
        MakeZombieNonSpreading(zombie);
        UseHumanScreamAudio(zombie);
        if (restoreHumanoidAppearance)
            RestoreCursedAppearance(zombie, comp, skinColor, eyeColor);

        SuppressZombiePassiveGroan(zombie);
        SuppressAutomaticDamageScream(zombie);
        ConfigureZombieDeathgasp(zombie);
        GiveZombieHands(zombie);
    }

    private void UseHumanScreamAudio(EntityUid zombie)
    {
        if (!TryComp(zombie, out ZombieComponent? zombieComp))
            return;

        zombieComp.EmoteSoundsId = null;
        zombieComp.EmoteSounds = null;
        Dirty(zombie, zombieComp);
    }

    private void SuppressZombiePassiveGroan(EntityUid zombie)
    {
        if (TryComp(zombie, out AutoEmoteComponent? autoEmote))
            _autoEmote.RemoveEmote(zombie, ZombiePassiveGroan, autoEmote);
    }

    private void SuppressAutomaticDamageScream(EntityUid zombie)
    {
        if (TryComp(zombie, out EmoteOnDamageComponent? emoteOnDamage))
            _emoteOnDamage.RemoveEmote(zombie, HumanScream, emoteOnDamage);
    }

    private void ConfigureZombieDeathgasp(EntityUid zombie)
    {
        var deathgasp = EnsureComp<DeathgaspComponent>(zombie);
        deathgasp.Prototype = HumanScream;
    }

    private void MakeZombieNonSpreading(EntityUid zombie)
    {
        EnsureComp<NonSpreaderZombieComponent>(zombie);
        RemCompDeferred<PendingZombieComponent>(zombie);
        RemCompDeferred<ZombifyOnDeathComponent>(zombie);

        if (!TryComp(zombie, out ZombieComponent? zombieComp))
            return;

        zombieComp.BaseZombieInfectionChance = 0f;
        zombieComp.MinZombieInfectionChance = 0f;
        Dirty(zombie, zombieComp);
    }

    private void RestoreCursedAppearance(
        EntityUid zombie,
        ZombieSummonerComponent comp,
        Color skinColor,
        Color eyeColor)
    {
        if (!TryComp(zombie, out HumanoidAppearanceComponent? humanoid))
            return;

        _humanoid.SetSkinColor(zombie, skinColor, false, false, humanoid);
        humanoid.EyeColor = eyeColor;
        EnsureRealisticEyeColor(zombie, humanoid, comp.EyeColors);
        _humanoid.AddMarking(zombie, comp.ZombieFaceMarking, comp.ZombieFaceMarkingColor, false, true, humanoid);
        Dirty(zombie, humanoid);
    }

    private void GiveZombieHands(EntityUid zombie)
    {
        var hands = EnsureComp<HandsComponent>(zombie);

        if (!_hands.TrySetHandLocation((zombie, hands), "right_hand", HandLocation.Right))
            _hands.AddHand((zombie, hands), "right_hand", HandLocation.Right);

        if (!_hands.TrySetHandLocation((zombie, hands), "left_hand", HandLocation.Left))
            _hands.AddHand((zombie, hands), "left_hand", HandLocation.Left);

        if (hands.ActiveHandId == null)
            _hands.TrySetActiveHand((zombie, hands), "right_hand");
    }

    private EntityUid? GiveZombieWeapon(EntityUid zombie, ZombieSummonerComponent comp, ZombieSummonerSpawnType type)
    {
        var prototypes = GetZombieWeaponPrototypes(comp, type);
        if (prototypes.Count == 0)
            return null;

        var start = _random.Next(prototypes.Count);
        for (var i = 0; i < prototypes.Count; i++)
        {
            var prototype = prototypes[(start + i) % prototypes.Count];
            if (!_prototype.HasIndex<EntityPrototype>(prototype))
                continue;

            var weapon = Spawn(prototype, Transform(zombie).Coordinates);
            if (_hands.TryPickupAnyHand(zombie, weapon, checkActionBlocker: false, animate: false))
                return weapon;

            QueueDel(weapon);
        }

        return null;
    }

    private List<EntProtoId> GetZombieWeaponPrototypes(ZombieSummonerComponent comp, ZombieSummonerSpawnType type)
    {
        return type == ZombieSummonerSpawnType.Military
            ? comp.MilitaryZombieWeaponPrototypes
            : comp.ZombieWeaponPrototypes;
    }

    private int GetZombieCost(ZombieSummonerComponent comp, ZombieSummonerSpawnType type)
    {
        return type == ZombieSummonerSpawnType.Military
            ? comp.MilitaryZombieCost
            : comp.ZombieCost;
    }

    private EntProtoId GetZombiePrototype(ZombieSummonerComponent comp, ZombieSummonerSpawnType type)
    {
        return type == ZombieSummonerSpawnType.Military
            ? comp.MilitaryZombiePrototype
            : comp.ZombiePrototype;
    }

    private float GetZombieMovementSpeedModifier(ZombieSummonerComponent comp, ZombieSummonerSpawnType type)
    {
        return type == ZombieSummonerSpawnType.Military
            ? comp.MilitaryZombieMovementSpeedModifier
            : comp.ZombieMovementSpeedModifier;
    }

    private void SetZombieName(EntityUid zombie)
    {
        var name = TryComp(zombie, out ZombieSummonerZombieLabelComponent? label)
            ? Loc.GetString(label.Name)
            : Loc.GetString("cmu-zombie-summoner-zombie-name");

        _meta.SetEntityName(zombie, name, raiseEvents: false);
    }

    private void MakeZombieMove(EntityUid zombie, float movementSpeedModifier)
    {
        if (!TryComp(zombie, out ZombieComponent? zombieComp))
            return;

        zombieComp.ZombieMovementSpeedDebuff = movementSpeedModifier;
        Dirty(zombie, zombieComp);
        _movementSpeed.RefreshMovementSpeedModifiers(zombie);
    }

    private void UpdateAllZombies(Entity<ZombieSummonerComponent> ent)
    {
        var stale = new List<EntityUid>();

        foreach (var zombie in ent.Comp.Zombies)
        {
            if (Deleted(zombie) ||
                !TryComp(zombie, out ZombieSummonerMinionComponent? minion) ||
                minion.Summoner != ent.Owner)
            {
                stale.Add(zombie);
                continue;
            }

            UpdateZombieNpc(ent.Owner, zombie, ent.Comp);
        }

        foreach (var zombie in stale)
        {
            ent.Comp.Zombies.Remove(zombie);
        }
    }

    private int PruneControlledZombies(EntityUid summoner, ZombieSummonerComponent comp)
    {
        List<EntityUid>? stale = null;
        foreach (var zombie in comp.Zombies)
        {
            if (!Deleted(zombie) &&
                TryComp(zombie, out ZombieSummonerMinionComponent? minion) &&
                minion.Summoner == summoner)
            {
                continue;
            }

            stale ??= new List<EntityUid>();
            stale.Add(zombie);
        }

        if (stale == null)
            return comp.Zombies.Count;

        foreach (var zombie in stale)
        {
            comp.Zombies.Remove(zombie);
        }

        return comp.Zombies.Count;
    }

    private void UpdateZombieNpc(
        EntityUid summoner,
        EntityUid zombie,
        ZombieSummonerComponent comp)
    {
        if (!TryComp(zombie, out HTNComponent? htn))
            return;

        htn.RootTask = new HTNCompoundTask { Task = comp.ZombieOrderCompound };
        SetBlackboard(zombie, htn, NPCBlackboard.Owner, zombie);
        SetBlackboard(zombie, htn, NPCBlackboard.FollowTarget, new EntityCoordinates(summoner, Vector2.Zero));
        SetBlackboard(zombie, htn, NPCBlackboard.CurrentOrders, comp.CurrentOrder);

        if (comp.CurrentOrder == ZombieSummonerOrderType.CheeseEm &&
            comp.OrderedTarget is { } target &&
            CanCheeseTarget(target))
        {
            SetZombieCheeseTarget(zombie, htn, target);
        }
        else
        {
            ClearZombieCheeseTarget(htn);
        }

        if (htn.Plan != null)
            _htn.ShutdownPlan(htn);

        _htn.Replan(htn);
    }

    private void SetBlackboard<T>(EntityUid npc, HTNComponent htn, string key, T value)
    {
        if (htn.Blackboard.TryGetValue<T>(key, out var existing, EntityManager) &&
            EqualityComparer<T>.Default.Equals(existing, value))
        {
            return;
        }

        _npc.SetBlackboard(npc, key, value!, htn);
    }

    private void RemoveBlackboard<T>(HTNComponent htn, string key)
    {
        if (htn.Blackboard.ContainsKey(key))
            htn.Blackboard.Remove<T>(key);
    }

    private void SetZombieCheeseTarget(EntityUid zombie, HTNComponent htn, EntityUid target)
    {
        _npc.SetBlackboard(zombie, NPCBlackboard.CurrentOrderedTarget, target, htn);
        RemoveBlackboard<EntityUid>(htn, TargetKey);
        RemoveBlackboard<EntityCoordinates>(htn, TargetCoordinatesKey);
    }

    private void ClearZombieCheeseTarget(HTNComponent htn)
    {
        RemoveBlackboard<EntityUid>(htn, NPCBlackboard.CurrentOrderedTarget);
        RemoveBlackboard<EntityUid>(htn, TargetKey);
        RemoveBlackboard<EntityCoordinates>(htn, TargetCoordinatesKey);
    }

    private void UpdateOrderActions(Entity<ZombieSummonerComponent> ent)
    {
        _actions.SetToggled(ent.Comp.ActionOrderHaltEntity, ent.Comp.CurrentOrder == ZombieSummonerOrderType.Halt);
        _actions.SetToggled(ent.Comp.ActionOrderAttackEntity, ent.Comp.CurrentOrder == ZombieSummonerOrderType.Attack);
        _actions.SetToggled(ent.Comp.ActionOrderFollowEntity, ent.Comp.CurrentOrder == ZombieSummonerOrderType.Follow);
        _actions.SetToggled(ent.Comp.ActionOrderCheeseEmEntity, ent.Comp.CurrentOrder == ZombieSummonerOrderType.CheeseEm);
        _actions.StartUseDelay(ent.Comp.ActionOrderHaltEntity);
        _actions.StartUseDelay(ent.Comp.ActionOrderAttackEntity);
        _actions.StartUseDelay(ent.Comp.ActionOrderFollowEntity);
        _actions.StartUseDelay(ent.Comp.ActionOrderCheeseEmEntity);
    }

    private void DoCommandCallout(Entity<ZombieSummonerComponent> ent)
    {
        if (!ent.Comp.OrderCallouts.TryGetValue(ent.Comp.CurrentOrder, out var datasetId) ||
            !_prototype.TryIndex<LocalizedDatasetPrototype>(datasetId, out var dataset))
        {
            return;
        }

        var message = _random.Pick(dataset);
        _chat.TrySendInGameICMessage(ent.Owner, message, InGameICChatType.Speak, hideChat: false);
    }

    private EntityCoordinates GetSpawnCoordinates(
        EntityUid summoner,
        int index,
        int total,
        ZombieSummonerComponent comp)
    {
        var coordinates = Transform(summoner).Coordinates;
        if (total <= 1 || comp.SpawnRadius <= 0)
            return coordinates;

        var angle = MathF.PI * 2f * index / total + _random.NextFloat(-0.2f, 0.2f);
        var radius = _random.NextFloat(0.35f, comp.SpawnRadius);
        var offset = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
        return coordinates.Offset(offset);
    }

    private void PushBuiState(EntityUid uid, ZombieSummonerComponent comp)
    {
        var controlledZombies = PruneControlledZombies(uid, comp);

        _ui.SetUiState(uid, ZombieSummonerUiKey.Key, new ZombieSummonerBuiState(
            comp.Points,
            comp.MaxPoints,
            comp.ZombieCost,
            comp.MilitaryZombieCost,
            controlledZombies,
            comp.MaxControlledZombies));
    }
}
