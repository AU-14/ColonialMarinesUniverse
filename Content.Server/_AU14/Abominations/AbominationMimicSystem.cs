using System.Linq;
using Content.Server.Chat.Systems;
using Content.Server.Polymorph.Components;
using Content.Server.Polymorph.Systems;
using Content.Server.Radio.Components;
using Content.Shared._AU14.Abominations;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared.Actions;
using Content.Shared.Chat.Prototypes;
using Content.Shared._RMC14.Xenonids.Parasite;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Jittering;
using Content.Shared.Mobs;
using Content.Shared.Stunnable;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.Polymorph;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._AU14.Abominations;

/// <summary>
/// Drives the mimic's transform action and disguise lifetime.
///
/// Flow:
///  1. Transform action -> picker BUI -> StartDisguise polymorphs into a real
///     CMMobHuman, patches name/factions/IFF/skills/appearance, and grants the
///     disguised entity an explicit Revert button.
///  2. Disguise lasts <see cref="AbominationMimicComponent.TransformDuration"/>
///     (default 4.5min). Expiry, crit/death, and pressing the Revert button
///     all funnel through <see cref="BeginRevert"/>.
///  3. BeginRevert adds <see cref="AbominationMimicRevertingComponent"/> which
///     spends a couple of seconds jittering + screaming, then polymorph-reverts
///     the entity back into its combat form. The cooldown
///     (<see cref="AbominationMimicComponent.TransformCooldown"/>, default 5min)
///     is stamped onto the original mimic at this point.
/// </summary>
public sealed partial class AbominationMimicSystem : EntitySystem
{
    public static readonly ProtoId<PolymorphPrototype> DisguisePolymorph = "AbominationMimicDisguise";
    public static readonly EntProtoId RevertAction = "ActionAbominationMimicRevert";
    public static readonly ProtoId<EmotePrototype> ScreamEmote = "Scream";
    public const string AbominationRadioChannel = "Abomination";

    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private NpcFactionSystem _faction = default!;
    [Dependency] private GunIFFSystem _gunIff = default!;
    [Dependency] private SharedJitteringSystem _jitter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SkillsSystem _skills = default!;
    [Dependency] private SharedHumanoidAppearanceSystem _humanoid = default!;
    [Dependency] private PolymorphSystem _polymorph = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<AbominationMimicComponent, AbominationMimicTransformActionEvent>(OnTransformAction);
        SubscribeLocalEvent<AbominationMimicTransformedComponent, AbominationMimicRevertActionEvent>(OnRevertAction);
        SubscribeLocalEvent<AbominationMimicTransformedComponent, MobStateChangedEvent>(OnDisguisedMobStateChanged);
        // Last-ditch revert if the disguise is being deleted/gibbed before
        // MobStateChanged got the chance to fire (e.g. instant gibs). Fires
        // before the entity is gone, so PolymorphedEntityComponent + parent
        // are still valid for the revert call.
        SubscribeLocalEvent<AbominationMimicTransformedComponent, EntityTerminatingEvent>(OnDisguisedTerminating);

