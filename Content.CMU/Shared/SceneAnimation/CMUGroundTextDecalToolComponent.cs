using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.SceneAnimation;

[RegisterComponent]
public sealed partial class CMUGroundTextDecalToolComponent : Component
{
    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(1);

    public TimeSpan NextUseAt;
}

[Serializable, NetSerializable]
public enum CMUGroundTextDecalUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class CMUGroundTextDecalMessage(
    string text,
    float letterSpacing,
    float lineSpacing,
    float wonkiness,
    Color color) : BoundUserInterfaceMessage
{
    public string Text = text;
    public float LetterSpacing = letterSpacing;
    public float LineSpacing = lineSpacing;
    public float Wonkiness = wonkiness;
    public Color Color = color;
}
