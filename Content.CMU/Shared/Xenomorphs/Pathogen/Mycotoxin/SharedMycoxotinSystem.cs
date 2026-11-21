using Content.Shared.CMU14.GasMask;
using Content.Shared._RMC14.Synth;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.Parasite;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Network;
using Robust.Shared.Physics.Events;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Content.Shared.Mobs;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;
using Content.Shared.CMU14.Xenomorphs.Pathogen.Walker;

namespace Content.Shared.CMU14.Xenomorphs.Pathogen.Mycotoxin;

public abstract partial class SharedMycotoxinSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedXenoHiveSystem _hive = default!;
    [Dependency] private SharedXenoParasiteSystem _parasite = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private CMUPathogenSporeProtectionSystem _protection = default!;
    [Dependency] private SharedGasMaskSystem _gasMask = default!;

    private enum ProtectionResult { None, Partial, Filter }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MycotoxinInjectorComponent, StartCollideEvent>(OnStartCollide);
        SubscribeLocalEvent<MycotoxinInjectorComponent, EndCollideEvent>(OnEndCollide);
    }

    private void OnStartCollide(Entity<MycotoxinInjectorComponent> ent, ref StartCollideEvent args)
    {
        if (!CanExposeTarget(args.OtherEntity))
            return;
        ent.Comp.ContactedEntities.Add(args.OtherEntity);
    }

    private void OnEndCollide(Entity<MycotoxinInjectorComponent> ent, ref EndCollideEvent args)
    {
        ent.Comp.ContactedEntities.Remove(args.OtherEntity);
    }

    private bool CanExposeTarget(EntityUid target)
    {
        if (!HasComp<Content.Shared.Mobs.Components.MobStateComponent>(target))
            return false;
        if (HasComp<CMUPathogenWalkerComponent>(target))
            return false;
        if (HasComp<XenoComponent>(target) || HasComp<SynthComponent>(target))
            return false;
        if (!HasComp<InfectableComponent>(target))
            return false;
        if (HasComp<VictimInfectedComponent>(target))
            return false;
        return true;
    }

    private ProtectionResult GetProtection(EntityUid target,
        out EntityUid? filterItem,
        out GasMaskFilterComponent? filter)
    {
        filterItem = null;
        filter = null;

        if (_protection.HasOpenWound(target))
            return ProtectionResult.None;

        // Internals block inhaled spores outright; Partial means this tick is blocked.
        if (_gasMask.IsBreathingInternals(target))
            return ProtectionResult.Partial;

        if (_gasMask.TryGetWornSporeFilter(target, out var working))
        {
            filterItem = working.Owner;
            filter = working.Comp;
            return ProtectionResult.Filter;
        }

        foreach (var slot in new[] { "mask", "head" })
        {
            if (!_inventory.TryGetSlotEntity(target, slot, out var item))
                continue;
            if (!TryComp(item, out MycotoxinProtectionComponent? prot) || !_gasMask.CanUsePassiveSporeProtection(item.Value))
                continue;

            if (prot.FullProtection)
                return _random.Prob(0.8f) ? ProtectionResult.Partial : ProtectionResult.None;

            return _random.Prob(prot.PartialBlockChance)
                ? ProtectionResult.Partial
                : ProtectionResult.None;
        }

        return ProtectionResult.None;
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var time = _timing.CurTime;

        var injectorQuery = EntityQueryEnumerator<MycotoxinInjectorComponent>();
        while (injectorQuery.MoveNext(out var uid, out var injector))
        {
            if (time < injector.NextInjectionAt)
                continue;

            injector.NextInjectionAt = time + injector.TimeBetweenInjects;

            foreach (var victim in injector.ContactedEntities)
            {
                if (!injector.AffectsDead && _mobState.IsDead(victim))
                    continue;
                if (!CanExposeTarget(victim))
                    continue;

                if (injector.Sporecaster is { } caster && IsOnCasterTile(victim, caster))
                {
                    Expose(victim, uid, injector);
                    continue;
                }

                var protection = GetProtection(victim, out var filterEnt, out var filterComp);

                switch (protection)
                {
                    case ProtectionResult.Partial:
                        continue;

                    case ProtectionResult.Filter:
                        _gasMask.DamageFilter(filterEnt!.Value, filterComp!, injector.FilterDrainPerTick);
                        continue;

                    case ProtectionResult.None:
                        break;
                }

                Expose(victim, uid, injector);
            }
        }

        var exposureQuery = EntityQueryEnumerator<MycotoxinExposureComponent>();
        while (exposureQuery.MoveNext(out var uid, out var exposure))
        {
            if (time < exposure.NextTickAt)
                continue;
            exposure.NextTickAt = time + exposure.UpdateEvery;
            Tick(uid, exposure);
        }
    }

    private bool IsOnCasterTile(EntityUid victim, EntityUid caster)
    {
        if (!TryComp<TransformComponent>(caster, out var source))
            return false;
        var wearer = Transform(victim);
        return (wearer.GridUid ?? wearer.MapUid) == (source.GridUid ?? source.MapUid) &&
            _transform.GetGridOrMapTilePosition(victim, wearer) == _transform.GetGridOrMapTilePosition(caster, source);
    }

    private void Expose(EntityUid victim, EntityUid source, MycotoxinInjectorComponent injector)
    {
        var isNew = !HasComp<MycotoxinExposureComponent>(victim);
        var exposure = EnsureComp<MycotoxinExposureComponent>(victim);
        if (isNew)
        {
            exposure.EmbryoSpawn = injector.EmbryoSpawn;
            exposure.SourceHive = _hive.GetHive(source)?.Owner;
            exposure.StrongEffects = injector.StrongExposureEffects;
            OnFirstExposure(victim, injector.StrongExposureEffects);
        }
        exposure.Exposure += injector.MycotoxinPerSecond;
        Dirty(victim, exposure);
    }

    protected virtual void OnFirstExposure(EntityUid victim, bool strongEffects) { }

    private void Tick(EntityUid victim, MycotoxinExposureComponent exposure)
    {
        if (exposure.Infected)
            return;

        exposure.Exposure -= exposure.DepletionPerTick;

        if (exposure.Exposure <= 0)
        {
            RemCompDeferred<MycotoxinExposureComponent>(victim);
            return;
        }

        if (exposure.Exposure >= exposure.InfectThreshold)
        {
            InfectWithEmbryo(victim, exposure);
            return;
        }

        Dirty(victim, exposure);
    }

    private void InfectWithEmbryo(EntityUid victim, MycotoxinExposureComponent exposure)
    {
        exposure.Infected = true;
        Dirty(victim, exposure);

        var victimComp = EnsureComp<VictimInfectedComponent>(victim);
        _parasite.SetBurstSpawn((victim, victimComp), exposure.EmbryoSpawn);
        _parasite.SetHive((victim, victimComp), exposure.SourceHive);
        _parasite.SetBurstsFromBack((victim, victimComp), true);
        Dirty(victim, victimComp);

        _popup.PopupEntity(Loc.GetString("cmu-xeno-spore-cloud-inhale-self"), victim, victim, PopupType.MediumCaution);
        _popup.PopupEntity(Loc.GetString("cmu-xeno-spore-cloud-inhale-others", ("target", MetaData(victim).EntityName)), victim, PopupType.LargeCaution);

        RemCompDeferred<MycotoxinExposureComponent>(victim);
    }

    public void SetSporecaster(EntityUid cloud, EntityUid caster)
    {
        if (TryComp<MycotoxinInjectorComponent>(cloud, out var injector))
            injector.Sporecaster = caster;
    }

    public void ForceInfect(EntityUid target, EntProtoId embryoSpawn, EntityUid? sourceHive = null)
    {
        var exposure = EnsureComp<MycotoxinExposureComponent>(target);
        exposure.EmbryoSpawn = embryoSpawn;
        exposure.SourceHive = sourceHive;
        exposure.Exposure = exposure.InfectThreshold + 1f;
        Dirty(target, exposure);
    }
}
