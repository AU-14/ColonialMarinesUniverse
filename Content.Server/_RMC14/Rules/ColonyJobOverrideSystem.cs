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
    public sealed class ColonyJobOverrideSystem : EntitySystem
    {
        [Dependency] private readonly RMCPlanetSystem _planetSystem = default!;
        [Dependency] private readonly GameTicker _gameTicker = default!;

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

            // Read the planet prototype data directly. Prefer any planet in rotation.
            var all = _planetSystem.GetAllPlanetsInRotation();
            if (all.Count == 0)
                return;

            var planetComp = all[0].Comp;
            if (planetComp.ColonyJobOverrides == null)
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

                    // Replace the profile with a copy that has updated priorities.
                    profiles[user] = profile
                        .WithGamemodeJobPriority(presetId, overridenJob, overridenPriority)
                        .WithGamemodeJobPriority(presetId, overrideJob, JobPriority.Never);
                }
            }
        }
    }
}






