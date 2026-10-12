using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Threats.Mobs.Wendigo.Abilities;

/// <summary>
/// Clawed Sweep: a telegraphed cone in front of the Wendigo. After <see cref="Delay"/> every non-allied
/// living mob in it takes rending slash damage, is knocked down and drops what it holds.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUClawedSweepComponent : Component
{
    [DataField]
    public float Range = 6f;

    [DataField]
    public Angle ConeAngle = Angle.FromDegrees(100);

    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(1);

    [DataField]
    public DamageSpecifier Damage = new()
    {
        DamageDict = { ["Slash"] = 45 },
    };

    /// <summary>Tier passed to <see cref="DamageImpact.XenoRendingSlash"/>, so CMU wounds make lacerations.</summary>
    [DataField]
    public int RendingTier = 3;

    [DataField]
    public TimeSpan KnockdownTime = TimeSpan.FromSeconds(2);

    [DataField]
    public bool DropHeldItems = true;

    [DataField]
    public EntProtoId? TelegraphEffect = "RMCEffectXenoTelegraphRedSlow";

    [DataField]
    public SoundSpecifier? WindupSound = new SoundPathSpecifier("/Audio/_RMC14/Xeno/alien_tail_swipe1.ogg");

    [DataField]
    public SoundSpecifier? SweepSound = new SoundPathSpecifier("/Audio/_RMC14/Weapons/alien_claw_swipe.ogg");
}
