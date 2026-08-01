using Content.Server.Antag;
using Content.Server.EUI;
using Content.Server._AU14.Insurgency.Selection;
using Content.Shared._AU14.Insurgency;
using Content.Shared._AU14.Insurgency.Selection;
using Content.Shared._CMU14.Threats.Mobs.CLF;
using Content.Shared._AU14.Vendors;
using Content.Shared._RMC14.Vendors;
using Content.Shared.AU14;
using Content.Shared.Access.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Construction.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Content.Shared.Roles.Jobs;
using Content.Shared.StatusIcon;
using Content.Shared.UserInterface;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._AU14.Insurgency;

/// <summary>
///     Applies a chosen <see cref="FactionDefinition"/> for the round: injects vendor sections,
///     sets the economy conversion rate, and pushes the faction's title / description / roleplay
///     to current INSFOR members as a briefing and a popup.
///
///     This is the single consume point for the schema. The apply runs once per faction selection,
///     driven by <see cref="ApplyFaction"/>, never by a tick loop. State is cleared on round restart.
/// </summary>
public sealed class InsurgencyFactionApplySystem : EntitySystem
{
    [Dependency] private AntagSelectionSystem _antag = default!;
    [Dependency] private EuiManager _eui = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private SharedGodmodeSystem _godmode = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedJobSystem _job = default!;

    // ---------------------------------------------------------------------
    // Presentation tunables. One place to change how the faction announcement
    // reads and sounds when it lands on members.
    // ---------------------------------------------------------------------
    private static readonly Color BriefingColor = Color.Red;
    private static readonly SoundSpecifier BriefingSound =
        new SoundPathSpecifier("/Audio/Ambience/Antag/headrev_start.ogg");

