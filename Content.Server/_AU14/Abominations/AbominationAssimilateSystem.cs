using Content.Server.Polymorph.Systems;
using Content.Shared._AU14.Abominations;
using Content.Shared._RMC14.Synth;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared.DoAfter;
using Content.Shared.GameTicking;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Components;
using Content.Shared.Polymorph;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;

namespace Content.Server._AU14.Abominations;

public sealed partial class AbominationAssimilateSystem : EntitySystem
{
    /// <summary>Polymorph used when a humanoid victim turns.</summary>
    public static readonly ProtoId<PolymorphPrototype> HumanoidTurnPolymorph = "AbominationAssimilationToMimic";

    /// <summary>Polymorph used when an animal victim turns — they become a spider, not a mimic.</summary>
    public static readonly ProtoId<PolymorphPrototype> AnimalTurnPolymorph = "AbominationAssimilationToSpider";

    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private PolymorphSystem _polymorph = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _proto = default!;

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

        var pool = GatherCurrentPool();
        if (pool.Count == 0)
            return;

        ent.Comp.AssimilatedPool = pool;
        Dirty(ent);
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent _)
    {
        var query = EntityQueryEnumerator<AbominationMimicComponent>();
        while (query.MoveNext(out var uid, out var mimic))
        {
            if (mimic.AssimilatedPool.Count == 0)
                continue;
            mimic.AssimilatedPool.Clear();
            Dirty(uid, mimic);
        }
    }

    private void OnAssimilateAction(Entity<AbominationAssimilateComponent> mimic, ref AbominationAssimilateActionEvent args)
    {
        if (args.Handled)
            return;

        if (!CanAssimilate(mimic.Owner, args.Target, out var reason))
        {
            _popup.PopupClient(reason, mimic, mimic);
            return;
        }

        args.Handled = true;

        var ev = new AbominationAssimilateDoAfterEvent();
        var doAfter = new DoAfterArgs(EntityManager, mimic.Owner, mimic.Comp.DoAfter, ev, mimic.Owner, target: args.Target)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false,
            RequireCanInteract = true,
        };

        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnAssimilateDoAfter(Entity<AbominationAssimilateComponent> mimic, ref AbominationAssimilateDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        if (args.Args.Target is not { } target)
            return;

        if (!CanAssimilate(mimic.Owner, target, out var reason))
        {
            _popup.PopupClient(reason, mimic, mimic);
            return;
        }

        args.Handled = true;

        var isHumanoid = HasComp<HumanoidAppearanceComponent>(target);
        var profile = BuildProfile(target);
        AddProfileToAllMimics(profile);

        _popup.PopupEntity(Loc.GetString("abomination-assimilate-complete", ("target", Name(target))), target, mimic);

        // Humanoid victims become mimics; animal victims become spiders.
        var polymorphId = isHumanoid ? HumanoidTurnPolymorph : AnimalTurnPolymorph;
        var newAbomination = _polymorph.PolymorphEntity(target, polymorphId);
        if (newAbomination is { } newUid && isHumanoid)
        {
            // Fresh mimics inherit the full current pool so they can
            // immediately impersonate any prior victim, including themselves.
            var newMimicComp = EnsureComp<AbominationMimicComponent>(newUid);
            newMimicComp.AssimilatedPool = new List<AbominationAssimilationProfile>(GatherCurrentPool());
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

        // Synths have no flesh to absorb. Same flavour as xenos refusing to nest them.
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
        var isHumanoid = HasComp<HumanoidAppearanceComponent>(target);

        // Animals key off the entity prototype id so all rats group as one
        // "rat" entry; humanoids stay per-victim by display name.
        var protoId = MetaData(target).EntityPrototype?.ID;
        var displayName = isHumanoid
            ? Name(target)
            : (protoId is not null && _proto.TryIndex<EntityPrototype>(protoId, out var proto)
                ? proto.Name
                : Name(target));

        var profile = new AbominationAssimilationProfile
        {
            Name = displayName,
            SourceEntity = GetNetEntity(target),
            SourceProtoId = isHumanoid ? null : protoId,
            IsTribal = HasComp<Content.Shared.AU14.TribalComponent>(target),
        };

        if (TryComp<NpcFactionMemberComponent>(target, out var npcFaction))
        {
            foreach (var faction in npcFaction.Factions)
                profile.Factions.Add(faction);
        }

        if (TryComp<UserIFFComponent>(target, out var iff))
        {
            foreach (var faction in iff.Factions)
                profile.IffFactions.Add(faction);
        }

        if (TryComp<HumanoidAppearanceComponent>(target, out var humanoid))
            profile.Appearance = SnapshotAppearance(humanoid);

        return profile;
    }

    private static AbominationAppearanceSnapshot SnapshotAppearance(HumanoidAppearanceComponent humanoid)
    {
        return new AbominationAppearanceSnapshot
        {
            Species = humanoid.Species,
            SkinColor = humanoid.SkinColor,
            EyeColor = humanoid.EyeColor,
            Sex = humanoid.Sex,
            Gender = humanoid.Gender,
            Age = humanoid.Age,
            MarkingSet = new MarkingSet(humanoid.MarkingSet),
            CustomBaseLayers = new Dictionary<HumanoidVisualLayers, CustomBaseLayerInfo>(humanoid.CustomBaseLayers),
        };
    }

    /// <summary>
    /// Push a profile into every living mimic's pool. The library is
    /// teamwide on purpose — once one mimic sees a face, the whole flesh-pod
    /// can wear it. Pool data lives on each mimic's component so it dies with
    /// the entity (and the round); there is no static cache.
    /// </summary>
    public void AddProfileToAllMimics(AbominationAssimilationProfile profile)
    {
        var query = EntityQueryEnumerator<AbominationMimicComponent>();
        while (query.MoveNext(out var uid, out var mimic))
        {
            // Animal profiles dedupe by SourceProtoId — only the first rat ever
            // assimilated goes in the pool, every subsequent rat is a no-op.
            if (profile.SourceProtoId is not null &&
                mimic.AssimilatedPool.Exists(p => p.SourceProtoId == profile.SourceProtoId))
            {
                continue;
            }

            mimic.AssimilatedPool.Add(profile);
            Dirty(uid, mimic);
        }
    }

    private List<AbominationAssimilationProfile> GatherCurrentPool()
    {
        var query = EntityQueryEnumerator<AbominationMimicComponent>();
        while (query.MoveNext(out _, out var mimic))
        {
            if (mimic.AssimilatedPool.Count > 0)
                return new List<AbominationAssimilationProfile>(mimic.AssimilatedPool);
        }

        return new List<AbominationAssimilationProfile>();
    }
}
