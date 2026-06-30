using Content.Shared._RMC14.Language.Prototypes;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Language;

[RegisterComponent]
public sealed partial class TranslatorDeviceComponent : Component
{
    [DataField]
    public List<ProtoId<LanguagePrototype>> SpokenLanguages { get; set; } = new();

    [DataField]
    public List<ProtoId<LanguagePrototype>> UnderstoodLanguages { get; set; } = new();
}