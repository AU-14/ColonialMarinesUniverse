using Content.Shared.Clothing;
using Content.Server.CMU14.Round;
using System.Collections.Generic;
using System.Linq;
using Content.Shared._RMC14.Rules;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Content.Server.GameTicking;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Server._RMC14.Rules
{
    /// <summary>
    /// Handles remapping of colony job overrides declared by planet/rule prototypes.
    /// When a rule declares a mapping of override -> overridden job, any ready players
    /// who selected the override job will have their profile updated to select the
    /// overridden job instead. This ensures assignment consumes the overridden job's slots.
    /// </summary>
    public sealed partial class ColonyJobOverrideSystem : EntitySystem
    {
        [Dependency] private AuRoundSystem _round = default!; // CMU14
        [Dependency] private GameTicker _gameTicker = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<RulePlayerSpawningEvent>(OnRulePlayerSpawning);
        }

        private void OnRulePlayerSpawning(RulePlayerSpawningEvent ev)
        {
            // Profiles is exposed as an IReadOnlyDictionary on the event, but the GameTicker
            // passes its concrete Dictionary instance. Try to cast so we can mutate profiles
            // before job assignment runs.
            if (ev.Profiles is not Dictionary<NetUserId, HumanoidCharacterProfile> profiles)
                return;

            // CMU14: only the selected planet determines which law-enforcement roles exist.
            var planetComp = _round.ActivePlanet ?? _round.GetSelectedPlanet();
            if (planetComp?.ColonyJobOverrides == null)
                return;

            var presetId = _gameTicker.CurrentPreset?.ID ?? _gameTicker.Preset?.ID;

            // The mapping is override -> overriden (key -> value).
            foreach (var (overrideJob, overridenJob) in planetComp.ColonyJobOverrides)
            {
                // Iterate a stable list of users to avoid modifying the collection while iterating.
                var users = profiles.Keys.ToList();
                foreach (var user in users)
                {
                    var profile = profiles[user];

                    var priority = profile.GetJobPriorityForGamemode(presetId, overrideJob);
                    if (priority <= JobPriority.Never)
                        continue;

                    var existing = profile.GetJobPriorityForGamemode(presetId, overridenJob);
                    var overridenPriority = (JobPriority)Math.Max((int)existing, (int)priority);

                    // CMU14: the generic job's saved accessories must follow the assigned role.
                    var assignedProfile = profile
                        .WithGamemodeJobPriority(presetId, overridenJob, overridenPriority)
                        .WithGamemodeJobPriority(presetId, overrideJob, JobPriority.Never);
                    if (priority >= existing &&
                        profile.Loadouts.TryGetValue(LoadoutSystem.GetJobPrototype(overrideJob), out var loadout))
                    {
                        assignedProfile = assignedProfile.WithLoadout(LoadoutSystem.GetJobPrototype(overridenJob), loadout);
                    }
                    profiles[user] = assignedProfile;
                }
            }
        }
    }
}






