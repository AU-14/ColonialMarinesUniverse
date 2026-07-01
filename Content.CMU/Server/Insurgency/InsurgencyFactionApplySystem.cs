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
using Content.Shared.UserInterface;
using Robust.Shared.Audio;
using Robust.Shared.Player;

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

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _activeFaction = null;
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
        _activeFaction = definition;

        // A faction is chosen: clear the leader's pending marker so their client drops the reopen button.
        var pending = EntityQueryEnumerator<InsurgencyPendingFactionSelectionComponent>();
        while (pending.MoveNext(out var leader, out _))
            RemComp<InsurgencyPendingFactionSelectionComponent>(leader);

        InjectVendorSections(definition);
        AnnounceToMembers(definition);

        var ev = new InsurgencyFactionAppliedEvent(definition);
        RaiseLocalEvent(ref ev);
    }

    /// <summary>
    ///     Turns any entity into this faction's vendor: injects the sections, strips the ID / job /
    ///     rank / faction gates a real GOVFOR vendor prototype might carry, initializes each entry's
    ///     runtime bounds, and grafts the vendor UI on so it actually opens when used. Called when the
    ///     Heavy Cell Kit deploys a vendor after the faction has been applied.
    /// </summary>
    public void ConfigureFactionVendor(EntityUid vendor, FactionVendorDefinition definition, int index)
    {
        // Built-in factions reuse a real, fully-configured vendor prototype. Keep its own arsenal,
        // points mode, and UI exactly as the prototype ships them; only apply the placement niceties
        // (unanchored, freely re-wrenchable, optional invulnerability) and the tracking marker.
        if (definition.UseBaseModelSections)
        {
            EnsureComp<AnchorableComponent>(vendor);
            _transform.Unanchor(vendor);

            if (definition.Invulnerable)
                _godmode.EnableGodmode(vendor);

            var baseMarker = EnsureComp<InsurgencyFactionVendorComponent>(vendor);
            baseMarker.VendorIndex = index;
            return;
        }

        var comp = EnsureComp<CMAutomatedVendorComponent>(vendor);
        comp.Sections = definition.Sections;

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

        // Build unwrenched and freely un/re-wrenchable regardless of the base entity: add anchoring
        // support and leave it unanchored so the leader can wrench it down or pick a new spot.
        EnsureComp<AnchorableComponent>(vendor);
        _transform.Unanchor(vendor);

        // Optional invulnerability so base entities that break or change state on damage stay put.
        if (definition.Invulnerable)
            _godmode.EnableGodmode(vendor);

        var marker = EnsureComp<InsurgencyFactionVendorComponent>(vendor);
        marker.VendorIndex = index;
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

        var submissions = _activeFaction.Economy.PointsSubmissions;
        if (submissions.Count == 0)
            return;

        if (!TryComp(analyzer, out AnalyzerComponent? comp))
            return;

        comp.Conversions.Clear();
        foreach (var entry in submissions)
        {
            comp.Conversions.Add(new AnalyzerConversionEntry
            {
                Entity = entry.Entity,
                AmountPerPoint = System.Math.Max(1, entry.AmountPerPoint),
            });
        }

        // Open the cash slot so the configured (possibly non-currency) goods can be inserted at all.
        if (TryComp(analyzer, out ItemSlotsComponent? slots))
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

            vendor.Sections = vendors[marker.VendorIndex].Sections;
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
        var briefing = BuildBriefing(definition);

        var query = EntityQueryEnumerator<CLFMemberComponent, ActorComponent>();
        while (query.MoveNext(out var uid, out var member, out var actor))
        {
            // Swap the team status icon so members read as this faction instead of generic CLF.
            if (definition.Metadata.StatusIcon is { } icon)
            {
                member.StatusIcon = icon;
                Dirty(uid, member);
            }

            ICommonSession session = actor.PlayerSession;
            _antag.SendBriefing(session, briefing, BriefingColor, BriefingSound);
            _eui.OpenEui(new InsurgencyFactionRevealEui(definition), session);
        }
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
        if (!string.IsNullOrWhiteSpace(meta.RoleplayText))
            parts.Add(meta.RoleplayText);

        return string.Join("\n\n", parts);
    }
}
