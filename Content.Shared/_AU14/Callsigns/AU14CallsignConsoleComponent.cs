namespace Content.Shared._AU14.Callsigns;

[RegisterComponent]
public sealed partial class AU14CallsignConsoleComponent : Component
{
    [DataField(required: true)]
    public string Faction = string.Empty;
}
