using Content.Shared.Humanoid;
using Content.Shared.Clothing;
using Content.Shared.Preferences;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Roles;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Lobby;

/// <summary>
/// Public appearance of a ready character. Never send the full saved profile: it contains
/// records, antagonist preferences, and other information unrelated to the lobby preview.
/// </summary>
[Serializable, NetSerializable]
public sealed class LobbyLineupEntry
{
    public NetUserId UserId { get; }
    public string Name { get; }
    public string Species { get; }
    public Sex Sex { get; }
    public ArmorPreference ArmorPreference { get; }
    public HumanoidCharacterAppearance Appearance { get; }
    public ProtoId<JobPrototype>? Job { get; }
    public string? LoadoutKey { get; }
    public RoleLoadout? Loadout { get; }
    public string SectionId { get; }
    public string SectionName { get; }
    public Color Color { get; }
    public int Order { get; }
    public int? ChatKey { get; }

    public LobbyLineupEntry(NetUserId userId, HumanoidCharacterProfile profile,
        ProtoId<JobPrototype>? job, string? loadoutKey, RoleLoadout? loadout,
        string sectionId, string sectionName, Color color, int order, int? chatKey = null)
    {
        UserId = userId;
        Name = profile.Name;
        Species = profile.Species;
        Sex = profile.Sex;
        ArmorPreference = profile.ArmorPreference;
        Appearance = profile.Appearance;
        Job = job;
        LoadoutKey = loadoutKey;
        Loadout = loadout?.Clone();
        SectionId = sectionId;
        SectionName = sectionName;
        Color = color;
        Order = order;
        ChatKey = chatKey;
    }

    public HumanoidCharacterProfile ToPreviewProfile()
    {
        var profile = new HumanoidCharacterProfile()
            .WithName(Name)
            .WithSpecies(Species)
            .WithSex(Sex)
            .WithArmorPreference(ArmorPreference)
            .WithCharacterAppearance(Appearance);
        return LoadoutKey != null && Loadout != null ? profile.WithLoadout(LoadoutKey, Loadout) : profile;
    }

    public bool SamePreview(LobbyLineupEntry other)
    {
        return Name == other.Name && Species == other.Species && Sex == other.Sex && ArmorPreference == other.ArmorPreference &&
               Appearance.Equals(other.Appearance) && Job == other.Job &&
               LoadoutKey == other.LoadoutKey && Equals(Loadout, other.Loadout);
    }
}
