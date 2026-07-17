using Robust.Shared.Prototypes;
using Robust.Shared.Audio;

namespace Content.Shared._CMU14.Paper;

[RegisterComponent]
public sealed partial class UniversalPaperToolComponent : Component
{
    public const string PaperSlotId = "paper_slot";

    [DataField]
    public List<UniversalPaperToolTemplate> Templates = new();

    [DataField]
    public SoundSpecifier PrintSound = new SoundPathSpecifier("/Audio/Machines/printer.ogg");
}

[DataDefinition]
public sealed partial class UniversalPaperToolTemplate
{
    [DataField(required: true)]
    public EntProtoId Prototype;

    [DataField]
    public string? Name;

    [DataField]
    public string? Description;
}
