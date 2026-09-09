using System.Globalization;
using Content.Shared.FixedPoint;
using JetBrains.Annotations;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Validation;
using Robust.Shared.Serialization.Markdown.Value;
using Robust.Shared.Serialization.TypeSerializers.Interfaces;

namespace Content.Shared.Radio;

/// <summary>
/// A radio carrier frequency stored as an exact number of kilohertz.
/// </summary>
/// <remarks>
/// Radio prototypes are written as MHz, while direct tuning and direction finding require 1 kHz precision.
/// Keeping the unit in the type prevents the historical 1471 / 147.1 / 1.471 scaling ambiguity.
/// </remarks>
[DataRecord, Serializable, NetSerializable]
public readonly partial record struct RadioFrequency : IComparable<RadioFrequency>
{
    [DataField]
    public readonly int Kilohertz;

    public static RadioFrequency Off => default;

    private RadioFrequency(int kilohertz)
    {
        Kilohertz = kilohertz;
    }

    public static RadioFrequency FromKilohertz(int kilohertz)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(kilohertz);
        return new RadioFrequency(kilohertz);
    }

    /// <summary>
    /// Converts a two-decimal MHz value into exact kHz. This conversion is intentionally explicit and named.
    /// </summary>
    public static RadioFrequency FromMegahertz(FixedPoint2 megahertz)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(megahertz.Value);
        return FromKilohertz(checked(megahertz.Value * 10));
    }

    public static RadioFrequency ParseMegahertz(string text)
    {
        if (!TryParseMegahertz(text, out var frequency))
            throw new ArgumentException($"'{text}' is not a non-negative MHz value with at most three decimal places.", nameof(text));

        return frequency;
    }

    public static bool TryParseMegahertz(string? text, out RadioFrequency frequency)
    {
        frequency = Off;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var span = text.AsSpan().Trim();
        var decimalPoint = span.IndexOf('.');
        if (decimalPoint >= 0 && span.Length - decimalPoint - 1 > 3)
            return false;

        if (
            !decimal.TryParse(
                span,
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out var megahertz) ||
            megahertz < 0)
        {
            return false;
        }

        var kilohertz = megahertz * 1000m;
        if (kilohertz != decimal.Truncate(kilohertz) || kilohertz > int.MaxValue)
            return false;

        frequency = FromKilohertz(decimal.ToInt32(kilohertz));
        return true;
    }

    public RadioFrequency OffsetKilohertz(int offset)
        => FromKilohertz(checked(Kilohertz + offset));

    public int DistanceKilohertzTo(RadioFrequency other)
        => checked(other.Kilohertz - Kilohertz);

    public string FormatMegahertz(bool trimTrailingZeros = false)
    {
        var value = Kilohertz / 1000m;
        return value.ToString(trimTrailingZeros ? "0.###" : "0.000", CultureInfo.InvariantCulture);
    }

    public int CompareTo(RadioFrequency other) => Kilohertz.CompareTo(other.Kilohertz);

    public override string ToString() => FormatMegahertz();

    public static bool operator <(RadioFrequency left, RadioFrequency right) => left.Kilohertz < right.Kilohertz;
    public static bool operator <=(RadioFrequency left, RadioFrequency right) => left.Kilohertz <= right.Kilohertz;
    public static bool operator >(RadioFrequency left, RadioFrequency right) => left.Kilohertz > right.Kilohertz;
    public static bool operator >=(RadioFrequency left, RadioFrequency right) => left.Kilohertz >= right.Kilohertz;
}

/// <summary>
/// Explicit compatibility adapters for the two legacy frequency-entry screens.
/// Decimal input is always canonical MHz.
/// </summary>
public static class RadioFrequencyInput
{
    /// <summary>
    /// AN/PRC digit-only input historically used tenths of a MHz: 2592 means 259.200 MHz.
    /// </summary>
    public static bool TryParseAnprcScreenInput(string? text, out RadioFrequency frequency)
    {
        if (TryParseDigits(text, out var legacyTenths))
        {
            if (legacyTenths > int.MaxValue / 100)
            {
                frequency = RadioFrequency.Off;
                return false;
            }

            frequency = RadioFrequency.FromKilohertz(legacyTenths * 100);
            return true;
        }

        return RadioFrequency.TryParseMegahertz(text, out frequency);
    }

    /// <summary>
    /// Tunable-headset digit-only input historically used raw kHz: 87999 means 87.999 MHz.
    /// </summary>
    public static bool TryParseTunableScreenInput(string? text, out RadioFrequency frequency)
    {
        if (TryParseDigits(text, out var legacyKilohertz))
        {
            frequency = RadioFrequency.FromKilohertz(legacyKilohertz);
            return true;
        }

        return RadioFrequency.TryParseMegahertz(text, out frequency);
    }

    private static bool TryParseDigits(string? text, out int value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var span = text.AsSpan().Trim();
        foreach (var character in span)
        {
            if (!char.IsAsciiDigit(character))
                return false;
        }

        return int.TryParse(span, NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }
}

/// <summary>
/// Serializes radio frequencies as canonical MHz scalars in prototypes.
/// </summary>
[UsedImplicitly, TypeSerializer]
public sealed class RadioFrequencySerializer : ITypeSerializer<RadioFrequency, ValueDataNode>
{
    public ValidationNode Validate(
        ISerializationManager serializationManager,
        ValueDataNode node,
        IDependencyCollection dependencies,
        ISerializationContext? context = null)
        => RadioFrequency.TryParseMegahertz(node.Value, out _)
            ? new ValidatedValueNode(node)
            : new ErrorNode(node, "Expected a non-negative MHz value with at most three decimal places.");

    public RadioFrequency Read(
        ISerializationManager serializationManager,
        ValueDataNode node,
        IDependencyCollection dependencies,
        SerializationHookContext hookCtx,
        ISerializationContext? context = null,
        ISerializationManager.InstantiationDelegate<RadioFrequency>? instanceProvider = null)
        => RadioFrequency.ParseMegahertz(node.Value);

    public DataNode Write(
        ISerializationManager serializationManager,
        RadioFrequency value,
        IDependencyCollection dependencies,
        bool alwaysWrite = false,
        ISerializationContext? context = null)
        => serializationManager.WriteValue(
            value.FormatMegahertz(trimTrailingZeros: true),
            notNullableOverride: true);
}
