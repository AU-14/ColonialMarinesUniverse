using Content.Shared._CMU14.GasMask;
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
using Content.Shared._CMU14.Medical.Injuries.Wounds;
using Content.Shared.Body.Systems;
using Robust.Shared.Prototypes;
using Content.Shared.Containers.ItemSlots;
using Content.Shared._CMU14.Xenomorphs.Pathogen.Walker;

namespace Content.Shared._CMU14.Xenomorphs.Pathogen.Mycotoxin;

public abstract class SharedMycotoxinSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedXenoHiveSystem _hive = default!;
    [Dependency] private readonly SharedXenoParasiteSystem _parasite = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly SharedCMUWoundsSystem _wounds = default!;
    [Dependency] private readonly SharedGasMaskSystem _gasMask = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;

    private enum ProtectionResult { None, Partial, FilterFull, FilterPartial }

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

    private bool TryGetFilter(EntityUid item,
        out EntityUid filterEnt,
        out GasMaskFilterComponent filter)
    {
        filterEnt = EntityUid.Invalid;
        filter = null!;

        if (!TryComp<ItemSlotsComponent>(item, out var slots))
            return false;

        if (!_itemSlots.TryGetSlot(item, "filter", out var slot, slots))
            return false;

        if (slot.ContainerSlot?.ContainedEntity is not { } fEnt)
            return false;

        if (!TryComp(fEnt, out GasMaskFilterComponent? f))
            return false;

        if (_gasMask.IsFilterBroken((fEnt, f)))
            return false;

        filterEnt = fEnt;
        filter = f;
        return true;
    }

    private ProtectionResult GetProtection(EntityUid target,
        out EntityUid? filterItem,
        out GasMaskFilterComponent? filter)
    {
        filterItem = null;
        filter = null;

        if (HasOpenWound(target))
            return ProtectionResult.None;

        foreach (var slot in new[] { "mask", "head" })
        {
            if (!_inventory.TryGetSlotEntity(target, slot, out var item))
                continue;
            if (!TryComp(item, out MycotoxinProtectionComponent? prot))
                continue;

            if (TryGetFilter(item.Value, out var fEnt, out var f))
            {
                filterItem = fEnt;
                filter = f;
                return prot.FullProtection
                    ? ProtectionResult.FilterFull
                    : ProtectionResult.FilterPartial;
            }

            if (prot.FullProtection)
                return _random.Prob(0.8f) ? ProtectionResult.Partial : ProtectionResult.None;

            return _random.Prob(prot.PartialBlockChance)
                ? ProtectionResult.Partial
                : ProtectionResult.None;
        }

        return ProtectionResult.None;
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

                var protection = GetProtection(victim, out var filterEnt, out var filterComp);

                switch (protection)
                {
                    case ProtectionResult.Partial:
                        continue;

                    case ProtectionResult.FilterFull:
                        _gasMask.DamageFilter(filterEnt!.Value, filterComp!, injector.FilterDrainPerTick);
                        if (!_gasMask.IsFilterBroken((filterEnt.Value, filterComp!)))
                            continue;
                        break;

                    case ProtectionResult.FilterPartial:
                        _gasMask.DamageFilter(filterEnt!.Value, filterComp!, injector.FilterDrainPerTick);
                        if (!_gasMask.IsFilterBroken((filterEnt.Value, filterComp!)))
                        {
                            if (!_random.Prob(0.05f))
                                continue;
                        }
                        break;

                    case ProtectionResult.None:
                        break;
                }

                Expose(victim, injector);
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

    private void Expose(EntityUid victim, MycotoxinInjectorComponent injector)
    {
        var isNew = !HasComp<MycotoxinExposureComponent>(victim);
        var exposure = EnsureComp<MycotoxinExposureComponent>(victim);
        if (isNew)
        {
            exposure.EmbryoSpawn = injector.EmbryoSpawn;
            exposure.SourceHive = _hive.GetHive(injector.Owner)?.Owner;
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

    public void ForceInfect(EntityUid target, EntProtoId embryoSpawn, EntityUid? sourceHive = null)
    {
        var exposure = EnsureComp<MycotoxinExposureComponent>(target);
        exposure.EmbryoSpawn = embryoSpawn;
        exposure.SourceHive = sourceHive;
        exposure.Exposure = exposure.InfectThreshold + 1f;
        Dirty(target, exposure);
    }
}