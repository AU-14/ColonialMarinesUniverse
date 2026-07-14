using Content.Server.GameTicking.Presets;
using Content.Shared._RMC14.Rules;
using Content.Shared.AU14;
using Content.Shared._CMU14.Threats;
using Content.Shared.AU14.util;

namespace Content.Server.AU14.Round;

internal sealed class AuRoundSelectionState
{
    public GamePresetPrototype? SelectedPreset { get; set; }
    public RMCPlanetMapPrototypeComponent? SelectedPlanet { get; set; }
    public string? SelectedPlanetId { get; set; }
    public ThreatPrototype? SelectedThreat { get; set; }
    public string? SelectedGovforShip { get; set; }
    public string? SelectedOpforShip { get; set; }
    public List<ThirdPartyPrototype> SelectedThirdParties { get; } = new();
    public bool DistressSignalThirdPartiesLocked { get; set; }
    public bool DistressSignalThirdPartyFillCompleted { get; set; }
    public int DistressSignalSurvivorCount { get; set; }

    public void Reset()
    {
        SelectedPreset = null;
        SelectedPlanet = null;
        SelectedPlanetId = null;
        SelectedThreat = null;
        SelectedGovforShip = null;
        SelectedOpforShip = null;
        SelectedThirdParties.Clear();
        DistressSignalThirdPartiesLocked = false;
        DistressSignalThirdPartyFillCompleted = false;
        DistressSignalSurvivorCount = 0;
    }

    public void ResetDistressSignalThirdPartyLock()
    {
        if (DistressSignalThirdPartiesLocked)
            SelectedThirdParties.Clear();

        DistressSignalThirdPartiesLocked = false;
        DistressSignalThirdPartyFillCompleted = false;
        DistressSignalSurvivorCount = 0;
    }

    public void SetPlanet(string planetId, RMCPlanetMapPrototypeComponent planet)
    {
        SelectedPlanetId = planetId;
        SelectedPlanet = planet;
    }
}
