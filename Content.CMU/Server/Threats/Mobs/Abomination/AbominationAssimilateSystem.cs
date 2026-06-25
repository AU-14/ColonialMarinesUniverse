using Content.Server.Polymorph.Systems;
using Content.Shared._RMC14.Synth;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared.DoAfter;
using Content.Shared.GameTicking;
using Content.Shared.Humanoid;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Prototypes;
using Content.Shared.Polymorph;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;
using AbominationAppearanceSnapshot = Content.Shared._CMU14.Threats.Mobs.Abomination.AbominationAppearanceSnapshot;
using AbominationAssimilateActionEvent
    = Content.Shared._CMU14.Threats.Mobs.Abomination.AbominationAssimilateActionEvent;
using AbominationAssimilateComponent = Content.Shared._CMU14.Threats.Mobs.Abomination.AbominationAssimilateComponent;
using AbominationAssimilateDoAfterEvent
    = Content.Shared._CMU14.Threats.Mobs.Abomination.AbominationAssimilateDoAfterEvent;
using AbominationAssimilationProfile = Content.Shared._CMU14.Threats.Mobs.Abomination.AbominationAssimilationProfile;
using AbominationComponent = Content.Shared._CMU14.Threats.Mobs.Abomination.AbominationComponent;
using AbominationInfectableComponent = Content.Shared._CMU14.Threats.Mobs.Abomination.AbominationInfectableComponent;
using AbominationMimicComponent = Content.Shared._CMU14.Threats.Mobs.Abomination.AbominationMimicComponent;
using TribalComponent = Content.Shared._CMU14.Threats.Mobs.Tribal.TribalComponent;

namespace Content.Server._CMU14.Threats.Mobs.Abomination;

public sealed partial class AbominationAssimilateSystem : EntitySystem
{
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private PolymorphSystem _polymorph = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    /// <summary>Polymorph used when a humanoid victim turns.</summary>
    public static readonly ProtoId<PolymorphPrototype> HumanoidTurnPolymorph = "AbominationAssimilationToMimic";

    /// <summary>Polymorph used when an animal victim turns — they become a spider, not a mimic.</summary>
    public static readonly ProtoId<PolymorphPrototype> AnimalTurnPolymorph = "AbominationAssimilationToSpider";

    public override void Initialize()
    {
        SubscribeLocalEvent<AbominationAssimilateComponent, AbominationAssimilateActionEvent>(OnAssimilateAction);
        SubscribeLocalEvent<AbominationAssimilateComponent, AbominationAssimilateDoAfterEvent>(OnAssimilateDoAfter);

        // Any fresh AbominationMimicComponent — partyspawn, ghost takeover,
        // infection-death polymorph, admin spawn — inherits the current global
        // pool on map-init. Without this, only assimilation-spawned mimics
        // ended up with the library.
        SubscribeLocalEvent<AbominationMimicComponent, MapInitEvent>(OnMimicMapInit);

        // Defensive cleanup — entities are normally destroyed on restart, but
        // if any AbominationMimicComponent leaks across the round boundary
        // (e.g. an admin-restart that doesn't reload the map), this resets
        // the pool so last round's faces don't bleed in.
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
    }

    private void OnMimicMapInit(Entity<AbominationMimicComponent> ent, ref MapInitEvent args)
    {
        // Skip if this mimic was already seeded (e.g. by OnAssimilateDoAfter).
        if (ent.Comp.AssimilatedPool.Count > 0)
            return;

        List<AbominationAssimilationProfile> pool = GatherCurrentPool();

        if (pool.Count == 0)
            return;

        ent.Comp.AssimilatedPool = pool;
        Dirty(ent);
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent _)
    {
        EntityQueryEnumerator<AbominationMimicComponent>
            query = EntityQueryEnumerator<AbominationMimicComponent>();
        while (query.MoveNext(out EntityUid uid, out AbominationMimicComponent? mimic))
        {
            if (mimic.AssimilatedPool.Count == 0)
                continue;
            mimic.AssimilatedPool.Clear();
            Dirty(uid, mimic);
        }
    }

    private void OnAssimilateAction(Entity<AbominationAssimilateComponent> mimic,
        ref AbominationAssimilateActionEvent args)
    {
        if (args.Handled)
            return;

        if (!CanAssimilate(mimic.Owner, args.Target, out string reason))
        {
            _popup.PopupClient(reason, mimic, mimic);

            return;
        }

        args.Handled = true;

        var ev = new AbominationAssimilateDoAfterEvent();
        var doAfter = new DoAfterArgs(EntityManager, mimic.Owner, mimic.Comp.DoAfter, ev, mimic.Owner, args.Target)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false,
            RequireCanInteract = true
        };

        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnAssimilateDoAfter(Entity<AbominationAssimilateComponent> mimic,
        ref AbominationAssimilateDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        if (args.Args.Target is not { } target)
            return;

        if (!CanAssimilate(mimic.Owner, target, out string reason))
        {
            _popup.PopupClient(reason, mimic, mimic);

            return;
        }

        args.Handled = true;

        bool isHumanoid = HasComp<HumanoidAppearanceComponent>(target);
        AbominationAssimilationProfile profile = BuildProfile(target);
        AddProfileToAllMimics(profile);

        _popup.PopupEntity(Loc.GetString("abomination-assimilate-complete", ("target", Name(target))),
            target, mimic);

        // Humanoid victims become mimics; animal victims become spiders.
        ProtoId<PolymorphPrototype> polymorphId = isHumanoid
            ? HumanoidTurnPolymorph
            : AnimalTurnPolymorph;
        EntityUid? newAbomination = _polymorph.PolymorphEntity(target, polymorphId);
        if (newAbomination is { } newUid && isHumanoid)
        {
            // Fresh mimics inherit the full current pool so they can
            // immediately impersonate any prior victim, including themselves.
            var newMimicComp = EnsureComp<AbominationMimicComponent>(newUid);
            newMimicComp.AssimilatedPool = new(GatherCurrentPool());
            Dirty(newUid, newMimicComp);
        }
    }

