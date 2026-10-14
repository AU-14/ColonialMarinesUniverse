using Content.Shared._RMC14.Deafness;
using Content.Shared.CMU14.Hearing;
using Content.Shared.Inventory;
using Robust.Client.Player;
using Robust.Shared.Audio.Components;

namespace Content.Client.CMU14.Dropship;

/// <summary>
///     Ear protection muffles the roar of a dropship: while the local player wears it, the dropship's
///     takeoff, flight and landing sounds play at 40% of their volume for them.
/// </summary>
public sealed class CMUDropshipEarProtectionSystem : EntitySystem
{
    private const float ProtectedGain = 0.4f;

    private static readonly string[] FlightSounds =
    [
        "/Audio/CMU14/Dropship/dropship_flight_start.ogg",
        "/Audio/CMU14/Dropship/dropship_flight_airlocked_start.ogg",
        "/Audio/CMU14/Dropship/dropship_flight_recurr.ogg",
        "/Audio/CMU14/Dropship/dropship_flight_end.ogg",
        "/Audio/_RMC14/Machines/Shuttle/engine_startup.ogg",
        "/Audio/_RMC14/Machines/Shuttle/engine_landing.ogg",
    ];

    [Dependency] private SharedDeafnessSystem _deafness = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private IPlayerManager _player = default!;

    // The gain each muffled sound had before, so it can be put back.
    private readonly Dictionary<EntityUid, float> _muffled = new();
    private readonly List<EntityUid> _gone = new();

    public override void FrameUpdate(float frameTime)
    {
        var protectedEars = _player.LocalEntity is { } player && HasEarProtection(player);

        _gone.Clear();
        foreach (var (uid, gain) in _muffled)
        {
            if (!TryComp(uid, out AudioComponent? audio))
            {
                _gone.Add(uid);
                continue;
            }

            if (!protectedEars)
            {
                audio.Gain = gain;
                _gone.Add(uid);
            }
        }

        foreach (var uid in _gone)
        {
            _muffled.Remove(uid);
        }

        if (!protectedEars)
            return;

        var query = EntityQueryEnumerator<AudioComponent>();
        while (query.MoveNext(out var uid, out var audio))
        {
            if (Array.IndexOf(FlightSounds, audio.FileName) < 0)
                continue;

            if (!_muffled.TryGetValue(uid, out var original))
            {
                original = audio.Gain;
                _muffled[uid] = original;
            }

            // A server update can reset the gain, so it is set again every frame.
            audio.Gain = original * ProtectedGain;
        }
    }

    private bool HasEarProtection(EntityUid player)
    {
        if (_deafness.HasEarProtection(player))
            return true;

        if (!_inventory.TryGetContainerSlotEnumerator(player, out var slots))
            return false;

        while (slots.NextItem(out var item, out _))
        {
            if (HasComp<CMUCombatEarProtectionComponent>(item))
                return true;
        }

        return false;
    }
}
