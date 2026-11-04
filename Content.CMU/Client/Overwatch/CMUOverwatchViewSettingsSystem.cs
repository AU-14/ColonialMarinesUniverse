using Content.Shared._RMC14.Overwatch;

namespace Content.Client.CMU14.Overwatch;

/// <summary>
/// The overwatch filters this player last used: location shown, dead and hidden marines, and the
/// squad info summary. Kept for the session so closing and reopening a console keeps them.
/// </summary>
public readonly record struct CMUOverwatchViewSettings(
    OverwatchLocation? Location,
    bool ShowDead,
    bool ShowHidden,
    bool SquadInfoHidden);

public sealed class CMUOverwatchViewSettingsSystem : EntitySystem
{
    public CMUOverwatchViewSettings? Settings { get; set; }
}
