using System.Linq;
using System.Numerics;
using Content.Server.GameTicking;
using Content.Shared.Body;
using Content.Shared.Clothing;
using Content.Shared.CMU14.Ghost;
using Content.Shared.GameTicking;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Inventory;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Ghost;

/// <summary>
///     A ghost with no body to copy, like a lobby observer, looks like the player's selected character instead: in their
///     preferred job's clothes, as the lobby preview shows them. Uses the same body-copying ghost appearance as a ghost
///     that left a body, with a hidden stand-in body carried by the ghost.
/// </summary>
public sealed partial class CMUGhostProfileAppearanceSystem : EntitySystem
{
    [Dependency] private CMUGhostAppearanceSystem _ghostAppearance = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private HumanoidProfileSystem _humanoidProfile = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private SharedVisualBodySystem _visualBody = default!;
    [Dependency] private VisibilitySystem _visibility = default!;

    /// <summary>
    ///     Dresses <paramref name="ghost"/> as <paramref name="player"/>'s selected character, unless it already has a
    ///     body to look like.
    /// </summary>
    public void DressAsSelectedCharacter(EntityUid ghost, ICommonSession player)
    {
        if (TryComp<CMUGhostAppearanceComponent>(ghost, out var existing) && existing.Source != null)
            return;

        var profile = _gameTicker.GetPlayerProfile(player);
        if (!_prototypes.TryIndex(profile.Species, out var species))
            return;

        var standIn = SpawnAttachedTo(species.DollPrototype, new EntityCoordinates(ghost, Vector2.Zero));
        EnsureComp<CMUGhostAppearanceStandInComponent>(standIn);

        // Whatever can't see the ghost can't see what it carries, or the engine would send the ghost along with it.
        if (TryComp<VisibilityComponent>(ghost, out var ghostVisibility))
            _visibility.SetLayer(standIn, ghostVisibility.Layer);

        _visualBody.ApplyProfileTo(standIn, profile);
        _humanoidProfile.ApplyProfileTo(standIn, profile);
        DressInPreferredJob(standIn, profile, player);

        _ghostAppearance.SetSource(ghost, standIn);
    }

    /// <summary>
    ///     The preferred job's clothes and the player's chosen loadout, worn equipment only, like the lobby preview.
    /// </summary>
    private void DressInPreferredJob(EntityUid standIn, HumanoidCharacterProfile profile, ICommonSession player)
    {
        var jobId = profile.JobPriorities.FirstOrDefault(p => p.Value == JobPriority.High).Key;
        if (!_prototypes.TryIndex(jobId, out JobPrototype? job) &&
            !_prototypes.TryIndex(SharedGameTicker.FallbackOverflowJob, out job))
        {
            return;
        }

        if (_prototypes.TryIndex(job.StartingGear, out var gear))
            Wear(standIn, gear);

        var (key, proto) = LoadoutSystem.GetJobLoadoutInfo(job.ID, _prototypes);
        if (proto == null)
            return;

        var loadout = profile.GetLoadoutOrDefault(key, player, profile.Species, EntityManager, _prototypes);
        foreach (var group in loadout.SelectedLoadouts.Values)
        {
            foreach (var selected in group)
            {
                if (!_prototypes.TryIndex(selected.Prototype, out var loadoutProto))
                    continue;

                if (_prototypes.TryIndex(loadoutProto.StartingGear, out var loadoutGear))
                    Wear(standIn, loadoutGear);

                Wear(standIn, loadoutProto);
            }
        }
    }

    private void Wear(EntityUid standIn, IEquipmentLoadout loadout)
    {
        if (!_inventory.TryGetSlots(standIn, out var slots))
            return;

        foreach (var slot in slots)
        {
            var itemType = loadout.GetGear(slot.Name);
            if (string.IsNullOrEmpty(itemType))
                continue;

            if (_inventory.TryUnequip(standIn, slot.Name, out var replaced, silent: true, force: true, reparent: false))
                Del(replaced.Value);

            var item = Spawn(itemType, MapCoordinates.Nullspace);
            if (!_inventory.TryEquip(standIn, item, slot.Name, silent: true, force: true))
                Del(item);
        }
    }
}
