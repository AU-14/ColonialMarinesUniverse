using Content.Server.GameTicking.Events;
using Content.Server.Preferences.Managers;
using Content.Shared.CMU14.Roles;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Roles;

/// <summary>
/// Blocks late joins into roles whose physical standards the selected character doesn't meet.
/// Round start and the lobby check the same standards; see <see cref="CMUMilitaryHeightRequirement"/>.
/// </summary>
public sealed class CMUMilitaryStandardsSystem : EntitySystem
{
    [Dependency] private IServerPreferencesManager _prefs = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GetDisallowedJobsEvent>(OnGetDisallowedJobs);
    }

    private void OnGetDisallowedJobs(ref GetDisallowedJobsEvent ev)
    {
        if (_prefs.GetPreferences(ev.Player.UserId).SelectedCharacter is not HumanoidCharacterProfile profile)
            return;

        foreach (var job in _proto.EnumeratePrototypes<JobPrototype>())
        {
            if (!CMUMilitaryHeightRequirement.Check(job, EntityManager.ComponentFactory, profile, out _))
                ev.Jobs.Add(job.ID);
        }
    }
}
