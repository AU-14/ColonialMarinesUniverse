using Content.Server.NPC.Systems;
using Content.Shared._CMU14.Xenomorphs.Pathogen.MycotoxinInject;
using Content.Shared._CMU14.Xenomorphs.Pathogen.Walker;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared.Humanoid;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using Content.Shared.Popups;
using Content.Shared.Rejuvenate;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Inventory;
using Content.Shared.Jittering;
using Content.Shared._CMU14.Medical.Injuries.Wounds;
using Content.Shared.Body.Systems;
using Content.Shared.StatusEffectNew;
using Content.Server._RMC14.Language.Systems;
using Content.Server.Radio.Components;
using Content.Server.Ghost.Roles.Components;
using Robust.Shared.Player;
using Content.Server.Mind;
using Content.Shared._RMC14.TacticalMap;
using Content.Shared.Examine;
using Content.Shared.IdentityManagement;
using Content.Shared._RMC14.Synth;
using Content.Shared.Mind;
using Content.Shared.Whitelist;
using Content.Shared._RMC14.Pulling;
using Robust.Shared.GameObjects;
using Content.Shared._CMU14.Medical.Anatomy.BodyParts.Events;
using Content.Shared.Body.Part;
using Content.Shared._CMU14.Medical.Anatomy.Organs.Eyes;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;

namespace Content.Server._CMU14.Xenomorphs.Pathogen.Walker;

