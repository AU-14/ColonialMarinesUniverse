using Content.Shared.CCVar;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.CMU14.util;
using Content.Shared.FixedPoint;
using Content.Shared.Fluids.Components;
using Content.Shared.Tag;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Util;

/// <summary>
/// Owner of the loose-clutter filters shared by the nuke:* commands and this sweep.
/// The sweep auto-despawns only residue with no lore value, on long fuses: empty
/// detachable magazines and trash. Puddles and decals are deliberately not swept:
/// blood trails and battle scars are the round's story, and the nuke:* commands
/// stay the manual tool for those. Commands collect and delete immediately; the
/// sweep stamps candidates with <see cref="CMULooseSinceComponent"/> and despawns
/// them once they have stayed loose past the family cvar. Contained entities are
/// never collected; picking an entity up and dropping it again does not reset its age.
/// </summary>
public sealed partial class CMULooseSweepSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private TagSystem _tag = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly TimeSpan SweepInterval = TimeSpan.FromSeconds(5);

    // Shared safety filter with nuke:puddles: only small spills count as clutter
    // for the manual command. The sweep never touches puddles; blood is lore.
    private static readonly FixedPoint2 SmallPuddleThreshold = FixedPoint2.New(5);

    private static readonly ProtoId<TagPrototype> TrashTag = "Trash";

    // Shared curated lists with nuke:trash: extra tags/prototypes that count as
    // trash even without the Trash tag.
    private static readonly string[] AdditionalTrashPrototypes = { };
    private static readonly string[] AdditionalTrashTags = { };

    // Shared exclusion with nuke:trash: readable clutter the filters always spare.
    private static readonly string[] ExcludedTrashComponents = { "Paper" };

    private readonly List<EntityUid> _scratch = new();
    private TimeSpan _nextSweep;

    public override void Initialize()
        => _nextSweep = _timing.CurTime + SweepInterval;

    public void CollectLooseMagazines(List<EntityUid> results, HashSet<string>? ids = null, bool all = false)
    {
        var query = EntityQueryEnumerator<BallisticAmmoProviderComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out var provider, out var meta))
        {
            if (!provider.MayTransfer
                || _containers.IsEntityInContainer(uid)
                || !MatchesIds(meta, ids)
                || (!all && provider.Entities.Count + provider.UnspawnedCount > 0))
                continue;

            results.Add(uid);
        }
    }

    public void CollectLoosePuddles(List<EntityUid> results, HashSet<string>? ids = null, bool all = false)
    {
        var query = EntityQueryEnumerator<PuddleComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out var puddle, out var meta))
        {
            if (_containers.IsEntityInContainer(uid)
                || !MatchesIds(meta, ids)
                || (!all
                    && (!_solution.TryGetSolution(uid, puddle.SolutionName, out _, out var solution)
                        || solution.Volume > SmallPuddleThreshold)))
                continue;

            results.Add(uid);
        }
    }

    // Full-fidelity enumeration for the admin command: curated trash prototypes may
    // carry no TagComponent, so every entity's metadata is checked.
    public void CollectLooseTrash(List<EntityUid> results, HashSet<string>? ids = null)
    {
        var query = EntityQueryEnumerator<MetaDataComponent>();
        while (query.MoveNext(out var uid, out var meta))
        {
            if (!IsLooseTrash(uid, meta)
                || !MatchesIds(meta, ids))
                continue;

            results.Add(uid);
        }
    }

    public bool IsLooseTrash(EntityUid uid, MetaDataComponent meta)
    {
        if (_containers.IsEntityInContainer(uid))
            return false;

        if (meta.EntityPrototype is { } proto)
        {
            foreach (var excluded in ExcludedTrashComponents)
            {
                if (proto.Components.ContainsKey(excluded))
                    return false;
            }

            if (ContainsId(AdditionalTrashPrototypes, proto.ID))
                return true;
        }

        return TryComp<TagComponent>(uid, out var tags) && IsTrashTagged(tags);
    }

    public bool IsMagazinePrototype(EntityPrototype proto)
        => proto.Components.TryGetValue("BallisticAmmoProvider", out var reg)
            && reg.Component is BallisticAmmoProviderComponent { MayTransfer: true };

    public bool IsPuddlePrototype(EntityPrototype proto)
        => proto.Components.ContainsKey("Puddle");

    public bool IsTrashPrototype(EntityPrototype proto)
    {
        foreach (var excluded in ExcludedTrashComponents)
        {
            if (proto.Components.ContainsKey(excluded))
                return false;
        }

        if (ContainsId(AdditionalTrashPrototypes, proto.ID))
            return true;

        if (proto.Components.TryGetValue("Tag", out var reg) && reg.Component is TagComponent tags && IsTrashTagged(tags))
            return true;

        return false;
    }

    public override void Update(float frameTime)
    {
        if (_timing.CurTime < _nextSweep)
            return;

        _nextSweep += SweepInterval;

        var magazine = TimeSpan.FromSeconds(_cfg.GetCVar(CCVars.LooseMagazineDespawnTime));
        if (magazine > TimeSpan.Zero)
        {
            _scratch.Clear();
            CollectLooseMagazines(_scratch);
            foreach (var uid in _scratch)
                Sweep(uid, magazine);
        }

        var trash = TimeSpan.FromSeconds(_cfg.GetCVar(CCVars.TrashDespawnTime));
        if (trash > TimeSpan.Zero)
        {
            // Fast path: every tagged trash entity carries a TagComponent, and a
            // metadata-only scan every few seconds is too costly on big rounds.
            _scratch.Clear();
            var query = EntityQueryEnumerator<TagComponent, MetaDataComponent>();
            while (query.MoveNext(out var uid, out _, out var meta))
            {
                if (IsLooseTrash(uid, meta))
                    _scratch.Add(uid);
            }

            foreach (var uid in _scratch)
                Sweep(uid, trash);
        }
    }

    private void Sweep(EntityUid uid, TimeSpan despawn)
    {
        var comp = EnsureComp<CMULooseSinceComponent>(uid);

        if (comp.FirstSeen == TimeSpan.Zero)
            comp.FirstSeen = _timing.CurTime;
        else if (_timing.CurTime - comp.FirstSeen >= despawn)
            QueueDel(uid);
    }

    private bool IsTrashTagged(TagComponent tags)
    {
        if (_tag.HasTag(tags, TrashTag))
            return true;

        foreach (var tag in AdditionalTrashTags)
        {
            if (_tag.HasTag(tags, tag))
                return true;
        }

        return false;
    }

    private static bool MatchesIds(MetaDataComponent meta, HashSet<string>? ids)
        => ids == null || (meta.EntityPrototype is { } proto && ids.Contains(proto.ID));

    private static bool ContainsId(string[] ids, string id)
    {
        foreach (var candidate in ids)
        {
            if (candidate == id)
                return true;
        }

        return false;
    }
}