        // BUI message — modern Subs.BuiEvents pattern filters by UI key so the
        // handler only fires for messages addressed to AbominationMimicUiKey.Key.
        // The previous plain SubscribeLocalEvent<TComp, BoundUserInterfaceMessage>
        // never dispatched on this codebase's UI plumbing.
        Subs.BuiEvents<AbominationMimicComponent>(AbominationMimicUiKey.Key, subs =>
        {
            subs.Event<AbominationMimicSelectFormMessage>(OnSelectForm);
        });
    }

    private void OnTransformAction(Entity<AbominationMimicComponent> mimic, ref AbominationMimicTransformActionEvent args)
    {
        Log.Info($"[mimic] OnTransformAction fired on {ToPrettyString(mimic)} performer={ToPrettyString(args.Performer)} action={ToPrettyString(args.Action.Owner)} pool={mimic.Comp.AssimilatedPool.Count}");

        if (args.Handled)
            return;

        // Disguised mimics already have the Revert button; the Transform action
        // on the disguised form is a no-op so they don't double-pick.
        if (HasComp<AbominationMimicTransformedComponent>(mimic))
        {
            Log.Info($"[mimic] OnTransformAction rejected: {ToPrettyString(mimic)} already transformed");
            return;
        }

        if (mimic.Comp.AssimilatedPool.Count == 0)
        {
            _popup.PopupClient(Loc.GetString("abomination-mimic-transform-no-profiles"), mimic, mimic);
            return;
        }

        // Remember the action entity (per-mimic by construction) so FinishRevert
        // can stamp the cooldown back onto THIS mimic only. The SharedActions
        // system enforces the cooldown on the action entity itself, so the
        // grey-out + timer rendering Just Work.
        mimic.Comp.TransformActionEntity = args.Action.Owner;
        Dirty(mimic);

        args.Handled = true;
        _ui.TryOpenUi(mimic.Owner, AbominationMimicUiKey.Key, args.Performer);
        PushBuiState(mimic);
    }

    private void OnSelectForm(Entity<AbominationMimicComponent> mimic, ref AbominationMimicSelectFormMessage args)
    {
        Log.Info($"[mimic] OnSelectForm fired on {ToPrettyString(mimic)} actor={ToPrettyString(args.Actor)} idx={args.Index} pool={mimic.Comp.AssimilatedPool.Count}");

        if (args.Index < 0 || args.Index >= mimic.Comp.AssimilatedPool.Count)
        {
            Log.Warning($"[mimic] OnSelectForm rejected: index {args.Index} out of range for pool {mimic.Comp.AssimilatedPool.Count}");
            return;
        }

        var profile = mimic.Comp.AssimilatedPool[args.Index];
        _ui.CloseUi(mimic.Owner, AbominationMimicUiKey.Key, args.Actor);
        var result = StartDisguise(mimic, profile, mimic.Comp.TransformDuration);
        if (result is { } r)
            Log.Info($"[mimic] disguise spawned: {ToPrettyString(r)}");
        else
            Log.Warning($"[mimic] disguise FAILED (PolymorphEntity returned null) for {ToPrettyString(mimic)}");
    }

    private void PushBuiState(Entity<AbominationMimicComponent> mimic)
    {
        var names = mimic.Comp.AssimilatedPool.Select(p => p.Name).ToList();
        _ui.SetUiState(mimic.Owner, AbominationMimicUiKey.Key, new AbominationMimicBuiState(names, null));
    }

    /// <summary>
    /// Polymorph the mimic into the right entity for this profile:
    ///  - Humanoid profile -> CMMobHuman (via the AbominationMimicDisguise prototype).
    ///  - Animal profile -> the source entity prototype itself (rat, monkey, …),
    ///    using a runtime PolymorphConfiguration so we don't need one prototype
    ///    per animal kind.
    /// Then patch the disguise on top and grant the Revert action.
    /// </summary>
    public EntityUid? StartDisguise(Entity<AbominationMimicComponent> mimic, AbominationAssimilationProfile profile, TimeSpan duration)
    {
        Log.Info($"[mimic] StartDisguise mimic={ToPrettyString(mimic)} profile={profile.Name} sourceProto={profile.SourceProtoId ?? "(humanoid)"}");

        EntityUid? disguised;
        // Animal profiles carry a SourceProtoId — polymorph straight into that
        // prototype so the mimic actually BECOMES that animal, instead of
        // becoming a human named "rat".
        if (profile.SourceProtoId is { } animalProto &&
            _proto.HasIndex<EntityPrototype>(animalProto))
        {
            var config = new PolymorphConfiguration
            {
                Entity = animalProto,
                Forced = true,
                Inventory = PolymorphInventoryChange.Drop,
                TransferName = false,
                TransferDamage = true,
                // Mimics from assimilation already have PolymorphedEntityComponent;
                // allow stacking the disguise polymorph on top.
                AllowRepeatedMorphs = true,
                RevertOnCrit = false,
                RevertOnDeath = false,
            };
            disguised = _polymorph.PolymorphEntity(mimic.Owner, config);
        }
        else
        {
            disguised = _polymorph.PolymorphEntity(mimic.Owner, DisguisePolymorph);
        }

        if (disguised is not { } disguisedUid)
        {
            Log.Warning($"[mimic] StartDisguise: PolymorphEntity returned null for {ToPrettyString(mimic)} - HasPolymorphedEntityComponent={HasComp<PolymorphedEntityComponent>(mimic)}");
            return null;
        }

        var carried = EnsureComp<AbominationMimicComponent>(disguisedUid);
        carried.AssimilatedPool = new List<AbominationAssimilationProfile>(mimic.Comp.AssimilatedPool);
        carried.TransformDuration = mimic.Comp.TransformDuration;
        carried.TransformCooldown = mimic.Comp.TransformCooldown;
        Dirty(disguisedUid, carried);

        var tracker = EnsureComp<AbominationMimicTransformedComponent>(disguisedUid);
        tracker.Profile = profile;
        tracker.ExpiresAt = _timing.CurTime + duration;
        Dirty(disguisedUid, tracker);

        ApplyProfile(disguisedUid, profile);

        // Mimics wearing human or animal skin are immune to xeno parasites —
        // the flesh underneath isn't compatible host material.
        RemComp<InfectableComponent>(disguisedUid);

        // Grant the disguise the abomination radio channel — the disguised
        // mimic still hears + speaks to the flesh-hivemind even while
        // wearing a face.
        var receiver = EnsureComp<IntrinsicRadioReceiverComponent>(disguisedUid);
        var transmitter = EnsureComp<IntrinsicRadioTransmitterComponent>(disguisedUid);
        var activeRadio = EnsureComp<ActiveRadioComponent>(disguisedUid);
        transmitter.Channels.Add(AbominationRadioChannel);
        activeRadio.Channels.Add(AbominationRadioChannel);

        // Every transform resets damage on the new body — mimics spawn
        // fresh into the disguise no matter how chewed-up they were.
        HealToFull(disguisedUid);

        _actions.AddAction(disguisedUid, RevertAction);
        return disguisedUid;
    }

    private void HealToFull(EntityUid uid)
    {
        if (TryComp<DamageableComponent>(uid, out var damageable))
            _damageable.SetAllDamage(uid, damageable, FixedPoint2.Zero);
    }

    private void OnRevertAction(Entity<AbominationMimicTransformedComponent> ent, ref AbominationMimicRevertActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        BeginRevert(ent.Owner);
    }

    private void OnDisguisedMobStateChanged(Entity<AbominationMimicTransformedComponent> ent, ref MobStateChangedEvent args)
    {
        // Death == instant revert at full health, no shake-and-scream wind-up
        // (engine can gib the disguise before the 7s timer fires, leaving the
        // parent mimic parked on PausedMap forever). Crit still uses the
        // dramatic revert sequence.
        if (args.NewMobState == MobState.Dead)
        {
            ImmediateRevert(ent.Owner);
            return;
        }

        if (args.NewMobState == MobState.Critical)
            BeginRevert(ent.Owner);
    }

    /// <summary>
    /// Skip the shake/scream wind-up and revert NOW. Used when the disguise
    /// dies or is otherwise about to be lost — we have to get the parent
    /// mimic off the paused map before its anchor entity is gone.
    /// </summary>
    private void ImmediateRevert(EntityUid disguisedUid)
    {
        if (!TryComp<PolymorphedEntityComponent>(disguisedUid, out var polymorphed))
            return;

        // Stop the slow wind-up if one was already in flight.
        RemComp<AbominationMimicRevertingComponent>(disguisedUid);

        FinishRevert(disguisedUid, polymorphed);
    }

    private void OnDisguisedTerminating(Entity<AbominationMimicTransformedComponent> ent, ref EntityTerminatingEvent args)
    {
        ImmediateRevert(ent.Owner);
    }

    /// <summary>
    /// Start the shake+scream revert sequence. Idempotent: if a revert is
    /// already pending we leave the existing timer alone.
    /// </summary>
    private void BeginRevert(EntityUid mimic)
    {
        if (!HasComp<AbominationMimicTransformedComponent>(mimic))
            return;

        if (HasComp<AbominationMimicRevertingComponent>(mimic))
            return;

        var reverting = EnsureComp<AbominationMimicRevertingComponent>(mimic);
        reverting.RevertAt = _timing.CurTime + reverting.JitterDuration;
        Dirty(mimic, reverting);

        // Fall over, scream and shake for the entire 7s wind-down before the
        // polymorph revert fires. Jitter amplitude is high so the seizure is
        // visible at a glance.
        _jitter.DoJitter(mimic, reverting.JitterDuration, refresh: true, amplitude: 20, frequency: 18);
        _stun.TryParalyze(mimic, reverting.JitterDuration, true);
        _chat.TryEmoteWithChat(mimic, ScreamEmote);
        _popup.PopupClient(Loc.GetString("abomination-mimic-transform-revert"), mimic, mimic);
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;

        // Stage 1: trigger BeginRevert when the disguise's lifetime ends.
        var disguised = EntityQueryEnumerator<AbominationMimicTransformedComponent, PolymorphedEntityComponent>();
        while (disguised.MoveNext(out var uid, out var tracker, out _))
        {
            if (HasComp<AbominationMimicRevertingComponent>(uid))
                continue;
            if (tracker.ExpiresAt > now)
                continue;

            BeginRevert(uid);
        }

        // Stage 2: actually polymorph-revert once the shake-and-scream timer ends.
        var reverting = EntityQueryEnumerator<AbominationMimicRevertingComponent, PolymorphedEntityComponent>();
        while (reverting.MoveNext(out var uid, out var revert, out var polymorphed))
        {
            if (revert.RevertAt > now)
                continue;

            FinishRevert(uid, polymorphed);
        }
    }

    private void FinishRevert(EntityUid disguisedUid, PolymorphedEntityComponent polymorphed)
    {
        // Carry pool changes back, then stamp the cooldown on the original
        // mimic's transform action entity. SetCooldown grays out only THAT
        // action; other mimics' action entities are untouched.
        if (TryComp<AbominationMimicComponent>(disguisedUid, out var disguisedMimic) &&
            TryComp<AbominationMimicComponent>(polymorphed.Parent, out var originalMimic))
        {
            originalMimic.AssimilatedPool = new List<AbominationAssimilationProfile>(disguisedMimic.AssimilatedPool);
            Dirty(polymorphed.Parent, originalMimic);

            // Resolve the action entity at revert time — scan the original mimic's
            // action container for the transform action. Stored UIDs could go
            // stale if MobStateActions re-grants the action mid-disguise.
            var foundAction = FindTransformAction(polymorphed.Parent);
            if (foundAction is { } actionEnt)
                _actions.SetCooldown(actionEnt, disguisedMimic.TransformCooldown);
        }

        // Heal the restored mimic back to full — every transform (in or out)
        // resets the body. Done before Revert so polymorph's transferDamage
        // doesn't pull the disguise's accumulated damage back onto it.
        HealToFull(polymorphed.Parent);

        _polymorph.Revert((disguisedUid, null));
    }

    private EntityUid? FindTransformAction(EntityUid mimic)
    {
        foreach (var (actionId, _) in _actions.GetActions(mimic))
        {
            if (_actions.GetEvent(actionId) is AbominationMimicTransformActionEvent)
                return actionId;
        }
        return null;
    }

    private void ApplyProfile(EntityUid disguised, AbominationAssimilationProfile profile)
    {
        if (!string.IsNullOrWhiteSpace(profile.Name))
            _metaData.SetEntityName(disguised, profile.Name);

        ApplyFactions(disguised, profile.Factions);
        ApplyIffFactions(disguised, profile.IffFactions);
        CopySkillsFromSource(disguised, profile);
        ApplyAppearance(disguised, profile.Appearance);

        // Carry over marker components from the source. Currently just
        // TribalComponent — mimics wearing a tribal's face need to count as
        // tribals for win rules and faction-target picks.
        if (profile.IsTribal)
            EnsureComp<Content.Shared.AU14.TribalComponent>(disguised);
    }

    private void ApplyFactions(EntityUid disguised, IEnumerable<string> factions)
    {
        if (!TryComp<NpcFactionMemberComponent>(disguised, out var npc))
            return;

        foreach (var existing in npc.Factions.ToArray())
            _faction.RemoveFaction((disguised, npc), existing.Id, dirty: false);

        foreach (var faction in factions)
            _faction.AddFaction((disguised, npc), faction);
    }

    private void ApplyIffFactions(EntityUid disguised, IEnumerable<string> iffFactions)
    {
        _gunIff.ClearUserFactions(disguised);
        foreach (var faction in iffFactions)
            _gunIff.AddUserFaction(disguised, faction);
    }

    private void CopySkillsFromSource(EntityUid disguised, AbominationAssimilationProfile profile)
    {
        if (profile.SourceEntity is not { } netSource ||
            !TryGetEntity(netSource, out var source) ||
            !TryComp<SkillsComponent>(source.Value, out var sourceSkills))
        {
            return;
        }

        _skills.SetSkills(disguised, new Dictionary<EntProtoId<SkillDefinitionComponent>, int>(sourceSkills.Skills));
    }

    private void ApplyAppearance(EntityUid disguised, AbominationAppearanceSnapshot? snapshot)
    {
        if (snapshot is null || string.IsNullOrEmpty(snapshot.Species.Id))
            return;

        if (!TryComp<HumanoidAppearanceComponent>(disguised, out var humanoid))
            return;

        _humanoid.SetSpecies(disguised, snapshot.Species, sync: false, humanoid);
        _humanoid.SetSex(disguised, snapshot.Sex, sync: false, humanoid);
        humanoid.SkinColor = snapshot.SkinColor;
        humanoid.EyeColor = snapshot.EyeColor;
        humanoid.Age = snapshot.Age;
        humanoid.Gender = snapshot.Gender;
        humanoid.MarkingSet = new MarkingSet(snapshot.MarkingSet);
        humanoid.CustomBaseLayers = new Dictionary<HumanoidVisualLayers, CustomBaseLayerInfo>(snapshot.CustomBaseLayers);
        Dirty(disguised, humanoid);
    }
}
