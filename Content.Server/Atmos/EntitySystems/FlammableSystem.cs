using Content.Server.Administration.Logs;
using Content.Server.Atmos.Components;
using Content.Server.Damage.Components;
using Content.Server.Stunnable;
using Content.Server.Temperature.Systems;
using Content.Shared._RMC14.Atmos;
using Content.Shared._RMC14.Water;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Projectile.Spit;
using Content.Shared.ActionBlocker;
using Content.Shared.Alert;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.FixedPoint;
using Content.Shared.IgnitionSource;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Rejuvenate;
using Content.Shared.Temperature;
using Content.Shared.Throwing;
using Content.Shared.Timing;
using Content.Shared.Toggleable;
using Content.Shared.Weapons.Melee.Events;
using JetBrains.Annotations;
using Robust.Server.Audio;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Atmos.EntitySystems
{
    public sealed partial class FlammableSystem : EntitySystem
    {
        [Dependency] private ActionBlockerSystem _actionBlockerSystem = default!;
        [Dependency] private AtmosphereSystem _atmosphereSystem = default!;
        [Dependency] private StunSystem _stunSystem = default!;
        [Dependency] private TemperatureSystem _temperatureSystem = default!;
        [Dependency] private SharedIgnitionSourceSystem _ignitionSourceSystem = default!;
        [Dependency] private DamageableSystem _damageableSystem = default!;
        [Dependency] private AlertsSystem _alertsSystem = default!;
        [Dependency] private FixtureSystem _fixture = default!;
        [Dependency] private IAdminLogManager _adminLogger = default!;
        [Dependency] private InventorySystem _inventory = default!;
        [Dependency] private SharedAppearanceSystem _appearance = default!;
        [Dependency] private SharedPopupSystem _popup = default!;
        [Dependency] private UseDelaySystem _useDelay = default!;
        [Dependency] private AudioSystem _audio = default!;
        [Dependency] private IRobustRandom _random = default!;
        [Dependency] private SharedRMCFlammableSystem _rmcFlammable = default!;
        [Dependency] private RMCWaterSystem _rmcWater = default!;
        [Dependency] private XenoSpitSystem _xenoSpit = default!;
        [Dependency] private IGameTiming _timing = default!;

        [Dependency] private EntityQuery<InventoryComponent> _inventoryQuery = default!;
        [Dependency] private EntityQuery<PhysicsComponent> _physicsQuery = default!;
        // CMU14: only alert-capable mobs take the per-second fire alert pass.
        [Dependency] private EntityQuery<AlertsComponent> _alertsQuery = default!;

        private static readonly TimeSpan UpdateTime = TimeSpan.FromSeconds(1);

        private readonly Dictionary<Entity<FlammableComponent>, float> _fireEvents = new();

        // CMU14: retains snapshot capacity between updates; entries live until the next rebuild.
        private readonly List<(EntityUid Uid, FlammableComponent Flammable)> _flammableUpdateQueue = new();

        // CMU14 Flammable Update Begin
        // The snapshot is rebuilt once per second and consumed in per-tick slices; rescanning every
        // flammable entity every tick dominated the system at map-wide counts (weeds, resin).
        private TimeSpan _nextSnapshot;
        private int _snapshotCursor;
        // CMU14 End

        // RMC14
        private EntityQuery<SteppingOnFireComponent> _steppingOnFireQuery;

        public override void Initialize()
        {
            UpdatesAfter.Add(typeof(AtmosphereSystem));

            SubscribeLocalEvent<FlammableComponent, MapInitEvent>(OnMapInit);
            // CMU14: components gained mid-cycle join the live snapshot without waiting for a rebuild.
            SubscribeLocalEvent<FlammableComponent, ComponentInit>(OnFlammableInit);
            SubscribeLocalEvent<FlammableComponent, InteractUsingEvent>(OnInteractUsing);
            SubscribeLocalEvent<FlammableComponent, StartCollideEvent>(OnCollide);
            SubscribeLocalEvent<FlammableComponent, IsHotEvent>(OnIsHot);
            SubscribeLocalEvent<FlammableComponent, TileFireEvent>(OnTileFire);
            SubscribeLocalEvent<FlammableComponent, RejuvenateEvent>(OnRejuvenate);
            SubscribeLocalEvent<FlammableComponent, ResistFireAlertEvent>(OnResistFireAlert);
            Subs.SubscribeWithRelay<FlammableComponent, ExtinguishEvent>(OnExtinguishEvent);

            SubscribeLocalEvent<IgniteOnCollideComponent, StartCollideEvent>(IgniteOnCollide);
            SubscribeLocalEvent<IgniteOnCollideComponent, LandEvent>(OnIgniteLand);

            SubscribeLocalEvent<IgniteOnMeleeHitComponent, MeleeHitEvent>(OnMeleeHit);

            SubscribeLocalEvent<ExtinguishOnInteractComponent, ActivateInWorldEvent>(OnExtinguishActivateInWorld);

            SubscribeLocalEvent<IgniteOnHeatDamageComponent, DamageChangedEvent>(OnDamageChanged);

            // RMC14
            _steppingOnFireQuery = GetEntityQuery<SteppingOnFireComponent>();
        }

        private void OnExtinguishEvent(Entity<FlammableComponent> ent, ref ExtinguishEvent args)
        {
            // You know I'm really not sure if having AdjustFireStacks *after* Extinguish,
            // but I'm just moving this code, not questioning it.
            TryExtinguish(ent.AsNullable());
            AdjustFireStacks(ent, args.FireStacksAdjustment, ent.Comp);
        }

        private void OnMeleeHit(EntityUid uid, IgniteOnMeleeHitComponent component, MeleeHitEvent args)
        {
            foreach (var entity in args.HitEntities)
            {
                if (!TryComp<FlammableComponent>(entity, out var flammable))
                    continue;

                AdjustFireStacks(entity, component.FireStacks, flammable);
                if (component.FireStacks >= 0)
                    Ignite(entity, args.Weapon, flammable, args.User);
            }
        }

        private void OnIgniteLand(EntityUid uid, IgniteOnCollideComponent component, ref LandEvent args)
        {
            RemCompDeferred<IgniteOnCollideComponent>(uid);
        }

        private void IgniteOnCollide(EntityUid uid, IgniteOnCollideComponent component, ref StartCollideEvent args)
        {
            if (!args.OtherFixture.Hard || component.Count == 0)
                return;

            var otherEnt = args.OtherEntity;

            if (!TryComp(otherEnt, out FlammableComponent? flammable))
                return;

            //Only ignite when the colliding fixture is projectile or ignition.
            if (args.OurFixtureId != component.FixtureId && args.OurFixtureId != SharedProjectileSystem.ProjectileFixture)
            {
                return;
            }

            flammable.FireStacks += component.FireStacks;
            Ignite(otherEnt, uid, flammable);
            component.Count--;

            if (component.Count == 0)
                RemCompDeferred<IgniteOnCollideComponent>(uid);
        }

        // CMU14 method
        private void OnFlammableInit(Entity<FlammableComponent> ent, ref ComponentInit args)
            => _flammableUpdateQueue.Add((ent.Owner, ent.Comp));

        private void OnMapInit(EntityUid uid, FlammableComponent component, MapInitEvent args)
        {
            component.NextUpdate = _timing.CurTime + UpdateTime;

            // Sets up a fixture for flammable collisions.
            // TODO: Should this be generalized into a general non-hard 'effects' fixture or something? I can't think of other use cases for it.
            // This doesn't seem great either (lots more collisions generated) but there isn't a better way to solve it either that I can think of.

            if (!TryComp<PhysicsComponent>(uid, out var body))
                return;

            _fixture.TryCreateFixture(uid, component.FlammableCollisionShape, component.FlammableFixtureID, density: 0,
                hard: false, collisionMask: (int)CollisionGroup.FullTileLayer, body: body);
        }

        private void OnInteractUsing(EntityUid uid, FlammableComponent flammable, InteractUsingEvent args)
        {
            if (args.Handled)
                return;

            var isHotEvent = new IsHotEvent();
            RaiseLocalEvent(args.Used, isHotEvent);

            if (!isHotEvent.IsHot)
                return;

            Ignite(uid, args.Used, flammable, args.User);
            args.Handled = true;
        }

        private void OnExtinguishActivateInWorld(EntityUid uid, ExtinguishOnInteractComponent component, ActivateInWorldEvent args)
        {
            if (args.Handled || !args.Complex)
                return;

            if (!TryComp(uid, out FlammableComponent? flammable))
                return;

            if (!flammable.OnFire)
                return;

            args.Handled = true;

            if (!TryComp(uid, out UseDelayComponent? useDelay) || !_useDelay.TryResetDelay((uid, useDelay), true))
                return;

            _audio.PlayPvs(component.ExtinguishAttemptSound, uid);

            if (_random.Prob(component.Probability))
            {
                AdjustFireStacks(uid, component.StackDelta, flammable);
            }
            else
            {
                _popup.PopupEntity(Loc.GetString(component.ExtinguishFailed), uid);
            }
        }

        private void OnCollide(EntityUid uid, FlammableComponent flammable, ref StartCollideEvent args)
        {
            var otherUid = args.OtherEntity;

            // Collisions cause events to get raised directed at both entities. We only want to handle this collision
            // once, hence the uid check.
            if (otherUid.Id < uid.Id)
                return;

            // Normal hard collisions, though this isn't generally possible since most flammable things are mobs
            // which don't collide with one another, shouldn't work here.
            if (args.OtherFixtureId != flammable.FlammableFixtureID && args.OurFixtureId != flammable.FlammableFixtureID)
                return;

            if (!flammable.FireSpread)
                return;

            if (!TryComp(otherUid, out FlammableComponent? otherFlammable) || !otherFlammable.FireSpread)
                return;

            if (!flammable.OnFire && !otherFlammable.OnFire)
                return; // Neither are on fire

            // Both are on fire -> equalize fire stacks.
            // Weight each thing's firestacks by its mass
            var mass1 = 1f;
            var mass2 = 1f;
            if (_physicsQuery.TryComp(uid, out var physics) && _physicsQuery.TryComp(otherUid, out var otherPhys))
            {
                mass1 = physics.Mass;
                mass2 = otherPhys.Mass;
            }

            // Get the average of both entity's firestacks * mass
            // Then for each entity, we divide the average by their mass and set their firestacks to that value
            // An entity with a higher mass will lose some fire and transfer it to the one with lower mass.
            var avg = (flammable.FireStacks * mass1 + otherFlammable.FireStacks * mass2) / 2f;

            // bring each entity to the same firestack mass, firestack amount is scaled by the inverse of the entity's mass
            SetFireStacks(uid, avg / mass1, flammable, ignite: true);
            SetFireStacks(otherUid, avg / mass2, otherFlammable, ignite: true);
        }

        private void OnIsHot(EntityUid uid, FlammableComponent flammable, IsHotEvent args)
        {
            args.IsHot = flammable.OnFire;
        }

        private void OnTileFire(Entity<FlammableComponent> ent, ref TileFireEvent args)
        {
            var tempDelta = args.Temperature - ent.Comp.MinIgnitionTemperature;

            _fireEvents.TryGetValue(ent, out var maxTemp);

            if (tempDelta > maxTemp)
                _fireEvents[ent] = tempDelta;
        }

        private void OnRejuvenate(Entity<FlammableComponent> ent, ref RejuvenateEvent args)
        {
            TryExtinguish(ent.AsNullable());
        }

        private void OnResistFireAlert(Entity<FlammableComponent> ent, ref ResistFireAlertEvent args)
        {
            if (args.Handled)
                return;

            // RMC14 use the normal stop-drop-roll resist before active water extinguishes.
            _rmcFlammable.DoStopDropRollAnimation(ent.Owner);
            var resistedFire = Resist(ent, ent);
            if (resistedFire)
                TryExtinguishWithWater(ent.Owner, ent.Comp);
            // RMC14 end

            _xenoSpit.Resist(ent.Owner, resistedFire);
            args.Handled = true;
        }

        // RMC14 water extinguishing helper. Active water reuses RMCWaterSystem.CanCollide so catwalk-covered
        // water does not count.
        private bool TryExtinguishWithWater(EntityUid uid, FlammableComponent flammable)
        {
            if (!CanWaterExtinguish(uid, flammable) || !_rmcWater.IsInWater(uid))
                return false;

            Extinguish(uid, flammable);
            return true;
        }

        private bool CanWaterExtinguish(EntityUid uid, FlammableComponent flammable)
        {
            return !TerminatingOrDeleted(uid) &&
                   flammable.OnFire &&
                   flammable.CanExtinguish;
        }
        // RMC14 end

        public void UpdateAppearance(EntityUid uid, FlammableComponent? flammable = null, AppearanceComponent? appearance = null)
        {
            if (!Resolve(uid, ref flammable, ref appearance, false))
                return;

            _appearance.SetData(uid, FireVisuals.OnFire, flammable.OnFire, appearance);
            _appearance.SetData(uid, FireVisuals.FireStacks, flammable.FireStacks, appearance);

            if (flammable.Displacement != null)
                _appearance.SetData(uid, FireVisuals.FireDisplacement, flammable.Displacement.Value.Id, appearance);
            // CMU14: RemoveData dirties appearance unconditionally, paid per tile-fire tick by every
            // flammable; nothing in this fork sets Displacement so the key is never there to remove
            // else
            //     _appearance.RemoveData(uid, FireVisuals.FireDisplacement);

            // Also enable toggleable-light visuals
            // This is intended so that matches & candles can re-use code for un-shaded layers on in-hand sprites.
            // However, this could cause conflicts if something is ACTUALLY both a toggleable light and flammable.
            // if that ever happens, then fire visuals will need to implement their own in-hand sprite management.
            _appearance.SetData(uid, ToggleableVisuals.Enabled, flammable.OnFire, appearance);
        }

        public void AdjustFireStacks(EntityUid uid, float relativeFireStacks, FlammableComponent? flammable = null, bool ignite = false)
        {
            if (!Resolve(uid, ref flammable))
                return;

            SetFireStacks(uid, flammable.FireStacks + relativeFireStacks, flammable, ignite);
        }

        public void SetFireStacks(EntityUid uid, float stacks, FlammableComponent? flammable = null, bool ignite = false)
        {
            if (!Resolve(uid, ref flammable))
                return;

            var attemptEv = new RMCIgniteAttemptEvent();
            RaiseLocalEvent(uid, attemptEv);

            if (attemptEv.Cancelled)
            {
                return;
            }

            // CMU14: Ignite no longer dirties unconditionally, so stack moves sync here; RMC fuel
            // raises the networked MaximumFireStacks right before this call.
            var oldStacks = flammable.FireStacks;
            flammable.FireStacks = MathF.Min(MathF.Max(flammable.MinimumFireStacks, stacks), flammable.MaximumFireStacks);

            if (flammable.FireStacks <= 0)
            {
                TryExtinguish((uid, flammable));
            }
            else
            {
                flammable.OnFire |= ignite;
                if (flammable.FireStacks != oldStacks || ignite)
                    Dirty(uid, flammable);
                UpdateAppearance(uid, flammable);
            }
        }

        /// <summary>
        /// Extinguishes an entity if it can be extinguished.
        /// </summary>
        [PublicAPI]
        [Obsolete("Use TryExtinguish(Entity<FlammableComponent>) instead.")]
        public void Extinguish(EntityUid uid, FlammableComponent? flammable = null)
        {
            // Maintaining prior resolve behavior.
            if (!Resolve(uid, ref flammable))
                return;

            TryExtinguish((uid, flammable));
        }

        /// <summary>
        /// Extinguishes an entity if it can be extinguished.
        /// </summary>
        /// <returns>
        /// Whether or not <paramref name="uid"> was extinguished.
        /// </returns>
        [PublicAPI]
        public bool TryExtinguish(Entity<FlammableComponent?> ent)
        {
            if (!Resolve(ent, ref ent.Comp, false))
                return false;

            if (!ent.Comp.OnFire || !ent.Comp.CanExtinguish)
                return false;

            _adminLogger.Add(LogType.Flammable, $"{ToPrettyString(ent):entity} stopped being on fire damage");
            ent.Comp.OnFire = false;
            ent.Comp.FireStacks = 0;

            _ignitionSourceSystem.SetIgnited(ent.Owner, false);

            var extinguished = new ExtinguishedEvent();
            RaiseLocalEvent(ent, ref extinguished);

            var rmcExtinguished = new RMCExtinguishedEvent();
            RaiseLocalEvent(ent, ref rmcExtinguished);

            UpdateAppearance(ent, ent.Comp);
            return true;
        }

        public void Ignite(EntityUid uid, EntityUid ignitionSource, FlammableComponent? flammable = null,
            EntityUid? ignitionSourceUser = null)
        {
            if (!Resolve(uid, ref flammable))
                return;

            // CMU14: dirty only on a real state change; tile-fire events re-ignite already burning
            // entities several times a second and PVS paid for every no-op dirty.
            var changed = false;
            if (flammable.AlwaysCombustible
                && flammable.FireStacks < flammable.FirestacksOnIgnite)
            {
                flammable.FireStacks = flammable.FirestacksOnIgnite;
                changed = true;
            }

            if (flammable.FireStacks > 0 && !flammable.OnFire)
            {
                if (ignitionSourceUser != null)
                    _adminLogger.Add(LogType.Flammable, $"{ToPrettyString(uid):target} set on fire by {ToPrettyString(ignitionSourceUser.Value):actor} with {ToPrettyString(ignitionSource):tool}");
                else
                    _adminLogger.Add(LogType.Flammable, $"{ToPrettyString(uid):target} set on fire by {ToPrettyString(ignitionSource):actor}");
                flammable.OnFire = true;
                changed = true;

                var extinguished = new IgnitedEvent();
                RaiseLocalEvent(uid, ref extinguished);
            }

            if (changed)
                Dirty(uid, flammable);

            UpdateAppearance(uid, flammable);
        }

        private void OnDamageChanged(EntityUid uid, IgniteOnHeatDamageComponent component, DamageChangedEvent args)
        {
            // Make sure the entity is flammable
            if (!TryComp<FlammableComponent>(uid, out var flammable))
                return;

            // Make sure the damage delta isn't null
            if (args.DamageDelta == null)
                return;

            // Check if its' taken any heat damage, and give the value
            if (args.DamageDelta.DamageDict.TryGetValue("Heat", out FixedPoint2 value))
            {
                // Make sure the value is greater than the threshold
                if (value <= component.Threshold)
                    return;

                // Ignite that sucker
                flammable.FireStacks += component.FireStacks;
                Ignite(uid, uid, flammable);
            }


        }

        public bool Resist(EntityUid uid,
            FlammableComponent? flammable = null)
        {
            if (!Resolve(uid, ref flammable))
                return false;

            if (!flammable.OnFire || flammable.Resisting || !_actionBlockerSystem.CanInteract(uid, null))
                return false;

            flammable.ResistCompleteTime = _timing.CurTime + flammable.ResistTime;

            _popup.PopupEntity(Loc.GetString("flammable-component-resist-message"), uid, uid);
            _stunSystem.TryUpdateParalyzeDuration(uid, flammable.ResistTime);
            // CMU14: RMC rolls take their stacks off on the press itself; the window only
            // suppresses the fade so a long pin cannot regrow what the roll removed.
            if (TryComp<OnFireComponent>(uid, out var rmcFire))
                AdjustFireStacks(uid, rmcFire.ResistStacks, flammable);
            return true;
        }

        public override void Update(float frameTime)
        {
            // process all fire events
            foreach (var (flammable, deltaTemp) in _fireEvents)
            {
                // 100 -> 1, 200 -> 2, 400 -> 3...
                var fireStackMod = Math.Max(MathF.Log2(deltaTemp / 100) + 1, 0);
                var fireStackDelta = fireStackMod - flammable.Comp.FireStacks;
                var flammableEntity = flammable.Owner;
                if (fireStackDelta > 0)
                    AdjustFireStacks(flammableEntity, fireStackDelta, flammable);

                Ignite(flammableEntity, flammableEntity, flammable);
            }
            _fireEvents.Clear();

            // CMU14: the snapshot persists across ticks now; UpdateFlammables manages its lifecycle.
            UpdateFlammables();
        }

        private void UpdateFlammables()
        {
            var curTime = _timing.CurTime;

            // TODO: This needs cleanup to take off the crust from TemperatureComponent and shit.
            // CMU14: fire protection and damage handlers can add FlammableComponents mid-iteration
            // and invalidate the query enumerator, so iterate over a snapshot instead
            // CMU14 Flammable Update Begin
            // Rebuild once per second, then walk a per-tick slice. The slice keeps the per-second
            // passes spread across ticks; entities not yet due simply wait for the next pass.
            if (curTime >= _nextSnapshot)
            {
                _nextSnapshot = curTime + UpdateTime;
                _snapshotCursor = 0;
                _flammableUpdateQueue.Clear();
                var query = EntityQueryEnumerator<FlammableComponent, TransformComponent>();
                while (query.MoveNext(out var uid, out var flammable, out _))
                    _flammableUpdateQueue.Add((uid, flammable));
            }

            var count = _flammableUpdateQueue.Count;
            if (count == 0)
                return;

            // CMU14: Small populations scan fully; larger ones take a slice sized to finish a traversal
            // within one second at the current tick rate.
            var take = count > _timing.TickRate
                ? count / _timing.TickRate + 1
                : count;

            for (var i = 0; i < take; i++)
            {
                var (uid, flammable) = _flammableUpdateQueue[(_snapshotCursor + i) % count];

                // CMU14: Snapshots age up to a second now, so skip components removed mid-cycle.
                if (flammable.Deleted
                    || flammable.LifeStage >= ComponentLifeStage.Stopping)
                    continue;

                if (curTime < flammable.NextUpdate)
                    continue;

                flammable.NextUpdate += UpdateTime;

                // Check if we finished resisting.
                if (curTime > flammable.ResistCompleteTime)
                    flammable.ResistCompleteTime = null;

                // Slowly dry ourselves off if wet.
                if (flammable.FireStacks < 0)
                    flammable.FireStacks = MathF.Min(0, flammable.FireStacks + 1);

                // RMC14: acid burns also use the fire alert to stop, drop, and roll.
                // CMU14: only alert-capable mobs take this path; weeds and resin paid the
                // per-second dispatch for a guaranteed no-op at map-wide flammable counts.
                if (_alertsQuery.HasComp(uid))
                {
                    var showAlert = new ShowFireAlertEvent(flammable.OnFire);
                    RaiseLocalEvent(uid, ref showAlert);
                    if (showAlert.Show)
                        _alertsSystem.ShowAlert(uid, flammable.FireAlert);
                    else
                        _alertsSystem.ClearAlert(uid, flammable.FireAlert);
                }

                if (!flammable.OnFire)
                    continue;

                if (flammable.FireStacks <= 0)
                {
                    TryExtinguish((uid, flammable));
                    continue;
                }

                // RMC14: incendiary fuel burns independently of simulated map atmosphere.
                if (!TryComp<OnFireComponent>(uid, out var onFire) || onFire.Intensity <= 0 || onFire.Duration <= 0)
                {
                    var air = _atmosphereSystem.GetContainingMixture(uid);
                    if (air == null || air.GetMoles(Gas.Oxygen) < 1f)
                    {
                        TryExtinguish((uid, flammable));
                        continue;
                    }
                }

                var source = EnsureComp<IgnitionSourceComponent>(uid);
                _ignitionSourceSystem.SetIgnited((uid, source));

                _temperatureSystem.ChangeHeat(uid, 12500 * flammable.FireStacks, false);

                var ev = new GetFireProtectionEvent();
                // let the thing on fire handle it
                RaiseLocalEvent(uid, ref ev);
                // and whatever it's wearing
                if (_inventoryQuery.TryComp(uid, out var inv))
                    _inventory.RelayEvent((uid, inv), ref ev);

                ApplyFireDamage(uid, flammable, ev.Multiplier);

                // CMU14: the roll's stacks came off on the press; suppress the fade for the rest
                // of the pin so flaring fuels cannot regrow what the roll removed.
                if (!flammable.Resisting)
                    AdjustFireStacks(uid, flammable.FirestackFade, flammable, flammable.OnFire);
            }

            _snapshotCursor = (_snapshotCursor + take) % count;
            // CMU14 End
        }

        private void ApplyFireDamage(EntityUid uid, FlammableComponent flammable, float protectionMultiplier)
        {
            if (!TryComp<OnFireComponent>(uid, out var rmcFire) ||
                rmcFire.Intensity <= 0 ||
                rmcFire.Duration <= 0)
            {
                _damageableSystem.TryChangeDamage(
                    uid,
                    flammable.Damage * flammable.FireStacks * protectionMultiplier,
                    interruptsDoAfters: false);
                return;
            }

            var damage = HasComp<XenoComponent>(uid)
                ? rmcFire.Intensity * (flammable.FireStacks / rmcFire.Duration * 0.2 + 0.8) * protectionMultiplier * flammable.Damage / 2
                : rmcFire.Intensity / 5f * flammable.Damage;

            if (_steppingOnFireQuery.HasComp(uid))
                damage *= 2;

            var immunity = new RMCGetFireImmunityEvent(null);
            RaiseLocalEvent(uid, ref immunity);

            if ((immunity.Immune || HasComp<RMCImmuneToFireTileDamageComponent>(uid)) &&
                !HasComp<RMCFireBypassActiveComponent>(uid))
            {
                return;
            }

            // CMU14: fire growth and synthetic resistance.
            _rmcFlammable.DamageFromFire(uid, damage, interruptsDoAfters: false, origin: uid);
        }

        public void CopyComponent(Entity<FlammableComponent?> entity, EntityUid clone)
        {
            if (!Resolve(entity, ref entity.Comp, false))
                return;

            // Don't clone being on fire here.
            var cloneComp = EnsureComp<FlammableComponent>(clone);
            cloneComp.Displacement = entity.Comp.Displacement;
            cloneComp.AlwaysCombustible = entity.Comp.AlwaysCombustible;
            cloneComp.CanExtinguish = entity.Comp.CanExtinguish;
            cloneComp.Damage = entity.Comp.Damage.Clone();
            cloneComp.FirestackFade = entity.Comp.FirestackFade;
            cloneComp.FirestacksOnIgnite = entity.Comp.FirestacksOnIgnite;
            cloneComp.MaximumFireStacks = entity.Comp.MaximumFireStacks;
            cloneComp.MinimumFireStacks = entity.Comp.MinimumFireStacks;
            cloneComp.ResistTime = entity.Comp.ResistTime;
            Dirty(clone, cloneComp);

            UpdateAppearance(clone, cloneComp);
        }
    }
}
