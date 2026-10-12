using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Medical.Blood;

[Serializable, NetSerializable]
public enum CMUBloodType : byte
{
    OPositive,
    ONegative,
    APositive,
    ANegative,
    BPositive,
    BNegative,
    ABPositive,
    ABNegative,
}

public static class CMUBloodTypes
{
    /// <summary>Every type except O-, the universal donor, which players have to pick themselves.</summary>
    public static readonly CMUBloodType[] DefaultTypes =
    [
        CMUBloodType.OPositive,
        CMUBloodType.APositive,
        CMUBloodType.ANegative,
        CMUBloodType.BPositive,
        CMUBloodType.BNegative,
        CMUBloodType.ABPositive,
        CMUBloodType.ABNegative,
    ];

    /// <summary>"O-", "AB+" and so on.</summary>
    public static string Name(CMUBloodType type) => type switch
    {
        CMUBloodType.OPositive => "O+",
        CMUBloodType.ONegative => "O-",
        CMUBloodType.APositive => "A+",
        CMUBloodType.ANegative => "A-",
        CMUBloodType.BPositive => "B+",
        CMUBloodType.BNegative => "B-",
        CMUBloodType.ABPositive => "AB+",
        CMUBloodType.ABNegative => "AB-",
        _ => "?",
    };

    /// <summary>The blood pack sprite's label state for this type.</summary>
    public static string LabelState(CMUBloodType type) => Name(type).ToLowerInvariant();

    /// <summary>Whether someone with <paramref name="recipient"/> blood can take <paramref name="donor"/> blood.</summary>
    public static bool CanReceive(CMUBloodType recipient, CMUBloodType donor)
    {
        var (recipientA, recipientB, recipientRh) = Antigens(recipient);
        var (donorA, donorB, donorRh) = Antigens(donor);
        return (!donorA || recipientA) && (!donorB || recipientB) && (!donorRh || recipientRh);
    }

    /// <summary>
    /// Whether someone with <paramref name="recipient"/> blood can take plasma from a <paramref name="donor"/>. Plasma
    /// carries the donor's antibodies, so it runs the other way to red cells: AB plasma suits anyone and O plasma only
    /// O. Rh doesn't matter.
    /// </summary>
    public static bool CanReceivePlasma(CMUBloodType recipient, CMUBloodType donor)
    {
        var (recipientA, recipientB, _) = Antigens(recipient);
        var (donorA, donorB, _) = Antigens(donor);
        return (!recipientA || donorA) && (!recipientB || donorB);
    }

    private static (bool A, bool B, bool Rh) Antigens(CMUBloodType type) => type switch
    {
        CMUBloodType.OPositive => (false, false, true),
        CMUBloodType.ONegative => (false, false, false),
        CMUBloodType.APositive => (true, false, true),
        CMUBloodType.ANegative => (true, false, false),
        CMUBloodType.BPositive => (false, true, true),
        CMUBloodType.BNegative => (false, true, false),
        CMUBloodType.ABPositive => (true, true, true),
        CMUBloodType.ABNegative => (true, true, false),
        _ => (true, true, true),
    };

    public static CMUBloodType RandomDefault(System.Random random) => DefaultTypes[random.Next(DefaultTypes.Length)];

    /// <summary>A stable default for a profile saved before blood types existed, so it doesn't change every round.</summary>
    public static CMUBloodType StableDefault(string seed)
    {
        var hash = 0;
        foreach (var c in seed)
            hash = unchecked(hash * 31 + c);

        return DefaultTypes[(int) ((uint) hash % (uint) DefaultTypes.Length)];
    }

    public static bool TryParse(string? value, out CMUBloodType type)
    {
        type = default;
        return !string.IsNullOrWhiteSpace(value) && Enum.TryParse(value, out type) && Enum.IsDefined(type);
    }
}
