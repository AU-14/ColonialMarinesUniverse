using System.Collections.Generic;
using System.Linq;
using Content.Shared.Fax.Components;
using Content.Server.Fax;
using Content.Shared.Paper;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.Server.AU14.Systems;

public sealed partial class WantedSystem : EntitySystem
{
    [Dependency] private EntityManager _entManager = default!;
    [Dependency] private FaxSystem _faxSystem = default!;

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

    /// <summary>
    /// Sends a fax with dynamic content to a named fax machine.
    /// </summary>
    public bool SendCustomFax(string faxname, string paperTitle, string content, string? stampState = null, List<StampDisplayInfo>? stampedBy = null, string? faxname2 = null, string paperPrototype = "CMPaper")
    {
        return SendFaxToMatching(
            faxComp => faxComp.FaxName == faxname || (faxname2 != null && faxComp.FaxName == faxname2),
            paperTitle, content, stampState, stampedBy, paperPrototype
        );
    }

    /// <summary>
    /// Sends a fax with dynamic content to every fax machine tagged with the given group.
    /// </summary>
    public bool SendFaxToGroup(string group, string paperTitle, string content, string? stampState = null, List<StampDisplayInfo>? stampedBy = null, string paperPrototype = "CMPaper")
    {
        return SendFaxToMatching(
            faxComp => faxComp.Groups.Contains(group, StringComparer.OrdinalIgnoreCase),
            paperTitle, content, stampState, stampedBy, paperPrototype
        );
    }

    private bool SendFaxToMatching(Func<FaxMachineComponent, bool> match, string paperTitle, string content, string? stampState, List<StampDisplayInfo>? stampedBy, string paperPrototype = "CMPaper")
    {
        var sent = false;
        var faxQuery = _entManager.EntityQueryEnumerator<FaxMachineComponent>();
        while (faxQuery.MoveNext(out var faxEnt, out var faxComp))
        {
            if (!match(faxComp))
                continue;

            var printout = new FaxPrintout(content, paperTitle, null, paperPrototype, stampState, stampedBy);

            _faxSystem.Receive(faxEnt, printout, null, faxComp);
            sent = true;
        }

        return sent;
    }
}

public record FugitiveInfo(string Name, string Crime, string AddedBy, DateTime AddedAt);
