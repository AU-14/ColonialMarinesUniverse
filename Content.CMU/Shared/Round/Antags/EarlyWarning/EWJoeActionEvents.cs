using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Round.Antags.EarlyWarning;

/// <summary>Test of Faith: flash and stun one chosen target that faces the Joe while the Joe faces it (playtest 3).</summary>
public sealed partial class EWJoeTestOfFaithActionEvent : EntityTargetActionEvent;

/// <summary>Optical Zoom: the bio-mask style focus far ahead in the facing direction (playtest 3).</summary>
public sealed partial class EWJoeOpticalZoomActionEvent : InstantActionEvent;

/// <summary>Opens the level and objective panel; a client-only action (playtest 3).</summary>
public sealed partial class EWJoeOpenStatusActionEvent : InstantActionEvent;

/// <summary>Disrupt: a small EMP pulse centred on the Joe.</summary>
public sealed partial class EWJoeDisruptActionEvent : InstantActionEvent;

/// <summary>Assimilate: start corrupting a whitelisted colony console (phase E2).</summary>
public sealed partial class EWJoeAssimilateActionEvent : EntityTargetActionEvent;

/// <summary>Raised on the Joe when the assimilation do-after ends; the do-after target is the console.</summary>
[Serializable, NetSerializable]
public sealed partial class EWJoeAssimilateDoAfterEvent : SimpleDoAfterEvent;

// Phase E4: level 1-2 abilities.

/// <summary>Drown: an aggressively grabbed organic is held under until its lungs fail (A-5).</summary>
public sealed partial class EWJoeDrownActionEvent : EntityTargetActionEvent;

[Serializable, NetSerializable]
public sealed partial class EWJoeDrownDoAfterEvent : SimpleDoAfterEvent;

/// <summary>Scour: digest a piece of colony technology for a timed bonus (A-6).</summary>
public sealed partial class EWJoeScourActionEvent : EntityTargetActionEvent;

[Serializable, NetSerializable]
public sealed partial class EWJoeScourDoAfterEvent : SimpleDoAfterEvent;

/// <summary>Offer: give a loose item an Offer directive asks for to the Grand Machine; it leaves play.</summary>
public sealed partial class EWJoeOfferActionEvent : EntityTargetActionEvent;

[Serializable, NetSerializable]
public sealed partial class EWJoeOfferDoAfterEvent : SimpleDoAfterEvent;

/// <summary>Sculpted Hands: leg sweep, an adjacent knockdown (A-7).</summary>
public sealed partial class EWJoeSweepActionEvent : EntityTargetActionEvent;

/// <summary>Sculpted Hands: slam a grabbed target into a wall (A-7).</summary>
public sealed partial class EWJoeSlamActionEvent : EntityTargetActionEvent;

/// <summary>Sculpted Hands: toggle a choke on the grabbed target (A-7).</summary>
public sealed partial class EWJoeChokeActionEvent : InstantActionEvent;

/// <summary>Sculpted Hands: throw the grabbed target (A-7).</summary>
public sealed partial class EWJoeThrowActionEvent : EntityTargetActionEvent;

/// <summary>Sculpted Hands: Desecrate, a targeted punch that breaks a bone and knocks the target back (ECS-05).</summary>
public sealed partial class EWJoeDesecrateActionEvent : EntityTargetActionEvent;

/// <summary>Repentance: direction and distance to whoever last hurt the Joe (A-8).</summary>
public sealed partial class EWJoeSenseActionEvent : InstantActionEvent;

/// <summary>Judgement: Test of Faith as a cone (A-1).</summary>
public sealed partial class EWJoeJudgementActionEvent : InstantActionEvent;

// Phase E5: level 3-4 abilities.

/// <summary>Overclock (formerly Smokescreen): toggle the speed and the venting cloud that comes with it (A-9).</summary>
public sealed partial class EWJoeOverclockActionEvent : InstantActionEvent;

/// <summary>Emergency Coolant: absorb nearby blood to shed heat (A-10).</summary>
public sealed partial class EWJoeCoolantActionEvent : InstantActionEvent;

/// <summary>Grand Machine's Hand: summon or dismiss the serrated machete (A-11).</summary>
public sealed partial class EWJoeMacheteActionEvent : InstantActionEvent;

/// <summary>Consecrate: a chained arc through up to three targets (A-12).</summary>
public sealed partial class EWJoeConsecrateActionEvent : EntityTargetActionEvent;

/// <summary>Unwind: eject magazines, lock safeties on and jam chambers around the Joe (A-13).</summary>
public sealed partial class EWJoeUnwindActionEvent : InstantActionEvent;

// Phase E6: level 5.

/// <summary>
/// Saturate: a cone that floors and strips everyone in it (A-15). Playtest 4: aimed at a tile, like
/// Consecrate is aimed; the cone points from the Joe toward it.
/// </summary>
public sealed partial class EWJoeSaturateActionEvent : WorldTargetActionEvent;
