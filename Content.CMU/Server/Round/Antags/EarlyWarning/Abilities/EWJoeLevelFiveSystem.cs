using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.Chat.Systems;
using Content.Server.CMU14.Chat;
using Content.Server.CMU14.Round.Antags.EarlyWarning.Combat;
using Content.Shared._RMC14.Barricade;
using Content.Shared.CMU14.Chemistry.Effects;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Brain;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Events;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.CMU14.Round.Antags.EarlyWarning.Abilities;
using Content.Shared.CMU14.Round.Antags.EarlyWarning.Melding;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Database;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Components;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Round.Antags.EarlyWarning.Abilities;

/// <summary>
/// Phase E6, level 5 (MELDING): the APOLLO announcement, THE LIGHT and Saturate. Magnetic Resonance is
/// <c>EWJoeResonanceComponent</c> with its own shared system; on the client THE LIGHT has the cable
/// vignette and the heartbeat pulse. Everything here is server-side.
/// </summary>
public sealed partial class EWJoeLevelFiveSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLogger = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private SharedDirectionalAttackBlockSystem _facing = default!;
    [Dependency] private ExamineSystemShared _examine = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private EWJoeHeatSystem _heat = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedPointLightSystem _light = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private EWJoeMessageSystem _messages = default!;
    [Dependency] private CMUChemicalMedicalSystem _medical = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private RotateToFaceSystem _rotate = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private EWJoeXenoKitSystem _xenoKit = default!;
    // RB-13: THE LIGHT's paralyze and Saturate's knockdown are hard CC through the shared window
    [Dependency] private EWJoeLiturgySystem _liturgy = default!;

    private static readonly SoundPathSpecifier AnnounceSound = new("/Audio/Announcements/announce.ogg");
    private static readonly ProtoId<DamageTypePrototype> BrainDamageType = "Blunt";

    /// <summary>The level whose arrival is announced and which carries THE LIGHT.</summary>
    public const int MeldingLevel = 5;

    private readonly HashSet<Entity<MobStateComponent>> _mobs = new();
    private readonly HashSet<EntityUid> _gazing = new();
    private readonly List<EntityUid> _released = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EWJoeLevelChangedEvent>(OnLevelChanged);
        SubscribeLocalEvent<EWJoeComponent, EWJoeSaturateActionEvent>(OnSaturate);
        SubscribeLocalEvent<EWJoeLightComponent, MobStateChangedEvent>(OnLightMobState);
        SubscribeLocalEvent<EWJoeLightComponent, ComponentShutdown>(OnLightShutdown);
    }

    #region Announcement

    /// <summary>D7: APOLLO tells the colony once, when the Joe first reaches level 5.</summary>
    private void OnLevelChanged(ref EWJoeLevelChangedEvent args)
    {
        if (args.NewLevel < MeldingLevel || args.OldLevel >= MeldingLevel)
            return;

        if (!TryComp(args.Joe, out EWJoeComponent? joe) || joe.AnnouncedMelding)
            return;

        joe.AnnouncedMelding = true;
        _chat.DispatchFilteredAnnouncement(
            ColonyAnnouncements.Recipients(EntityManager),
            Loc.GetString("cmu-ew-joe-melding-announcement"),
            sender: Loc.GetString("cmu-ew-joe-announcement-sender"),
            playSound: true,
            announcementSound: AnnounceSound);

        _messages.Notify(args.Joe, Loc.GetString("cmu-ew-joe-melding-reached"));
        _adminLogger.Add(LogType.AntagSelection, LogImpact.High,
            $"Early Warning Joe {ToPrettyString(args.Joe):joe} reached the Melding; APOLLO announced it");
    }

    #endregion

    #region THE LIGHT

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<EWJoeLightComponent, EWJoeComponent>();
        while (query.MoveNext(out var uid, out var light, out _))
        {
            if (now < light.NextTick)
                continue;

            light.NextTick = now + light.TickInterval;
            Tick((uid, light), now);
        }
    }

    /// <summary>
    /// A-14: organic people within range who face the Joe and can see it (its own smoke blocks the
    /// gaze) fill their envelope; everyone else caught by this Joe drains theirs.
    /// </summary>
    private void Tick(Entity<EWJoeLightComponent> joe, TimeSpan now)
    {
        _gazing.Clear();

        if (_mobState.IsAlive(joe))
        {
            _mobs.Clear();
            _lookup.GetEntitiesInRange(Transform(joe).Coordinates, joe.Comp.Range, _mobs, LookupFlags.Uncontained);
            foreach (var victim in _mobs)
            {
                if (IsGazing(joe, victim))
                    _gazing.Add(victim);
            }
        }

        var fill = (float) (joe.Comp.TickInterval / joe.Comp.FillTime);
        foreach (var victim in _gazing)
        {
            var envelope = EnsureComp<EWJoeEnvelopedComponent>(victim);
            if (now < envelope.ImmuneUntil)
                continue;

            envelope.Source = joe;
            SetProgress((victim, envelope), envelope.Progress + fill);
            if (envelope.Progress >= 1f)
                Envelop(joe, (victim, envelope), now);
            else
                TellStage((victim, envelope));
        }

        var decay = (float) (joe.Comp.TickInterval / joe.Comp.DecayTime);
        _released.Clear();
        var envelopes = EntityQueryEnumerator<EWJoeEnvelopedComponent>();
        while (envelopes.MoveNext(out var uid, out var envelope))
        {
            if (envelope.Source != joe.Owner || _gazing.Contains(uid))
                continue;

            SetProgress((uid, envelope), envelope.Progress - decay);
            if (envelope.Progress <= 0f)
                envelope.Stage = 0;
            if (envelope.Progress <= 0f && now >= envelope.ImmuneUntil)
                _released.Add(uid);
        }

        foreach (var uid in _released)
        {
            RemComp<EWJoeEnvelopedComponent>(uid);
        }
    }

    public bool IsGazing(Entity<EWJoeLightComponent> joe, Entity<MobStateComponent> victim)
    {
        if (victim.Owner == joe.Owner || !_mobState.IsAlive(victim, victim.Comp))
            return false;

        // FLOW-12: organics and Yautja only: synthetics have no brain to burn and thralls belong to the Joe
        if (!EWJoeTargetRules.IsLightTarget(EntityManager, victim) || HasComp<RiverMeldedComponent>(victim))
            return false;

        return _facing.IsFacingTarget(victim, joe) && _examine.InRangeUnOccluded(victim.Owner, joe.Owner, joe.Comp.Range);
    }

    private void Envelop(Entity<EWJoeLightComponent> joe, Entity<EWJoeEnvelopedComponent> victim, TimeSpan now)
    {
        // ECS-11 + RB-13: scaled for xenofauna (when the source is a Joe), then the CC window
        _liturgy.ApplyHardCc(joe, victim, joe.Comp.Paralyze, EWJoeHardCcKind.Paralyze);
        // Direct: the light lands whatever the victim has in their blood
        _medical.DamageOrgan<CMUBrainComponent>(victim, joe.Comp.BrainDamage, BrainDamageType, OrganDamageSource.Direct);

        victim.Comp.ImmuneUntil = now + joe.Comp.Immunity;
        victim.Comp.Progress = 0f;
        victim.Comp.Stage = 0;
        Dirty(victim);

        // text pass A: the sound becomes too much, and the signal speaks
        _messages.Sense(victim, _messages.Pick(EWJoeMessageSystem.LightOverwhelm));
        _messages.Whisper(victim, _messages.Pick(EWJoeMessageSystem.LightVoice));
        _popup.PopupEntity(Loc.GetString("cmu-ew-joe-light-enveloped", ("target", victim.Owner)), joe, joe);
        _adminLogger.Add(LogType.Action, LogImpact.Medium,
            $"Early Warning Joe {ToPrettyString(joe):joe} enveloped {ToPrettyString(victim):victim} in THE LIGHT");
    }

    /// <summary>
    /// Text pass A: a third of the way in the victim starts to hear something (static, whispering,
    /// water); two thirds in they wonder what it is, and the signal speaks. Each stage is told once per fill.
    /// </summary>
    private void TellStage(Entity<EWJoeEnvelopedComponent> victim)
    {
        var stage = victim.Comp.Progress >= 2f / 3f ? 2 : victim.Comp.Progress >= 1f / 3f ? 1 : 0;
        if (stage <= victim.Comp.Stage)
            return;

        victim.Comp.Stage = stage;
        if (stage == 1)
        {
            _messages.Sense(victim, _messages.Pick(EWJoeMessageSystem.LightHear), PopupType.MediumCaution);
            return;
        }

        _messages.Sense(victim, _messages.Pick(EWJoeMessageSystem.LightWonder), PopupType.MediumCaution);
        _messages.Whisper(victim, _messages.Pick(EWJoeMessageSystem.LightVoice));
    }

    /// <summary>Clamped; dirtied only when the tenths change, which is all the vignette needs.</summary>
    private void SetProgress(Entity<EWJoeEnvelopedComponent> ent, float progress)
    {
        var old = ent.Comp.Progress;
        ent.Comp.Progress = Math.Clamp(progress, 0f, 1f);
        if ((int) (old * 10) != (int) (ent.Comp.Progress * 10))
            Dirty(ent);
    }

    private void OnLightMobState(Entity<EWJoeLightComponent> ent, ref MobStateChangedEvent args)
    {
        _light.SetEnabled(ent, args.NewMobState == MobState.Alive);
        if (args.NewMobState != MobState.Alive)
            ReleaseAll(ent);
    }

    private void OnLightShutdown(Entity<EWJoeLightComponent> ent, ref ComponentShutdown args)
    {
        ReleaseAll(ent);
    }

    private void ReleaseAll(EntityUid source)
    {
        _released.Clear();
        var envelopes = EntityQueryEnumerator<EWJoeEnvelopedComponent>();
        while (envelopes.MoveNext(out var uid, out var envelope))
        {
            if (envelope.Source == source)
                _released.Add(uid);
        }

        foreach (var uid in _released)
        {
            RemComp<EWJoeEnvelopedComponent>(uid);
        }
    }

    #endregion

    #region Saturate

    private void OnSaturate(Entity<EWJoeComponent> ent, ref EWJoeSaturateActionEvent args)
    {
        if (args.Handled)
            return;

        if (!_heat.TrySpendHeat(ent, ent.Comp.SaturateHeat))
            return;

        args.Handled = true;

        // playtest 4: the cone points at the chosen tile, and the Joe turns to it
        Angle? aim = null;
        var target = _transform.ToMapCoordinates(args.Target);
        var origin = _transform.GetMapCoordinates(ent);
        if (target.MapId == origin.MapId && (target.Position - origin.Position).LengthSquared() > 0.01f)
        {
            aim = (target.Position - origin.Position).ToWorldAngle();
            _rotate.TryFaceCoordinates(ent, target.Position);
        }

        var hit = Saturate(ent, aim);
        _popup.PopupEntity(Loc.GetString("cmu-ew-joe-saturate-done", ("count", hit)), ent, ent, PopupType.Medium);
    }

    /// <summary>
    /// A-15: everyone alive in the cone and in the Joe's line of sight is knocked down and loses what they
    /// hold, their helmet and their armor. Unremoveable items stay put (they would be deleted). Thralls are
    /// spared. The cone points along <paramref name="aim"/> (world angle), or the Joe's facing when null.
    /// Playtest 4: every tile of the cone flashes and a shockwave rings out. Returns how many were hit.
    /// </summary>
    public int Saturate(Entity<EWJoeComponent> joe, Angle? aim = null)
    {
        var origin = _transform.GetMapCoordinates(joe);
        var facing = aim ?? _transform.GetWorldRotation(joe);
        var halfAngle = Angle.FromDegrees(joe.Comp.SaturateConeDegrees / 2f);
        var range = joe.Comp.SaturateRange;

        ShowWave(joe, origin, facing, halfAngle, range);

        _mobs.Clear();
        _lookup.GetEntitiesInRange(origin, range, _mobs, LookupFlags.Uncontained);

        var hit = 0;
        foreach (var target in _mobs)
        {
            if (target.Owner == joe.Owner || !_mobState.IsAlive(target, target.Comp) || HasComp<RiverMeldedComponent>(target))
                continue;

            var toTarget = _transform.GetMapCoordinates(target).Position - origin.Position;
            if (!EWJoeTargetRules.InCone(toTarget, facing, halfAngle))
                continue;

            if (!_examine.InRangeUnOccluded(joe.Owner, target.Owner, range))
                continue;

            Strip(joe, target);
            hit++;
        }

        _adminLogger.Add(LogType.Action, LogImpact.Medium,
            $"Early Warning Joe {ToPrettyString(joe):joe} saturated {hit} target(s)");
        return hit;
    }

    /// <summary>
    /// The visible wave: a shockwave at the Joe and a flash on every tile of the cone it can see. Tiles
    /// are taken on the Joe's grid when it stands on one, so the flashes sit on the tile grid.
    /// </summary>
    private void ShowWave(Entity<EWJoeComponent> joe, MapCoordinates origin, Angle facing, Angle halfAngle, float range)
    {
        var xform = Transform(joe);
        SpawnAtPosition(joe.Comp.SaturateWaveEffect, xform.Coordinates);

        var radius = (int) MathF.Ceiling(range);
        Entity<MapGridComponent>? grid = null;
        if (xform.GridUid is { } gridUid && TryComp(gridUid, out MapGridComponent? gridComp))
            grid = (gridUid, gridComp);

        var centre = grid is { } g ? _map.TileIndicesFor(g.Owner, g.Comp, xform.Coordinates) : Vector2i.Zero;

        for (var dx = -radius; dx <= radius; dx++)
        {
            for (var dy = -radius; dy <= radius; dy++)
            {
                if (dx == 0 && dy == 0)
                    continue;

                MapCoordinates tile;
                if (grid is { } on)
                    tile = _transform.ToMapCoordinates(_map.GridTileToLocal(on.Owner, on.Comp, centre + new Vector2i(dx, dy)));
                else
                    tile = new MapCoordinates(origin.Position + new System.Numerics.Vector2(dx, dy), origin.MapId);

                var offset = tile.Position - origin.Position;
                if (offset.Length() > range || !EWJoeTargetRules.InCone(offset, facing, halfAngle))
                {
                    continue;
                }

                if (!_examine.InRangeUnOccluded(origin, tile, range, null))
                    continue;

                Spawn(joe.Comp.SaturateTileEffect, tile);
            }
        }
    }

    private void Strip(Entity<EWJoeComponent> joe, EntityUid target)
    {
        // ECS-11 + RB-13: scaled for xenofauna, then the CC window; refreshed, standing back up, dropping what is held
        _liturgy.ApplyHardCc(joe, target, joe.Comp.SaturateKnockdown, EWJoeHardCcKind.Knockdown);

        // 5B Saturate (xeno): xenofauna wear no helmet to tear away, so the cone dazes them and cracks their armor
        _xenoKit.SaturateXeno(joe, target);

        // knockdown only drops held items on the first fall, so drop them explicitly
        foreach (var held in _hands.EnumerateHeld(target).ToArray())
        {
            if (!HasComp<UnremoveableComponent>(held))
                _hands.TryDrop(target, held, checkActionBlocker: false);
        }

        foreach (var slot in joe.Comp.SaturateSlots)
        {
            if (!_inventory.TryGetSlotEntity(target, slot, out var item) || HasComp<UnremoveableComponent>(item))
                continue;

            _inventory.TryUnequip(target, target, slot, out _, silent: true, force: true);
        }
    }

    #endregion
}