    /// <summary>
    ///     The faction applied for the current round, or null if none has been applied yet.
    ///     Cleared on round restart.
    /// </summary>
    private FactionDefinition? _activeFaction;
    private uint _activeGeneration;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
    }

    // A cell member who spawns after the faction was already chosen (a late join) still needs the faction's
    // icon, briefing, and reveal popup - the one-shot announcement at apply time only reached whoever was
    // already in.
    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        if (_activeFaction is not { } definition)
            return;

        if (!TryComp<CLFMemberComponent>(ev.Mob, out var member))
            return;

        ApplyToMember(ev.Mob, member, ev.Player, definition);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _activeFaction = null;
        _activeGeneration = 0;
    }

    /// <summary>
    ///     The faction definition applied this round, if any.
    /// </summary>
    public FactionDefinition? GetActiveFaction() => _activeFaction;

    /// <summary>
    ///     Intel-dollar to vendor-point conversion for the active faction, or the schema default
    ///     if no faction is applied. The colony economy reads this rather than owning its own rate
    ///     (wired in Phase 5). Single point of change for the conversion.
    /// </summary>
    public float GetDollarsToPointsRate() =>
        _activeFaction?.Economy.DollarsToPointsRate ?? FactionDefinition.DefaultDollarsToPointsRate;

    /// <summary>
    ///     Apply a faction definition for the round. Server-authoritative: callers pass an already
    ///     validated definition (Default factions from the DB, Custom factions after the Phase 2
    ///     server validation gate). Safe to call again to switch factions mid-setup.
    /// </summary>
    public void ApplyFaction(FactionDefinition definition)
    {
        // Never retain an editor/DB DTO or any of its nested collections. Runtime systems receive a
        // server-owned copy that can no longer be changed through the caller's object graph.
        var runtimeDefinition = CloneFactionDefinition(definition);
        InsurgencyFactionValidator.Sanitize(runtimeDefinition);
        _activeFaction = runtimeDefinition;
        _activeGeneration++;

        // A faction is chosen: clear the leader's pending marker so their client drops the reopen button.
        var pending = EntityQueryEnumerator<InsurgencyPendingFactionSelectionComponent>();
        while (pending.MoveNext(out var leader, out _))
            RemComp<InsurgencyPendingFactionSelectionComponent>(leader);

        InjectVendorSections(runtimeDefinition);
        AnnounceToMembers(runtimeDefinition);

        var ev = new InsurgencyFactionAppliedEvent(runtimeDefinition);
        RaiseLocalEvent(ref ev);
    }

    private static FactionDefinition CloneFactionDefinition(FactionDefinition source)
    {
        var clone = new FactionDefinition
        {
            SchemaVersion = source.SchemaVersion,
            Metadata = new FactionMetadata
            {
                Title = source.Metadata.Title,
                Description = source.Metadata.Description,
                RoleplayText = source.Metadata.RoleplayText,
                RecruitedMessage = source.Metadata.RecruitedMessage,
                FlagEntity = source.Metadata.FlagEntity,
                StatusIcon = source.Metadata.StatusIcon,
                RecruitStatusIcon = source.Metadata.RecruitStatusIcon,
                BuiltinOverrideOf = source.Metadata.BuiltinOverrideOf,
                OpposedGovforFactions = new List<string>(source.Metadata.OpposedGovforFactions),
            },
            Economy = new FactionEconomy
            {
                DollarsToPointsRate = source.Economy.DollarsToPointsRate,
                IncludeDollars = source.Economy.IncludeDollars,
            },
            CellKit = new CellKitManifest
            {
                PlaceableEntities = new List<EntProtoId>(source.CellKit.PlaceableEntities),
                VendorDefinitions = new List<FactionVendorDefinition>(),
            },
            RoleLoadouts = new List<FactionRoleLoadout>(),
        };

        foreach (var icon in source.Metadata.JobStatusIcons)
            clone.Metadata.JobStatusIcons.Add(new FactionJobIcon { Role = icon.Role, Icon = icon.Icon });

        foreach (var submission in source.Economy.PointsSubmissions)
        {
            clone.Economy.PointsSubmissions.Add(new PointsSubmissionEntry
            {
                Entity = submission.Entity,
                PointsPerItemMode = submission.PointsPerItemMode,
                AmountPerPoint = submission.AmountPerPoint,
                PointsPerItem = submission.PointsPerItem,
            });
        }

        foreach (var vendor in source.CellKit.VendorDefinitions)
        {
            clone.CellKit.VendorDefinitions.Add(new FactionVendorDefinition
            {
                Name = vendor.Name,
                BaseModel = vendor.BaseModel,
                Sections = CloneVendorSections(vendor.Sections),
                Wrenchable = vendor.Wrenchable,
                Invulnerable = vendor.Invulnerable,
                UsesIntelPoints = vendor.UsesIntelPoints,
                UseBaseModelSections = vendor.UseBaseModelSections,
            });
        }

        foreach (var loadout in source.RoleLoadouts)
        {
            clone.RoleLoadouts.Add(new FactionRoleLoadout
            {
                Role = loadout.Role,
                Contents = new List<EntProtoId>(loadout.Contents),
            });
        }

        return clone;
    }

    private static List<CMVendorSection> CloneVendorSections(List<CMVendorSection> source)
    {
        var sections = new List<CMVendorSection>(source.Count);
        foreach (var section in source)
        {
            var cloned = new CMVendorSection
            {
                Name = section.Name,
                Choices = section.Choices,
                TakeAll = section.TakeAll,
                TakeOne = section.TakeOne,
                SharedSpecLimit = section.SharedSpecLimit,
                SharedJOLimit = section.SharedJOLimit,
                Jobs = new(section.Jobs),
                Ranks = new(section.Ranks),
                Holidays = new(section.Holidays),
                HasBoxes = section.HasBoxes,
            };

            foreach (var entry in section.Entries)
                cloned.Entries.Add(entry with { LinkedEntries = new(entry.LinkedEntries) });

            sections.Add(cloned);
        }

        return sections;
    }

    /// <summary>
    ///     Turns any entity into this faction's vendor: injects the sections, strips the ID / job /
    ///     rank / faction gates a real GOVFOR vendor prototype might carry, initializes each entry's
    ///     runtime bounds, and grafts the vendor UI on so it actually opens when used. Called when the
    ///     Heavy Cell Kit deploys a vendor after the faction has been applied.
    /// </summary>
    public void ConfigureFactionVendor(EntityUid vendor, FactionVendorDefinition definition, int index)
    {
        // Show the faction-authored name on the placed vendor, whichever configuration branch runs below.
        if (!string.IsNullOrWhiteSpace(definition.Name))
            _metaData.SetEntityName(vendor, definition.Name);

        // Built-in factions reuse a real, fully-configured vendor prototype. Keep its own arsenal,
        // points mode, and UI exactly as the prototype ships them; only apply the placement niceties
        // (unanchored, freely re-wrenchable, optional invulnerability) and the tracking marker.
        if (definition.UseBaseModelSections)
        {
            ApplyWrenchable(vendor, definition.Wrenchable);

            if (definition.Invulnerable)
                _godmode.EnableGodmode(vendor);

            var baseMarker = EnsureComp<InsurgencyFactionVendorComponent>(vendor);
            baseMarker.VendorIndex = index;
            return;
        }

        var comp = EnsureComp<CMAutomatedVendorComponent>(vendor);
        comp.Sections = CloneVendorSections(definition.Sections);

        // Any entity can be a faction vendor, so drop the access, job, rank, and faction restrictions.
        // INSFOR members are never on a GOVFOR vendor's ID whitelist, and the faction editor may well
        // reuse a real GOVFOR vendor as the base model.
        comp.Jobs.Clear();
        comp.Ranks.Clear();
        comp.Access.Clear();
        RemComp<AccessReaderComponent>(vendor);
        RemComp<ActivatableUIRequiresAccessComponent>(vendor);

        // Wire the vendor to the cell's shared intel points (the "clf" win-point pool the intel computer
        // feeds) when the author opted in, so submitting money at the intel machine stocks the vendors.
        comp.UseObjectivePoints = definition.UsesIntelPoints;
        comp.Faction = "clf";

        // MapInit already ran on the base entity before its sections were injected, so mirror what it
        // does for a freshly stocked vendor: the current amount is also the ceiling it restocks to.
        foreach (var section in comp.Sections)
        {
            foreach (var entry in section.Entries)
            {
                if (entry.Box != null)
                    continue;

                entry.Multiplier = entry.Amount;
                entry.Max = entry.Amount;
            }
        }

        // A global per-category cap (section.SharedJOLimit) is enforced by the existing vend logic
        // through this component, so add it whenever any section sets that limit.
        foreach (var section in comp.Sections)
        {
            if (section.SharedJOLimit != null)
            {
                EnsureComp<AU14VendorJOComponent>(vendor);
                break;
            }
        }

        Dirty(vendor, comp);

        // Graft the vendor interface onto the entity so using it opens the arsenal, whatever the base
        // entity normally is.
        _ui.SetUi(vendor, CMAutomatedVendorUI.Key, new InterfaceData("CMAutomatedVendorBui"));
        var activatable = EnsureComp<ActivatableUIComponent>(vendor);
        activatable.Key = CMAutomatedVendorUI.Key;
        Dirty(vendor, activatable);

        ApplyWrenchable(vendor, definition.Wrenchable);

        // Optional invulnerability so base entities that break or change state on damage stay put.
        if (definition.Invulnerable)
            _godmode.EnableGodmode(vendor);

        var marker = EnsureComp<InsurgencyFactionVendorComponent>(vendor);
        marker.VendorIndex = index;
    }

    /// <summary>
    ///     Applies the vendor's wrenchable choice. Wrenchable vendors are built unanchored and gain
    ///     anchoring support so the leader can wrench them down or reposition them, whatever the base
    ///     entity allowed. Non-wrenchable vendors have anchoring stripped so they cannot be moved.
    /// </summary>
    private void ApplyWrenchable(EntityUid vendor, bool wrenchable)
    {
        if (wrenchable)
        {
            EnsureComp<AnchorableComponent>(vendor);
            _transform.Unanchor(vendor);
        }
        else
        {
            RemComp<AnchorableComponent>(vendor);
        }
    }

    /// <summary>
    ///     Applies the active faction's submittable-for-points table onto a deployed analyzer machine.
    ///     No table means the analyzer keeps its built-in dollars behavior. When a table is set, the
    ///     analyzer's cash-slot whitelist is opened so the configured goods can be inserted; anything
    ///     not in the table is simply not credited (handled in AnalyzerSystem).
    /// </summary>
    public void ConfigureFactionAnalyzer(EntityUid analyzer)
    {
        if (_activeFaction == null)
            return;

        if (!TryComp(analyzer, out AnalyzerComponent? comp))
            return;

        var economy = _activeFaction.Economy;

        // Whether plain dollars still count is a faction-wide switch, independent of custom submittables.
        comp.IncludeDollars = economy.IncludeDollars;

        var submissions = economy.PointsSubmissions;
        comp.Conversions.Clear();
        foreach (var entry in submissions)
        {
            comp.Conversions.Add(new AnalyzerConversionEntry
            {
                Entity = entry.Entity,
                PointsPerItemMode = entry.PointsPerItemMode,
                AmountPerPoint = System.Math.Max(1, entry.AmountPerPoint),
                PointsPerItem = System.Math.Max(1, entry.PointsPerItem),
            });
        }

        // Open the cash slot so the configured (possibly non-currency) goods can be inserted at all.
        if (submissions.Count > 0 && TryComp(analyzer, out ItemSlotsComponent? slots))
        {
            foreach (var slot in slots.Slots.Values)
                slot.Whitelist = null;

            Dirty(analyzer, slots);
        }
    }

    /// <summary>
    ///     Copies each faction vendor definition's sections onto every placed vendor tagged for
    ///     that definition. No-op when there are no tagged vendors, which is the normal Phase 0 case.
    /// </summary>
    private void InjectVendorSections(FactionDefinition definition)
    {
        var vendors = definition.CellKit.VendorDefinitions;
        if (vendors.Count == 0)
            return;

        var query = EntityQueryEnumerator<InsurgencyFactionVendorComponent, CMAutomatedVendorComponent>();
        while (query.MoveNext(out var uid, out var marker, out var vendor))
        {
            if (marker.VendorIndex < 0 || marker.VendorIndex >= vendors.Count)
                continue;

            // Base-model vendors keep their prototype sections; do not overwrite them.
            if (vendors[marker.VendorIndex].UseBaseModelSections)
                continue;

            vendor.Sections = CloneVendorSections(vendors[marker.VendorIndex].Sections);
            Dirty(uid, vendor);
        }
    }

    /// <summary>
    ///     For every current INSFOR member with a session: swaps their faction status icon to the
    ///     chosen faction's icon, sends the faction briefing to chat, and opens the reveal popup that
    ///     shows the title, roleplay style, and flag/icon sprites. Runs over the existing member set
    ///     once; faction selection happens after spawn so members already exist when this is called.
    /// </summary>
    private void AnnounceToMembers(FactionDefinition definition)
    {
        var query = EntityQueryEnumerator<CLFMemberComponent, ActorComponent>();
        while (query.MoveNext(out var uid, out var member, out var actor))
            ApplyToMember(uid, member, actor.PlayerSession, definition);
    }

    // Everything a single cell member gets from the applied faction: the team status icon swap, the chat
    // briefing, and the reveal popup with the title / roleplay style. Shared by the initial announcement and
    // by late-joining members so both paths behave identically.
    private void ApplyToMember(EntityUid uid, CLFMemberComponent member, ICommonSession session, FactionDefinition definition)
    {
        var application = EnsureComp<InsurgencyFactionApplicationComponent>(uid);
        if (application.BriefingGeneration == _activeGeneration)
            return;

        application.BriefingGeneration = _activeGeneration;

        // Swap the team status icon so members read as this faction instead of generic CLF. A member whose
        // job has a per-job override uses that; everyone else falls back to the faction-wide icon.
        if (ResolveJobIcon(uid, definition) is { } icon)
        {
            member.StatusIcon = icon;
            Dirty(uid, member);
        }

        _antag.SendBriefing(session, BuildBriefing(definition), BriefingColor, BriefingSound);
        _eui.OpenEui(new InsurgencyFactionRevealEui(definition), session);
    }

    /// <summary>
    ///     Gives a member recruited in-round (for example tattooed) the active faction's membership icon, so
    ///     they read as this faction instead of the default CLF. Uses the faction's recruit-fallback icon when
    ///     set, otherwise a per-job override for their job, otherwise the faction-wide icon. No-op when no
    ///     faction is active or it configures no icon at all.
    /// </summary>
    public void ApplyRecruitIcon(EntityUid member, CLFMemberComponent memberComp)
    {
        if (GetActiveFaction() is not { } definition)
            return;

        var icon = definition.Metadata.RecruitStatusIcon ?? ResolveJobIcon(member, definition);
        if (icon is not { } resolved)
            return;

        memberComp.StatusIcon = resolved;
        Dirty(member, memberComp);
    }

    /// <summary>
    ///     Picks the status icon for a member: the override for their job if one is configured, otherwise the
    ///     faction-wide icon. Null only when the faction sets no icon at all.
    /// </summary>
    private ProtoId<FactionIconPrototype>? ResolveJobIcon(EntityUid member, FactionDefinition definition)
    {
        if (definition.Metadata.JobStatusIcons.Count > 0 &&
            _mind.TryGetMind(member, out var mindId, out _) &&
            _job.MindTryGetJobId(mindId, out var job) && job is { } jobId)
        {
            foreach (var entry in definition.Metadata.JobStatusIcons)
            {
                if (entry.Icon is { } jobIcon && string.Equals(entry.Role, jobId.Id, StringComparison.OrdinalIgnoreCase))
                    return jobIcon;
            }
        }

        return definition.Metadata.StatusIcon;
    }

    /// <summary>
    ///     Assembles the briefing text from the faction metadata. Kept plain and readable; the
    ///     roleplay line tells members how they are meant to play this faction.
    /// </summary>
    private static string BuildBriefing(FactionDefinition definition)
    {
        var meta = definition.Metadata;
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(meta.Title))
            parts.Add(meta.Title);
        if (!string.IsNullOrWhiteSpace(meta.Description))
            parts.Add(meta.Description);
        // The roleplay style deliberately stays out of chat; it lives in the reveal popup only.

        return string.Join("\n\n", parts);
    }
}
