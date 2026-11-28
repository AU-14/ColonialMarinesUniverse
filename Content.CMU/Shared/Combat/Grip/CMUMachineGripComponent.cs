using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Combat.Grip;

/// <summary>
/// N5 (FLOW-02) Machine Grip. The body's first Ctrl-click on a valid target telegraphs and then goes straight to an
/// aggressive grab (the server's <c>CMUMachineGripSystem</c>). While the component exists the body's manual upgrade
/// waits as long as the telegraph, its grabs impose <see cref="EscapeMultiplier"/> on the victim's struggle and, with
/// <see cref="XenoGrab"/>, it can grab xenos up to tier 3 and size Big under a hold cap and a cooldown. Values the mob
/// had of its own are recorded when the component starts and put back when it goes. The defaults are the Early Warning
/// Joe's at level 0; its level table raises the escape multiplier and turns the xeno grab on by level.
/// </summary>
[RegisterComponent]
public sealed partial class CMUMachineGripComponent : Component
{
    /// <summary>The windup between the pull and the aggressive grab; the victim breaks it by moving away.</summary>
    [DataField]
    public TimeSpan Telegraph = TimeSpan.FromSeconds(0.4);

    /// <summary>
    /// The windup against a large xeno (RMC size Big: the grabbable T3s such as the praetorian, boiler and despoiler)
    /// is this many times <see cref="Telegraph"/>; the manual Ctrl-click upgrade waits just as long.
    /// </summary>
    [DataField]
    public float LargeXenoMultiplier = 1.5f;

    /// <summary>How far apart the body and the victim may get during the windup.</summary>
    [DataField]
    public float TelegraphRange = 1.5f;

    /// <summary>Break-free time multiplier the body's grabs impose. 1 leaves the struggle unchanged.</summary>
    [DataField]
    public float EscapeMultiplier = 1f;

    /// <summary>
    /// The body can aggressively grab xenos up to tier 3 and size Big, never the heavies (CMUXenoGrabberComponent), with
    /// pull immunity copied from <see cref="XenoPullImmunity"/>.
    /// </summary>
    [DataField]
    public bool XenoGrab;

    /// <summary>An aggressive grab on a xeno is released after this long.</summary>
    [DataField]
    public TimeSpan XenoHoldCap = TimeSpan.FromSeconds(3);

    /// <summary>After a xeno grab ends, no xeno can be aggressively grabbed again for this long.</summary>
    [DataField]
    public TimeSpan XenoCooldown = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Entity prototype whose ParalyzeImmuneFrom entry is copied onto a body that lacks one while <see cref="XenoGrab"/>
    /// is on, so pulling a small caste does not paralyze it. The synthetic add-list whitelists the same RMCSynthGrabbable castes.
    /// </summary>
    [DataField]
    public EntProtoId XenoPullImmunity = "RMCSynthAddComponents";

    // ---- server bookkeeping ----

    /// <summary>The <see cref="EscapeMultiplier"/> the component started with, for an owner that resets its overrides.</summary>
    [ViewVariables]
    public float BaseEscapeMultiplier = 1f;

    /// <summary>The <see cref="XenoGrab"/> the component started with, for an owner that resets its overrides.</summary>
    [ViewVariables]
    public bool BaseXenoGrab;

    /// <summary>The mob's own CanFiremanCarry.AggressiveGrabDelay, restored when the component goes.</summary>
    [DataField]
    public TimeSpan? OriginalAggressiveGrabDelay;

    /// <summary>The mob's own escape multiplier when it had one before the grip. Restored when the component goes.</summary>
    [DataField]
    public float? OriginalEscapeMultiplier;

    [DataField]
    public bool AddedEscapeMultiplier;

    [DataField]
    public bool AddedXenoGrabber;

    [DataField]
    public bool AddedParalyzeImmuneFrom;
}
