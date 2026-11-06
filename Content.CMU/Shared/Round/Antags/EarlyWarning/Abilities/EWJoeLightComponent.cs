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

    [DataField]
    public TimeSpan FillTime = TimeSpan.FromSeconds(4);

    [DataField]
    public TimeSpan DecayTime = TimeSpan.FromSeconds(2);

    [DataField]
    public TimeSpan TickInterval = TimeSpan.FromSeconds(0.25);

    [DataField]
    public TimeSpan Paralyze = TimeSpan.FromSeconds(4);

    [DataField]
    public float BrainDamage = 8f;

    [DataField]
    public TimeSpan Immunity = TimeSpan.FromSeconds(10);

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
