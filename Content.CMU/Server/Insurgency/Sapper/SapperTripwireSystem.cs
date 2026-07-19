using System;
using System.Linq;
using System.Numerics;
using Content.Server.Explosion.EntitySystems;
using Content.Shared._AU14.Insurgency.Sapper;
using Content.Shared._CMU14.Threats.Mobs.CLF;
using Content.Shared.Examine;
using Content.Shared.Explosion.Components;
using Content.Shared.Explosion.EntitySystems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Content.Shared.StepTrigger.Systems;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;

namespace Content.Server._AU14.Insurgency.Sapper;

/// <summary>
///     The two-part tripwire. Planting the charge hands the sapper the wire's "other end"; they carry it to
///     where they want the line to run and use it there. If that spot is in a straight line, in range, and in
///     line of sight, a near-invisible wire is strung between the two points with a trip trigger on every tile
///     between them. Anything that isn't a friendly crossing any part of the wire detonates the charge and
///     every explosive lashed to it.
///
///     Planting, arming, hiding, and disarming are the ordinary <see cref="SapperTrapComponent"/> lifecycle
///     handled by the shared sapper system; this system runs out the wire, cleans it up, attaches explosive
///     payloads, and fires everything when the wire is crossed.
/// </summary>
public sealed class SapperTripwireSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly CollisionWakeSystem _collisionWake = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly SharedExplosionSystem _explosion = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly TriggerSystem _trigger = default!;

    // Very short fuse the attached devices are set off on, so each fires its own real prototype effect.
    private const float PayloadFuseDelay = 0.2f;

    public override void Initialize()
    {
        base.Initialize();

        // Both of these run after the shared sapper system's own deploy/disarm handlers so the device is
        // already planted (or already torn down) by the time we hand out the end / clear the wire.
        SubscribeLocalEvent<SapperTripwireComponent, SapperTrapDeployDoAfterEvent>(OnDeployed, after: new[] { typeof(SapperTrapSystem) });
        SubscribeLocalEvent<SapperTripwireComponent, SapperTrapDisarmDoAfterEvent>(OnDisarmed, after: new[] { typeof(SapperTrapSystem) });
        // Runs before the shared plant handler so it can refuse to deploy a tripwire with no charge attached.
        SubscribeLocalEvent<SapperTripwireComponent, UseInHandEvent>(OnDeviceUseInHand, before: new[] { typeof(SapperTrapSystem) });
        SubscribeLocalEvent<SapperTripwireComponent, InteractUsingEvent>(OnAttachPayload);
        // Before it is planted, an attached explosive can be taken back off via the alt-click menu.
        SubscribeLocalEvent<SapperTripwireComponent, GetVerbsEvent<AlternativeVerb>>(OnGetEjectVerb);
        // Whatever removes the device (detonation, being shot apart, admin delete) takes its wire with it.
        SubscribeLocalEvent<SapperTripwireComponent, ComponentShutdown>(OnDeviceShutdown);

        // The carried "other end": using it in hand runs the wire from the charge to that spot.
        SubscribeLocalEvent<SapperTripwireEndPlacerComponent, UseInHandEvent>(OnPlacerUseInHand, before: new[] { typeof(SapperTrapSystem) });

        SubscribeLocalEvent<SapperTripwireSegmentComponent, StepTriggerAttemptEvent>(OnSegmentStepAttempt);
        SubscribeLocalEvent<SapperTripwireSegmentComponent, StepTriggeredOffEvent>(OnSegmentStepped);
    }

    // ----- planting hands out the other end -------------------------------

    private void OnDeployed(Entity<SapperTripwireComponent> ent, ref SapperTrapDeployDoAfterEvent args)
    {
        // The shared deploy handler sets Deployed once the plant actually lands; only hand out the end then,
        // and never twice.
        if (!TryComp<SapperTrapComponent>(ent, out var trap) || !trap.Deployed)
            return;

        if (ent.Comp.WireEnd != null || ent.Comp.Segments.Count > 0 || Exists(ent.Comp.PendingPlacer))
            return;

        // Hand the sapper the other end to carry to where they want the wire to run.
        var placer = Spawn(ent.Comp.EndPlacerPrototype, Transform(args.User).Coordinates);
        EnsureComp<SapperTripwireEndPlacerComponent>(placer).Device = ent;
        ent.Comp.PendingPlacer = placer;

        _hands.TryPickupAnyHand(args.User, placer);
        _popup.PopupClient(Loc.GetString("insfor-sapper-tripwire-place-other-end", ("range", ent.Comp.MaxWireRange)), ent, args.User);
    }

    private void OnPlacerUseInHand(Entity<SapperTripwireEndPlacerComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (!TryComp<SapperTripwireComponent>(ent.Comp.Device, out var deviceComp))
        {
            _popup.PopupClient(Loc.GetString("insfor-sapper-tripwire-charge-gone"), args.User, args.User, PopupType.SmallCaution);
            QueueDel(ent);
            return;
        }

        var device = new Entity<SapperTripwireComponent>(ent.Comp.Device, deviceComp);

        // Already strung (shouldn't happen): just clean up the stray end.
        if (device.Comp.WireEnd != null || device.Comp.Segments.Count > 0)
        {
            QueueDel(ent);
            return;
        }

        if (!TryStringWire(device, args.User))
            return; // TryStringWire already told them why; keep the end so they can reposition and retry.

        device.Comp.PendingPlacer = null;
        _popup.PopupClient(Loc.GetString("insfor-sapper-tripwire-strung"), args.User, args.User);
        QueueDel(ent);
    }

    // ----- stringing / clearing the wire ----------------------------------

    private bool TryStringWire(Entity<SapperTripwireComponent> device, EntityUid user)
    {
        var deviceXform = Transform(device);
        var userXform = Transform(user);

        var grid = deviceXform.GridUid;
        if (grid == null || grid != userXform.GridUid || !TryComp<MapGridComponent>(grid, out var gridComp))
        {
            _popup.PopupClient(Loc.GetString("insfor-sapper-tripwire-bad-spot"), user, user, PopupType.SmallCaution);
            return false;
        }

        var deviceTile = _map.TileIndicesFor(grid.Value, gridComp, deviceXform.Coordinates);
        var targetTile = _map.TileIndicesFor(grid.Value, gridComp, userXform.Coordinates);
        var delta = targetTile - deviceTile;

        if (delta == Vector2i.Zero)
        {
            _popup.PopupClient(Loc.GetString("insfor-sapper-tripwire-too-close"), user, user, PopupType.SmallCaution);
            return false;
        }

        // The wire has to be a clean straight run: a cardinal or a perfect diagonal.
        if (delta.X != 0 && delta.Y != 0 && Math.Abs(delta.X) != Math.Abs(delta.Y))
        {
            _popup.PopupClient(Loc.GetString("insfor-sapper-tripwire-not-straight"), user, user, PopupType.SmallCaution);
            return false;
        }

        var count = Math.Max(Math.Abs(delta.X), Math.Abs(delta.Y));
        if (count > device.Comp.MaxWireRange)
        {
            _popup.PopupClient(Loc.GetString("insfor-sapper-tripwire-too-far"), user, user, PopupType.SmallCaution);
            return false;
        }

        // Need a clear line of sight between the two ends: no walls in the way.
        if (!_examine.InRangeUnOccluded(device.Owner, userXform.Coordinates, count + 1.5f))
        {
            _popup.PopupClient(Loc.GetString("insfor-sapper-tripwire-no-los"), user, user, PopupType.SmallCaution);
            return false;
        }

        var step = new Vector2i(Math.Sign(delta.X), Math.Sign(delta.Y));
        var strandAngle = new Vector2(step.X, step.Y).ToAngle();

        for (var i = 1; i <= count; i++)
        {
            var coords = _map.GridTileToLocal(grid.Value, gridComp, deviceTile + step * i);
            var isEnd = i == count;
            var piece = Spawn(isEnd ? device.Comp.EndPrototype : device.Comp.SegmentPrototype, coords);

            var pieceXform = Transform(piece);
            _transform.SetLocalRotation(pieceXform, strandAngle);
            // Guard against a double-anchor (the prototype must not also anchor) which trips a snap-grid assert.
            if (!pieceXform.Anchored)
                _transform.AnchorEntity(piece, pieceXform);
            _physics.SetBodyType(piece, BodyType.Static);
            // Keep the anchored body awake, otherwise it sleeps and never registers a step.
            _collisionWake.SetEnabled(piece, false);

            EnsureComp<SapperTripwireSegmentComponent>(piece).Device = device;

            if (isEnd)
                device.Comp.WireEnd = piece;
            else
                device.Comp.Segments.Add(piece);
        }

        return true;
    }

    private void OnDisarmed(Entity<SapperTripwireComponent> ent, ref SapperTrapDisarmDoAfterEvent args)
    {
        if (args.Cancelled)
            return;

        CleanupWire(ent);

        // Drop any attached explosives at the device so they aren't swallowed when it's pocketed.
        if (_container.TryGetContainer(ent, ent.Comp.PayloadContainer, out var container))
            _container.EmptyContainer(container);
    }

    private void OnDeviceShutdown(Entity<SapperTripwireComponent> ent, ref ComponentShutdown args)
    {
        CleanupWire(ent);
    }

    private void CleanupWire(Entity<SapperTripwireComponent> ent)
    {
        foreach (var seg in ent.Comp.Segments)
            QueueDel(seg);
        ent.Comp.Segments.Clear();

        if (ent.Comp.WireEnd is { } end)
            QueueDel(end);
        ent.Comp.WireEnd = null;

        // A charge that never got its wire run still has the "other end" out in someone's hand: bin it too.
        if (ent.Comp.PendingPlacer is { } placer)
            QueueDel(placer);
        ent.Comp.PendingPlacer = null;
    }

    // ----- refusing to plant an empty tripwire ----------------------------

    private void OnDeviceUseInHand(Entity<SapperTripwireComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        // Audio traps carry no charge at all; they plant as-is.
        if (HasComp<SapperAudioTrapComponent>(ent))
            return;

        // A tripwire is nothing without a charge: it can only be planted once at least one explosive is on it.
        if (_container.TryGetContainer(ent, ent.Comp.PayloadContainer, out var container) && container.ContainedEntities.Count > 0)
            return;

        _popup.PopupClient(Loc.GetString("insfor-sapper-tripwire-need-explosive"), ent, args.User, PopupType.MediumCaution);
        args.Handled = true;
    }

    // ----- attaching explosives -------------------------------------------

    private void OnAttachPayload(Entity<SapperTripwireComponent> ent, ref InteractUsingEvent args)
    {
        // Only explosives can be lashed on; the shared system still handles the wirecutter disarm separately.
        // Audio traps take no payload at all.
        if (args.Handled || !HasComp<ExplosiveComponent>(args.Used) || HasComp<SapperAudioTrapComponent>(ent))
            return;

        var container = _container.EnsureContainer<Container>(ent, ent.Comp.PayloadContainer);
        if (container.ContainedEntities.Count >= ent.Comp.MaxPayload)
        {
            _popup.PopupClient(Loc.GetString("insfor-sapper-tripwire-full"), ent, args.User, PopupType.SmallCaution);
            args.Handled = true;
            return;
        }

        if (_container.Insert(args.Used, container))
        {
            _popup.PopupClient(Loc.GetString("insfor-sapper-tripwire-attached"), ent, args.User);
            args.Handled = true;
        }
    }

    // ----- ejecting explosives before planting -----------------------------

    private void OnGetEjectVerb(Entity<SapperTripwireComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        // Only while it is still a carried item: once planted, disarming is the way to get the payload back.
        if (TryComp<SapperTrapComponent>(ent, out var trap) && trap.Deployed)
            return;

        if (!_container.TryGetContainer(ent, ent.Comp.PayloadContainer, out var container) || container.ContainedEntities.Count == 0)
            return;

        var user = args.User;
        var device = ent;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("insfor-sapper-tripwire-eject-verb"),
            Act = () =>
            {
                if (!_container.TryGetContainer(device, device.Comp.PayloadContainer, out var c) || c.ContainedEntities.Count == 0)
                    return;

                // Take the most recently attached explosive back off, straight into the hand.
                var item = c.ContainedEntities[^1];
                if (_container.Remove(item, c))
                {
                    _hands.TryPickupAnyHand(user, item);
                    _popup.PopupEntity(Loc.GetString("insfor-sapper-tripwire-ejected"), device, user);
                }
            },
        });
    }

    // ----- crossing the wire ----------------------------------------------

    private void OnSegmentStepAttempt(Entity<SapperTripwireSegmentComponent> seg, ref StepTriggerAttemptEvent args)
    {
        // Live only while the parent device is armed, and never for the cell that laid it.
        if (!TryComp<SapperTrapComponent>(seg.Comp.Device, out var trap))
        {
            args.Continue = false;
            return;
        }

        args.Continue = trap.Armed && !HasComp<CLFMemberComponent>(args.Tripper);
    }

    private void OnSegmentStepped(Entity<SapperTripwireSegmentComponent> seg, ref StepTriggeredOffEvent args)
    {
        Detonate(seg.Comp.Device, args.Tripper);
    }

    private void Detonate(EntityUid device, EntityUid tripper)
    {
        if (!TryComp<SapperTripwireComponent>(device, out var comp))
            return;

        // Audio traps don't blow up: they whistle and report, and the wire stays strung for the next crossing.
        if (HasComp<SapperAudioTrapComponent>(device))
        {
            var ev = new SapperAudioTrapTrippedEvent(tripper, Transform(tripper).Coordinates);
            RaiseLocalEvent(device, ref ev);
            return;
        }

        // Set off each attached device through its own fuse/activation pipeline on a very short delay, so it
        // produces its real prototype-defined effect (blast, gas, VFX/SFX, whatever) rather than a generic
        // explosion. Multiple copies of the same item each fire, which naturally amplifies that item's effect.
        if (_container.TryGetContainer(device, comp.PayloadContainer, out var payload))
        {
            foreach (var item in payload.ContainedEntities.ToArray())
            {
                // Drop it out of the charge to the wire spot first so its effect originates there.
                _container.Remove(item, payload, force: true);
                _trigger.HandleTimerTrigger(item, tripper, PayloadFuseDelay, 0f, null, null);
            }
        }

        CleanupWire((device, comp));

        // Only the attached payload fires: the bare firing box has no blast of its own any more (it
        // used to double-explode alongside the payload). The box is simply consumed.
        QueueDel(device);
    }
}
