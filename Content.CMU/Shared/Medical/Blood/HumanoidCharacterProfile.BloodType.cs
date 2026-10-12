using Content.Shared.CMU14.Medical.Blood;

namespace Content.Shared.Preferences;

// CMU14: the character's blood type, picked in character creation.
public sealed partial class HumanoidCharacterProfile
{
    [DataField]
    public CMUBloodType BloodType { get; private set; } = CMUBloodTypes.RandomDefault(new Random());

    public HumanoidCharacterProfile WithBloodType(CMUBloodType bloodType)
    {
        return new(this) { BloodType = Enum.IsDefined(bloodType) ? bloodType : CMUBloodTypes.DefaultTypes[0] };
    }
}
