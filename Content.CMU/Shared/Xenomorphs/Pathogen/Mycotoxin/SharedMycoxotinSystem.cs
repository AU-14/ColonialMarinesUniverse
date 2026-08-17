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
using Robust.Shared.Prototypes;
using Content.Shared._CMU14.Xenomorphs.Pathogen.SporeCloud;
using Content.Shared.Mobs;
using Robust.Shared.GameObjects;
using Content.Shared.Popups;
using Content.Shared._CMU14.Medical.Injuries.Wounds;
using Content.Shared.Body.Systems;

namespace Content.Shared._CMU14.Xenomorphs.Pathogen.Mycotoxin;

public sealed class SharedMycotoxinSystem : EntitySystem
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

        if (HasComp<XenoComponent>(target) || HasComp<SynthComponent>(target))
            return false;

        if (!HasComp<InfectableComponent>(target))
            return false;

        if (HasComp<VictimInfectedComponent>(target))
            return false;

        return true;
    }

    /// <summary>
    /// Checks worn mask/head slots for Mycotoxin protection. Mirrors DM's
    /// SPOREPROOF (always blocks) / BLOCKGASEFFECT (prob(80) blocks) checks.
    /// Wearing two or more protective items (even partial ones) guarantees
    /// full protection instead of stacking independent rolls.
    /// </summary>
    private bool IsProtected(EntityUid target)
    {
        // Open wounds let mycotoxin in directly, bypassing mask/head protection.
        if (HasOpenWound(target))
            return false;

        MycotoxinProtectionComponent? single = null;
        var protectiveItemCount = 0;

        foreach (var slot in new[] { "mask", "head" })
        {
            if (!_inventory.TryGetSlotEntity(target, slot, out var item))
                continue;

            if (!TryComp(item, out MycotoxinProtectionComponent? protection))
                continue;

            if (protection.FullProtection)
                return true;

            protectiveItemCount++;
            single = protection;
        }

        if (protectiveItemCount >= 2)
            return true;

        if (protectiveItemCount == 1 && single != null)
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

                if (IsProtected(victim))
                    continue;

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
            exposure.SourceHive = _hive.GetHive(injector.Owner)?.Owner; // NOTE: adjust to actual API
        }

        exposure.Exposure += injector.MycotoxinPerSecond;
        Dirty(victim, exposure);
    }

    // Simple per-victim lookup since these components aren't networked fields
    // (EntProtoId/EntityUid? don't need to be re-synced every tick).
    private readonly Dictionary<EntityUid, EntProtoId> _pendingEmbryo = new();
    private readonly Dictionary<EntityUid, EntityUid?> _pendingHive = new();

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

        // Show infection popups
        _popup.PopupEntity(
            Loc.GetString("cmu-xeno-spore-cloud-inhale-self"),
            victim, victim, PopupType.MediumCaution);
        _popup.PopupEntity(
            Loc.GetString("cmu-xeno-spore-cloud-inhale-others", ("target", MetaData(victim).EntityName)),
            victim, PopupType.LargeCaution);

        RemCompDeferred<MycotoxinExposureComponent>(victim);
    }

    /// <summary>
    /// Immediately infects a target by setting their mycotoxin exposure above
    /// the threshold. Safe to call from other systems.
    /// </summary>
    public void ForceInfect(EntityUid target, EntProtoId embryoSpawn)
    {
        var exposure = EnsureComp<MycotoxinExposureComponent>(target);
        exposure.EmbryoSpawn = embryoSpawn;
        exposure.Exposure = exposure.InfectThreshold + 1f;
        Dirty(target, exposure);
    }
}
