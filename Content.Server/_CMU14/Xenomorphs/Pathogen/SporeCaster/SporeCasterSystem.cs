using Content.Shared._CMU14.Xenomorphs.Pathogen.Sporecaster;
using Content.Shared._CMU14.Xenomorphs.Pathogen.Mycotoxin;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Synth;
using Content.Shared._RMC14.Xenonids.Parasite;
using Content.Shared.Destructible;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Content.Shared.Interaction;
using Content.Shared.Body.Systems;
using Content.Shared._CMU14.Medical.Injuries.Wounds;
using Content.Shared._CMU14.Xenomorphs.Pathogen.Walker;
using Content.Shared.Examine;

namespace Content.Server._CMU14.Xenomorphs.Pathogen.Sporecaster;

public sealed class CMUPathogenSporecasterSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedXenoHiveSystem _hive = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly Content.Shared._CMU14.Medical.Injuries.Wounds.SharedCMUWoundsSystem _wounds = default!;

    private readonly HashSet<Entity<MobStateComponent>> _nearby = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUPathogenSporecasterComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CMUPathogenSporecasterComponent, DestructionEventArgs>(OnDestruction);
        SubscribeLocalEvent<CMUPathogenSporecasterComponent, InteractHandEvent>(OnInteract);
        SubscribeLocalEvent<CMUPathogenSporecasterComponent, ExaminedEvent>(OnExamine);
    }

    private void OnMapInit(Entity<CMUPathogenSporecasterComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.NextGrowAt = _timing.CurTime + ent.Comp.GrowInterval;
        Dirty(ent);
    }

    private void OnDestruction(Entity<CMUPathogenSporecasterComponent> ent, ref DestructionEventArgs args)
    {
        if (ent.Comp.StoredClouds <= 0)
            return;

        _popup.PopupEntity(
            Loc.GetString("cmu-sporecaster-destruction-release"),
            ent.Owner,
            PopupType.LargeCaution);

        var coords = Transform(ent.Owner).Coordinates;
        for (var i = 0; i < ent.Comp.StoredClouds; i++)
        {
            if (!_random.Prob(ent.Comp.DestructionReleaseChance))
                continue;

            var parasite = SpawnAtPosition(ent.Comp.ParasiteProto, coords);
            // Assign hive
            if (_hive.GetHive(ent.Owner) is { } hive)
                _hive.SetHive(parasite, hive);
        }
    }

    // Allow xenos from same hive to manually trigger a cloud
    private void OnInteract(Entity<CMUPathogenSporecasterComponent> ent, ref InteractHandEvent args)
    {
        if (!HasComp<XenoComponent>(args.User))
            return;

        if (!_hive.FromSameHive(ent.Owner, args.User))
            return;

        if (ent.Comp.StoredClouds <= 0)
        {
            _popup.PopupEntity(
                Loc.GetString("cmu-sporecaster-empty"),
                ent.Owner, args.User, PopupType.SmallCaution);
            return;
        }

        ReleaseCloud(ent);
        args.Handled = true;
    }

    private void ReleaseCloud(Entity<CMUPathogenSporecasterComponent> ent)
    {
        ent.Comp.StoredClouds = Math.Max(0, ent.Comp.StoredClouds - 1);

        // Reset grow timer if was full
        if (ent.Comp.StoredClouds == ent.Comp.MaxClouds - 1)
            ent.Comp.NextGrowAt = _timing.CurTime + ent.Comp.GrowInterval;

        Dirty(ent);
        SpawnAtPosition(ent.Comp.SporeCloudProto, Transform(ent.Owner).Coordinates);
    }

    public override void Update(float frameTime)
    {
        var time = _timing.CurTime;
        var query = EntityQueryEnumerator<CMUPathogenSporecasterComponent>();

        while (query.MoveNext(out var uid, out var caster))
        {
            // Grow stored clouds
            if (time >= caster.NextGrowAt && caster.StoredClouds < caster.MaxClouds)
            {
                caster.StoredClouds = Math.Min(caster.MaxClouds, caster.StoredClouds + 1);
                caster.NextGrowAt = time + caster.GrowInterval;
                Dirty(uid, caster);
            }

            // Auto-release at targets
            if (caster.StoredClouds <= 0)
                continue;

            if (time < caster.NextAutoReleaseAt)
                continue;

            _nearby.Clear();
            _lookup.GetEntitiesInRange<MobStateComponent>(
                Transform(uid).Coordinates,
                caster.DetectionRange,
                _nearby);

            foreach (var (target, _) in _nearby)
            {
                if (HasComp<XenoComponent>(target))
                    continue;
                if (HasComp<SynthComponent>(target))
                    continue;
                if (!HasComp<InfectableComponent>(target))
                    continue;
                if (HasComp<VictimInfectedComponent>(target))
                    continue;
                if (HasComp<CMUPathogenWalkerComponent>(target))
                    continue;
                if (_mobState.IsDead(target))
                    continue;
                if (IsProtected(target))
                    continue;

                caster.NextAutoReleaseAt = time + caster.AutoReleaseInterval;
                Dirty(uid, caster);
                ReleaseCloud((uid, caster));
                break;
            }
        }
    }

    private bool IsProtected(EntityUid target)
    {
        if (HasOpenWound(target))
            return false;

        MycotoxinProtectionComponent? single = null;
        var count = 0;

        foreach (var slot in new[] { "mask", "head" })
        {
            if (!_inventory.TryGetSlotEntity(target, slot, out var item))
                continue;

            if (!TryComp(item, out MycotoxinProtectionComponent? protection))
                continue;

            if (protection.FullProtection)
                return true;

            count++;
            single = protection;
        }

        if (count >= 2)
            return true;

        if (count == 1 && single != null)
            return _random.Prob(single.PartialBlockChance);

        return false;
    }

    private bool HasOpenWound(EntityUid target)
    {
        foreach (var (partUid, _) in _body.GetBodyChildren(target))
        {
            if (_wounds.HasOpenWound(partUid))
                return true;
        }

        return false;
    }

    private void OnExamine(Entity<CMUPathogenSporecasterComponent> ent, ref ExaminedEvent args)
    {
        if (!HasComp<XenoComponent>(args.Examiner) && !_hive.FromSameHive(ent.Owner, args.Examiner))
            return;

        var remaining = ent.Comp.NextGrowAt - _timing.CurTime;
        var seconds = (int) Math.Max(0, remaining.TotalSeconds);

        args.PushMarkup(Loc.GetString("cmu-sporecaster-examine",
            ("current", ent.Comp.StoredClouds),
            ("max", ent.Comp.MaxClouds),
            ("seconds", seconds)));
    }
}