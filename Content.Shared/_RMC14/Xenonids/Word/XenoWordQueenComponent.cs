using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Xenonids.Word;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(XenoWordQueenSystem))]
public sealed partial class XenoWordQueenComponent : Component
{
    [DataField, AutoNetworkedField]
    public FixedPoint2 PlasmaCost = 50;

    [DataField, AutoNetworkedField]
    public SoundSpecifier Sound = new SoundCollectionSpecifier("XenoQueenCommand", AudioParams.Default.WithVolume(-6));

    [DataField, AutoNetworkedField]
    public string Header = "rmc-xeno-words-of-the-queen-header";

    [DataField, AutoNetworkedField]
    public Color MessageColor = Color.Red;
}