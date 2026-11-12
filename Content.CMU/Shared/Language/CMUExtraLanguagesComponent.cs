using Content.Shared._RMC14.Language.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Language;

/// <summary>
/// Put in a job's <c>roundComponents</c> to give that job extra spoken and understood languages, on top of
/// whatever traits, origin, platoon and colony give it. Applied after the colony languages.
/// </summary>
[RegisterComponent]
public sealed partial class CMUExtraLanguagesComponent : Component
{
    [DataField]
    public List<ProtoId<LanguagePrototype>> Languages = new();

    /// <summary>
    /// If set, this becomes the character's default language (it is spoken too, even if not in <see cref="Languages"/>).
    /// Leave unset to keep the default the colony or platoon gives.
    /// </summary>
    [DataField]
    public ProtoId<LanguagePrototype>? DefaultLanguage;
}
