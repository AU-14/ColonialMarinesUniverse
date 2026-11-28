using Content.Shared.Botany.Components;
using Content.Shared.Botany.Events;
using Content.Shared.Botany.Items.Components;
using Content.Shared.Botany.Systems;

namespace Content.Server.CMU14.Botany;

/// <summary>
/// CMU14: planting shared by the machines that farm a hydroponics tray (the auto-tray and the tray arms). A machine plants a seed
/// packet exactly as a player planting it by hand: the planting handler spawns the plant from the seed's genes and deletes the seed.
/// </summary>
public sealed class CMUTrayFarmingSystem : EntitySystem
{
    [Dependency] private readonly PlantTraySystem _tray = default!;

    /// <summary>Plants <paramref name="seed"/> in the empty <paramref name="tray"/>, with <paramref name="user"/> as the planter.</summary>
    /// <returns>The planted plant; null when nothing was planted (occupied tray, bad seed).</returns>
    public EntityUid? TryPlantSeed(Entity<PlantTrayComponent?> tray, EntityUid seed, EntityUid user)
    {
        if (!Resolve(tray, ref tray.Comp, false) ||
            _tray.TryGetPlant((tray.Owner, tray.Comp), out _) ||
            TerminatingOrDeleted(seed) ||
            EntityManager.IsQueuedForDeletion(seed) ||
            !TryComp<SeedComponent>(seed, out var seedComp))
        {
            return null;
        }

        // The planting handler never cancels the event, so success is read back from the tray.
        var ev = new PlantingSeedAttemptEvent((seed, seedComp), user);
        RaiseLocalEvent(tray, ref ev);

        return _tray.TryGetPlant((tray.Owner, tray.Comp), out var planted) ? planted : null;
    }
}
