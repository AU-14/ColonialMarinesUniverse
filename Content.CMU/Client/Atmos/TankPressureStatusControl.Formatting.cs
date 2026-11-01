namespace Content.Client.Atmos.UI;

public sealed partial class TankPressureStatusControl
{
    private readonly ILocalizationManager _cmuLocalization = IoCManager.Resolve<ILocalizationManager>();
    private string _cmuPressureText = string.Empty;

    private string CMUFormatPressure(float pressure)
    {
        // Use the formatter itself: rounding raw pressure can disagree with F1 at boundaries.
        Span<char> text = stackalloc char[64];
        if (!pressure.TryFormat(text, out var length, "F1"))
            return _cmuPressureText = pressure.ToString("F1");

        var formatted = text[..length];
        if (!formatted.SequenceEqual(_cmuPressureText.AsSpan()))
            _cmuPressureText = new string(formatted);

        return _cmuPressureText;
    }
}
