using Content.Shared._CMU14.Medical;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.Mobs.Components;
using Content.Shared.StatusIcon.Components;
using Content.Shared._RMC14.Marines;
using Robust.Shared.Console;
using Robust.Shared.Map;

namespace Content.Client._CMU14.Medical;

public sealed class CMUMedicalPerfCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entities = default!;

    private readonly HashSet<EntityUid> _nearby = new();
    private readonly HashSet<Entity<StatusIconComponent>> _statusIcons = new();
    private readonly HashSet<Entity<MobThresholdsComponent>> _healthBars = new();
    private readonly HashSet<Entity<MarineComponent>> _marineIcons = new();

    public string Command => "cmu_medical_perf";
    public string Description => "Reports nearby CMU medical client visibility and overlay candidate counts.";
    public string Help => "Usage: cmu_medical_perf [range=12]";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player?.AttachedEntity is not { } player)
        {
            shell.WriteError("You must be attached to an entity.");
            return;
        }

        var range = 12f;
        if (args.Length > 0 && (!float.TryParse(args[0], out range) || range <= 0))
        {
            shell.WriteError(Help);
            return;
        }

        var xform = _entities.System<SharedTransformSystem>();
        var lookup = _entities.System<EntityLookupSystem>();
        var origin = xform.GetMapCoordinates(player);
        if (origin.MapId == MapId.Nullspace)
        {
            shell.WriteError("Attached entity is in nullspace.");
            return;
        }

        var flags = LookupFlags.Dynamic | LookupFlags.Static | LookupFlags.Sundries | LookupFlags.Sensors;
        _nearby.Clear();
        lookup.GetEntitiesInRange(origin.MapId, origin.Position, range, _nearby, flags);

        var cmuBodies = 0;
        var attachedInternals = 0;
        foreach (var uid in _nearby)
        {
            if (_entities.HasComponent<CMUHumanMedicalComponent>(uid))
                cmuBodies++;

            if (IsAttachedCmuInternal(uid))
                attachedInternals++;
        }

        _statusIcons.Clear();
        _healthBars.Clear();
        _marineIcons.Clear();
        lookup.GetEntitiesInRange(origin, range, _statusIcons, flags);
        lookup.GetEntitiesInRange(origin, range, _healthBars, flags);
        lookup.GetEntitiesInRange(origin, range, _marineIcons, flags);

        shell.WriteLine($"CMU medical perf around {range:F1}m:");
        shell.WriteLine($"  nearby visible entities: {_nearby.Count}");
        shell.WriteLine($"  nearby CMU bodies: {cmuBodies}");
        shell.WriteLine($"  visible attached CMU internals: {attachedInternals}");
        shell.WriteLine($"  status icon candidates: {_statusIcons.Count}");
        shell.WriteLine($"  health bar candidates: {_healthBars.Count}");
        shell.WriteLine($"  marine icon candidates: {_marineIcons.Count}");
    }

    private bool IsAttachedCmuInternal(EntityUid uid)
    {
        if (_entities.TryGetComponent(uid, out BodyPartComponent? part) &&
            part.Body is { } partBody &&
            _entities.HasComponent<CMUHumanMedicalComponent>(partBody))
        {
            return true;
        }

        if (_entities.TryGetComponent(uid, out OrganComponent? organ) &&
            organ.Body is { } organBody &&
            _entities.HasComponent<CMUHumanMedicalComponent>(organBody))
        {
            return true;
        }

        return false;
    }
}
