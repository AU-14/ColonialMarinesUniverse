using Content.Shared._RMC14.Language.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Language;

/// <summary>
/// Put on a planet prototype (next to its <c>RMCPlanetMapPrototype</c>) to set the languages that colony's
/// colonists, corporate staff and CLF spawn with. The default language becomes the one they speak first.
/// </summary>
[RegisterComponent]
public sealed partial class CMUColonyLanguageComponent : Component
{
    /// <summary>
    /// The language colonists speak by default. Always spoken and understood.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<LanguagePrototype> DefaultLanguage;

    /// <summary>
    /// Further languages colonists speak and understand.
    /// </summary>
    [DataField]
    public List<ProtoId<LanguagePrototype>> Languages = new();
}
