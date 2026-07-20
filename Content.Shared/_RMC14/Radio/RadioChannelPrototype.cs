// ReSharper disable CheckNamespace

namespace Content.Shared.Radio;

public sealed partial class RadioChannelPrototype
{
    [DataField]
    public Color? ColorblindColor;

    [DataField]
    public char RadioPrefix = ':';

    [DataField]
    public bool Tower;

    [DataField]
    public bool Planet = true;
}
