using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Round.Antags.EarlyWarning.Abilities;

/// <summary>
/// THE LIGHT (phase E6, balance A-14), granted by the level-5 table beside an orange
/// <c>PointLight</c>. Organic people within <see cref="Range"/> who face the Joe and can see it fill an
/// envelope over <see cref="FillTime"/>; a full envelope paralyzes them and damages the brain, then
/// leaves them immune for <see cref="Immunity"/>. Looking away drains it over <see cref="DecayTime"/>.
/// </summary>
[RegisterComponent]
public sealed partial class EWJoeLightComponent : Component
{
    [DataField]
    public float Range = 7f;

    /// <summary>Gaze time that fills the envelope; the telegraph (rebalance RB-22: 5 s).</summary>
    [DataField]
    public TimeSpan FillTime = TimeSpan.FromSeconds(5);

    [DataField]
    public TimeSpan DecayTime = TimeSpan.FromSeconds(2);

    [DataField]
    public TimeSpan TickInterval = TimeSpan.FromSeconds(0.25);

    /// <summary>Paralyze when the envelope fills (rebalance RB-22: 2.5 s, inside the area-CC norm).</summary>
    [DataField]
    public TimeSpan Paralyze = TimeSpan.FromSeconds(2.5);

    [DataField]
    public float BrainDamage = 8f;

    /// <summary>How long a filled envelope leaves its victim immune (rebalance RB-22: 20 s).</summary>
    [DataField]
    public TimeSpan Immunity = TimeSpan.FromSeconds(20);

    [DataField]
    public TimeSpan NextTick;
}

/// <summary>
/// On someone caught in THE LIGHT: how full their envelope is. Only their own client receives it; it
/// drives the cable vignette over their screen.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class EWJoeEnvelopedComponent : Component
{
    public override bool SendOnlyToOwner => true;

    /// <summary>0 to 1; dirtied in tenths so the overlay grows without a state every tick.</summary>
    [DataField, AutoNetworkedField]
    public float Progress;

    /// <summary>The Joe whose light this is. Server bookkeeping.</summary>
    [DataField]
    public EntityUid Source;

    [DataField]
    public TimeSpan ImmuneUntil;

    /// <summary>
    /// Text pass A: the last third of the envelope the victim was told about (0 none, 1 hearing,
    /// 2 wondering); the full envelope tells the rest. Server bookkeeping, not networked.
    /// </summary>
    [DataField]
    public int Stage;
}