public sealed partial class CMUPathogenWalkerSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly NpcFactionSystem _faction = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedXenoHiveSystem _hive = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly IPrototypeManager _protoMgr = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedJitteringSystem _jitter = default!;
    [Dependency] private readonly SharedStatusEffectsSystem _status = default!;
    [Dependency] private readonly LanguageSystem _language = default!;
    [Dependency] private readonly ISharedPlayerManager _player = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly BlindableSystem _blindable = default!;

    private static readonly ProtoId<NpcFactionPrototype> WalkerFaction = "CMU14PathogenWalker";
    private static readonly ProtoId<DamageGroupPrototype> BruteGroup = "Brute";
    private static readonly ProtoId<DamageGroupPrototype> BurnGroup = "Burn";
    private static readonly string[] PainStatusEffects =
    {
        "StatusEffectCMUPainMild",
        "StatusEffectCMUPainModerate",
        "StatusEffectCMUPainSevere",
        "StatusEffectCMUPainShock",
    };

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUMycotoxinInjectDoReanimateEvent>(OnReanimate);
        SubscribeLocalEvent<CMUPathogenWalkerComponent, MobStateChangedEvent>(OnWalkerDeath);
        SubscribeLocalEvent<CMUPathogenWalkerComponent, ExaminedEvent>(OnWalkerExamined);
        SubscribeNetworkEvent<CMUPathogenWalkerAcceptNetEvent>(OnAcceptNet);
        SubscribeNetworkEvent<CMUPathogenWalkerDeclineNetEvent>(OnDeclineNet);
        SubscribeLocalEvent<CMUPathogenWalkerComponent, BodyPartSeveredEvent>(OnWalkerSevered);
    }

    private void OnReanimate(CMUMycotoxinInjectDoReanimateEvent ev)
    {
        var target = ev.Target;
        var injector = ev.Injector;

        if (HasComp<CMUPathogenWalkerComponent>(target))
            return;

        if (HasComp<SynthComponent>(target))
            return;

        var walker = EnsureComp<CMUPathogenWalkerComponent>(target);

        var hives = EntityQueryEnumerator<HiveComponent, MetaDataComponent>();
        while (hives.MoveNext(out var hiveUid, out _, out var meta))
        {
            if (meta.EntityPrototype?.ID != "CMUPathogenHive")
                continue;

            _hive.SetHive(target, hiveUid);
            walker.Hive = hiveUid;
            break;
        }

        RemComp<CMUOrganBlindnessComponent>(target);
        if (TryComp<CMUEyeDamageContributionComponent>(target, out var eyeTracker))
        {
            if (TryComp<BlindableComponent>(target, out var blindable))
                _blindable.AdjustEyeDamage((target, blindable), -eyeTracker.Applied);
            eyeTracker.Applied = 0;
        }

        _faction.AddFaction(target, WalkerFaction);
        _language.SetExclusiveLanguage(target, "Pathogen");

        EnsureComp<IntrinsicRadioReceiverComponent>(target);
        var transmitter = EnsureComp<IntrinsicRadioTransmitterComponent>(target);
        transmitter.Channels = new HashSet<string>() { "Hivemind" };
        var radio = EnsureComp<ActiveRadioComponent>(target);
        radio.Channels = new HashSet<string>() { "Hivemind" };
        var tacIcon = EnsureComp<TacticalMapIconComponent>(target);

        EnsureComp<PullWhitelistComponent>(target);
        var whitelistEv = new SetPullWhitelistEvent(new EntityWhitelist
        {
            Components = new[] { "Xeno", "Infectable", "Synth", "Yautja" }
        });
        RaiseLocalEvent(target, ref whitelistEv);

        EquipMarker(target, walker);

        // If the victim has a connected player, show the offer popup.
        // Otherwise skip straight to ghost role.
        if (TryGetWalkerSession(target, out var session))
        {
            walker.OfferExpiresAt = _timing.CurTime + walker.OfferTimeout;
            walker.OfferResolved = false;
            Dirty(target, walker);

            RaiseNetworkEvent(new CMUPathogenWalkerOfferEvent(
                GetNetEntity(target),
                walker.OfferTimeout.TotalSeconds), session);
        }
        else
        {
            MakeGhostRole(target, walker);
        }

        Dirty(target, walker);
    }

    private void OnWalkerSevered(Entity<CMUPathogenWalkerComponent> walker, ref BodyPartSeveredEvent args)
    {
        if (args.Type != BodyPartType.Head)
            return;

        // Cancel any pending revive and mark as exhausted
        walker.Comp.ReviveAt = null;
        walker.Comp.RevivesUsed = walker.Comp.MaxRevives;
        Dirty(walker);

        _popup.PopupEntity(Loc.GetString("cmu14-walker-permanent-death"), walker, PopupType.Medium);
    }

    private void OnAcceptNet(CMUPathogenWalkerAcceptNetEvent ev, EntitySessionEventArgs args)
    {
        var uid = GetEntity(ev.Target);
        if (!TryComp<CMUPathogenWalkerComponent>(uid, out var walker) || walker.OfferResolved)
            return;

        walker.OfferResolved = true;
        walker.OfferExpiresAt = null;
        Dirty(uid, walker);
        ActivateWalker(uid, walker);
    }

    private void OnDeclineNet(CMUPathogenWalkerDeclineNetEvent ev, EntitySessionEventArgs args)
    {
        var uid = GetEntity(ev.Target);
        if (!TryComp<CMUPathogenWalkerComponent>(uid, out var walker) || walker.OfferResolved)
            return;

        walker.OfferResolved = true;
        walker.OfferExpiresAt = null;
        Dirty(uid, walker);
        MakeGhostRole(uid, walker);
    }

    private void ActivateWalker(EntityUid uid, CMUPathogenWalkerComponent walker)
    {
        Revive(uid, walker);
    }

    private void MakeGhostRole(EntityUid uid, CMUPathogenWalkerComponent walker)
    {
        // Eject current mind so the ghost role system can hand out a fresh one.
        if (_mind.TryGetMind(uid, out var mindId, out _))
            _mind.TransferTo(mindId, null);

        var ghostRole = EnsureComp<GhostRoleComponent>(uid);
        ghostRole.RoleName = Loc.GetString("cmu14-walker-ghost-role-name");
        ghostRole.RoleDescription = Loc.GetString("cmu14-walker-ghost-role-desc");

        EnsureComp<GhostTakeoverAvailableComponent>(uid);

        Revive(uid, walker); // body is still alive/animated even before someone takes it
    }

    private void OnWalkerExamined(Entity<CMUPathogenWalkerComponent> walker, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var locUser = ("user", Identity.Entity(walker, EntityManager));

        args.PushMarkup($"[color=red][bold]{Loc.GetString("cmu14-walker-examine-fungal-growth", locUser)}[/bold][/color]");
    }

    private void EquipMarker(EntityUid target, CMUPathogenWalkerComponent walker)
    {
        if (_inventory.TryGetSlotEntity(target, "head", out var existing) && existing != null)
            _inventory.TryUnequip(target, "head", force: true);

        var marker = Spawn(walker.MarkerPrototype, Transform(target).Coordinates);
        if (_inventory.TryEquip(target, marker, "head", force: true))
            walker.MarkerItem = marker;
        else
            QueueDel(marker);
    }

    private void OnWalkerDeath(Entity<CMUPathogenWalkerComponent> walker, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        if (walker.Comp.ReviveAt != null)
            return;

        if (walker.Comp.RevivesUsed >= walker.Comp.MaxRevives)
        {
            _popup.PopupEntity(Loc.GetString("cmu14-walker-permanent-death"), walker, PopupType.Medium);
            return;
        }

        walker.Comp.RevivesUsed++;
        walker.Comp.ReviveAt = _timing.CurTime + walker.Comp.ReviveDelay;
        walker.Comp.PreReviveJitterPlayed = false;
        Dirty(walker);

        _popup.PopupEntity(Loc.GetString("cmu14-walker-death-reviving"), walker, PopupType.Medium);
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var time = _timing.CurTime;
        var jitterWarning = TimeSpan.FromSeconds(5);

        var query = EntityQueryEnumerator<CMUPathogenWalkerComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out var walker, out var mobState))
        {
            // Offer timeout — independent of revive logic
            if (!walker.OfferResolved &&
                walker.OfferExpiresAt != null &&
                time >= walker.OfferExpiresAt)
            {
                walker.OfferResolved = true;
                walker.OfferExpiresAt = null;
                Dirty(uid, walker);

                MakeGhostRole(uid, walker);
            }

            if (mobState.CurrentState == MobState.Alive && time >= walker.NextHeal)
            {
                walker.NextHeal = time + walker.HealInterval;
                HealTick(uid, walker);
                Dirty(uid, walker);
            }

            if (walker.ReviveAt == null)
                continue;

            if (!walker.PreReviveJitterPlayed &&
                mobState.CurrentState == MobState.Dead &&
                time >= walker.ReviveAt - jitterWarning)
            {
                walker.PreReviveJitterPlayed = true;
                Dirty(uid, walker);
                _jitter.DoJitter(uid, jitterWarning, true, 14f, 5f, true);

                if (TryGetWalkerSession(uid, out var warnSession))
                {
                    RaiseNetworkEvent(new CMUPathogenWalkerReviveWarningEvent(
                        GetNetEntity(uid), jitterWarning.TotalSeconds), warnSession);
                }
            }

            if (time < walker.ReviveAt)
                continue;

            walker.ReviveAt = null;
            Dirty(uid, walker);

            if (mobState.CurrentState != MobState.Dead)
                continue;

            if (TryGetWalkerSession(uid, out _))
                Revive(uid, walker);
            else
                MakeGhostRole(uid, walker); // nobody home — hand it off as a ghost role instead
        }
    }

    private void HealTick(EntityUid uid, CMUPathogenWalkerComponent walker)
    {
        if (TryComp<DamageableComponent>(uid, out var damageable))
        {
            if (_protoMgr.TryIndex(BruteGroup, out var brute))
                _damageable.TryChangeDamage(uid, new DamageSpecifier(brute, -walker.HealPerTick), ignoreResistances: true, damageable: damageable);

            if (_protoMgr.TryIndex(BurnGroup, out var burn))
                _damageable.TryChangeDamage(uid, new DamageSpecifier(burn, -walker.HealPerTick), ignoreResistances: true, damageable: damageable);
        }

        StripPain(uid);
    }

    private void StripPain(EntityUid uid)
    {
        foreach (var effect in PainStatusEffects)
            _status.TryRemoveStatusEffect(uid, effect);
    }

    private void Revive(EntityUid uid, CMUPathogenWalkerComponent walker)
    {
        // Full CMU heal: parts, organs, bones, wounds, status effects - not just DamageableComponent.
        RaiseLocalEvent(uid, new RejuvenateEvent());

        if (walker.Hive is { } hiveUid && hiveUid.IsValid())
            _hive.SetHive(uid, hiveUid);

        _mobState.ChangeMobState(uid, MobState.Alive, null);

        if (TryComp<HumanoidAppearanceComponent>(uid, out var humanoid))
        {
            humanoid.SkinColor = walker.WalkerSkinColor;
            humanoid.EyeColor = walker.WalkerEyeColor;
            Dirty(uid, humanoid);
        }

        _popup.PopupEntity(Loc.GetString("cmu14-walker-rise"), uid, PopupType.LargeCaution);
    }

    private bool TryGetWalkerSession(EntityUid uid, out ICommonSession session)
    {
        session = default!;
        if (_mind.TryGetMind(uid, out _, out var mind) &&
            mind.UserId != null &&
            _player.TryGetSessionById(mind.UserId.Value, out var found))
        {
            session = found;
            return true;
        }

        return false;
    }
}