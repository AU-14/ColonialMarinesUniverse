using Content.Shared.NPC.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._RMC14.Marines;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
//[Access(typeof(SharedMarineSystem))]
public sealed partial class MarineComponent : Component
{
    [DataField, AutoNetworkedField]
    public SpriteSpecifier? Icon;
    [DataField("Faction"), AutoNetworkedField]
    public string? Faction { get; set; } = "marine";

    [DataField, AutoNetworkedField]
    public Dictionary<ProtoId<NpcFactionPrototype>, SpriteSpecifier> GenericFactionIcons = new()
    {
        { "UNMC", new SpriteSpecifier.Rsi(new("/Textures/_RMC14/Interface/faction_icons.rsi"), "unmc") },
        { "CLF", new SpriteSpecifier.Rsi(new("/Textures/_RMC14/Interface/faction_icons.rsi"), "clf") },
//      { "GOVFOR", new SpriteSpecifier.Rsi(new("/Textures/_RMC14/Interface/faction_icons.rsi"), "govfor") },
//      { "OPFOR", new SpriteSpecifier.Rsi(new("/Textures/_RMC14/Interface/faction_icons.rsi"), "opfor") },
        { "RoyalMarines", new SpriteSpecifier.Rsi(new("/Textures/_RMC14/Interface/faction_icons.rsi"), "tse") },
        { "TSE", new SpriteSpecifier.Rsi(new("/Textures/_RMC14/Interface/faction_icons.rsi"), "tse") },
        { "SPP", new SpriteSpecifier.Rsi(new ("/Textures/_RMC14/Interface/faction_icons.rsi"), "spp") },
        { "AUUpp", new SpriteSpecifier.Rsi(new ("/Textures/_RMC14/Interface/faction_icons.rsi"), "spp") },
        { "WeYa", new SpriteSpecifier.Rsi(new ("/Textures/_RMC14/Interface/faction_icons.rsi"), "weya") },
        { "AUWeYu", new SpriteSpecifier.Rsi(new ("/Textures/_RMC14/Interface/faction_icons.rsi"), "weya") },
    };
}
