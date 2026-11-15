namespace Content.Server.CMU14.ZLevels.Weather;

/// <summary>
/// Additional floors of a game map share its ground map's weather.
/// Roofs and tiles above still decide where that weather shows on each floor.
/// </summary>
[RegisterComponent, UnsavedComponent, Access(typeof(CMUZLevelWeatherSystem))]
public sealed partial class CMUZLevelWeatherComponent : Component
{
    [DataField]
    public EntityUid Source;
}
