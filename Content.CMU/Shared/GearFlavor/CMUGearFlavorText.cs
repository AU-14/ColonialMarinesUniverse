using Content.Shared._RMC14.Marines.Roles.Ranks;
using Content.Shared.CMU14.Medical.Blood;
using Content.Shared.IdentityManagement;

namespace Content.Shared.CMU14.GearFlavor;

/// <summary>
///     Flavor text written on a character's armor or helmet, with the cmss13-devs/cmss13#9966 inserts:
///     %name% (the name others see, so hidden by a helmet), %bloodtype% (e.g. "O+") and %rank%
///     (the full rank, e.g. "Lieutenant").
/// </summary>
public static class CMUGearFlavorText
{
    public const int MaxLength = 100;

    public static string Clamp(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        text = text.Replace('\n', ' ').Replace('\r', ' ').Trim();
        return text.Length > MaxLength ? text[..MaxLength] : text;
    }

    public static string Fill(IEntityManager entities, EntityUid wearer, EntityUid? viewer, string text)
    {
        if (text.Contains("%name%"))
            text = text.Replace("%name%", Identity.Name(wearer, entities, viewer).Name);

        if (text.Contains("%bloodtype%"))
        {
            var bloodType = entities.TryGetComponent(wearer, out CMUBloodTypeComponent? blood)
                ? CMUBloodTypes.Name(blood.Type)
                : string.Empty;
            text = text.Replace("%bloodtype%", bloodType);
        }

        if (text.Contains("%rank%"))
        {
            var rank = entities.System<SharedRankSystem>().GetRankString(wearer);
            text = text.Replace("%rank%", rank ?? string.Empty);
        }

        return text;
    }
}
