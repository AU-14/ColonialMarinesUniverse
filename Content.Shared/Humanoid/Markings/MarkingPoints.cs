using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Humanoid.Markings;

[DataDefinition]
[Serializable, NetSerializable]
public sealed partial class MarkingPoints
{
    [DataField(required: true)]
    public int Points;

    [DataField(required: true)]
    public bool Required;

    [DataField]
    public bool OnlyWhitelisted;

    [DataField]
    public List<ProtoId<MarkingPrototype>> DefaultMarkings = new();

    public static Dictionary<MarkingCategories, MarkingPoints> CloneMarkingPointDictionary(
        Dictionary<MarkingCategories, MarkingPoints> source)
    {
        var clone = new Dictionary<MarkingCategories, MarkingPoints>();

        foreach (var (category, points) in source)
        {
            clone[category] = new MarkingPoints
            {
                Points = points.Points,
                Required = points.Required,
                OnlyWhitelisted = points.OnlyWhitelisted,
                DefaultMarkings = new(points.DefaultMarkings),
            };
        }

        return clone;
    }
}

[Prototype]
public sealed partial class MarkingPointsPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public bool OnlyWhitelisted;

    [DataField(required: true)]
    public Dictionary<MarkingCategories, MarkingPoints> Points { get; private set; } = default!;
}
