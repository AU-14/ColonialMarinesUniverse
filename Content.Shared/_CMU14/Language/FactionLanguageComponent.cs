using Content.Shared._RMC14.Language.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Language;

[RegisterComponent, NetworkedComponent]
public sealed partial class FactionLanguageLeaderComponent : Component
{
    [DataField(required: true)]
    public string FactionTag = string.Empty;

    [DataField]
    public ProtoId<LanguagePrototype>? ChosenLanguage;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class FactionLanguageMemberComponent : Component
{
    [DataField(required: true)]
    public string FactionTag = string.Empty;

    [DataField]
    public bool LanguageApplied;
}

[Serializable, NetSerializable]
public enum FactionLanguageUiKey
{
    Key
}

[Serializable, NetSerializable]
public sealed class FactionLanguagePickerState : BoundUserInterfaceState
{
    public List<string> Languages;
    public string FactionTag;

    public FactionLanguagePickerState(List<string> languages, string factionTag)
    {
        Languages = languages;
        FactionTag = factionTag;
    }
}

[Serializable, NetSerializable]
public sealed class FactionLanguagePickedMessage : BoundUserInterfaceMessage
{
    public ProtoId<LanguagePrototype> Language;

    public FactionLanguagePickedMessage(ProtoId<LanguagePrototype> language)
    {
        Language = language;
    }
}