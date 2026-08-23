using Content.Shared._RMC14.Hands;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Events;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Network;

namespace Content.Shared._CMU14.Yautja;

/// <summary>
/// Client-side prediction for the source-pack powered dual plasma cannons.
/// The authoritative power drain and pack lifecycle remain on the server system.
/// </summary>
public sealed partial class YautjaCannonPackPredictionSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedGunSystem _gun = default!;

    public override void Initialize()
    {
        if (!_net.IsClient)
            return;

        SubscribeLocalEvent<YautjaCannonPackLinkedCannonComponent, AttemptShootEvent>(OnAttemptShoot);
        SubscribeLocalEvent<YautjaCannonPackLinkedCannonComponent, TakeAmmoEvent>(OnTakeAmmo, before: [typeof(SharedGunSystem)]);
        SubscribeLocalEvent<YautjaCannonPackLinkedCannonComponent, RMCItemDropAttemptEvent>(OnDropAttempt);
        SubscribeLocalEvent<YautjaCannonPackLinkedCannonComponent, ThrowItemAttemptEvent>(OnThrowAttempt);
        SubscribeLocalEvent<YautjaCannonPackLinkedCannonComponent, DroppedEvent>(OnDropped);
    }

    private void OnAttemptShoot(Entity<YautjaCannonPackLinkedCannonComponent> ent, ref AttemptShootEvent args)
    {
        if (!_net.IsClient || args.Cancelled || !TryGetClientPack(ent, out var pack))
            return;

        if (pack.Comp.Charge >= ent.Comp.ChargeCost)
            return;

        args.Cancelled = true;
        args.Message = GetPackDrainFailureMessage(pack.Comp, ent.Comp.ChargeCost);
    }

    private void OnTakeAmmo(Entity<YautjaCannonPackLinkedCannonComponent> ent, ref TakeAmmoEvent args)
    {
        if (!_net.IsClient ||
            args.Ammo.Count != 0 ||
            args.Shots <= 0 ||
            args.User is not { } ||
            !TryGetClientPack(ent, out var pack))
        {
            return;
        }

        if (pack.Comp.Charge < ent.Comp.ChargeCost)
        {
            args.Reason = GetPackDrainFailureMessage(pack.Comp, ent.Comp.ChargeCost);
            return;
        }

        for (var shot = 0; shot < args.Shots; shot++)
        {
            var projectile = Spawn(ent.Comp.Projectile, args.Coordinates);
            args.Ammo.Add((projectile, _gun.EnsureShootable(projectile)));
        }
    }

    private void OnDropAttempt(Entity<YautjaCannonPackLinkedCannonComponent> ent, ref RMCItemDropAttemptEvent args)
    {
        if (!_net.IsClient || !TryGetClientPack(ent, out var pack))
            return;

        args.Cancelled = true;
        if (!TryGetCurrentHolder(ent.Owner, out var user))
            return;

        RetractClient(pack, user, ent.Owner);
    }

    private void OnThrowAttempt(Entity<YautjaCannonPackLinkedCannonComponent> ent, ref ThrowItemAttemptEvent args)
    {
        if (!_net.IsClient || !TryGetClientPack(ent, out var pack))
            return;

        RetractClient(pack, args.User, ent.Owner);
        args.Cancelled = true;
    }

    private void OnDropped(Entity<YautjaCannonPackLinkedCannonComponent> ent, ref DroppedEvent args)
    {
        if (!_net.IsClient || !TryGetClientPack(ent, out var pack))
            return;

        RetractClient(pack, args.User, ent.Owner);
    }

    private bool TryGetClientPack(
        Entity<YautjaCannonPackLinkedCannonComponent> ent,
        out Entity<YautjaCannonPackComponent> pack)
    {
        pack = default;
        if (!_net.IsClient ||
            TerminatingOrDeleted(ent.Comp.Pack) ||
            !TryComp(ent.Comp.Pack, out YautjaCannonPackComponent? packComp))
        {
            return false;
        }

        pack = (ent.Comp.Pack, packComp);
        return true;
    }

    private bool TryGetCurrentHolder(EntityUid item, out EntityUid user)
    {
        user = default;
        if (!_containers.TryGetContainingContainer((item, null, null), out var container) ||
            !HasComp<HandsComponent>(container.Owner))
        {
            return false;
        }

        user = container.Owner;
        return true;
    }

    private void RetractClient(Entity<YautjaCannonPackComponent> pack, EntityUid user, EntityUid cannon)
    {
        if (_hands.IsHolding(user, cannon))
            _hands.TryDrop(user, cannon, checkActionBlocker: false, doDropInteraction: false);

        pack.Comp.CannonContainer ??= _containers.EnsureContainer<ContainerSlot>(pack.Owner, pack.Comp.CannonContainerId);
        _containers.Insert(cannon, pack.Comp.CannonContainer, force: true);
        pack.Comp.CannonsDeployed = false;
    }

    private string GetPackDrainFailureMessage(YautjaCannonPackComponent pack, FixedPoint2 amount)
    {
        return Loc.GetString(
            "cmu-yautja-cannon-pack-drain-failed",
            ("charge", (int) pack.Charge),
            ("max", (int) pack.MaxCharge),
            ("amount", (int) amount));
    }
}
