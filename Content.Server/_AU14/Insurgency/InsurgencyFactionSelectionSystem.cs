using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Content.Server.AU14.Round;
using Content.Server._AU14.Insurgency.Database;

namespace Content.Server._AU14.Insurgency;

/// <summary>
///     GOVFOR to Default-faction matching and the server-authoritative selection path. A Default
///     faction declares the GOVFOR platoons (USMC, TWE RMC, UPP, and so on) it may oppose; only
///     factions that list the round's chosen GOVFOR platoon are offered, and the match is re-checked
///     here before a faction is applied so a tampered client cannot force an unmatched faction.
/// </summary>
public sealed class InsurgencyFactionSelectionSystem : EntitySystem
{
    [Dependency] private PlatoonSpawnRuleSystem _platoons = default!;
    [Dependency] private InsurgencyFactionDbSystem _db = default!;
    [Dependency] private InsurgencyFactionApplySystem _apply = default!;

    /// <summary>
    ///     Stored Default factions whose OpposedGovforFactions include the round's GOVFOR platoon.
    ///     Empty until a GOVFOR platoon is chosen. This is List A ("GOVFOR's Opposition") for the
    ///     leader spawn popup.
    /// </summary>
    public async Task<List<InsurgencyFactionDbSystem.StoredFaction>> GetOpposingDefaultFactionsAsync()
    {
        var govfor = _platoons.SelectedGovforPlatoon?.ID;
        var all = await _db.GetFactionsAsync();
        return all.Where(f => f.IsDefault && OpposesGovfor(f.Definition, govfor)).ToList();
    }

    /// <summary>
    ///     Loads, re-validates the GOVFOR match, and applies a stored Default faction for the round.
    ///     Returns false if the id is gone or the faction does not oppose the round's GOVFOR platoon.
    /// </summary>
    public async Task<bool> SelectDefaultFactionAsync(int id)
    {
        var def = await _db.GetFactionAsync(id);
        if (def == null)
            return false;

        if (!OpposesGovfor(def, _platoons.SelectedGovforPlatoon?.ID))
            return false;

        _apply.ApplyFaction(def);
        return true;
    }

    private static bool OpposesGovfor(Content.Shared._AU14.Insurgency.FactionDefinition def, string? govforPlatoon)
    {
        // No GOVFOR platoon picked yet: nothing is confirmed to match, so offer nothing rather than
        // everything. A faction must explicitly list the platoon it opposes.
        if (govforPlatoon == null)
            return false;

        // The built-in CLF (now seeded as an ordinary DB row) opposes every GOVFOR, exactly as the
        // code-built copy always did, regardless of its OpposedGovforFactions list.
        if (def.Metadata.BuiltinOverrideOf == Content.Shared._AU14.Insurgency.InsurgencyBuiltinFactions.VanillaClfId)
            return true;

        return def.Metadata.OpposedGovforFactions
            .Any(g => string.Equals(g, govforPlatoon, StringComparison.OrdinalIgnoreCase));
    }
}
