using System.Linq;
using Content.Client.CMU14.Lobby;
using Content.Shared.CMU14.Lobby;
using Content.Shared.CMU14.Round.Roles;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Content.Shared._RMC14.Prototypes;

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private CMUEventAccessSystem EventAccess => _entManager.System<CMUEventAccessSystem>();

    private void RefreshRoleRequirements()
    {
        RefreshYautjaTab();
        RefreshJobs();
        RefreshAntags();
        MarkPreviewJobsDirty();
    }

    private void UpdateEventSideControls()
    {
        var govfor = EventAccess.CanAccessSide(RoundJobSide.Govfor);
        var opfor = EventAccess.CanAccessSide(RoundJobSide.Opfor);
        FoFSideButton.Clear();
        if (govfor && opfor)
            FoFSideButton.AddItem(Loc.GetString("cmu-fof-side-either"), (int) ForceOnForceSide.Either);
        if (govfor)
            FoFSideButton.AddItem(Loc.GetString("cmu-fof-side-govfor"), (int) ForceOnForceSide.Govfor);
        if (opfor)
            FoFSideButton.AddItem(Loc.GetString("cmu-fof-side-opfor"), (int) ForceOnForceSide.Opfor);
        if (!govfor && !opfor)
            FoFSideButton.AddItem(Loc.GetString("cmu-event-sides-locked"), -1);

        FoFSideButton.SelectId(!govfor && !opfor ? -1 : (int) EventAccess.EffectiveSide(Profile?.FoFSide ?? ForceOnForceSide.Either));
        FoFSideButton.Disabled = FoFSideButton.ItemCount == 1;
        FoFSideButton.ToolTip = EventAccess.Enabled ? Loc.GetString("cmu-event-side-help") : null;
    }

    private HumanoidCharacterProfile? EventProfile()
    {
        return Profile?.WithForceOnForcePreferences(EventAccess.EffectiveSide(Profile.FoFSide), Profile.FoFFallback);
    }

    private void AddForceOnForceJobSelectors((string, int)[] items)
    {
        var profile = EventProfile();
        var roles = CMUEventJobCatalog.GetJobs(_prototypeManager)
            .Where(job => job.RoundSide is RoundJobSide.Govfor or RoundJobSide.Opfor)
            .GroupBy(CMUEventJobCatalog.PreferenceRole);
        var representatives = new List<JobPrototype>();
        foreach (var role in roles)
        {
            // One shared priority per role, evaluated against the side this player can actually join.
            var job = role.OrderBy(candidate => profile?.IsForceOnForceJobReachable(candidate) == false)
                .ThenBy(candidate => EventAccess.IsLocked(candidate.ID))
                .ThenBy(candidate => !_requirements.IsAllowed(candidate, Profile, out _))
                .ThenBy(candidate => profile?.IsForceOnForceOtherSide(candidate) == true)
                .ThenBy(candidate => candidate.RoundSide)
                .ThenBy(candidate => candidate.ID)
                .First();
            representatives.Add(job);
        }
        foreach (var job in representatives.OrderBy(job => GetMilitaryJobSegment(job).Key)
                     .ThenBy(job => job, JobUIComparer.Instance))
        {
            var (key, title) = GetMilitaryJobSegment(job);
            AddJobSelector(FoFJobList, GamemodeForceOnForce, $"fof-{key}", title, true, title, items, job);
        }
    }
}
