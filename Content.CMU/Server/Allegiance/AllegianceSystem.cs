using Content.Shared._RMC14.Rules;
using Content.Shared.AU14.Allegiance;
using Content.Shared.AU14.Origin;
using Content.Shared.AU14.util;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.AU14.Allegiance;

/// <summary>
/// Server-side system that tracks ignore-allegiance state per player
/// and provides helpers for allegiance-aware character selection at spawn time.
/// </summary>
public sealed class AllegianceSystem : EntitySystem
{
    [Dependency] private readonly IServerNetManager _netManager = default!;

    /// <summary>
    /// Tracks which players have toggled "Ignore Allegiance".
    /// </summary>
    private readonly Dictionary<NetUserId, bool> _ignoreAllegiance = new();

    public override void Initialize()
    {
        base.Initialize();
        _netManager.RegisterNetMessage<MsgIgnoreAllegiance>(OnIgnoreAllegianceMessage);
    }

    private void OnIgnoreAllegianceMessage(MsgIgnoreAllegiance message)
    {
        var userId = message.MsgChannel.UserId;
        _ignoreAllegiance[userId] = message.IgnoreAllegiance;
    }

    /// <summary>
    /// Returns whether the given player has opted to ignore allegiance.
    /// </summary>
    public bool IsIgnoringAllegiance(NetUserId userId)
    {
        return _ignoreAllegiance.TryGetValue(userId, out var ignore) && ignore;
    }


    /// <summary>
    /// Checks whether a character's allegiance matches a platoon's allegiance.
    /// If the platoon has no allegiance set, any character is accepted.
    /// If the job has IgnoreAllegiance, always returns true.
    /// If the job has AllegianceOverride, the character must have that specific allegiance.
    /// </summary>
    public bool IsAllegianceApplicableForPlatoon(HumanoidCharacterProfile profile, PlatoonPrototype platoon, JobPrototype? job = null)
    {
        if (job != null && !DoesCharacterMeetJobOrigin(profile, job))
            return false;

        if (job is { IgnoreAllegiance: true })
            return true;

        // Characters with no allegiance (null) are considered unaffiliated and may join any platoon.
        if (profile.Allegiance == null)
            return true;

        if (platoon.Allegiance == null)
            return true;

        // Job requires a specific allegiance — character must have it (and it must match the platoon)
        if (job?.AllegianceOverride != null)
            return profile.Allegiance != null
                   && profile.Allegiance.Value == job.AllegianceOverride.Value
                   && profile.Allegiance.Value == platoon.Allegiance.Value;

        return profile.Allegiance != null && profile.Allegiance.Value == platoon.Allegiance.Value;
    }

    /// <summary>
    /// Checks whether a character's allegiance matches a colony's allegiance.
    /// If the colony has no allegiance set, any character is accepted.
    /// If the job has IgnoreAllegiance, always returns true.
    /// If the job has AllegianceOverride, the character must have that specific allegiance.
    /// </summary>
    public bool IsAllegianceApplicableForColony(HumanoidCharacterProfile profile, RMCPlanetMapPrototypeComponent colony, JobPrototype? job = null)
    {
        if (job != null && !DoesCharacterMeetJobOrigin(profile, job))
            return false;

        if (job is { IgnoreAllegiance: true })
            return true;

        // Characters with no allegiance (null) are considered unaffiliated and may join any colony.
        if (profile.Allegiance == null)
            return true;

        if (colony.Allegiance == null)
            return true;

        // Job requires a specific allegiance — character must have it (and it must match the colony)
        if (job?.AllegianceOverride != null)
            return profile.Allegiance != null
                   && profile.Allegiance.Value == job.AllegianceOverride.Value
                   && profile.Allegiance.Value == colony.Allegiance.Value;

        return profile.Allegiance != null && profile.Allegiance.Value == colony.Allegiance.Value;
    }

