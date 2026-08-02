using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using System;
using System.Collections.Generic;
using System.Text;

namespace Content.Shared._AU14.Chemistry.Research;

[RegisterComponent, AutoGenerateComponentState, NetworkedComponent]
public sealed partial class XRFScannerComponent : Component
{
    [AutoNetworkedField]
    public bool Processing = false;

    [AutoNetworkedField]
    public NetEntity LastUser = NetEntity.Invalid;

    [AutoNetworkedField]
    public int Sample = 0;

    //how long it takes to process a sample
    [DataField, AutoNetworkedField]
    public TimeSpan Inefficiency = TimeSpan.FromSeconds(10);

    [DataField]
    public SoundPathSpecifier PrintSound = new("/Audio/_RMC14/Machines/fax.ogg");

    [DataField]
    public SoundPathSpecifier SuccessSound = new("/Audio/_CMU14/Machines/twobeep.ogg");

    [DataField]
    public SoundPathSpecifier FailSound = new("/Audio/_RMC14/Machines/buzz_two.ogg");

    [DataField("faction")]
    // who does this XRF belong to?
    // i.e. who gets the money.
    // if the string is empty then it spits out the money as dollars, rounded.
    public string Faction = string.Empty;
}
[Serializable, NetSerializable]
public enum XRFScannerVisuals
{
    State,
}

[Serializable, NetSerializable]
public enum XRFScannerState
{
    Scanner,
    Sample,
    Processing,
    Finished,
    Error,
    Failed,
}
