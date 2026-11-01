using Content.Shared.CMU14.Antags.GovForVeteran;
using Content.Shared.Hands.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;

namespace Content.Server.CMU14.Round.Antags.GovForVeteran;

/// <summary>
///     CMU14: behavior for the A.W. GovFor Veteran's signature machete.
///     - thrown faster than a normal item,
///     - knocks down whatever it hits,
///     - only the veteran can pick it back up (see WhitelistPickupBy on the prototype); anyone can drag it away.
/// </summary>
public sealed partial class CMUVeteranMacheteSystem : EntitySystem
{
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedStunSystem _stun = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMUVeteranMacheteComponent, ThrowDoHitEvent>(OnThrowHit);

        // BeforeThrowEvent is raised on the thrower, not the item, so the speed bonus is applied here.
        SubscribeLocalEvent<HandsComponent, BeforeThrowEvent>(OnBeforeThrow);
    }

    /// <summary>
    ///     The machete is heavy scrap thrown by a man who has done it before: it travels faster than
    ///     the standard hand throw without turning into a projectile.
    /// </summary>
    private void OnBeforeThrow(Entity<HandsComponent> ent, ref BeforeThrowEvent args)
    {
        if (args.Cancelled)
            return;

        if (!TryComp<CMUVeteranMacheteComponent>(args.ItemUid, out var machete))
            return;

        args.ThrowSpeed *= machete.ThrowSpeedMultiplier;
    }

    private void OnThrowHit(Entity<CMUVeteranMacheteComponent> ent, ref ThrowDoHitEvent args)
    {
        // Damage is handled by the weapon's own components; this only adds the knockdown.
        if (args.Target == args.Component.Thrower)
            return;

        if (_mobState.IsDead(args.Target))
            return;

        _stun.TryKnockdown(args.Target, ent.Comp.Knockdown, refresh: true, autoStand: true, drop: true);
    }
}
