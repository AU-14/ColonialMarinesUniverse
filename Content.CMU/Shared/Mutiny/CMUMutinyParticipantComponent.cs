using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.CMU14.Mutiny;

[Serializable, NetSerializable]
public enum CMUMutinySide : byte
{
    Mutineer,
    Leader,
    Loyalist,
    NonCombatant,
}

// which side someone openly took. sworn-but-hidden recruits don't get this, it's networked to everyone
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUMutinyParticipantComponent : Component
{
    [DataField, AutoNetworkedField]
    public CMUMutinySide Side;

    [DataField]
    public Dictionary<CMUMutinySide, SpriteSpecifier> Icons = new()
    {
        [CMUMutinySide.Mutineer] = new SpriteSpecifier.Rsi(new ResPath("/Textures/CMU14/Interface/mutiny_hud.rsi"), "mutineer"),
        [CMUMutinySide.Leader] = new SpriteSpecifier.Rsi(new ResPath("/Textures/CMU14/Interface/mutiny_hud.rsi"), "mutineer_leader"),
        [CMUMutinySide.Loyalist] = new SpriteSpecifier.Rsi(new ResPath("/Textures/CMU14/Interface/mutiny_hud.rsi"), "loyalist"),
        [CMUMutinySide.NonCombatant] = new SpriteSpecifier.Rsi(new ResPath("/Textures/CMU14/Interface/mutiny_hud.rsi"), "noncombatant"),
    };
}
