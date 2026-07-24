namespace Content.Shared._CMU14.Compatibility;

/// <summary>
/// Deserialization-only schemas retained for component deltas embedded in the
/// pre-rebase USS Bush maps. No systems process these compatibility components.
/// </summary>
[RegisterComponent]
public sealed partial class DayNightCycleComponent : Component
{
    [DataField("cycleDuration")]
    public float CycleDurationMinutes = 45f;

    [DataField("timeEntries")]
    public List<LegacyBushTimeEntry> TimeEntries = [];
}

[DataDefinition]
public sealed partial class LegacyBushTimeEntry
{
    [DataField("colorHex")]
    public string ColorHex = "#FFFFFF";

    [DataField("time")]
    public float Time;
}

[RegisterComponent]
[ComponentProtoName("LegacyBushOrganHealth")]
public sealed partial class OrganHealthComponent : Component
{
    [DataField]
    public TimeSpan NextRegenTick;
}

[RegisterComponent]
[ComponentProtoName("LegacyBushHeart")]
public sealed partial class HeartComponent : Component
{
    [DataField]
    public TimeSpan NextPulseUpdate;
}

[RegisterComponent]
[ComponentProtoName("LegacyBushKidneys")]
public sealed partial class KidneysComponent : Component
{
    [DataField]
    public TimeSpan NextSelfDamageTick;
}

[RegisterComponent]
[ComponentProtoName("LegacyBushLiver")]
public sealed partial class LiverComponent : Component
{
    [DataField]
    public TimeSpan NextSelfDamageTick;
}

[RegisterComponent]
[ComponentProtoName("LegacyBushLungs")]
public sealed partial class LungsComponent : Component
{
    [DataField]
    public TimeSpan NextAsphyxTick;
}

[RegisterComponent]
[ComponentProtoName("LegacyBushStomach")]
public sealed partial class CMUStomachComponent : Component
{
    [DataField]
    public TimeSpan NextVomitCheck;
}
