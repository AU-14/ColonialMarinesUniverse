using Content.Shared.Alert;
using Content.Shared.Antag;
using Content.Shared.CMU14.Threats;
using Content.Shared.Radio;
using Content.Shared.Random;
using Content.Shared.Whitelist;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Round.Antags.EarlyWarning;

/// <summary>
/// Marks a Working Joe as the Early Warning Joe antagonist and tracks its level progression.
/// Levels 0-5 are defined by <see cref="EWJoeLevelsPrototype"/>; each level grants actions and
/// components that are tracked here so they can be removed cleanly on level change or cleanup.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class EWJoeComponent : Component
{
    /// <summary>The antag's identity must not leak to other clients; only the Joe predicts its own abilities.</summary>
    public override bool SendOnlyToOwner => true;

    /// <summary>Current level, 0 (baseline) to 5 (Melding).</summary>
    [DataField, AutoNetworkedField]
    public int Level;

    /// <summary>
    /// Highest level whose grants are applied; -1 before the first grant. A respawn lowers it with the
    /// level (EWJoeRuleSystem.LowerLevel), so the lost levels are granted again on the way back up.
    /// </summary>
    [DataField]
    public int AppliedLevel = -1;

    /// <summary>The mob's own innate slowdown, recorded on the first grant and restored on cleanup.</summary>
    [DataField]
    public float? OriginalSlowdown;

    /// <summary>The level table that defines per-level grants.</summary>
    [DataField]
    public ProtoId<EWJoeLevelsPrototype> LevelTable = "CMUEWJoeLevels";

    /// <summary>Bullet armor added per level (Sculpted Form; rebalance RB-01: +4, 20 at level 5).</summary>
    [DataField, AutoNetworkedField]
    public int BulletArmorPerLevel = 4;

    /// <summary>
    /// Melee armor added per level (Sculpted Form; rebalance RB-02: +2, 10 at level 5). Scales slower than
    /// <see cref="BulletArmorPerLevel"/> so xeno claws keep up with the Joe at even tier.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int MeleeArmorPerLevel = 2;

    /// <summary>The innate slowdown the Joe starts with; shed by <see cref="SlowdownShedPerLevel"/> each level.</summary>
    [DataField, AutoNetworkedField]
    public float BaseSlowdown = 1.25f;

    [DataField, AutoNetworkedField]
    public float SlowdownShedPerLevel = 0.25f;

    /// <summary>Test of Faith: both the Joe and the target must face each other within this range.</summary>
    [DataField, AutoNetworkedField]
    public float TestOfFaithRange = 6f;

    [DataField, AutoNetworkedField]
    public TimeSpan TestOfFaithFlash = TimeSpan.FromSeconds(3);

    [DataField, AutoNetworkedField]
    public TimeSpan TestOfFaithStun = TimeSpan.FromSeconds(1.5);

    /// <summary>
    /// ECS-11: every EW Joe stun on xenofauna is multiplied by this before RMC's own xeno reduction (×0.667),
    /// so a xeno is held for a real window instead of a split second. Routed through EWJoeTargetRules.ScaleStun.
    /// Rebalance RB-16: 1.5, so after RMC's reduction a xeno is held about as long as a human (≈1.0×).
    /// </summary>
    [DataField, AutoNetworkedField]
    public float XenofaunaStunMultiplier = 1.5f;

    /// <summary>Disrupt: the level-0 EMP pulse. Lights, headsets and batteries respond today.</summary>
    [DataField, AutoNetworkedField]
    public float DisruptRange = 3f;

    [DataField, AutoNetworkedField]
    public float DisruptEnergy = 50000f;

    [DataField, AutoNetworkedField]
    public TimeSpan DisruptDuration = TimeSpan.FromSeconds(10);

    /// <summary>Inventory slot the overwatch camera is removed from at selection (D13).</summary>
    [DataField]
    public string CameraSlot = "ears2";

    /// <summary>Only this prototype is deleted from the slot; anything else a Joe wears there stays.</summary>
    [DataField]
    public EntProtoId CameraPrototype = "RMCM5CameraGear";

    /// <summary>Selection added CMArmorUser because the body lacked it; stand-down removes it again.</summary>
    [DataField]
    public bool AddedArmorUser;

    /// <summary>The machine radio channel was granted to the Joe (first thrall); stand-down revokes it.</summary>
    [DataField]
    public bool GrantedMachineRadio;

    /// <summary>Actions granted by the level table, in grant order. Server bookkeeping.</summary>
    [DataField]
    public List<EntityUid> GrantedActions = new();

    /// <summary>Component names granted by the level table, keyed to the level that granted them.</summary>
    [DataField]
    public Dictionary<string, int> GrantedComponents = new();

    /// <summary>How many objectives each level offers (D8: three offered).</summary>
    [DataField]
    public int ObjectivesPerLevel = 3;

    /// <summary>Credit needed to advance a level (D8: two completed). A completion is worth 1 credit, or
    /// <see cref="PreThreatCreditMultiplier"/> while the round's threat is not yet selected in
    /// <see cref="PreThreatPreset"/> (balance A-17).</summary>
    [DataField]
    public int CreditPerLevel = 2;

    [DataField]
    public int PreThreatCreditMultiplier = 2;

    [DataField]
    public string PreThreatPreset = "ColonyFall";

    /// <summary>Objectives offered for the current level, finished or not. Server bookkeeping.</summary>
    [DataField]
    public List<EntityUid> CurrentObjectives = new();

    /// <summary>Credit earned per finished objective of the current level. Cleared on advance.</summary>
    [DataField]
    public Dictionary<EntityUid, int> CompletedCredit = new();

    /// <summary>
    /// Items that already satisfied a credited collect objective. A later collect objective never
    /// counts them again, in progress or in the existence check at assignment. Server bookkeeping.
    /// </summary>
    [DataField]
    public HashSet<EntityUid> SpentCollectibles = new();

    /// <summary>When an offer came up empty (no valid targets yet), the next time the pool is retried.</summary>
    [DataField]
    public TimeSpan NextOfferAttempt;

    /// <summary>How long to wait before retrying an empty offer.</summary>
    [DataField]
    public TimeSpan OfferRetryDelay = TimeSpan.FromSeconds(60);

    /// <summary>Live roster of thralls; entries leave on cure, the body's destruction or the Joe's death (the panel hides dead ones).</summary>
    [DataField]
    public List<EntityUid> Thralls = new();

    /// <summary>How many thralls the Joe ever made. The round-end summary reads this, not the roster.</summary>
    [DataField]
    public int ThrallsMade;

    [DataField]
    public int ConsolesAssimilated;

    [DataField]
    public bool AnnouncedMelding;

    /// <summary>
    /// Consoles the assimilate action accepts (phase E2). Component names only, the same set the
    /// assimilate objective uses: matching by parent prototype would catch the embassy consoles.
    /// </summary>
    [DataField]
    public EntityWhitelist ConsoleWhitelist = new()
    {
        Components = ["BudgetConsole", "CorporateConsole", "ResearchDataTerminal", "DepartmentConsole", "ColonyCommsConsole"],
    };

    /// <summary>Length of the assimilation do-after (design §8.2: 8 s).</summary>
    [DataField]
    public TimeSpan AssimilateDelay = TimeSpan.FromSeconds(8);

    /// <summary>How far the Joe may drift from the console during the do-after.</summary>
    [DataField]
    public float AssimilateRange = 1.5f;

    /// <summary>Clamp on every console randomization: ±40% of the current value (audit §8.1).</summary>
    [DataField]
    public float ConsoleEffectVariance = 0.4f;

    /// <summary>Spawned once where the Joe dies; the last step of console repair.</summary>
    [DataField]
    public EntProtoId CoreShardPrototype = "CMUEWJoeCoreShard";

    /// <summary>Set after the shard dropped so a revived Joe never drops a second one.</summary>
    [DataField]
    public bool ShardDropped;

    // ---- Phase E4: level 1-2 abilities (balance A-1, A-5, A-6, A-7, A-8) ----

    /// <summary>Hold time before the drowning lands. Playtest 2 asked for longer than the 4 s of A-5.</summary>
    [DataField]
    public TimeSpan DrownDelay = TimeSpan.FromSeconds(8);

    /// <summary>Organ damage to the lungs; 55 puts human lungs in their dead stage.</summary>
    [DataField]
    public int DrownLungDamage = 55;

    [DataField]
    public TimeSpan DrownSleep = TimeSpan.FromSeconds(30);

    /// <summary>Share of the Joe's own dead threshold healed by a drowning.</summary>
    [DataField]
    public float DrownHealFraction = 0.15f;

    [DataField]
    public TimeSpan SweepKnockdown = TimeSpan.FromSeconds(1.5);

    [DataField]
    public int SlamDamage = 40;

    [DataField]
    public TimeSpan SlamParalyze = TimeSpan.FromSeconds(2);

    /// <summary>Slow applied by the impact (RMC obstacle slamming bonus).</summary>
    [DataField]
    public TimeSpan SlamSlow = TimeSpan.FromSeconds(2);

    /// <summary>How far from the victim the chosen obstacle may be.</summary>
    [DataField]
    public float SlamRange = 3f;

    /// <summary>Throw speed into the obstacle; RMC's impact damage scales with it.</summary>
    [DataField]
    public float SlamThrowSpeed = 15f;

    [DataField]
    public double ChokeStaminaPerSecond = 25;

    [DataField]
    public float ThrowDistance = 4f;

    [DataField]
    public float ThrowSpeed = 10f;

    [DataField]
    public TimeSpan RepentanceMemory = TimeSpan.FromSeconds(120);

    [DataField]
    public float JudgementRange = 5f;

    [DataField]
    public float JudgementConeDegrees = 100f;

    [DataField]
    public TimeSpan ScourDelay = TimeSpan.FromSeconds(3);

    /// <summary>How long Offer takes to consume an item, like Scour.</summary>
    [DataField]
    public TimeSpan OfferDelay = TimeSpan.FromSeconds(3);

    /// <summary>How long a timed Scour buff lasts (baton, radio). Body armor has its own, shorter window.</summary>
    [DataField]
    public TimeSpan ScourDuration = TimeSpan.FromMinutes(5);

    /// <summary>How long the digested body armor's bullet armor lasts (rebalance RB-03: 3 min).</summary>
    [DataField]
    public TimeSpan ScourArmorDuration = TimeSpan.FromMinutes(3);

    /// <summary>Bullet armor from digested body armor (rebalance RB-03: +5).</summary>
    [DataField]
    public int ScourBulletArmor = 5;

    [DataField]
    public double ScourPunchStamina = 15;

    /// <summary>Fuel-can self-repair per second (rebalance RB-04: 2, so 90 over the window).</summary>
    [DataField]
    public float ScourRepairPerSecond = 2f;

    /// <summary>The fuel-can self-repair window (rebalance RB-04: 45 s).</summary>
    [DataField]
    public TimeSpan ScourRepairDuration = TimeSpan.FromSeconds(45);

    /// <summary>
    /// Rebalance RB-04: the fuel-can self-repair stops for this long after the Joe takes damage. The pause does
    /// not extend the repair window.
    /// </summary>
    [DataField]
    public TimeSpan ScourRepairDamagePause = TimeSpan.FromSeconds(5);

    [DataField]
    public List<ProtoId<RadioChannelPrototype>> ScourRadioChannels = new() { "Colony", "radioCMB" };

    // ---- Phase E5: level 3-4 abilities (balance A-2, A-9 to A-13, A-18, D11) ----

    /// <summary>Emergency Coolant: blood decals and puddles within this range are absorbed.</summary>
    [DataField]
    public float CoolantRange = 2f;

    /// <summary>Playtest 3: halved from A-10's ten.</summary>
    [DataField]
    public float CoolantPerDecal = 5f;

    [DataField]
    public float CoolantPerPuddleUnit = 0.5f;

    /// <summary>Playtest 3: at most this much heat (out of 100) is shed per use.</summary>
    [DataField]
    public float CoolantCap = 40f;

    /// <summary>Grand Machine's Hand: the summoned machete.</summary>
    [DataField]
    public EntProtoId MachetePrototype = "CMUEWJoeMachete";

    /// <summary>Heat paid per Consecrate (rebalance RB-20: 30).</summary>
    [DataField]
    public float ConsecrateHeat = 30f;

    /// <summary>Targets in the chain, the clicked one included.</summary>
    [DataField]
    public int ConsecrateJumps = 3;

    [DataField]
    public float ConsecrateJumpRange = 3f;

    /// <summary>
    /// Raw Heat per target in the chain (rebalance RB-11: 40). The rebalance audit (RB-11) found that M3 armor
    /// does not reduce it; the old "~18 through M3" note was wrong.
    /// </summary>
    [DataField]
    public int ConsecrateDamage = 40;

    /// <summary>Paralyze per target in the chain (rebalance RB-20: 1.5 s).</summary>
    [DataField]
    public TimeSpan ConsecrateParalyze = TimeSpan.FromSeconds(1.5);

    [DataField]
    public EntProtoId ConsecrateBeam = "EffectTeslaBeam";

    /// <summary>Unwind's reach, guns and sentries alike (rebalance RB-23: 4 tiles).</summary>
    [DataField]
    public float UnwindRange = 4f;

    /// <summary>How long Unwind holds a sentry off (rebalance RB-23: 6 s).</summary>
    [DataField]
    public TimeSpan UnwindSentryLockout = TimeSpan.FromSeconds(6);

    /// <summary>Heat paid per Unwind (rebalance RB-23: 20; it was free).</summary>
    [DataField]
    public float UnwindHeat = 20f;

    /// <summary>Racking the jammed chamber clear takes this long (A-13: 2 s).</summary>
    [DataField]
    public float UnwindRackTime = 2f;

    /// <summary>Heat for a completed Z-Level Leap (D11: 15).</summary>
    [DataField]
    public float ZLeapHeat = 15f;

    // ---- Phase E6: level 5 (balance A-15) ----

    [DataField]
    public float SaturateHeat = 10f;

    [DataField]
    public float SaturateRange = 5f;

    [DataField]
    public float SaturateConeDegrees = 90f;

    [DataField]
    public TimeSpan SaturateKnockdown = TimeSpan.FromSeconds(2);

    /// <summary>Worn slots Saturate tears away; held items are dropped as well.</summary>
    [DataField]
    public List<string> SaturateSlots = new() { "outerClothing", "head" };

    /// <summary>Playtest 4: flashed on every tile of the cone.</summary>
    [DataField]
    public EntProtoId SaturateTileEffect = "CMUEWJoeSaturateTile";

    /// <summary>Playtest 4: the shockwave that rings out from the Joe.</summary>
    [DataField]
    public EntProtoId SaturateWaveEffect = "CMUEWJoeSaturateWave";

    /// <summary>Disrupt-II (A-2): the level from which the pulse also opens doors, disarms traps and silences sentries.</summary>
    [DataField]
    public int DisruptTierTwoLevel = 2;

    [DataField]
    public TimeSpan DisruptDoorHold = TimeSpan.FromSeconds(10);

    /// <summary>Phase E3: time from the first warning to the conversion (design: 60/40/20 s warnings).</summary>
    [DataField]
    public TimeSpan MeldDuration = TimeSpan.FromSeconds(60);

    /// <summary>Specifier read as a data bag (components, mind roles, briefing) for a synthetic thrall.</summary>
    [DataField]
    public ProtoId<AntagSpecifierPrototype> ThrallSpecifier = "EWJoeThrall";

    /// <summary>Machine radio shared by the Joe and its thralls.</summary>
    [DataField]
    public ProtoId<RadioChannelPrototype>? ThrallRadioChannel = "CMUGrandMachine";

    // Playtest 4: the river takes synthetics only. An assimilation that finds no synthetic to meld keeps
    // a copy of the Joe instead: one respawn, never more than one held at a time.

    /// <summary>The Joe holds its one respawn. Shown on its own status panel and as an alert.</summary>
    [DataField, AutoNetworkedField]
    public bool RespawnReady;

    /// <summary>How many times the Joe came back; for admins (View Variables) and tests, also in the admin log.</summary>
    [DataField]
    public int Respawns;

    [DataField]
    public ProtoId<AlertPrototype> RespawnAlert = "CMUEWJoeRespawn";

    /// <summary>Levels lost on a respawn (floor 0).</summary>
    [DataField]
    public int RespawnLevelLoss = 2;

    [DataField]
    public float RespawnEmpRange = 8f;

    [DataField]
    public float RespawnEmpEnergy = 100000f;

    [DataField]
    public TimeSpan RespawnEmpDuration = TimeSpan.FromSeconds(20);

    /// <summary>The Joe comes back at least this far from every living player when the map allows.</summary>
    [DataField]
    public float RespawnClearance = 15f;

    /// <summary>Random tiles tried when looking for a place to come back.</summary>
    [DataField]
    public int RespawnAttempts = 300;

    // ---- N7 (FLOW-05): meld debt. An assimilation that finds no synthetic does not give the copy at
    // once: the river keeps searching the planet network every MeldRetry for up to MeldSearchWindow.
    // Every debt still open when the window closes gives the copy once between them (never more than
    // one held). Server bookkeeping.

    /// <summary>Assimilations still waiting for a synthetic to meld.</summary>
    [DataField]
    public int PendingMelds;

    /// <summary>When the current search gives up and grants the copy. Set by the first debt of a search.</summary>
    [DataField]
    public TimeSpan MeldSearchUntil;

    /// <summary>The next time the open debts look for a synthetic again.</summary>
    [DataField]
    public TimeSpan NextMeldRetry;

    /// <summary>How long the river searches before it keeps a copy instead. Zero grants the copy at once (playtest 4 behaviour).</summary>
    [DataField]
    public TimeSpan MeldSearchWindow = TimeSpan.FromMinutes(5);

    /// <summary>How often an open debt looks for a synthetic again.</summary>
    [DataField]
    public TimeSpan MeldRetry = TimeSpan.FromSeconds(15);

    // ---- N5 (FLOW-02): Machine Grip. The Joe's first Ctrl-click on a valid target telegraphs and then goes
    // straight to an aggressive grab (EWJoeMachineGripSystem). EWJoeRuleSystem applies the per-level grants below.

    /// <summary>The windup between the pull and the aggressive grab; the victim breaks it by moving away.</summary>
    [DataField]
    public TimeSpan GripTelegraph = TimeSpan.FromSeconds(0.4);

    /// <summary>
    /// The windup against a large xeno (RMC size Big: the grabbable T3s such as the praetorian, boiler and despoiler)
    /// is this many times <see cref="GripTelegraph"/>; the manual Ctrl-click upgrade waits just as long.
    /// </summary>
    [DataField]
    public float LargeXenoGripMultiplier = 1.5f;

    /// <summary>How far apart the Joe and the victim may get during the windup.</summary>
    [DataField]
    public float GripTelegraphRange = 1.5f;

    /// <summary>
    /// Break-free time multiplier the Joe's grabs impose, indexed by level; levels past the end use the last entry
    /// (rebalance RB-19: x1.0 at L0-1, x1.25 at L2-3, x1.5 from L4).
    /// </summary>
    [DataField]
    public List<float> GripEscapeMultipliers = new() { 1f, 1f, 1.25f, 1.25f, 1.5f };

    /// <summary>
    /// From this level the Joe can aggressively grab xenos up to tier 3 and size Big, never the heavies
    /// (CMUXenoGrabberComponent).
    /// </summary>
    [DataField]
    public int XenoGripLevel = 2;

    /// <summary>An aggressive grab on a xeno is released after this long.</summary>
    [DataField]
    public TimeSpan XenoGripHoldCap = TimeSpan.FromSeconds(3);

    /// <summary>After a xeno grab ends, no xeno can be aggressively grabbed again for this long.</summary>
    [DataField]
    public TimeSpan XenoGripCooldown = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Entity prototype whose ParalyzeImmuneFrom entry is copied onto a Joe that lacks one at the xeno-grip level, so
    /// pulling a small caste does not paralyze it. The synthetic add-list whitelists the same RMCSynthGrabbable castes.
    /// </summary>
    [DataField]
    public EntProtoId XenoGripPullImmunity = "RMCSynthAddComponents";

    /// <summary>The mob's own CanFiremanCarry.AggressiveGrabDelay, restored on stand-down. Server bookkeeping.</summary>
    [DataField]
    public TimeSpan? OriginalAggressiveGrabDelay;

    /// <summary>The mob's own escape multiplier when it had one before the grants. Server bookkeeping.</summary>
    [DataField]
    public float? OriginalGripEscapeMultiplier;

    [DataField]
    public bool AddedGripEscapeMultiplier;

    [DataField]
    public bool AddedXenoGrabber;

    [DataField]
    public bool AddedParalyzeImmuneFrom;
}