    private bool CanAssimilate(EntityUid mimic, EntityUid target, out string reason)
    {
        reason = string.Empty;

        if (mimic == target)
        {
            reason = Loc.GetString("abomination-assimilate-self");

            return false;
        }

        // Humanoid OR a tagged-infectable animal — both are valid prey.
        if (!HasComp<HumanoidAppearanceComponent>(target) && !HasComp<AbominationInfectableComponent>(target))
        {
            reason = Loc.GetString("abomination-assimilate-not-humanoid");

            return false;
        }

        // Synths have no flesh to absorb. Same flavor as xenos refusing to nest them.
        if (HasComp<SynthComponent>(target))
        {
            reason = Loc.GetString("abomination-assimilate-synth");

            return false;
        }

        if (!_mobState.IsIncapacitated(target))
        {
            reason = Loc.GetString("abomination-assimilate-not-incapacitated");

            return false;
        }

        if (HasComp<AbominationComponent>(target))
        {
            reason = Loc.GetString("abomination-assimilate-not-humanoid");

            return false;
        }

        return true;
    }

    public AbominationAssimilationProfile BuildProfile(EntityUid target)
    {
        bool isHumanoid = HasComp<HumanoidAppearanceComponent>(target);

        // Animals key off the entity prototype id so all rats group as one
        // "rat" entry; humanoids stay per-victim by display name.
        string? protoId = MetaData(target).EntityPrototype?.ID;
        string displayName = isHumanoid
            ? Name(target)
            : protoId is not null && _proto.TryIndex(protoId, out EntityPrototype? proto)
                ? proto.Name
                : Name(target);

        var profile = new AbominationAssimilationProfile
        {
            Name = displayName,
            SourceEntity = GetNetEntity(target),
            SourceProtoId = isHumanoid ? null : protoId,
            IsTribal = HasComp<TribalComponent>(target)
        };

        if (TryComp(target, out NpcFactionMemberComponent? npcFaction))
        {
            foreach (ProtoId<NpcFactionPrototype> faction in npcFaction.Factions)
            {
                profile.Factions.Add(faction);
            }
        }

        if (TryComp(target, out UserIFFComponent? iff))
        {
            foreach (EntProtoId<IFFFactionComponent> faction in iff.Factions)
            {
                profile.IffFactions.Add(faction);
            }
        }

        if (TryComp(target, out HumanoidAppearanceComponent? humanoid))
            profile.Appearance = AbominationAssimilateSystem.SnapshotAppearance(humanoid);

        return profile;
    }

    private static AbominationAppearanceSnapshot SnapshotAppearance(HumanoidAppearanceComponent humanoid) => new()
    {
        Species = humanoid.Species,
        SkinColor = humanoid.SkinColor,
        EyeColor = humanoid.EyeColor,
        Sex = humanoid.Sex,
        Gender = humanoid.Gender,
        Age = humanoid.Age,
        MarkingSet = new(humanoid.MarkingSet),
        CustomBaseLayers = new(humanoid.CustomBaseLayers)
    };

    /// <summary>
    ///     Push a profile into every living mimic's pool. The library is
    ///     teamwide on purpose — once one mimic sees a face, the whole flesh-pod
    ///     can wear it. Pool data lives on each mimic's component so it dies with
    ///     the entity (and the round); there is no static cache.
    /// </summary>
    public void AddProfileToAllMimics(AbominationAssimilationProfile profile)
    {
        EntityQueryEnumerator<AbominationMimicComponent>
            query = EntityQueryEnumerator<AbominationMimicComponent>();
        while (query.MoveNext(out EntityUid uid, out AbominationMimicComponent? mimic))
        {
            // Animal profiles dedupe by SourceProtoId — only the first rat ever
            // assimilated goes in the pool, every subsequent rat is a no-op.
            if (profile.SourceProtoId is not null
                && mimic.AssimilatedPool.Exists(p => p.SourceProtoId == profile.SourceProtoId))
                continue;

            mimic.AssimilatedPool.Add(profile);
            Dirty(uid, mimic);
        }
    }

    private List<AbominationAssimilationProfile> GatherCurrentPool()
    {
        EntityQueryEnumerator<AbominationMimicComponent>
            query = EntityQueryEnumerator<AbominationMimicComponent>();
        while (query.MoveNext(out _, out AbominationMimicComponent? mimic))
        {
            if (mimic.AssimilatedPool.Count > 0)
                return [..mimic.AssimilatedPool];
        }

        return [];
    }
}
