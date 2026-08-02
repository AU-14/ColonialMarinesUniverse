using Content.Shared.AU14.Allegiance;
using Content.Shared.AU14.Origin;
using Content.Shared.Preferences;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.CharacterDescription;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CharacterDescriptionComponent : Component
{
    [DataField, AutoNetworkedField]
    public string ShortExamine = string.Empty;

    [DataField, AutoNetworkedField]
    public string FullDescription = string.Empty;

    [DataField, AutoNetworkedField]
    public string MedicalRecord = string.Empty;

    [DataField, AutoNetworkedField]
    public string CriminalRecord = string.Empty;

    [DataField, AutoNetworkedField]
    public string GeneralRecord = string.Empty;

    [DataField, AutoNetworkedField]
    public List<string> DisabilityTraitNames = new();

    [DataField, AutoNetworkedField]
    public bool HasDrugAllergyTrait;

    [DataField, AutoNetworkedField]
    public string Height = string.Empty;

    [DataField, AutoNetworkedField]
    public int Weight = 160;

    [DataField, AutoNetworkedField]
    public BuildType Build = BuildType.Average;

    [DataField, AutoNetworkedField]
    public int Age = 18;

    [DataField, AutoNetworkedField]
    public ProtoId<AllegiancePrototype>? Allegiance;

    [DataField, AutoNetworkedField]
    public ProtoId<OriginPrototype>? Origin;

    [DataField, AutoNetworkedField]
    public bool HideMetaInformation;
}
