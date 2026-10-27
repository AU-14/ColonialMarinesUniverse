using System.Globalization;
using Content.Shared._RMC14.IconLabel;

namespace Content.Client.CMU14.Storage;

/// <summary>
/// Presentation owned by one item control, never by a global entity cache.
/// </summary>
public sealed class CMUIconLabelPresentation
{
    private LocId? _id;
    private int _maxSize;
    private string? _colorName;
    private CultureInfo? _culture;
    private (string, object)[] _arguments = [];
    private string _text = "";
    private Color _color;
    private Color _fallbackColor;
    private bool _found;
    private bool _cacheable;
    private TimeSpan _refreshAt;

    public bool TryGet(IconLabelComponent label, ILocalizationManager localization, TimeSpan now,
        out string text, out Color color, Color fallbackColor = default)
    {
        if (_cacheable && now < _refreshAt && _id == label.LabelTextLocId &&
            _maxSize == label.LabelMaxSize && _colorName == label.TextColor && _fallbackColor == fallbackColor &&
            Equals(_culture, localization.DefaultCulture) && ArgumentsMatch(label.LabelTextParams))
        {
            text = _text;
            color = _color;
            return _found;
        }

        _id = label.LabelTextLocId;
        _maxSize = label.LabelMaxSize;
        _colorName = label.TextColor;
        _fallbackColor = fallbackColor;
        _culture = localization.DefaultCulture;
        if (_arguments.Length != label.LabelTextParams.Count)
            _arguments = new (string, object)[label.LabelTextParams.Count];
        label.LabelTextParams.CopyTo(_arguments);

        // Entity arguments and mutable objects may localize differently without
        // changing identity. Only cache immutable literal parameters.
        _cacheable = true;
        foreach (var (_, value) in _arguments)
        {
            if (value is not (null or string or bool or char or byte or sbyte or short or ushort or
                int or uint or long or ulong or float or double or decimal))
                _cacheable = false;
        }

        // Localization reload has no content event. Bound its display latency.
        _refreshAt = now + TimeSpan.FromSeconds(1);
        _text = "";
        _found = _id is { } id && localization.TryGetString(id, out _text!, _arguments);
        if (_found && _text.Length > _maxSize)
            _text = _text[.._maxSize];

        if (!Color.TryFromName(_colorName, out _color))
            _color = fallbackColor;
        text = _text;
        color = _color;
        return _found;
    }

    private bool ArgumentsMatch(List<(string, object)> arguments)
    {
        if (_arguments.Length != arguments.Count)
            return false;
        for (var i = 0; i < arguments.Count; i++)
        {
            if (_arguments[i].Item1 != arguments[i].Item1 || !Equals(_arguments[i].Item2, arguments[i].Item2))
                return false;
        }
        return true;
    }
}
