#pragma warning disable RA0002 // Integration regression intentionally inspects restricted component state.

using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server._CMU14.Medical.Injuries.Pain.Penalties;
using Content.Server._CMU14.Traits.PanicProne;
using Content.Shared._CMU14.Medical.Core;
using Content.Shared._CMU14.Medical.Injuries.Pain.Penalties;
using Content.Shared._CMU14.Traits.PanicProne;
using Content.Shared._RMC14.Attachable.Components;
using Content.Shared._RMC14.Attachable.Events;
using Content.Shared._RMC14.Attachable.Systems;
using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests._CMU14;

[TestFixture]
[TestOf(typeof(PanicGunSystem))]
[TestOf(typeof(CMUMedicalSpeedSystem))]
public sealed class GunOwnerPenaltyRegressionTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false };

    private const string Underbarrel = "rmc-aslot-underbarrel";
    private const string Barrel = "rmc-aslot-barrel";

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  parent: BaseItem
  id: GunOwnerPenaltyTestHolder
  components:
  - type: Gun
  - type: AttachableHolder
    randomAttachmentChance: 0
    slots:
      rmc-aslot-underbarrel:
        whitelist:
          components:
          - Attachable
      rmc-aslot-barrel:
        whitelist:
          components:
          - Attachable
  - type: GunOwnerPenaltyRefreshProbe

- type: entity
  parent: BaseItem
  id: GunOwnerPenaltyTestPlainGun
  components:
  - type: Gun
  - type: GunOwnerPenaltyRefreshProbe

- type: entity
  parent: BaseItem
  id: GunOwnerPenaltyTestNotGun

- type: entity
  parent: BaseItem
  id: GunOwnerPenaltyTestAttachableA
  components:
  - type: Gun
  - type: Attachable
  - type: AttachableToggleable
    attachedOnly: true
    doAfterBreakOnMove: false
    doAfterNeedHand: false
    doInterrupt: true
    showTogglePopup: false
    supercedeHolder: true
  - type: GunOwnerPenaltyRefreshProbe

- type: entity
  parent: GunOwnerPenaltyTestAttachableA
  id: GunOwnerPenaltyTestAttachableB