/// <summary>
/// One level of the Early Warning Joe's progression.
/// </summary>
[DataDefinition]
public sealed partial class EWJoeLevelDef
{
    /// <summary>Localized level name (UNDERTOW, MURMUR, ...).</summary>
    [DataField(required: true)]
    public LocId Name;

    /// <summary>Actions granted when this level is reached.</summary>
    [DataField]
    public List<EntProtoId> Actions = new();

    /// <summary>Components added when this level is reached. Existing components are not replaced.</summary>
    [DataField]
    public ComponentRegistry Components = new();

    /// <summary>
    /// Objective pool (a weightedRandom of weightedRandom groups, as the traitor pools are) offered when
    /// this level is reached. Null for the last level: nothing is offered past Melding.
    /// </summary>
    [DataField]
    public ProtoId<WeightedRandomPrototype>? ObjectiveGroup;

    /// <summary>
    /// Actions an earlier level granted that this level takes away, by prototype (playtest 3: Judgement
    /// replaces Test of Faith).
    /// </summary>
    [DataField]
    public List<EntProtoId> RemoveActions = new();

    /// <summary>An objective always included in this level's offer, before the random picks (D8: level 4 always offers a console).</summary>
    [DataField]
    public EntProtoId? ForcedObjective;

    /// <summary>Advancing out of this level also requires a finished console assimilation (D8: level 4 to 5).</summary>
    [DataField]
    public bool RequireAssimilation;
}

/// <summary>
/// The Early Warning Joe's level table. Kept as a prototype so balance lives in one YAML file and
/// tests can apply levels without running antag selection.
/// </summary>
[Prototype("ewJoeLevels")]
public sealed partial class EWJoeLevelsPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>Index = level. Level 0 is the baseline granted at selection.</summary>
    [DataField(required: true)]
    public List<EWJoeLevelDef> Levels = new();
}
