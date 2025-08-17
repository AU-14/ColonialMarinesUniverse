using System.Collections.Generic;
using Content.Shared.Fax.Components;
using Content.Server.Fax;
using Content.Shared.Paper;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.Server.AU14.Systems;

public sealed class WantedSystem : EntitySystem
{
    [Dependency] private readonly EntityManager _entManager = default!;
    [Dependency] private readonly FaxSystem _faxSystem = default!;

    private readonly List<FugitiveInfo> _fugitives = new();

    public IReadOnlyList<FugitiveInfo> Fugitives => _fugitives;



    public void SendFax(IEntitySystemManager systemManager, IEntityManager entityManager, string faxname, string papername, string? faxname2 = null)
    {
        var faxSystem = systemManager.GetEntitySystem<FaxSystem>();
        var faxQuery = entityManager.EntityQueryEnumerator<FaxMachineComponent>();
        while (faxQuery.MoveNext(out var faxEnt, out var faxComp))
        {
            if (faxComp.FaxName == faxname || (faxname2 != null && faxComp.FaxName == faxname2))
            {
                var synthPaper = entityManager.SpawnEntity(papername, MapCoordinates.Nullspace);

                if (entityManager.TryGetComponent<PaperComponent>(synthPaper, out var paperComp) &&
                    entityManager.TryGetComponent<MetaDataComponent>(synthPaper, out var metaComp))
                {
                    var printout = new FaxPrintout(
                        paperComp.Content,
                        metaComp.EntityName,
                        null, // No label
                        papername,
                        paperComp.StampState,
                        paperComp.StampedBy
                    );

                    faxSystem.Receive(faxEnt, printout, null, faxComp);
                }

                entityManager.DeleteEntity(synthPaper);
            }
        }
    }
}

public record FugitiveInfo(string Name, string Crime, string AddedBy, DateTime AddedAt);
