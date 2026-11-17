using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Yautja.BadBlood;

/// <summary>
/// A Bad Blood's clan code, like a xeno's XX number: other Bad Bloods see "Grunt (BB-12)" instead of "unknown", and
/// the radio says it too. Assigned once per body, unique for the round.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BadBloodCodeComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Number;
}
