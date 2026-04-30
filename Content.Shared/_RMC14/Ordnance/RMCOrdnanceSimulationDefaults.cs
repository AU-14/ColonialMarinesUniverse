using Robust.Shared.Utility;

namespace Content.Shared._RMC14.Ordnance;

/// <summary>
///     Shared defaults used by the ordnance simulators so both devices replay against the same chamber layout.
/// </summary>
public static class RMCOrdnanceSimulationDefaults
{
    // The original custom chamber map is currently corrupted and trips the map loader with
    // EndOfStreamException while deserializing tile chunks, so we point both simulators at a
    // known-good template until the bespoke chamber is regenerated in the editor.
    public static readonly ResPath ChamberMap = new("/Maps/Test/admin_test_arena.yml");
}
