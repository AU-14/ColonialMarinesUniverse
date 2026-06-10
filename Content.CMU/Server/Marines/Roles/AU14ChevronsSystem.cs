using Content.Server.Players.PlayTimeTracking;
using Content.Shared.GameTicking;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;
using Content.Shared._RMC14.UniformAccessories;
using Content.Shared.Hands.EntitySystems;

namespace Content.Server._AU14.Marines.Roles.Chevrons;

public sealed partial class ChevronSystem : EntitySystem
{
    [Dependency] private PlayTimeTrackingManager _tracking = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private SharedUniformAccessorySystem _uniformAccessory = default!;
    [Dependency] private SharedHandsSystem _hands = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        if (ev.JobId == null)
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
                    if (!req.Check(_entityManager, _prototypes, ev.Profile, playTimes, out _))
                    {
                        failed = true;
                        break;
                    }
                }
            }

            if (!failed)
            {
                var coords = _entityManager.GetComponent<TransformComponent>(ev.Mob).Coordinates;
                var chevron = _entityManager.SpawnEntity(chevronDef.Entity, coords);

                if (!_uniformAccessory.TryInsertToValidSlot(chevron, ev.Mob))
                    _hands.TryPickupAnyHand(ev.Mob, chevron, false);

                break;
            }
        }
    }
}
