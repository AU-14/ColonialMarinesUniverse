using System.Numerics;
using Content.Shared._RMC14.NightVision;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Popups;
using Content.Shared.Tools.Systems;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Clothing.NightVision;

/// <summary>
///     Runs <see cref="CMUNightVisionMalfunctionComponent"/>: the glow on the wearer, the random cut-outs, refusing to
///     switch on while broken, and the screwdriver repair.
/// </summary>
public sealed class CMUNightVisionMalfunctionSystem : EntitySystem
{
    /// <summary>How often the glow is moved to match who's wearing the goggles. Matches the goggles' own check.</summary>
    private static readonly TimeSpan GlowInterval = TimeSpan.FromSeconds(0.25);

    [Dependency] private ItemToggleSystem _itemToggle = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedToolSystem _tools = default!;

    private TimeSpan _nextGlowUpdate;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUNightVisionMalfunctionComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<CMUNightVisionMalfunctionComponent, ItemToggleActivateAttemptEvent>(OnActivateAttempt);
        SubscribeLocalEvent<CMUNightVisionMalfunctionComponent, ItemToggledEvent>(OnToggled);
        SubscribeLocalEvent<CMUNightVisionMalfunctionComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<CMUNightVisionMalfunctionComponent, CMUNightVisionRepairDoAfterEvent>(OnRepairDoAfter);
        SubscribeLocalEvent<CMUNightVisionMalfunctionComponent, ExaminedEvent>(OnExamined);
    }

    private void OnShutdown(Entity<CMUNightVisionMalfunctionComponent> ent, ref ComponentShutdown args)
    {
        RemoveGlow(ent);
    }

    private void OnActivateAttempt(Entity<CMUNightVisionMalfunctionComponent> ent, ref ItemToggleActivateAttemptEvent args)
    {
        if (!ent.Comp.Broken)
            return;

        args.Cancelled = true;
        args.Popup = Loc.GetString("cmu-nvg-malfunction-wont-turn-on", ("item", ent.Owner));
    }

    private void OnToggled(Entity<CMUNightVisionMalfunctionComponent> ent, ref ItemToggledEvent args)
    {
        // Switching them on starts a fresh countdown, so they never cut out the moment they're pulled down.
        if (args.Activated)
            ent.Comp.NextCheck = _timing.CurTime + ent.Comp.CheckInterval;

        UpdateGlow(ent);
    }

    private void OnInteractUsing(Entity<CMUNightVisionMalfunctionComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !ent.Comp.Broken || !_tools.HasQuality(args.Used, ent.Comp.RepairQuality))
            return;

        args.Handled = true;
        if (_tools.UseTool(args.Used,
                args.User,
                ent,
                ent.Comp.RepairDelay,
                [ent.Comp.RepairQuality],
                new CMUNightVisionRepairDoAfterEvent(),
                out _))
        {
            _popup.PopupClient(Loc.GetString("cmu-nvg-malfunction-repair-start", ("item", ent.Owner)), ent, args.User);
        }
    }

    private void OnRepairDoAfter(Entity<CMUNightVisionMalfunctionComponent> ent, ref CMUNightVisionRepairDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || !ent.Comp.Broken)
            return;

        args.Handled = true;
        Repair(ent);
        _popup.PopupClient(Loc.GetString("cmu-nvg-malfunction-repair-done", ("item", ent.Owner)), ent, args.User);
    }

    private void OnExamined(Entity<CMUNightVisionMalfunctionComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.Broken)
            args.PushMarkup(Loc.GetString("cmu-nvg-malfunction-examine-broken", ("item", ent.Owner)));
    }

    /// <summary>
    ///     Makes the goggles cut out: they switch off, stop glowing and refuse to switch on until repaired.
    /// </summary>
    public void Malfunction(Entity<CMUNightVisionMalfunctionComponent> ent)
    {
        if (ent.Comp.Broken)
            return;

        var wearer = Wearer(ent);
        ent.Comp.Broken = true;
        Dirty(ent);

        _itemToggle.TryDeactivate(ent.Owner, predicted: false, showPopup: false);
        UpdateGlow(ent);

        var message = Loc.GetString("cmu-nvg-malfunction-cut-out", ("item", ent.Owner));
        if (wearer is { } user)
            _popup.PopupEntity(message, user, user, PopupType.MediumCaution);
        else
            _popup.PopupEntity(message, ent, PopupType.SmallCaution);
    }

    /// <summary>Fixes goggles that have cut out so they can be switched on again.</summary>
    public void Repair(Entity<CMUNightVisionMalfunctionComponent> ent)
    {
        if (!ent.Comp.Broken)
            return;

        ent.Comp.Broken = false;
        Dirty(ent);
    }

    public override void Update(float frameTime)
    {
        // The rolls and the glow entity are the server's call; clients just see the results.
        if (_net.IsClient)
            return;

        var now = _timing.CurTime;
        if (now < _nextGlowUpdate)
            return;

        _nextGlowUpdate = now + GlowInterval;

        var query = EntityQueryEnumerator<CMUNightVisionMalfunctionComponent, ItemToggleComponent>();
        while (query.MoveNext(out var uid, out var comp, out var toggle))
        {
            var ent = (uid, comp);
            UpdateGlow(ent);

            if (!toggle.Activated || comp.Broken)
                continue;

            // Only time spent actually giving someone night vision counts towards a cut-out.
            if (Wearer(ent) == null)
            {
                comp.NextCheck = now + comp.CheckInterval;
                continue;
            }

            if (now < comp.NextCheck)
                continue;

            comp.NextCheck = now + comp.CheckInterval;
            if (_random.Prob(comp.MalfunctionChance))
                Malfunction(ent);
        }
    }

    /// <summary>Who the goggles are currently giving night vision to.</summary>
    private EntityUid? Wearer(Entity<CMUNightVisionMalfunctionComponent> ent)
    {
        return TryComp(ent, out NightVisionItemComponent? nightVision) ? nightVision.User : null;
    }

    /// <summary>
    ///     Keeps a glow attached to whoever the goggles are working for, and none otherwise. The goggles sit inside a
    ///     helmet, whose storage hides any light on them, so the glow is its own entity riding on the wearer.
    /// </summary>
    private void UpdateGlow(Entity<CMUNightVisionMalfunctionComponent> ent)
    {
        if (_net.IsClient)
            return;

        EntityUid? target = null;
        if (!ent.Comp.Broken && _itemToggle.IsActivated(ent.Owner) && Wearer(ent) is { } wearer)
            target = wearer;

        if (ent.Comp.Glow is { } glow &&
            (target == null || TerminatingOrDeleted(glow) || Transform(glow).ParentUid != target))
        {
            RemoveGlow(ent);
        }

        if (target is { } user && ent.Comp.Glow == null && !TerminatingOrDeleted(user))
            ent.Comp.Glow = SpawnAttachedTo(ent.Comp.GlowPrototype, new EntityCoordinates(user, Vector2.Zero));
    }

    private void RemoveGlow(Entity<CMUNightVisionMalfunctionComponent> ent)
    {
        if (ent.Comp.Glow is not { } glow)
            return;

        ent.Comp.Glow = null;
        if (!TerminatingOrDeleted(glow))
            QueueDel(glow);
    }
}
