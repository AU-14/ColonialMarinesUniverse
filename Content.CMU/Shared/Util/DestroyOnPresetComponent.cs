namespace Content.Shared.AU14.util;

[RegisterComponent]
public sealed partial class DestroyOnPresetComponent : Component
{
    [DataField("preset")]
    public string Preset { get; set; } = string.Empty;

    /// <summary>
    /// If true, invert the behavior: remove on all presets except the one specified in <see cref="Preset"/>.
    /// When false (default), remove only when the current preset matches <see cref="Preset"/>.
    /// </summary>
    [DataField("inverted")]
    public bool Inverted { get; set; } =  false;
}
