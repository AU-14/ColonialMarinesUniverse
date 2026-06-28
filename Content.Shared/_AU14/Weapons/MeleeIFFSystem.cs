using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared.Interaction.Events;

namespace Content.Shared._AU14.Weapons;

/// <summary>
///     System that prevents IFF weapons from attacking IFF protected entities
/// </summary>
public sealed partial class MeleeIFFSystem : EntitySystem
{
    [Dependency] private GunIFFSystem _gunIFF = default!;
    public override void Initialize()
    {
        SubscribeLocalEvent<MeleeIFFComponent, AttackAttemptEvent>(OnAttackAttempt);
    }

    /// <summary>
    ///     Cancel attacks on IFF protected objects
    /// </summary>
    private void OnAttackAttempt(Entity<MeleeIFFComponent> ent, ref AttackAttemptEvent args)
    {
        if (args.Target is not { } target)
            return;
        // Ideally IFF should be generalized out of GunIFFSystem
        if (_gunIFF.TryGetUserFaction(target, out var faction) && ent.Comp.Factions.Contains(faction))
            args.Cancel();
    }
}
