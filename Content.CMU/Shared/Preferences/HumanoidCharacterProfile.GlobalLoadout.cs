using System.Linq;
using Content.Shared.Preferences.Loadouts;
using Robust.Shared.Prototypes;

namespace Content.Shared.Preferences;

public sealed partial class HumanoidCharacterProfile
{
    public const string GlobalLoadoutId = "CMUGlobalLoadout";
    public static readonly ProtoId<LoadoutGroupPrototype> GlobalPinsGroup = "RMCLoadoutGroupPins";

    private void MigrateGlobalLoadout(IPrototypeManager prototypes)
    {
        if (!_loadouts.ContainsKey(GlobalLoadoutId))
        {
            // Preserve legacy selections once; an explicit global selection, even empty, always wins.
            var selected = _loadouts.Values
                .Where(loadout => loadout.SelectedLoadouts.ContainsKey(GlobalPinsGroup))
                .SelectMany(loadout => loadout.SelectedLoadouts[GlobalPinsGroup])
                .Select(loadout => loadout.Prototype)
                .ToHashSet();
            if (selected.Count == 0)
                return;

            var global = new RoleLoadout(GlobalLoadoutId);
            var group = prototypes.Index(GlobalPinsGroup);
            global.SelectedLoadouts[GlobalPinsGroup] = group.Loadouts
                .Where(selected.Contains)
                .Take(group.MaxLimit > 0 ? group.MaxLimit : int.MaxValue)
                .Select(id => new Loadout { Prototype = id })
                .ToList();
            _loadouts[GlobalLoadoutId] = global;
        }

        foreach (var (key, loadout) in _loadouts.ToArray())
        {
            if (key == GlobalLoadoutId || !loadout.SelectedLoadouts.ContainsKey(GlobalPinsGroup))
                continue;

            var migrated = loadout.Clone();
            migrated.SelectedLoadouts.Remove(GlobalPinsGroup);
            _loadouts[key] = migrated;
        }
    }
}
