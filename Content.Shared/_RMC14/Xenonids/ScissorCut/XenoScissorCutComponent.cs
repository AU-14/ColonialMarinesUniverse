using Content.Shared.Chat.Prototypes;
using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Content.Shared.FixedPoint;

namespace Content.Shared._RMC14.Xenonids.ScissorCut;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class XenoScissorCutComponent : Component
{
    [DataField, AutoNetworkedField]
    public DamageSpecifier Damage = new();

    [DataField, AutoNetworkedField]
    public FixedPoint2 Bloodloss = FixedPoint2.Zero; // CMU14 Bloodloss System for Bloodletter Venator, unused by Ravager

    [DataField, AutoNetworkedField]
    public float Range = 4;

    [DataField, AutoNetworkedField]
    public TimeSpan SuperSlowDuration = TimeSpan.FromSeconds(5);

    [DataField, AutoNetworkedField]
    public SoundSpecifier SlashSound = new SoundCollectionSpecifier("AlienClaw");

    [DataField, AutoNetworkedField]
    public ProtoId<EmotePrototype> Emote = "XenoRoar";

    [DataField, AutoNetworkedField]
    public EntProtoId AttackEffect = "RMCEffectExtraSlash";

    [DataField, AutoNetworkedField]
    public EntProtoId TelegraphEffect = "RMCEffectXenoTelegraphRed";

    [DataField, AutoNetworkedField]
    public EntProtoId TelegraphEffectEdge = "RMCEffectXenoTelegraphRedSmall";
}
