using Content.Shared.CCVar;
using Content.Shared.Temperature;
using Robust.Shared.Configuration;

namespace Content.Client.CMU14.Temperature;

/// <summary>
/// Client-side temperature formatting honoring cmu.temperature.fahrenheit.
/// All simulation values are Kelvin; this only decides what a readout says.
/// </summary>
public static class TemperatureDisplay
{
    public static bool Fahrenheit =>
        IoCManager.Resolve<IConfigurationManager>().GetCVar(CCVars.CMUTemperatureFahrenheit);

    public static string Unit => Fahrenheit ? "F" : "C";

    public static float FromKelvin(float kelvin) => Fahrenheit
        ? TemperatureHelpers.KelvinToFahrenheit(kelvin)
        : TemperatureHelpers.KelvinToCelsius(kelvin);
}
