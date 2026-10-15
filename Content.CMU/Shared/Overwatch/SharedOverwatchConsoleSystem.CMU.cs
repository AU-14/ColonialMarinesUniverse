using Content.Shared._RMC14.Tracker.SquadLeader;
using Content.Shared.CMU14.Preferences;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs.Systems;

namespace Content.Shared._RMC14.Overwatch;

// CMU14: callsign, fireteam and health on the overwatch roster.
public abstract partial class SharedOverwatchConsoleSystem
{
    [Dependency] private DamageableSystem _cmuDamageable = default!;
    [Dependency] private MobThresholdSystem _cmuThresholds = default!;

    private OverwatchMarine CMUAddMarineDetails(EntityUid member, OverwatchMarine marine)
    {
        var details = new CMUOverwatchMarineDetailsEvent();
        RaiseLocalEvent(member, ref details);

        TryComp(member, out FireteamMemberComponent? fireteam);

        return marine with
        {
            // Rank (added by the console) and first and last name only; no nickname.
            Name = CMUCharacterName.StripNickname(marine.Name),
            Callsign = details.Callsign ?? string.Empty,
            Fireteam = fireteam?.Fireteam,
            FireteamLeader = fireteam?.Leader ?? false,
            Health = CMUGetHealth(member),
        };
    }

    /// <summary>
    /// Orbital bombardment keeps its own saved coordinates, separate from the supply drop's.
    /// </summary>
    private void CMUOnOrbitalSave(Entity<OverwatchConsoleComponent> ent, ref OverwatchConsoleOrbitalSaveBuiMsg args)
    {
        var locations = ent.Comp.OrbitalSavedLocations;
        if (locations.Length == 0)
            return;

        if (ent.Comp.LastOrbitalLocation >= locations.Length)
            ent.Comp.LastOrbitalLocation = 0;

        locations[ent.Comp.LastOrbitalLocation] = new OverwatchSavedLocation(args.Longitude, args.Latitude, string.Empty);
        ent.Comp.LastOrbitalLocation++;
        Dirty(ent);
    }

    private void CMUOnOrbitalComment(Entity<OverwatchConsoleComponent> ent, ref OverwatchConsoleOrbitalCommentBuiMsg args)
    {
        var locations = ent.Comp.OrbitalSavedLocations;
        if (args.Index < 0 || args.Index >= locations.Length || locations[args.Index] is not { } location)
            return;

        var comment = args.Comment.Length > 50 ? args.Comment[..50] : args.Comment;
        locations[args.Index] = location with { Comment = comment };
        Dirty(ent);
    }

    /// <summary>
    /// Health as the health analyzer shows it: 1 when unhurt, 0 at the incapacitation threshold and
    /// negative past it.
    /// </summary>
    private float? CMUGetHealth(EntityUid member)
    {
        if (!TryComp(member, out DamageableComponent? damageable) ||
            !_cmuThresholds.TryGetIncapThreshold(member, out var incap) ||
            incap.Value <= 0)
        {
            return null;
        }

        var total = _cmuDamageable.GetTotalDamage((member, damageable));
        return Math.Clamp((incap.Value - total).Float() / incap.Value.Float(), -1f, 1f);
    }
}

/// <summary>
/// Raised on a squad member when the overwatch roster is built, so server-only data such as the
/// radio callsign can be filled in.
/// </summary>
[ByRefEvent]
public record struct CMUOverwatchMarineDetailsEvent(string? Callsign = null);
