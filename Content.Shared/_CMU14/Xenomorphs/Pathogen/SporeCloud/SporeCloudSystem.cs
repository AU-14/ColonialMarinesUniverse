using Content.Shared._CMU14.Xenomorphs.Pathogen.Mycotoxin;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.Parasite;
using Content.Shared._RMC14.Synth;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Robust.Shared.Network;
using Robust.Shared.Physics.Events;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Content.Shared._CMU14.Xenomorphs.Pathogen.Walker;

namespace Content.Shared._CMU14.Xenomorphs.Pathogen.SporeCloud;

public sealed partial class CMUPathogenSporeCloudSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedXenoParasiteSystem _parasite = default!;
    [Dependency] private readonly SharedXenoHiveSystem _hive = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUPathogenSporeCloudComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<CMUPathogenSporeCloudComponent, StartCollideEvent>(OnStartCollide);
    }

    private void OnStartup(Entity<CMUPathogenSporeCloudComponent> cloud, ref ComponentStartup args)
    {
        var lifetime = TimeSpan.FromSeconds(_random.NextFloat(30f, 60f));
        cloud.Comp.DecayAt = _timing.CurTime + lifetime;
    }

    private void OnStartCollide(Entity<CMUPathogenSporeCloudComponent> cloud, ref StartCollideEvent args)
    {
        TryInhale(cloud, args.OtherEntity);
    }

    private bool TryInhale(Entity<CMUPathogenSporeCloudComponent> cloud, EntityUid target)
    {
        if (cloud.Comp.Inhaling)
            return false;

        if (HasComp<SynthComponent>(target))
            return false;

        if (HasComp<CMUPathogenWalkerComponent>(target))
            return false;

        if (!HasComp<MobStateComponent>(target))
            return false;

        // Check for protection via masks/helmets (SPOREPROOF or BLOCKGASEFFECT).
        if (IsProtected(target))
            return false;

        // Check for existing embryos from the same hive.
        var cloudHive = _hive.GetHive(cloud.Owner);
        if (HasExistingEmbryo(target, cloudHive?.Owner))
            return false;

        if (HasComp<VictimInfectedComponent>(target))
            return false;

        cloud.Comp.Inhaling = true;
        Dirty(cloud);

        if (_net.IsServer)
        {
            var victimComp = EnsureComp<VictimInfectedComponent>(target);
            _parasite.SetBurstSpawn((target, victimComp), cloud.Comp.EmbryoSpawn);
            if (cloudHive?.Owner is { } hiveEnt)
                _parasite.SetHive((target, victimComp), hiveEnt);

            if (!cloud.Comp.SilentInhale)
            {
                _popup.PopupEntity(
                    Loc.GetString("cmu-xeno-spore-cloud-inhale-others", ("target", target)),
                    target, PopupType.LargeCaution);
                _popup.PopupEntity(
                    Loc.GetString("cmu-xeno-spore-cloud-inhale-self"),
                    target, target, PopupType.MediumCaution);
            }
        }

        QueueDel(cloud);
        return true;
    }

    /// <summary>
    /// Checks if the target is wearing protective gear (mask/helmet) that blocks spores.
    /// Mirrors DM's SPOREPROOF (always blocks) / BLOCKGASEFFECT (prob 80% blocks) logic.
    /// </summary>
    private bool IsProtected(EntityUid target)
    {
        MycotoxinProtectionComponent? single = null;
        var protectiveItemCount = 0;

        foreach (var slot in new[] { "mask", "head" })
        {
            if (!_inventory.TryGetSlotEntity(target, slot, out var item))
                continue;

            if (!TryComp(item, out MycotoxinProtectionComponent? protection))
                continue;

            // A single fully-protective item (SPOREPROOF) blocks outright.
            if (protection.FullProtection)
                return true;

            protectiveItemCount++;
            single = protection;
        }

        // Two or more partial-protection items together count as full protection.
        if (protectiveItemCount >= 2)
            return true;

        // Exactly one partial-protection item: roll its chance (mirrors prob(80) in DM).
        if (protectiveItemCount == 1 && single != null)
            return _random.Prob(single.PartialBlockChance);

        return false;
    }

    /// <summary>
    /// Checks if the target already has an embryo from this hive.
    /// Mirrors DM's embryo-counting loop to prevent duplicate infections.
    /// </summary>
    private bool HasExistingEmbryo(EntityUid target, EntityUid? cloudHive)
    {
        // Walk every inventory slot on the target looking for a parasite/embryo.
        if (!_inventory.TryGetContainerSlotEnumerator(target, out var enumerator))
            return false;

        while (enumerator.MoveNext(out var containerSlot))
        {
            if (containerSlot?.ContainedEntity is not { } item)
                continue;

            if (!HasComp<XenoParasiteComponent>(item))
                continue;

            // Check if this embryo belongs to the same hive as the cloud.
            // If cloudHive is null (cloud has no hive), allow infection.
            // If hives match, target already has one, so reject.
            var embryoHive = _hive.GetHive(item)?.Owner;
            if (cloudHive != null && embryoHive == cloudHive)
                return true;
        }

        return false;
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var time = _timing.CurTime;
        var query = EntityQueryEnumerator<CMUPathogenSporeCloudComponent>();
        while (query.MoveNext(out var uid, out var cloud))
        {
            if (time >= cloud.DecayAt)
                QueueDel(uid);
        }
    }
}