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

    /// <summary>
    ///     The body of a Queen's message. Neutral, not red: the heading above it carries the xeno
    ///     violet, and a whole announcement set in pure red measured 3.6:1 on its own band.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Color MessageColor = Color.FromHex("#D6DCE0");
}