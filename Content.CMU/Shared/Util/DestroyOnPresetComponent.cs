namespace Content.Shared.CMU14.util;

[RegisterComponent]
public sealed partial class DestroyOnPresetComponent : Component
{
    [DataField]
    public string Preset { get; set; } = string.Empty;

    /// <summary>
    /// Additional presets to destroy on. The entity is destroyed when the current preset
    /// matches <see cref="Preset"/> or any entry in this list, subject to <see cref="Inverted"/>.
    /// </summary>
    [DataField]
    public List<string> Presets { get; set; } = new();

    /// <summary>
    /// If true, invert the behavior: remove on all presets except those specified in
    /// <see cref="Preset"/> and <see cref="Presets"/>.
    /// When false (default), remove only when the current preset matches <see cref="Preset"/> or <see cref="Presets"/>.
    /// </summary>
    [DataField]
    public bool Inverted { get; set; } =  false;
}
