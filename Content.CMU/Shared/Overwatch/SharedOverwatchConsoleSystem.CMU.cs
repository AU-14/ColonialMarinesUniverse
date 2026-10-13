using Content.Shared._RMC14.Tracker.SquadLeader;
using Content.Shared.CMU14.Medical.Diagnostics.Examine;
using Content.Shared.CMU14.Preferences;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs.Systems;

namespace Content.Shared._RMC14.Overwatch;

// CMU14: callsign, fireteam and casualty details on the overwatch roster.
public abstract partial class SharedOverwatchConsoleSystem
{
    [Dependency] private DamageableSystem _cmuDamageable = default!;
    [Dependency] private CMUMedicalExamineSystem _cmuMedicalExamine = default!;
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
            Bleeding = _cmuMedicalExamine.GetWorstExternalBleeding(member),
        };
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
