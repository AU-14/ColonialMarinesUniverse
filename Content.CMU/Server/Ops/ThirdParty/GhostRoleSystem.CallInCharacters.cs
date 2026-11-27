using System.Linq;
using Content.Server.CMU14.Ops.ThirdParty;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Humanoid.Components;
using Content.Server.Preferences.Managers;
using Content.Server.Station.Systems;
using Content.Shared._RMC14.Synth;
using Content.Shared.CMU14.Ops.ThirdParty;
using Content.Shared.CMU14.Yautja;
using Content.Shared.GameTicking;
using Content.Shared.Ghost.Components;
using Content.Shared.Humanoid;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server.Ghost.Roles;

public sealed partial class GhostRoleSystem
{
    [Dependency] private IServerPreferencesManager _callInPreferences = default!;

    private const int CallInChoiceSeconds = 30;
    private readonly Dictionary<uint, CMUCallInCharacterEui> _callInChoices = new();
    private readonly HashSet<string> _playedNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<(NetUserId, int)> _playedSlots = new();

    private void InitializeCallInCharacters()
    {
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(ev =>
        {
            if (!IsOrganicCallInHuman(ev.Mob) || ev.Profile.Synthetic ||
                ev.JobId != null && ProtoMan.TryIndex<JobPrototype>(ev.JobId, out var job) && !job.UsePlayerProfile)
                return;

            // A normal spawn can display a job-specific name. Also remember the saved identity.
            _playedNames.Add(ev.Profile.Name);
            var prefs = _callInPreferences.GetPreferencesOrNull(ev.Player.UserId);
            if (prefs == null)
                return;
            foreach (var (slot, saved) in prefs.Characters)
            {
                if (string.Equals(saved.Name, ev.Profile.Name, StringComparison.OrdinalIgnoreCase))
                    _playedSlots.Add((ev.Player.UserId, slot));
            }
        });
    }

    private void ClearCallInCharacters()
    {
        foreach (var id in _callInChoices.Keys.ToArray())
            CloseCallInChoice(id);
        _playedNames.Clear();
        _playedSlots.Clear();
    }

    private bool IsOrganicCallInHuman(EntityUid mob)
    {
        return TryComp<HumanoidProfileComponent>(mob, out var body) &&
               body.Species == HumanoidCharacterProfile.DefaultSpecies &&
               !HasComp<SynthComponent>(mob) && !HasComp<YautjaComponent>(mob) && !HasComp<GhostComponent>(mob);
    }

    private bool CanUseCallInProfile(NetUserId player, int slot, HumanoidCharacterProfile profile)
    {
        return !profile.Synthetic && profile.Species == HumanoidCharacterProfile.DefaultSpecies &&
               !string.IsNullOrWhiteSpace(profile.Name) &&
               !_playedNames.Contains(profile.Name) && !_playedSlots.Contains((player, slot));
    }

    private bool TryOfferCallInChoice(ICommonSession winner, Entity<GhostRoleComponent> role)
    {
        if (!HasComp<CMUCallInCharacterComponent>(role) || !IsOrganicCallInHuman(role) ||
            !HasComp<GhostTakeoverAvailableComponent>(role) || !CanTakeGhost(role, role.Comp) ||
            !TryComp<RandomHumanoidAppearanceComponent>(role, out var appearance) || !appearance.RandomizeName ||
            role.Comp.JobProto is { } jobId && ProtoMan.TryIndex(jobId, out var job) && job.IsSynthetic)
            return false;

        var characters = new Dictionary<int, string>();
        if (_callInPreferences.GetPreferencesOrNull(winner.UserId) is { } prefs)
        {
            foreach (var (slot, profile) in prefs.Characters)
            {
                if (CanUseCallInProfile(winner.UserId, slot, profile))
                    characters.Add(slot, profile.Name);
            }
        }

        var state = new CMUCallInCharacterState(role.Comp.RoleName, characters, CallInChoiceSeconds);
        var prompt = new CMUCallInCharacterEui(state, _timing.CurTime + TimeSpan.FromSeconds(CallInChoiceSeconds));
        _callInChoices.Add(role.Comp.Identifier, prompt);
        _euiManager.OpenEui(prompt, winner);
        prompt.StateDirty();
        CloseEui(winner);
        UpdateAllEui();
        return true;
    }

    internal bool HasCallInWinner(EntityUid body)
        => TryComp<GhostRoleComponent>(body, out var role) && _callInChoices.ContainsKey(role.Identifier);

    private bool IsReservedCallIn(ICommonSession player, uint id, bool explicitRequest)
    {
        if (_callInChoices.Count == 0)
            return false;
        // Keep the original draw's members for a redraw if its winner disconnects.
        if (explicitRequest && _callInChoices.ContainsKey(id))
            return true;
        return _callInChoices.Any(pair => pair.Key != id && pair.Value.Player == player);
    }

    private bool UpdateCallInChoice(EntityUid body, GhostRoleRaffleComponent raffle)
    {
        if (!_callInChoices.TryGetValue(raffle.Identifier, out var prompt))
            return false;

        // The normal raffle update has just rechecked bans, requirements, and ghost/lobby status.
        if (!raffle.CurrentMembers.Contains(prompt.Player))
        {
            CloseCallInChoice(raffle.Identifier);
            return false;
        }

        if (!prompt.Finished && _timing.CurTime < prompt.Deadline)
            return true;

        if (TryTakeover(prompt.Player, raffle.Identifier))
            RemoveRaffleAndUpdateEui(body, raffle);
        else
            raffle.CurrentMembers.Remove(prompt.Player);
        CloseCallInChoice(raffle.Identifier);
        return true;
    }

    private void CloseCallInChoice(uint id)
    {
        if (_callInChoices.Remove(id, out var prompt) && !prompt.Finished)
            prompt.Close();
    }

    private void ApplyWinningCallInCharacter(ICommonSession winner, EntityUid mob, GhostRoleComponent role)
    {
        if (!RemComp<CMUCallInCharacterComponent>(mob) ||
            !_callInChoices.TryGetValue(role.Identifier, out var prompt) || prompt.Player != winner ||
            prompt.SelectedSlot is not { } slot || !IsOrganicCallInHuman(mob) ||
            !prompt.Offer.Characters.TryGetValue(slot, out var offeredName) ||
            _callInPreferences.GetPreferencesOrNull(winner.UserId) is not { } prefs ||
            !prefs.Characters.TryGetValue(slot, out var profile) || profile.Name != offeredName ||
            !CanUseCallInProfile(winner.UserId, slot, profile))
            return;

        EntityManager.System<StationSpawningSystem>().ApplyCallInIdentity(mob, profile, role.JobProto?.Id);
        _playedNames.Add(profile.Name);
        _playedSlots.Add((winner.UserId, slot));
    }
}
