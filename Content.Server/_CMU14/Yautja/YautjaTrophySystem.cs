using Content.Server.Administration.Logs;
using Content.Shared._CMU14.Medical.Anatomy.BodyParts.Events;
using Content.Shared._CMU14.Medical.Core;
using Content.Shared._CMU14.Yautja;
using Content.Shared._RMC14.Medical.Unrevivable;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.Body.Part;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Storage;
using Content.Shared.Traits.Assorted;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._CMU14.Yautja;

public sealed partial class YautjaTrophySystem : EntitySystem
{
    private static readonly EntProtoId HumanSkullPrototype = "CMUYautjaHumanSkullTrophy";
    private static readonly EntProtoId HumanLeftArmBonePrototype = "CMUYautjaHumanLeftArmBoneTrophy";
    private static readonly EntProtoId HumanRightArmBonePrototype = "CMUYautjaHumanRightArmBoneTrophy";
    private static readonly EntProtoId HumanLeftHandBonePrototype = "CMUYautjaHumanLeftHandBoneTrophy";
    private static readonly EntProtoId HumanRightHandBonePrototype = "CMUYautjaHumanRightHandBoneTrophy";
    private static readonly EntProtoId HumanLeftLegBonePrototype = "CMUYautjaHumanLeftLegBoneTrophy";
    private static readonly EntProtoId HumanRightLegBonePrototype = "CMUYautjaHumanRightLegBoneTrophy";
    private static readonly EntProtoId HumanLeftFootBonePrototype = "CMUYautjaHumanLeftFootBoneTrophy";
    private static readonly EntProtoId HumanRightFootBonePrototype = "CMUYautjaHumanRightFootBoneTrophy";
    private static readonly EntProtoId HumanRibcagePrototype = "CMUYautjaHumanRibcageTrophy";
    private static readonly EntProtoId XenoSkullPrototype = "CMUYautjaXenoSkullTrophy";
    private static readonly EntProtoId XenoPeltPrototype = "CMUYautjaXenoPeltTrophy";
    private static readonly EntProtoId HumanMeatPrototype = "FoodMeatHuman";
    private static readonly EntProtoId XenoMeatPrototype = "FoodMeatXeno";
    private static readonly EntProtoId HumanHidePrototype = "CMUYautjaHumanHide";
    private static readonly EntProtoId HumanSpinePrototype = "CMUYautjaHumanSpine";
    private static readonly EntProtoId HumanRemainsPrototype = "CMUYautjaHumanButcheredRemains";
    private static readonly EntProtoId XenoRemainsPrototype = "CMUYautjaXenoButcheredRemains";

    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private CMUMedicalBodyIndexSystem _medicalIndex = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private MetaDataSystem _meta = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private RMCUnrevivableSystem _unrevivable = default!;
    [Dependency] private YautjaMarkSystem _marks = default!;
    [Dependency] private YautjaRitualSystem _ritual = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<MobStateComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAlternativeVerbs);
        SubscribeLocalEvent<YautjaTrophySourceComponent, YautjaHarvestTrophyDoAfterEvent>(OnHarvestTrophyDoAfter);
        SubscribeLocalEvent<YautjaTrophySourceComponent, YautjaButcherDoAfterEvent>(OnButcherDoAfter);
        SubscribeLocalEvent<YautjaTrophyComponent, ExaminedEvent>(OnTrophyExamined);
        SubscribeLocalEvent<YautjaTrophyComponent, InteractUsingEvent>(OnTrophyInteractUsing);
        SubscribeLocalEvent<YautjaTrophyRecordComponent, ExaminedEvent>(OnRecordExamined);
        SubscribeLocalEvent<YautjaTrophyDisplayComponent, ExaminedEvent>(OnDisplayExamined);
    }

    private void OnGetAlternativeVerbs(Entity<MobStateComponent> target, ref GetVerbsEvent<AlternativeVerb> args)
    {
        AddTargetVerbs(target.Owner, target.Comp, args.User, args.CanInteract, args.Verbs);
    }

    private void AddTargetVerbs<TVerb>(
        EntityUid targetUid,
        MobStateComponent mobState,
        EntityUid user,
        bool canInteract,
        SortedSet<TVerb> verbs)
        where TVerb : Verb, new()
    {
        if (!canInteract ||
            user == targetUid ||
            !HasComp<YautjaComponent>(user))
        {
            return;
        }

        if (_mobState.IsAlive(targetUid, mobState))
        {
            AddRitualVerbs(user, targetUid, verbs);
            return;
        }

        if (!_mobState.IsDead(targetUid, mobState))
            return;

        var isBadBlood = HasComp<YautjaBadBloodComponent>(user);
        var humanTarget = IsHumanTrophyTarget(targetUid);
        var badBloodHumanRestricted = isBadBlood && humanTarget && !HasComp<UnrevivableComponent>(targetUid);

        if (!badBloodHumanRestricted)
            AddButcherVerb(user, targetUid, verbs);

        if (!badBloodHumanRestricted && CanHarvest(user, targetUid, YautjaTrophyKind.HumanSkull))
            AddHarvestVerb(user, targetUid, YautjaTrophyKind.HumanSkull, verbs);

        if (humanTarget)
        {
            AddHumanBoneVerb(user, targetUid, YautjaTrophyKind.HumanLeftArmBone, verbs);
            AddHumanBoneVerb(user, targetUid, YautjaTrophyKind.HumanRightArmBone, verbs);
            AddHumanBoneVerb(user, targetUid, YautjaTrophyKind.HumanLeftHandBone, verbs);
            AddHumanBoneVerb(user, targetUid, YautjaTrophyKind.HumanRightHandBone, verbs);
            AddHumanBoneVerb(user, targetUid, YautjaTrophyKind.HumanLeftLegBone, verbs);
            AddHumanBoneVerb(user, targetUid, YautjaTrophyKind.HumanRightLegBone, verbs);
            AddHumanBoneVerb(user, targetUid, YautjaTrophyKind.HumanLeftFootBone, verbs);
            AddHumanBoneVerb(user, targetUid, YautjaTrophyKind.HumanRightFootBone, verbs);

            if (!badBloodHumanRestricted)
                AddHumanBoneVerb(user, targetUid, YautjaTrophyKind.HumanRibcage, verbs);
        }
        else if (IsXenoTrophyTarget(targetUid))
        {
            if (CanHarvest(user, targetUid, YautjaTrophyKind.XenoSkull))
                AddHarvestVerb(user, targetUid, YautjaTrophyKind.XenoSkull, verbs);

            if (CanHarvest(user, targetUid, YautjaTrophyKind.XenoPelt))
                AddHarvestVerb(user, targetUid, YautjaTrophyKind.XenoPelt, verbs);
        }
    }

    private void AddRitualVerbs<TVerb>(EntityUid hunter, EntityUid target, SortedSet<TVerb> verbs)
        where TVerb : Verb, new()
    {
        if (!TryComp(target, out YautjaRitualDuelComponent? ritual))
        {
            if (!_ritual.CanClaimCaptive(hunter, target, true, false))
                return;

            verbs.Add(new TVerb
            {
                Text = Loc.GetString("cmu-yautja-ritual-claim-verb"),
                Priority = 4,
                Act = () => _ritual.TryClaimCaptive(hunter, target),
            });
            return;
        }

        if (ritual.Hunter != hunter)
            return;

        if (ritual.State == YautjaRitualState.Captive)
        {
            verbs.Add(new TVerb
            {
                Text = Loc.GetString("cmu-yautja-ritual-begin-duel-verb"),
                Priority = 4,
                Act = () => _ritual.TryBeginDuel(hunter, target),
            });
        }

        verbs.Add(new TVerb
        {
            Text = Loc.GetString("cmu-yautja-ritual-release-verb"),
            Priority = 3,
            Act = () => _ritual.TryReleaseCaptive(hunter, target),
        });
    }

    private void AddHarvestVerb<TVerb>(
        EntityUid hunter,
        EntityUid target,
        YautjaTrophyKind kind,
        SortedSet<TVerb> verbs)
        where TVerb : Verb, new()
    {
        verbs.Add(new TVerb
        {
            Text = GetVerbText(kind),
            Priority = 3,
            Act = () => TryStartHarvestTrophy(hunter, target, kind),
        });
    }

    private void AddButcherVerb<TVerb>(EntityUid hunter, EntityUid target, SortedSet<TVerb> verbs)
        where TVerb : Verb, new()
    {
        if (!CanButcher(hunter, target, out _) ||
            TryComp(target, out YautjaTrophySourceComponent? source) && source.ButcheryProgress >= 4)
        {
            return;
        }

        verbs.Add(new TVerb
        {
            Text = Loc.GetString("cmu-yautja-butcher-verb"),
            Priority = 5,
            Act = () => TryStartButcher(hunter, target),
        });
    }

    private void AddHumanBoneVerb<TVerb>(
        EntityUid hunter,
        EntityUid target,
        YautjaTrophyKind kind,
        SortedSet<TVerb> verbs)
        where TVerb : Verb, new()
    {
        if (CanHarvest(hunter, target, kind))
            AddHarvestVerb(hunter, target, kind, verbs);
    }

    public bool TryStartHarvestTrophy(EntityUid hunter, EntityUid target, YautjaTrophyKind kind)
    {
        if (!TryValidateHarvestTrophy(hunter, target, kind, out _, out var source, true))
            return false;

        var doAfter = new DoAfterArgs(
            EntityManager,
            hunter,
            source.HarvestDelay,
            new YautjaHarvestTrophyDoAfterEvent(kind),
            target,
            target)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            BreakOnHandChange = true,
            BlockDuplicate = true,
            CancelDuplicate = true,
            DuplicateCondition = DuplicateConditions.SameTarget | DuplicateConditions.SameEvent,
            DistanceThreshold = 1.5f,
            ForceVisible = true,
            TargetEffect = "RMCEffectXenoTelegraphRedEmpower",
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return false;

        _audio.PlayPvs(source.HarvestStartSound, target);
        _popup.PopupEntity(
            Loc.GetString("cmu-yautja-trophy-harvest-start-self", ("target", target)),
            target,
            hunter,
            PopupType.LargeCaution);

        var filter = Filter.Pvs(target, entityManager: EntityManager)
            .RemoveWhereAttachedEntity(attached => attached == hunter);
        _popup.PopupEntity(
            Loc.GetString("cmu-yautja-trophy-harvest-start-others", ("hunter", HunterDisplayName(hunter)), ("target", target)),
            target,
            filter,
            true,
            PopupType.LargeCaution);

        return true;
    }

    public bool TryStartButcher(EntityUid hunter, EntityUid target)
    {
        if (!CanButcher(hunter, target, out var kind))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-trophy-invalid"), hunter, hunter, PopupType.SmallCaution);
            return false;
        }

        var source = EnsureComp<YautjaTrophySourceComponent>(target);
        if (source.ButcheryProgress >= 4)
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-butcher-already-finished"), hunter, hunter, PopupType.SmallCaution);
            return false;
        }

        var stage = source.ButcheryProgress + 1;
        var delay = GetButcherDelay(stage);
        var doAfter = new DoAfterArgs(
            EntityManager,
            hunter,
            delay,
            new YautjaButcherDoAfterEvent(kind, stage),
            target,
            target)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            BreakOnHandChange = true,
            BlockDuplicate = true,
            CancelDuplicate = true,
            DuplicateCondition = DuplicateConditions.SameTarget | DuplicateConditions.SameEvent,
            DistanceThreshold = 1.5f,
            ForceVisible = true,
            TargetEffect = "RMCEffectXenoTelegraphRedEmpower",
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return false;

        _audio.PlayPvs(source.ButcherStartSound, target);
        _popup.PopupEntity(
            Loc.GetString("cmu-yautja-butcher-start-self", ("target", target), ("stage", stage)),
            target,
            hunter,
            PopupType.LargeCaution);

        var filter = Filter.Pvs(target, entityManager: EntityManager)
            .RemoveWhereAttachedEntity(attached => attached == hunter);
        _popup.PopupEntity(
            Loc.GetString("cmu-yautja-butcher-start-others", ("hunter", HunterDisplayName(hunter)), ("target", target)),
            target,
            filter,
            true,
            PopupType.LargeCaution);

        return true;
    }

    private void OnHarvestTrophyDoAfter(Entity<YautjaTrophySourceComponent> target, ref YautjaHarvestTrophyDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        args.Handled = true;
        TryHarvestTrophy(args.User, target.Owner, args.Kind, out _);
    }

    private void OnButcherDoAfter(Entity<YautjaTrophySourceComponent> target, ref YautjaButcherDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        args.Handled = true;
        TryCompleteButcherStage(args.User, target, args.Kind, args.Stage);
    }

    public bool TryHarvestTrophy(EntityUid hunter, EntityUid target, YautjaTrophyKind kind, out EntityUid trophy)
    {
        trophy = default;

        if (!TryValidateHarvestTrophy(hunter, target, kind, out var prototype, out var source, true))
            return false;

        prototype = GetTrophyPrototype(target, kind, prototype);
        trophy = Spawn(prototype, Transform(target).Coordinates);
        var trophyComp = EnsureComp<YautjaTrophyComponent>(trophy);
        trophyComp.Kind = kind;
        trophyComp.Source = target;
        trophyComp.Hunter = hunter;
        trophyComp.SourceName = GetSourceName(target, kind);
        Dirty(trophy, trophyComp);
        ApplyTrophyName(trophy, target, kind, trophyComp.SourceName);

        SetTaken(source, kind);
        RecordTrophy(hunter, kind);
        SeverTrophyPart(target, kind);
        MakeTargetUnrevivableForTrophy(target, kind);
        TryCompletePreyClaim(hunter, target, kind);

        if (!TryStoreTrophy(hunter, trophy))
            _hands.TryPickupAnyHand(hunter, trophy);

        _audio.PlayPvs(source.HarvestFinishSound, target);
        _popup.PopupEntity(Loc.GetString("cmu-yautja-trophy-harvested", ("trophy", trophy)), hunter, hunter);
        var filter = Filter.Pvs(target, entityManager: EntityManager)
            .RemoveWhereAttachedEntity(attached => attached == hunter);
        _popup.PopupEntity(
            Loc.GetString("cmu-yautja-trophy-harvest-finished-others", ("hunter", HunterDisplayName(hunter)), ("target", target), ("trophy", trophy)),
            target,
            filter,
            true,
            PopupType.MediumCaution);
        _adminLog.Add(LogType.Action, LogImpact.Medium, $"{ToPrettyString(hunter):player} harvested {kind} trophy {ToPrettyString(trophy):trophy} from {ToPrettyString(target):target}");
        return true;
    }

    private void MakeTargetUnrevivableForTrophy(EntityUid target, YautjaTrophyKind kind)
    {
        if (!IsUnrevivableTrophy(kind))
            return;

        _unrevivable.MakeUnrevivable(target);

        var unrevivable = EnsureComp<UnrevivableComponent>(target);
        unrevivable.Analyzable = false;
        unrevivable.Cloneable = false;
        unrevivable.ReasonMessage = "rmc-defibrillator-unrevivable";
        Dirty(target, unrevivable);
    }

    private static bool IsUnrevivableTrophy(YautjaTrophyKind kind)
    {
        return kind is YautjaTrophyKind.HumanSkull
            or YautjaTrophyKind.HumanRibcage
            or YautjaTrophyKind.XenoSkull;
    }

    private EntProtoId GetTrophyPrototype(EntityUid target, YautjaTrophyKind kind, EntProtoId fallback)
    {
        if (kind != YautjaTrophyKind.XenoSkull && kind != YautjaTrophyKind.XenoPelt)
            return fallback;

        var prototypeId = MetaData(target).EntityPrototype?.ID;
        if (prototypeId == null)
            return fallback;

        var caste = GetXenoTrophyCaste(prototypeId);
        return caste switch
        {
            "queen" when kind == YautjaTrophyKind.XenoSkull => "CMUYautjaQueenSkullTrophy",
            "king" when kind == YautjaTrophyKind.XenoSkull => "CMUYautjaKingSkullTrophy",
            "crusher" when kind == YautjaTrophyKind.XenoSkull => "CMUYautjaCrusherSkullTrophy",
            "praetorian" when kind == YautjaTrophyKind.XenoSkull => "CMUYautjaPraetorianSkullTrophy",
            "corroder" when kind == YautjaTrophyKind.XenoSkull => "CMUYautjaCorroderSkullTrophy",
            "despoiler" when kind == YautjaTrophyKind.XenoSkull => "CMUYautjaDespoilerSkullTrophy",
            "deacon" when kind == YautjaTrophyKind.XenoSkull => "CMUYautjaDeaconSkullTrophy",
            "ravager" when kind == YautjaTrophyKind.XenoSkull => "CMUYautjaRavagerSkullTrophy",
            "boiler" when kind == YautjaTrophyKind.XenoSkull => "CMUYautjaBoilerSkullTrophy",
            "defender" when kind == YautjaTrophyKind.XenoSkull => "CMUYautjaDefenderSkullTrophy",
            "warrior" when kind == YautjaTrophyKind.XenoSkull => "CMUYautjaWarriorSkullTrophy",
            "carrier" when kind == YautjaTrophyKind.XenoSkull => "CMUYautjaCarrierSkullTrophy",
            "hivelord" when kind == YautjaTrophyKind.XenoSkull => "CMUYautjaHivelordSkullTrophy",
            "burrower" when kind == YautjaTrophyKind.XenoSkull => "CMUYautjaBurrowerSkullTrophy",
            "hunter" when kind == YautjaTrophyKind.XenoSkull => "CMUYautjaHunterSkullTrophy",
            "lurker" when kind == YautjaTrophyKind.XenoSkull => "CMUYautjaLurkerSkullTrophy",
            "sentinel" when kind == YautjaTrophyKind.XenoSkull => "CMUYautjaSentinelSkullTrophy",
            "spitter" when kind == YautjaTrophyKind.XenoSkull => "CMUYautjaSpitterSkullTrophy",
            "runner" when kind == YautjaTrophyKind.XenoSkull => "CMUYautjaRunnerSkullTrophy",
            "drone" when kind == YautjaTrophyKind.XenoSkull => "CMUYautjaDroneSkullTrophy",
            "queen" => "CMUYautjaQueenPeltTrophy",
            "king" => "CMUYautjaKingPeltTrophy",
            "crusher" => "CMUYautjaCrusherPeltTrophy",
            "praetorian" => "CMUYautjaPraetorianPeltTrophy",
            "corroder" => "CMUYautjaCorroderPeltTrophy",
            "despoiler" => "CMUYautjaDespoilerPeltTrophy",
            "deacon" => "CMUYautjaDeaconPeltTrophy",
            "ravager" => "CMUYautjaRavagerPeltTrophy",
            "boiler" => "CMUYautjaBoilerPeltTrophy",
            "defender" => "CMUYautjaDefenderPeltTrophy",
            "warrior" => "CMUYautjaWarriorPeltTrophy",
            "carrier" => "CMUYautjaCarrierPeltTrophy",
            "hivelord" => "CMUYautjaHivelordPeltTrophy",
            "burrower" => "CMUYautjaBurrowerPeltTrophy",
            "hunter" => "CMUYautjaHunterPeltTrophy",
            "lurker" => "CMUYautjaLurkerPeltTrophy",
            "sentinel" => "CMUYautjaSentinelPeltTrophy",
            "spitter" => "CMUYautjaSpitterPeltTrophy",
            "runner" => "CMUYautjaRunnerPeltTrophy",
            "drone" => "CMUYautjaDronePeltTrophy",
            "larva" => "CMUYautjaLarvaPeltTrophy",
            _ => fallback,
        };
    }

    private static string? GetXenoTrophyCaste(string prototypeId)
    {
        if (prototypeId.Contains("Queen", StringComparison.OrdinalIgnoreCase))
            return "queen";
        if (prototypeId.Contains("King", StringComparison.OrdinalIgnoreCase))
            return "king";
        if (prototypeId.Contains("Crusher", StringComparison.OrdinalIgnoreCase))
            return "crusher";
        if (prototypeId.Contains("Praetorian", StringComparison.OrdinalIgnoreCase))
            return "praetorian";
        if (prototypeId.Contains("Corroder", StringComparison.OrdinalIgnoreCase))
            return "corroder";
        if (prototypeId.Contains("Despoiler", StringComparison.OrdinalIgnoreCase))
            return "despoiler";
        if (prototypeId.Contains("Deacon", StringComparison.OrdinalIgnoreCase))
            return "deacon";
        if (prototypeId.Contains("Ravager", StringComparison.OrdinalIgnoreCase))
            return "ravager";
        if (prototypeId.Contains("Boiler", StringComparison.OrdinalIgnoreCase))
            return "boiler";
        if (prototypeId.Contains("Defender", StringComparison.OrdinalIgnoreCase))
            return "defender";
        if (prototypeId.Contains("Warrior", StringComparison.OrdinalIgnoreCase))
            return "warrior";
        if (prototypeId.Contains("Carrier", StringComparison.OrdinalIgnoreCase))
            return "carrier";
        if (prototypeId.Contains("Hivelord", StringComparison.OrdinalIgnoreCase))
            return "hivelord";
        if (prototypeId.Contains("Burrower", StringComparison.OrdinalIgnoreCase))
            return "burrower";
        if (prototypeId.Contains("Hunter", StringComparison.OrdinalIgnoreCase))
            return "hunter";
        if (prototypeId.Contains("Lurker", StringComparison.OrdinalIgnoreCase))
            return "lurker";
        if (prototypeId.Contains("Sentinel", StringComparison.OrdinalIgnoreCase))
            return "sentinel";
        if (prototypeId.Contains("Spitter", StringComparison.OrdinalIgnoreCase))
            return "spitter";
        if (prototypeId.Contains("Runner", StringComparison.OrdinalIgnoreCase))
            return "runner";
        if (prototypeId.Contains("Larva", StringComparison.OrdinalIgnoreCase))
            return "larva";
        if (prototypeId.Contains("Drone", StringComparison.OrdinalIgnoreCase))
            return "drone";

        return null;
    }

    private bool TryCompleteButcherStage(EntityUid hunter, Entity<YautjaTrophySourceComponent> target, YautjaButcherKind kind, int stage)
    {
        if (!CanButcher(hunter, target.Owner, out var actualKind) ||
            actualKind != kind ||
            target.Comp.ButcheryProgress + 1 != stage)
        {
            return false;
        }

        var coords = Transform(target).Coordinates;
        switch (stage)
        {
            case 1:
            case 2:
                SpawnButcherOutput(kind == YautjaButcherKind.Xeno ? XenoMeatPrototype : HumanMeatPrototype, coords, 2);
                break;
            case 3:
                Spawn(kind == YautjaButcherKind.Xeno ? XenoRemainsPrototype : HumanRemainsPrototype, coords);
                break;
            case 4:
                CompleteFinalButcherStage(hunter, target, kind, coords);
                break;
            default:
                return false;
        }

        target.Comp.ButcheryProgress = stage;
        _audio.PlayPvs(target.Comp.ButcherFinishSound, target);

        _popup.PopupEntity(
            Loc.GetString(stage >= 4 ? "cmu-yautja-butcher-finished" : "cmu-yautja-butcher-stage-complete", ("target", target.Owner), ("stage", stage)),
            hunter,
            hunter);

        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(hunter):hunter} completed Yautja butchery stage {stage} on {ToPrettyString(target.Owner):target}");

        if (stage >= 4)
            QueueDel(target.Owner);

        return true;
    }

    private void CompleteFinalButcherStage(EntityUid hunter, Entity<YautjaTrophySourceComponent> target, YautjaButcherKind kind, EntityCoordinates coords)
    {
        if (kind == YautjaButcherKind.Xeno)
        {
            Spawn(XenoRemainsPrototype, coords);
            TryHarvestTrophy(hunter, target.Owner, YautjaTrophyKind.XenoSkull, out _);
            TryHarvestTrophy(hunter, target.Owner, YautjaTrophyKind.XenoPelt, out _);
            return;
        }

        Spawn(HumanHidePrototype, coords);
        Spawn(HumanSpinePrototype, coords);
        Spawn(HumanRemainsPrototype, coords);
        TryHarvestTrophy(hunter, target.Owner, YautjaTrophyKind.HumanSkull, out _);
        TryHarvestTrophy(hunter, target.Owner, YautjaTrophyKind.HumanRibcage, out _);
    }

    private void SpawnButcherOutput(EntProtoId prototype, EntityCoordinates coordinates, int amount)
    {
        for (var i = 0; i < amount; i++)
            Spawn(prototype, coordinates);
    }

    private bool TryValidateHarvestTrophy(
        EntityUid hunter,
        EntityUid target,
        YautjaTrophyKind kind,
        out EntProtoId prototype,
        out YautjaTrophySourceComponent source,
        bool popup)
    {
        prototype = default;
        source = default!;

        if (Deleted(hunter) ||
            Deleted(target) ||
            hunter == target ||
            !HasComp<YautjaComponent>(hunter))
        {
            return false;
        }

        if (!TryComp<MobStateComponent>(target, out var mobState) ||
            !_mobState.IsDead(target, mobState))
        {
            if (popup)
                _popup.PopupEntity(Loc.GetString("cmu-yautja-trophy-target-alive"), hunter, hunter, PopupType.SmallCaution);
            return false;
        }

        source = EnsureComp<YautjaTrophySourceComponent>(target);
        if (IsTaken(source, kind))
        {
            if (popup)
                _popup.PopupEntity(Loc.GetString("cmu-yautja-trophy-already-taken"), hunter, hunter, PopupType.SmallCaution);
            return false;
        }

        if (!CanHarvest(hunter, target, kind))
        {
            if (popup)
                _popup.PopupEntity(Loc.GetString("cmu-yautja-trophy-invalid"), hunter, hunter, PopupType.SmallCaution);
            return false;
        }

        if (!TryGetTrophyPrototype(kind, out prototype))
            return false;

        return true;
    }

    private void TryCompletePreyClaim(EntityUid hunter, EntityUid target, YautjaTrophyKind kind)
    {
        if (!_marks.IsMarkedBy(target, YautjaMarkKind.Prey, hunter) ||
            !_marks.TryClearMark(target, YautjaMarkKind.Prey, hunter))
        {
            return;
        }

        _audio.PlayPvs(new SoundCollectionSpecifier("CMUYautjaRoars"), hunter);
        var query = EntityQueryEnumerator<YautjaComponent>();
        while (query.MoveNext(out var yautja, out _))
        {
            _popup.PopupEntity(
                Loc.GetString("cmu-yautja-prey-claim-complete", ("hunter", HunterDisplayName(hunter)), ("target", target), ("kind", GetVerbText(kind))),
                yautja,
                yautja,
                PopupType.LargeCaution);
        }

        _adminLog.Add(LogType.Action, LogImpact.High,
            $"{ToPrettyString(hunter):hunter} completed a Yautja prey claim on {ToPrettyString(target):target} with trophy {kind}");
    }

    private void SeverTrophyPart(EntityUid target, YautjaTrophyKind kind)
    {
        if (!TryGetPartForTrophy(kind, out var type, out var symmetry))
            return;

        if (!_medicalIndex.TryGetBodyPart(target, new CMUMedicalBodyPartKey(type, symmetry), out var part))
            return;

        var ev = new BodyPartSeveredEvent(target, part, type);
        RaiseLocalEvent(part, ref ev);
    }

    private static bool TryGetPartForTrophy(YautjaTrophyKind kind, out BodyPartType type, out BodyPartSymmetry symmetry)
    {
        switch (kind)
        {
            case YautjaTrophyKind.HumanLeftArmBone:
                type = BodyPartType.Arm;
                symmetry = BodyPartSymmetry.Left;
                return true;
            case YautjaTrophyKind.HumanRightArmBone:
                type = BodyPartType.Arm;
                symmetry = BodyPartSymmetry.Right;
                return true;
            case YautjaTrophyKind.HumanLeftHandBone:
                type = BodyPartType.Hand;
                symmetry = BodyPartSymmetry.Left;
                return true;
            case YautjaTrophyKind.HumanRightHandBone:
                type = BodyPartType.Hand;
                symmetry = BodyPartSymmetry.Right;
                return true;
            case YautjaTrophyKind.HumanLeftLegBone:
                type = BodyPartType.Leg;
                symmetry = BodyPartSymmetry.Left;
                return true;
            case YautjaTrophyKind.HumanRightLegBone:
                type = BodyPartType.Leg;
                symmetry = BodyPartSymmetry.Right;
                return true;
            case YautjaTrophyKind.HumanLeftFootBone:
                type = BodyPartType.Foot;
                symmetry = BodyPartSymmetry.Left;
                return true;
            case YautjaTrophyKind.HumanRightFootBone:
                type = BodyPartType.Foot;
                symmetry = BodyPartSymmetry.Right;
                return true;
            default:
                type = BodyPartType.Other;
                symmetry = BodyPartSymmetry.None;
                return false;
        }
    }

    private void OnTrophyInteractUsing(Entity<YautjaTrophyComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !HasComp<YautjaPolishingRagComponent>(args.Used))
            return;

        args.Handled = true;

        if (!HasComp<YautjaComponent>(args.User))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-polish-denied"), args.User, args.User, PopupType.SmallCaution);
            return;
        }

        if (ent.Comp.Polished)
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-polish-already"), args.User, args.User, PopupType.SmallCaution);
            return;
        }

        ent.Comp.Polished = true;
        Dirty(ent);

        var name = MetaData(ent).EntityName;
        if (!name.StartsWith("polished ", StringComparison.OrdinalIgnoreCase))
            _meta.SetEntityName(ent, $"polished {name}");

        var record = EnsureComp<YautjaTrophyRecordComponent>(args.User);
        record.PolishedTrophies++;
        AddScore(args.User, record, 1);

        _popup.PopupEntity(Loc.GetString("cmu-yautja-polish-finished", ("trophy", ent.Owner)), args.User, args.User);
        _adminLog.Add(LogType.Action, LogImpact.Low, $"{ToPrettyString(args.User):hunter} polished Yautja trophy {ToPrettyString(ent):trophy}");
    }

    public void RecordRitualDuelWin(EntityUid hunter, EntityUid target)
    {
        if (Deleted(hunter) || !HasComp<YautjaComponent>(hunter))
            return;

        var record = EnsureComp<YautjaTrophyRecordComponent>(hunter);
        record.RitualDuelWins++;
        AddScore(hunter, record, 5);
        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(hunter):hunter} gained Yautja ritual duel credit for defeating {ToPrettyString(target):target}");
    }

    private void RecordTrophy(EntityUid hunter, YautjaTrophyKind kind)
    {
        var record = EnsureComp<YautjaTrophyRecordComponent>(hunter);
        var score = kind switch
        {
            YautjaTrophyKind.HumanSkull => 2,
            YautjaTrophyKind.HumanLeftArmBone or
            YautjaTrophyKind.HumanRightArmBone or
            YautjaTrophyKind.HumanLeftHandBone or
            YautjaTrophyKind.HumanRightHandBone or
            YautjaTrophyKind.HumanLeftLegBone or
            YautjaTrophyKind.HumanRightLegBone or
            YautjaTrophyKind.HumanLeftFootBone or
            YautjaTrophyKind.HumanRightFootBone or
            YautjaTrophyKind.HumanRibcage => 1,
            YautjaTrophyKind.XenoSkull => 4,
            YautjaTrophyKind.XenoPelt => 3,
            _ => 0,
        };

        switch (kind)
        {
            case YautjaTrophyKind.HumanSkull:
                record.HumanSkulls++;
                break;
            case YautjaTrophyKind.HumanLeftArmBone:
            case YautjaTrophyKind.HumanRightArmBone:
            case YautjaTrophyKind.HumanLeftHandBone:
            case YautjaTrophyKind.HumanRightHandBone:
            case YautjaTrophyKind.HumanLeftLegBone:
            case YautjaTrophyKind.HumanRightLegBone:
            case YautjaTrophyKind.HumanLeftFootBone:
            case YautjaTrophyKind.HumanRightFootBone:
            case YautjaTrophyKind.HumanRibcage:
                record.HumanBones++;
                break;
            case YautjaTrophyKind.XenoSkull:
                record.XenoSkulls++;
                break;
            case YautjaTrophyKind.XenoPelt:
                record.XenoPelts++;
                break;
        }

        AddScore(hunter, record, score);
    }

    private void AddScore(EntityUid hunter, YautjaTrophyRecordComponent record, int score)
    {
        record.Score += score;
        var rank = GetRankName(record.Score);
        if (rank == record.RankName)
            return;

        record.RankName = rank;
        if (TryComp(hunter, out YautjaComponent? yautja))
        {
            yautja.RankName = rank;
            Dirty(hunter, yautja);
        }

        _popup.PopupEntity(Loc.GetString("cmu-yautja-rank-advanced", ("rank", Loc.GetString(rank))), hunter, hunter);
        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(hunter):hunter} advanced to Yautja rank {rank} with trophy score {record.Score}");
    }

    private bool TryStoreTrophy(EntityUid hunter, EntityUid trophy)
    {
        var slots = _inventory.GetSlotEnumerator(hunter, SlotFlags.BELT | SlotFlags.BACK);
        while (slots.MoveNext(out var slot))
        {
            if (slot.ContainedEntity is not { } contained ||
                !TryComp<YautjaTrophyDisplayComponent>(contained, out _) ||
                !TryComp(contained, out StorageComponent? storage))
            {
                continue;
            }

            if (_containers.Insert(trophy, storage.Container, force: true))
                return true;
        }

        return false;
    }

    private void OnRecordExamined(Entity<YautjaTrophyRecordComponent> ent, ref ExaminedEvent args)
    {
        if (args.Examiner != ent.Owner && !HasComp<YautjaComponent>(args.Examiner))
            return;

        args.PushMarkup(Loc.GetString("cmu-yautja-trophy-record-examine",
            ("rank", Loc.GetString(ent.Comp.RankName)),
            ("score", ent.Comp.Score),
            ("human", ent.Comp.HumanSkulls),
            ("bones", ent.Comp.HumanBones),
            ("xenoSkull", ent.Comp.XenoSkulls),
            ("xenoPelt", ent.Comp.XenoPelts),
            ("polished", ent.Comp.PolishedTrophies),
            ("duels", ent.Comp.RitualDuelWins)));
    }

    private void OnTrophyExamined(Entity<YautjaTrophyComponent> ent, ref ExaminedEvent args)
    {
        if (!HasComp<YautjaComponent>(args.Examiner))
            return;

        var source = string.IsNullOrWhiteSpace(ent.Comp.SourceName)
            ? Loc.GetString("cmu-yautja-trophy-source-unknown")
            : ent.Comp.SourceName;

        args.PushMarkup(Loc.GetString("cmu-yautja-trophy-examine",
            ("source", source),
            ("polished", Loc.GetString(ent.Comp.Polished
                ? "cmu-yautja-trophy-polished-yes"
                : "cmu-yautja-trophy-polished-no"))));
    }

    private void OnDisplayExamined(Entity<YautjaTrophyDisplayComponent> ent, ref ExaminedEvent args)
    {
        if (!TryComp(ent, out StorageComponent? storage))
            return;

        var human = 0;
        var bones = 0;
        var xenoSkull = 0;
        var xenoPelt = 0;
        var polished = 0;
        foreach (var contained in storage.Container.ContainedEntities)
        {
            if (!TryComp(contained, out YautjaTrophyComponent? trophy))
                continue;

            if (trophy.Polished)
                polished++;

            switch (trophy.Kind)
            {
                case YautjaTrophyKind.HumanSkull:
                    human++;
                    break;
                case YautjaTrophyKind.HumanLeftArmBone:
                case YautjaTrophyKind.HumanRightArmBone:
                case YautjaTrophyKind.HumanLeftHandBone:
                case YautjaTrophyKind.HumanRightHandBone:
                case YautjaTrophyKind.HumanLeftLegBone:
                case YautjaTrophyKind.HumanRightLegBone:
                case YautjaTrophyKind.HumanLeftFootBone:
                case YautjaTrophyKind.HumanRightFootBone:
                case YautjaTrophyKind.HumanRibcage:
                    bones++;
                    break;
                case YautjaTrophyKind.XenoSkull:
                    xenoSkull++;
                    break;
                case YautjaTrophyKind.XenoPelt:
                    xenoPelt++;
                    break;
            }
        }

        args.PushMarkup(Loc.GetString("cmu-yautja-trophy-display-examine",
            ("human", human),
            ("bones", bones),
            ("xenoSkull", xenoSkull),
            ("xenoPelt", xenoPelt),
            ("polished", polished),
            ("total", human + bones + xenoSkull + xenoPelt)));
    }

    private bool CanHarvest(EntityUid hunter, EntityUid target, YautjaTrophyKind kind)
    {
        if (Deleted(hunter) || Deleted(target) || hunter == target || HasComp<YautjaComponent>(target))
            return false;

        if (TryComp<YautjaTrophySourceComponent>(target, out var source) && IsTaken(source, kind))
            return false;

        return kind switch
        {
            YautjaTrophyKind.HumanSkull or
            YautjaTrophyKind.HumanLeftArmBone or
            YautjaTrophyKind.HumanRightArmBone or
            YautjaTrophyKind.HumanLeftHandBone or
            YautjaTrophyKind.HumanRightHandBone or
            YautjaTrophyKind.HumanLeftLegBone or
            YautjaTrophyKind.HumanRightLegBone or
            YautjaTrophyKind.HumanLeftFootBone or
            YautjaTrophyKind.HumanRightFootBone or
            YautjaTrophyKind.HumanRibcage => IsHumanTrophyTarget(target),
            YautjaTrophyKind.XenoSkull or YautjaTrophyKind.XenoPelt => IsXenoTrophyTarget(target),
            _ => false,
        };
    }

    private bool CanButcher(EntityUid hunter, EntityUid target, out YautjaButcherKind kind)
    {
        kind = default;
        if (Deleted(hunter) ||
            Deleted(target) ||
            hunter == target ||
            !HasComp<YautjaComponent>(hunter) ||
            HasComp<YautjaComponent>(target) ||
            !TryComp<MobStateComponent>(target, out var mobState) ||
            !_mobState.IsDead(target, mobState))
        {
            return false;
        }

        if (IsXenoTrophyTarget(target))
        {
            kind = YautjaButcherKind.Xeno;
            return true;
        }

        if (IsHumanTrophyTarget(target))
        {
            kind = YautjaButcherKind.Human;
            return true;
        }

        return false;
    }

    private static TimeSpan GetButcherDelay(int stage)
    {
        return stage switch
        {
            1 => TimeSpan.FromSeconds(7),
            2 => TimeSpan.FromSeconds(6.5),
            3 => TimeSpan.FromSeconds(7),
            _ => TimeSpan.FromSeconds(9),
        };
    }

    private bool IsHumanTrophyTarget(EntityUid target)
    {
        return HasComp<HumanoidAppearanceComponent>(target) &&
               !HasComp<XenoComponent>(target) &&
               !HasComp<YautjaComponent>(target);
    }

    private bool IsXenoTrophyTarget(EntityUid target)
    {
        return HasComp<XenoComponent>(target);
    }

    private static bool TryGetTrophyPrototype(YautjaTrophyKind kind, out EntProtoId prototype)
    {
        switch (kind)
        {
            case YautjaTrophyKind.HumanSkull:
                prototype = HumanSkullPrototype;
                return true;
            case YautjaTrophyKind.HumanLeftArmBone:
                prototype = HumanLeftArmBonePrototype;
                return true;
            case YautjaTrophyKind.HumanRightArmBone:
                prototype = HumanRightArmBonePrototype;
                return true;
            case YautjaTrophyKind.HumanLeftHandBone:
                prototype = HumanLeftHandBonePrototype;
                return true;
            case YautjaTrophyKind.HumanRightHandBone:
                prototype = HumanRightHandBonePrototype;
                return true;
            case YautjaTrophyKind.HumanLeftLegBone:
                prototype = HumanLeftLegBonePrototype;
                return true;
            case YautjaTrophyKind.HumanRightLegBone:
                prototype = HumanRightLegBonePrototype;
                return true;
            case YautjaTrophyKind.HumanLeftFootBone:
                prototype = HumanLeftFootBonePrototype;
                return true;
            case YautjaTrophyKind.HumanRightFootBone:
                prototype = HumanRightFootBonePrototype;
                return true;
            case YautjaTrophyKind.HumanRibcage:
                prototype = HumanRibcagePrototype;
                return true;
            case YautjaTrophyKind.XenoSkull:
                prototype = XenoSkullPrototype;
                return true;
            case YautjaTrophyKind.XenoPelt:
                prototype = XenoPeltPrototype;
                return true;
            default:
                prototype = default;
                return false;
        }
    }

    private string GetVerbText(YautjaTrophyKind kind)
    {
        return Loc.GetString(kind switch
        {
            YautjaTrophyKind.HumanSkull => "cmu-yautja-trophy-verb-human-skull",
            YautjaTrophyKind.HumanLeftArmBone => "cmu-yautja-trophy-verb-human-left-arm",
            YautjaTrophyKind.HumanRightArmBone => "cmu-yautja-trophy-verb-human-right-arm",
            YautjaTrophyKind.HumanLeftHandBone => "cmu-yautja-trophy-verb-human-left-hand",
            YautjaTrophyKind.HumanRightHandBone => "cmu-yautja-trophy-verb-human-right-hand",
            YautjaTrophyKind.HumanLeftLegBone => "cmu-yautja-trophy-verb-human-left-leg",
            YautjaTrophyKind.HumanRightLegBone => "cmu-yautja-trophy-verb-human-right-leg",
            YautjaTrophyKind.HumanLeftFootBone => "cmu-yautja-trophy-verb-human-left-foot",
            YautjaTrophyKind.HumanRightFootBone => "cmu-yautja-trophy-verb-human-right-foot",
            YautjaTrophyKind.HumanRibcage => "cmu-yautja-trophy-verb-human-ribcage",
            YautjaTrophyKind.XenoSkull => "cmu-yautja-trophy-verb-xeno-skull",
            YautjaTrophyKind.XenoPelt => "cmu-yautja-trophy-verb-xeno-pelt",
            _ => "cmu-yautja-trophy-verb-generic",
        });
    }

    private string HunterDisplayName(EntityUid hunter)
    {
        return HasComp<YautjaComponent>(hunter)
            ? Loc.GetString("cmu-yautja-identity-unknown")
            : Name(hunter);
    }

    private static bool IsTaken(YautjaTrophySourceComponent source, YautjaTrophyKind kind)
    {
        return source.TakenTrophies.Contains(kind);
    }

    private static void SetTaken(YautjaTrophySourceComponent source, YautjaTrophyKind kind)
    {
        source.TakenTrophies.Add(kind);
    }

    private static LocId GetRankName(int score)
    {
        if (score >= 25)
            return "cmu-yautja-rank-elder";

        if (score >= 12)
            return "cmu-yautja-rank-elite";

        if (score >= 5)
            return "cmu-yautja-rank-blooded";

        return "cmu-yautja-rank-hunter";
    }

    private string GetSourceName(EntityUid target, YautjaTrophyKind kind)
    {
        if (kind is YautjaTrophyKind.XenoSkull or YautjaTrophyKind.XenoPelt)
            return GetXenoCasteName(target);

        return MetaData(target).EntityName;
    }

    private void ApplyTrophyName(EntityUid trophy, EntityUid target, YautjaTrophyKind kind, string sourceName)
    {
        if (kind == YautjaTrophyKind.XenoSkull)
        {
            _meta.SetEntityName(trophy, Loc.GetString("cmu-yautja-xeno-skull-name", ("caste", sourceName)));
            _meta.SetEntityDescription(trophy, Loc.GetString("cmu-yautja-xeno-skull-desc", ("caste", sourceName)));
        }
        else if (kind == YautjaTrophyKind.XenoPelt)
        {
            _meta.SetEntityName(trophy, Loc.GetString("cmu-yautja-xeno-pelt-name", ("caste", sourceName)));
            _meta.SetEntityDescription(trophy, Loc.GetString("cmu-yautja-xeno-pelt-desc", ("caste", sourceName)));
        }
    }

    private string GetXenoCasteName(EntityUid target)
    {
        if (TryComp(target, out XenoComponent? xeno))
            return FormatXenoRole(xeno.Role.Id);

        var meta = MetaData(target);
        return meta.EntityPrototype?.Name ?? meta.EntityName;
    }

    private static string FormatXenoRole(string role)
    {
        role = role.Replace("CMXeno", string.Empty)
            .Replace("RMCXeno", string.Empty)
            .Replace("Xeno", string.Empty);

        if (role == "LesserDrone")
            role = "Drone";

        var result = string.Empty;
        for (var i = 0; i < role.Length; i++)
        {
            var c = role[i];
            if (i > 0 && char.IsUpper(c) && !char.IsWhiteSpace(role[i - 1]))
                result += " ";
            result += c;
        }

        return string.IsNullOrWhiteSpace(result) ? "Xeno" : result;
    }
}
