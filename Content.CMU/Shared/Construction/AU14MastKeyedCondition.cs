using Content.Shared.Construction;
using Content.Shared.Examine;
using JetBrains.Annotations;

namespace Content.Shared.CMU14.Construction;

/// <summary>
///     Holds the feed of a field mast open until at least one COMSEC key is loaded. Sealing an unkeyed mast
///     would stand up a structure that relays nothing, which only ever happens by mistake.
/// </summary>
[UsedImplicitly]
[DataDefinition]
public sealed partial class AU14MastKeyed : IGraphCondition
{
    public bool Condition(EntityUid uid, IEntityManager entityManager)
    {
        return entityManager.TryGetComponent(uid, out AU14MastKeyComponent? key) && key.Keys.Count > 0;
    }

    public bool DoExamine(ExaminedEvent args)
    {
        if (Condition(args.Examined, IoCManager.Resolve<IEntityManager>()))
            return false;

        args.PushMarkup(Loc.GetString("au14-mast-condition-keyed") + "\n");
        return true;
    }

    public IEnumerable<ConstructionGuideEntry> GenerateGuideEntry()
    {
        yield return new ConstructionGuideEntry
        {
            Localization = "au14-mast-condition-keyed-guide",
        };
    }
}
