using Content.Server.Players.PlayTimeTracking;
using Content.Shared.GameTicking;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;
using Content.Shared._RMC14.UniformAccessories;
using Content.Shared.Hands.EntitySystems;
using Content.Shared._AU14.Marines.Roles.Chevrons;
using Content.Shared.Mind.Components;
using Content.Shared.Mind;
using Content.Server.Ghost.Roles.Components;
using Robust.Shared.Utility;
using Robust.Server.Player;
using System.Linq;
using Robust.Shared.Player;

namespace Content.Server._AU14.Marines.Roles.Chevrons;

public sealed partial class ChevronSystem : EntitySystem
{
    [Dependency] private PlayTimeTrackingManager _tracking = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private SharedUniformAccessorySystem _uniformAccessory = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private IPlayerManager _playerManager = default!;

    public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
            SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        }

        private void OnPlayerAttached(PlayerAttachedEvent ev)
        {
            if (HasComp<ChevronSpawnedComponent>(ev.Entity)) // already spawned -> we dont spawn another one
                return;
            // Only care about entities with a ghost role job
            if (!TryComp<GhostRoleComponent>(ev.Entity, out var ghostRole) || ghostRole.JobProto == null)
                return;

            if (!_prototypes.TryIndex<JobPrototype>(ghostRole.JobProto, out var jobPrototype))
                return;

            if (jobPrototype.Chevrons == null || jobPrototype.Chevrons.Count == 0)
                return;

            if (!_tracking.TryGetTrackerTimes(ev.Player, out var playTimes))
            {
                Log.Warning($"Playtimes not ready for ghost role takeover by {ev.Player}");
                playTimes ??= new Dictionary<string, TimeSpan>();
            }

            foreach (var (_, chevronDef) in jobPrototype.Chevrons)
            {
                var failed = false;

                if (chevronDef.Requirements != null)
                {
                    foreach (var req in chevronDef.Requirements)
                    {
                        if (!req.Check(_entityManager, _prototypes, null, playTimes, out FormattedMessage? _))
                        {
                            failed = true;
                            break;
                        }
                    }
                }

                if (!failed)
                {
                    SpawnChevron(chevronDef, ev.Entity);
                    break;
                }
            }
        }

        private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
        {
            if (ev.JobId == null || ev.Player == null)
                return;

            if (!_prototypes.TryIndex<JobPrototype>(ev.JobId, out var jobPrototype))
                return;

            if (jobPrototype.Chevrons == null || jobPrototype.Chevrons.Count == 0)
                return;

            if (!_tracking.TryGetTrackerTimes(ev.Player, out var playTimes))
            {
                Log.Error($"Playtimes weren't ready yet for {ev.Player} on roundstart!");
                playTimes ??= new Dictionary<string, TimeSpan>();
            }

            foreach (var (_, chevronDef) in jobPrototype.Chevrons)
            {
                var failed = false;

                if (chevronDef.Requirements != null)
                {
                    foreach (var req in chevronDef.Requirements)
                    {
                        if (!req.Check(_entityManager, _prototypes, ev.Profile, playTimes, out FormattedMessage? _))
                        {
                            failed = true;
                            break;
                        }
                    }
                }

                if (!failed)
                {
                    SpawnChevron(chevronDef, ev.Mob);
                    break;
                }
            }
        }

        private void SpawnChevron(ChevronDefinition chevronDef, EntityUid mob)
            {
                var coords = _entityManager.GetComponent<TransformComponent>(mob).Coordinates;
                var chevron = _entityManager.SpawnEntity(chevronDef.Entity, coords);

                if (!_uniformAccessory.TryInsertToValidSlot(chevron, mob))
                    _hands.TryPickupAnyHand(mob, chevron, false);

                EnsureComp<ChevronSpawnedComponent>(mob);
            }
    }