";

    [Test]
    public async Task SupercedingResolverRejectsMissingAndNonGunAndDelegatesCurrentUser()
    {
        var map = await Pair.CreateTestMap();
        var cleanup = new List<EntityUid>();

        try
        {
            await Server.WaitAssertion(() =>
            {
                var hands = Server.System<SharedHandsSystem>();
                var holders = Server.System<AttachableHolderSystem>();
                var rmcGun = Server.System<CMGunSystem>();
                var user = Spawn("CMMobHuman", map.GridCoords, cleanup);
                var holder = Spawn("GunOwnerPenaltyTestHolder", map.GridCoords, cleanup);
                var notGun = Spawn("GunOwnerPenaltyTestNotGun", map.GridCoords, cleanup);
                var nested = Spawn("GunOwnerPenaltyTestAttachableA", map.GridCoords, cleanup);
                var holderComp = SEntMan.GetComponent<AttachableHolderComponent>(holder);

                Assert.That(holders.TryGetSupercedingGun((holder, holderComp), out _), Is.False);

                holders.SetSupercedingAttachable((holder, holderComp), notGun);
                Assert.That(holders.TryGetSupercedingGun((holder, holderComp), out _), Is.False,
                    "a stale or non-gun owner pointer must not manufacture a gun");

                holders.SetSupercedingAttachable((holder, holderComp), null);
                Assert.That(holders.Attach((holder, holderComp), nested, user, Underbarrel), Is.True);
                Assert.That(hands.TryPickupAnyHand(user, holder, checkActionBlocker: false), Is.True);
                holders.SetSupercedingAttachable((holder, holderComp), nested);

                Assert.Multiple(() =>
                {
                    Assert.That(holders.TryGetSupercedingGun((holder, holderComp), out var resolved), Is.True);
                    Assert.That(resolved.Owner, Is.EqualTo(nested));
                    Assert.That(rmcGun.TryGetGunUser(nested, out var gunUser), Is.True,
                        "the nested gun must delegate modifier ownership through its current holder");
                    Assert.That(gunUser.Owner, Is.EqualTo(user));
                });
            });
        }
        finally
        {
            await Delete(cleanup);
        }
    }

    [Test]
    public async Task PanicTracksBothHandsAndTheActiveNestedGunAcrossItsFullLifecycle()
    {
        var map = await Pair.CreateTestMap();
        var cleanup = new List<EntityUid>();

        try
        {
            EntityUid user = default;
            EntityUid holder = default;
            EntityUid plain = default;
            EntityUid nestedA = default;
            EntityUid nestedB = default;

            await Server.WaitAssertion(() =>
            {
                _ = Server.System<GunOwnerPenaltyRefreshProbeSystem>();
                var hands = Server.System<SharedHandsSystem>();
                user = Spawn("CMMobHuman", map.GridCoords, cleanup);
                holder = Spawn("GunOwnerPenaltyTestHolder", map.GridCoords, cleanup);
                plain = Spawn("GunOwnerPenaltyTestPlainGun", map.GridCoords, cleanup);
                nestedA = Spawn("GunOwnerPenaltyTestAttachableA", map.GridCoords, cleanup);
                nestedB = Spawn("GunOwnerPenaltyTestAttachableB", map.GridCoords, cleanup);

                SEntMan.RemoveComponent<CMUHumanMedicalComponent>(user);
                var panic = SEntMan.EnsureComponent<PanicComponent>(user);
                panic.EmoteThreshold = panic.Max + 1;
                panic.RegenPerTick = 0;
                panic.WarnThreshold = panic.Max + 1;

                Assert.That(hands.TryPickupAnyHand(user, holder, checkActionBlocker: false), Is.True);
                Assert.That(hands.TryPickupAnyHand(user, plain, checkActionBlocker: false), Is.True);
                Server.System<PanicGunSystem>().AddPanic((user, panic), panic.PeakThreshold);

                AssertPenaltyOwners<PanicGunAimPenaltyComponent>(holder, plain);

                var holders = Server.System<AttachableHolderSystem>();
                var holderComp = SEntMan.GetComponent<AttachableHolderComponent>(holder);
                Assert.That(holders.Attach((holder, holderComp), nestedA, user, Underbarrel), Is.True);
                Assert.That(holders.Attach((holder, holderComp), nestedB, user, Barrel), Is.True);
                AssertPenaltyOwners<PanicGunAimPenaltyComponent>(holder, plain);
            });

            await Server.WaitAssertion(() => Toggle(nestedA, holder, user, Underbarrel));
            await Pair.RunTicksSync(2);
            await Server.WaitAssertion(() =>
            {
                Assert.That(SEntMan.GetComponent<AttachableHolderComponent>(holder).SupercedingAttachable,
                    Is.EqualTo(nestedA));
                AssertPenaltyOwners<PanicGunAimPenaltyComponent>(holder, plain, nestedA);
                AssertSpread(nestedA, 1.6);

                var panic = SEntMan.GetComponent<PanicComponent>(user);
                Server.System<PanicGunSystem>().AddPanic((user, panic), -100);
                ResetRefreshes(holder, plain, nestedA, nestedB);
                Server.System<PanicGunSystem>().AddPanic((user, panic), 100);

                AssertPenaltyOwners<PanicGunAimPenaltyComponent>(holder, plain, nestedA);
                AssertRefreshes(1, holder, plain, nestedA);
                AssertRefreshes(0, nestedB);

                ResetRefreshes(holder, plain, nestedA, nestedB);
                Server.System<PanicGunSystem>().AddPanic((user, panic), 0);
                AssertPenaltyOwners<PanicGunAimPenaltyComponent>(holder, plain, nestedA);
                AssertRefreshes(0, holder, plain, nestedA, nestedB);
            });

            await Server.WaitAssertion(() =>
            {
                ResetRefreshes(holder, nestedA);
                Assert.That(Server.System<SharedHandsSystem>()
                    .TryDrop(user, holder, checkActionBlocker: false), Is.True);
                AssertPenaltyOwners<PanicGunAimPenaltyComponent>(plain);
            });
            await Pair.RunTicksSync(2);
            await Server.WaitAssertion(() =>
            {
                AssertRefreshes(1, holder, nestedA);
                AssertSpread(nestedA, 1);
                ResetRefreshes(holder, nestedA);
                Assert.That(Server.System<SharedHandsSystem>()
                    .TryPickupAnyHand(user, holder, checkActionBlocker: false), Is.True);
                AssertPenaltyOwners<PanicGunAimPenaltyComponent>(holder, plain, nestedA);
            });
            await Pair.RunTicksSync(2);
            await Server.WaitAssertion(() =>
            {
                AssertRefreshes(2, holder);
                AssertRefreshes(1, nestedA);
                AssertSpread(nestedA, 1.6);
            });

            await Server.WaitAssertion(() =>
            {
                ResetRefreshes(holder, plain, nestedA, nestedB);
                Toggle(nestedB, holder, user, Barrel);
            });
            await Pair.RunTicksSync(2);
            await Server.WaitAssertion(() =>
            {
                Assert.That(SEntMan.GetComponent<AttachableHolderComponent>(holder).SupercedingAttachable,
                    Is.EqualTo(nestedB));
                AssertPenaltyOwners<PanicGunAimPenaltyComponent>(holder, plain, nestedB);
                AssertRefreshes(1, nestedA, nestedB);
                AssertSpread(nestedA, 1);
                AssertSpread(nestedB, 1.6);
            });

            await Server.WaitAssertion(() =>
            {
                ResetRefreshes(nestedB);
                var interrupted = new AttachableToggleableInterruptEvent(user);
                SEntMan.EventBus.RaiseLocalEvent(nestedB, ref interrupted);
            });
            await Pair.RunTicksSync(2);
            await Server.WaitAssertion(() =>
            {
                Assert.That(SEntMan.GetComponent<AttachableHolderComponent>(holder).SupercedingAttachable, Is.Null);
                AssertPenaltyOwners<PanicGunAimPenaltyComponent>(holder, plain);
                AssertRefreshes(1, nestedB);
                AssertSpread(nestedB, 1);
            });

            await Server.WaitAssertion(() => Toggle(nestedA, holder, user, Underbarrel));
            await Pair.RunTicksSync(2);
            await Server.WaitAssertion(() =>
            {
                var holders = Server.System<AttachableHolderSystem>();
                var holderComp = SEntMan.GetComponent<AttachableHolderComponent>(holder);
                ResetRefreshes(nestedA);
                Assert.That(holders.Detach((holder, holderComp), nestedA, user, Underbarrel), Is.True);
            });
            await Pair.RunTicksSync(2);
            await Server.WaitAssertion(() =>
            {
                Assert.That(SEntMan.GetComponent<AttachableHolderComponent>(holder).SupercedingAttachable, Is.Null);
                AssertPenaltyOwners<PanicGunAimPenaltyComponent>(holder, plain);
                AssertRefreshes(1, nestedA);
                AssertSpread(nestedA, 1);

                var panic = SEntMan.GetComponent<PanicComponent>(user);
                Server.System<PanicGunSystem>().AddPanic((user, panic), -100);
                AssertPenaltyOwners<PanicGunAimPenaltyComponent>();
            });
        }
        finally
        {
            await Delete(cleanup);
        }
    }

    [Test]
    public async Task MedicalTracksNestedOwnerAndHonorsTheEnabledModifierGate()
    {
        await OverrideCVar(Side.Server, CMUMedicalCCVars.Enabled, true);
        await OverrideCVar(Side.Server, CMUMedicalCCVars.StatusEffectsEnabled, true);
        var map = await Pair.CreateTestMap();
        var cleanup = new List<EntityUid>();

        try
        {
            EntityUid user = default;
            EntityUid holder = default;
            EntityUid plain = default;
            EntityUid nested = default;

            await Server.WaitAssertion(() =>
            {
                _ = Server.System<GunOwnerPenaltyRefreshProbeSystem>();
                var hands = Server.System<SharedHandsSystem>();
                var holders = Server.System<AttachableHolderSystem>();
                user = Spawn("CMMobHuman", map.GridCoords, cleanup);
                holder = Spawn("GunOwnerPenaltyTestHolder", map.GridCoords, cleanup);
                plain = Spawn("GunOwnerPenaltyTestPlainGun", map.GridCoords, cleanup);
                nested = Spawn("GunOwnerPenaltyTestAttachableA", map.GridCoords, cleanup);

                Assert.That(hands.TryPickupAnyHand(user, holder, checkActionBlocker: false), Is.True);
                Assert.That(hands.TryPickupAnyHand(user, plain, checkActionBlocker: false), Is.True);
                var holderComp = SEntMan.GetComponent<AttachableHolderComponent>(holder);
                Assert.That(holders.Attach((holder, holderComp), nested, user, Underbarrel), Is.True);
                AssertPenaltyOwners<CMUMedicalGunAimPenaltyComponent>(holder, plain);
                SEntMan.EnsureComponent<CMUAimAccuracyComponent>(user).SpreadMultiplier = 2;
            });

            await Server.WaitAssertion(() => Toggle(nested, holder, user, Underbarrel));
            await Pair.RunTicksSync(2);
            await Server.WaitAssertion(() =>
            {
                AssertPenaltyOwners<CMUMedicalGunAimPenaltyComponent>(holder, plain, nested);
                AssertSpread(nested, 2);
                ResetRefreshes(holder, plain, nested);

                var medical = Server.System<CMUMedicalSpeedSystem>();
                medical.RefreshAggregatedPenalties(user);
                AssertPenaltyOwners<CMUMedicalGunAimPenaltyComponent>(holder, plain, nested);
                AssertRefreshes(1, holder, plain, nested);

                medical.RefreshAggregatedPenalties(user);
                AssertPenaltyOwners<CMUMedicalGunAimPenaltyComponent>(holder, plain, nested);
                AssertRefreshes(2, holder, plain, nested);

                var aim = SEntMan.EnsureComponent<CMUAimAccuracyComponent>(user);
                aim.SpreadMultiplier = 2;
                Server.System<SharedGunSystem>().RefreshModifiers(nested);
                var gun = SEntMan.GetComponent<GunComponent>(nested);
                Assert.That(gun.MaxAngleModified.Theta, Is.EqualTo(gun.MaxAngle.Theta * 2).Within(0.0001),
                    "the active nested gun must resolve the holder's medical aim state");
            });

            await OverrideCVar(Side.Server, CMUMedicalCCVars.Enabled, false);
            await Server.WaitAssertion(() =>
            {
                Server.System<SharedGunSystem>().RefreshModifiers(nested);
                var gun = SEntMan.GetComponent<GunComponent>(nested);
                Assert.That(gun.MaxAngleModified.Theta, Is.EqualTo(gun.MaxAngle.Theta).Within(0.0001),
                    "the marker may remain for lifecycle tracking, but disabled medical effects must be inert");
            });

            await OverrideCVar(Side.Server, CMUMedicalCCVars.Enabled, true);
            await OverrideCVar(Side.Server, CMUMedicalCCVars.StatusEffectsEnabled, false);
            await Server.WaitAssertion(() =>
            {
                Server.System<SharedGunSystem>().RefreshModifiers(nested);
                var gun = SEntMan.GetComponent<GunComponent>(nested);
                Assert.That(gun.MaxAngleModified.Theta, Is.EqualTo(gun.MaxAngle.Theta).Within(0.0001),
                    "disabling the status-effect layer must also make the retained marker inert");

                Assert.That(Server.System<SharedHandsSystem>()
                    .TryDrop(user, holder, checkActionBlocker: false), Is.True);
                AssertPenaltyOwners<CMUMedicalGunAimPenaltyComponent>(plain);
            });
            await Server.WaitAssertion(() =>
            {
                Assert.That(Server.System<SharedHandsSystem>()
                    .TryPickupAnyHand(user, holder, checkActionBlocker: false), Is.True);
                AssertPenaltyOwners<CMUMedicalGunAimPenaltyComponent>(holder, plain, nested);
            });
            await Pair.RunTicksSync(2);
        }
        finally
        {
            await Delete(cleanup);
        }
    }

    [Test]
    public async Task SharedLifecycleFanoutAppliesAndRemovesBothPoliciesTogether()
    {
        await OverrideCVar(Side.Server, CMUMedicalCCVars.Enabled, true);
        await OverrideCVar(Side.Server, CMUMedicalCCVars.StatusEffectsEnabled, true);
        var map = await Pair.CreateTestMap();
        var cleanup = new List<EntityUid>();

        try
        {
            EntityUid user = default;
            EntityUid holder = default;
            EntityUid nested = default;

            await Server.WaitAssertion(() =>
            {
                _ = Server.System<GunOwnerPenaltyRefreshProbeSystem>();
                var hands = Server.System<SharedHandsSystem>();
                var holders = Server.System<AttachableHolderSystem>();
                user = Spawn("CMMobHuman", map.GridCoords, cleanup);
                holder = Spawn("GunOwnerPenaltyTestHolder", map.GridCoords, cleanup);
                nested = Spawn("GunOwnerPenaltyTestAttachableA", map.GridCoords, cleanup);

                var panic = SEntMan.EnsureComponent<PanicComponent>(user);
                panic.EmoteThreshold = panic.Max + 1;
                panic.RegenPerTick = 0;
                panic.WarnThreshold = panic.Max + 1;
                Server.System<PanicGunSystem>().AddPanic((user, panic), panic.PeakThreshold);

                var holderComp = SEntMan.GetComponent<AttachableHolderComponent>(holder);
                Assert.That(holders.Attach((holder, holderComp), nested, user, Underbarrel), Is.True);
                Assert.That(hands.TryPickupAnyHand(user, holder, checkActionBlocker: false), Is.True);

                AssertPenaltyOwners<PanicGunAimPenaltyComponent>(holder);
                AssertPenaltyOwners<CMUMedicalGunAimPenaltyComponent>(holder);
            });

            await Server.WaitAssertion(() => Toggle(nested, holder, user, Underbarrel));
            await Pair.RunTicksSync(2);
            await Server.WaitAssertion(() =>
            {
                AssertPenaltyOwners<PanicGunAimPenaltyComponent>(holder, nested);
                AssertPenaltyOwners<CMUMedicalGunAimPenaltyComponent>(holder, nested);

                Assert.That(Server.System<SharedHandsSystem>()
                    .TryDrop(user, holder, checkActionBlocker: false), Is.True);
                AssertPenaltyOwners<PanicGunAimPenaltyComponent>();
                AssertPenaltyOwners<CMUMedicalGunAimPenaltyComponent>();
            });
            await Pair.RunTicksSync(2);
            await Server.WaitAssertion(() =>
            {
                AssertPenaltyOwners<PanicGunAimPenaltyComponent>();
                AssertPenaltyOwners<CMUMedicalGunAimPenaltyComponent>();
            });
        }
        finally
        {
            await Delete(cleanup);
        }
    }

    private EntityUid Spawn(string prototype, EntityCoordinates coordinates, ICollection<EntityUid> cleanup)
    {
        var entity = SEntMan.SpawnEntity(prototype, coordinates);
        cleanup.Add(entity);
        return entity;
    }

    private void Toggle(EntityUid attachable, EntityUid holder, EntityUid user, string slot)
    {
        var toggle = new AttachableToggleDoAfterEvent(slot, string.Empty);
        var args = new DoAfterArgs(
            SEntMan,
            user,
            TimeSpan.Zero,
            toggle,
            attachable,
            attachable,
            holder);
        toggle.DoAfter = new DoAfter(0, args, TimeSpan.Zero);
        SEntMan.EventBus.RaiseLocalEvent(attachable, toggle);
        Assert.That(toggle.Handled, Is.True);
    }

    private void AssertPenaltyOwners<T>(params EntityUid[] expected) where T : Component
    {
        var actual = SEntMan.EntityQuery<T>().Select(component => component.Owner).ToHashSet();
        Assert.That(actual, Is.EquivalentTo(expected));
    }

    private void ResetRefreshes(params EntityUid[] guns)
    {
        foreach (var gun in guns)
            SEntMan.GetComponent<GunOwnerPenaltyRefreshProbeComponent>(gun).Refreshes = 0;
    }

    private void AssertRefreshes(int expected, params EntityUid[] guns)
    {
        foreach (var gun in guns)
        {
            Assert.That(SEntMan.GetComponent<GunOwnerPenaltyRefreshProbeComponent>(gun).Refreshes,
                Is.EqualTo(expected),
                $"gun {gun} refresh count");
        }
    }

    private void AssertSpread(EntityUid gunUid, double multiplier)
    {
        var gun = SEntMan.GetComponent<GunComponent>(gunUid);
        Assert.That(gun.MaxAngleModified.Theta,
            Is.EqualTo(gun.MaxAngle.Theta * multiplier).Within(0.0001),
            $"gun {gunUid} spread multiplier");
    }

    private async Task Delete(IEnumerable<EntityUid> entities)
    {
        await Server.WaitPost(() =>
        {
            foreach (var entity in entities.Distinct().Reverse())
            {
                if (SEntMan.EntityExists(entity))
                    SEntMan.DeleteEntity(entity);
            }
        });
    }
}

[RegisterComponent]
public sealed partial class GunOwnerPenaltyRefreshProbeComponent : Component
{
    public int Refreshes;
}

public sealed class GunOwnerPenaltyRefreshProbeSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GunOwnerPenaltyRefreshProbeComponent, GunRefreshModifiersEvent>(OnRefresh);
    }

    private static void OnRefresh(
        Entity<GunOwnerPenaltyRefreshProbeComponent> entity,
        ref GunRefreshModifiersEvent args)
    {
        entity.Comp.Refreshes++;
    }
}

#pragma warning restore RA0002
