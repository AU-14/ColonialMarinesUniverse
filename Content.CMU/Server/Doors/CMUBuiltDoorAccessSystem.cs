using System.Linq;
using Content.Server.Construction;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Doors.Electronics;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Doors;

// RMC doors only read their own AccessReader, never the board's, so every built door came out all-access
// no matter what was set on the circuit with a multitool. copy the board's access onto the finished door
public sealed partial class CMUBuiltDoorAccessSystem : EntitySystem
{
    private const string BoardContainer = "board";

    [Dependency] private AccessReaderSystem _accessReader = default!;
    [Dependency] private SharedContainerSystem _container = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ConstructionChangeEntityEvent>(OnConstructionChangeEntity);
        SubscribeLocalEvent<CMUBuiltDoorAccessComponent, ComponentStartup>(OnStartup);
    }

    private void OnConstructionChangeEntity(ConstructionChangeEntityEvent args)
    {
        if (!HasComp<AccessReaderComponent>(args.New) ||
            !_container.TryGetContainer(args.New, BoardContainer, out var container))
        {
            return;
        }

        foreach (var board in container.ContainedEntities)
        {
            if (!HasComp<DoorElectronicsComponent>(board) ||
                !TryComp(board, out AccessReaderComponent? boardReader))
            {
                continue;
            }

            // the new door isn't started yet, so set it once it is
            var pending = EnsureComp<CMUBuiltDoorAccessComponent>(args.New);
            pending.Access = boardReader.AccessLists
                .Select(group => new HashSet<ProtoId<AccessLevelPrototype>>(group))
                .ToList();
            return;
        }
    }

    private void OnStartup(Entity<CMUBuiltDoorAccessComponent> ent, ref ComponentStartup args)
    {
        if (TryComp(ent, out AccessReaderComponent? reader) && ent.Comp.Access.Count > 0)
            _accessReader.TrySetAccesses((ent, reader), ent.Comp.Access, updateOriginal: true);

        RemCompDeferred<CMUBuiltDoorAccessComponent>(ent);
    }
}

[RegisterComponent]
public sealed partial class CMUBuiltDoorAccessComponent : Component
{
    public List<HashSet<ProtoId<AccessLevelPrototype>>> Access = new();
}