    /// <summary>
    /// Checks whether a character satisfies a job's allegiance requirements.
    /// Returns false if the job requires an allegiance the character doesn't have.
    /// Returns true if the job has no allegiance requirements or ignores allegiance.
    /// </summary>
    public bool DoesCharacterMeetJobAllegiance(HumanoidCharacterProfile profile, JobPrototype job)
    {
        if (job.IgnoreAllegiance)
            return true;

        if (job.AllegianceOverride == null)
            return true;

        // Characters with no allegiance should be allowed to take jobs unless the job explicitly requires a specific allegiance.
        if (profile.Allegiance == null)
            return true;

        return profile.Allegiance.Value == job.AllegianceOverride.Value;
    }

    /// <summary>
    /// Checks whether a character satisfies a job's origin requirements.
    /// </summary>
    public bool DoesCharacterMeetJobOrigin(HumanoidCharacterProfile profile, JobPrototype job)
    {
        var origin = profile.Origin;

        if (job.OriginBlackist is { Count: > 0 } &&
            origin != null &&
            job.OriginBlackist.Contains(origin.Value))
        {
            return false;
        }

        if (job.OriginWhitelist is { Count: > 0 })
        {
            return origin != null && job.OriginWhitelist.Contains(origin.Value);
        }

        return true;
    }

    /// <summary>
    /// Given a player's character profiles, finds a character whose allegiance
    /// matches a platoon. Returns null if no match found.
    /// </summary>
    public HumanoidCharacterProfile? FindApplicableCharacterForPlatoon(
        IReadOnlyDictionary<int, ICharacterProfile> characters,
        int selectedIndex,
        PlatoonPrototype platoon,
        JobPrototype? job = null)
    {
        // If the platoon has no allegiance, the selected character is fine
        if (platoon.Allegiance == null)
        {
            if (characters.TryGetValue(selectedIndex, out var sel) && sel is HumanoidCharacterProfile selH)
                return selH;
        }

        // First check the selected character
        if (characters.TryGetValue(selectedIndex, out var selectedProfile) &&
            selectedProfile is HumanoidCharacterProfile selectedHumanoid)
        {
            if (IsAllegianceApplicableForPlatoon(selectedHumanoid, platoon, job))
                return selectedHumanoid;
        }

        // Then check all other characters
        foreach (var (_, profile) in characters)
        {
            if (profile is not HumanoidCharacterProfile humanoid)
                continue;

            if (IsAllegianceApplicableForPlatoon(humanoid, platoon, job))
                return humanoid;
        }

        return null;
    }

    /// <summary>
    /// Given a player's character profiles, finds a character whose allegiance
    /// matches a colony. Returns null if no match found.
    /// </summary>
    public HumanoidCharacterProfile? FindApplicableCharacterForColony(
        IReadOnlyDictionary<int, ICharacterProfile> characters,
        int selectedIndex,
        RMCPlanetMapPrototypeComponent colony,
        JobPrototype? job = null)
    {
        // If the colony has no allegiance, the selected character is fine
        if (colony.Allegiance == null)
        {
            if (characters.TryGetValue(selectedIndex, out var sel) && sel is HumanoidCharacterProfile selH)
                return selH;
        }

        // First check the selected character
        if (characters.TryGetValue(selectedIndex, out var selectedProfile) &&
            selectedProfile is HumanoidCharacterProfile selectedHumanoid)
        {
            if (IsAllegianceApplicableForColony(selectedHumanoid, colony, job))
                return selectedHumanoid;
        }

        // Then check all other characters
        foreach (var (_, profile) in characters)
        {
            if (profile is not HumanoidCharacterProfile humanoid)
                continue;

            if (IsAllegianceApplicableForColony(humanoid, colony, job))
                return humanoid;
        }

        return null;
    }

    /// <summary>
    /// Clears the ignore allegiance state for a player (e.g. on disconnect).
    /// </summary>
    public void ClearPlayer(NetUserId userId)
    {
        _ignoreAllegiance.Remove(userId);
    }
}

