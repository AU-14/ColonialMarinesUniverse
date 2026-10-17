using System.Numerics;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Attachable.Components;
using Content.Shared._RMC14.Attachable.Systems;
using Content.Shared._RMC14.NightVision;
using Content.Shared._RMC14.Requisitions.Components;
using Content.Shared._RMC14.Scoping;
using Content.Shared._RMC14.Vendors;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Movement.Components;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.PowerCell;
using Content.Shared.PowerCell.Components;
using Content.Shared.Storage.Components;
using Content.Shared.Wieldable;
using Content.Shared.Wieldable.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Weapons;

[TestFixture]
public sealed class CMUNightVisionScopeTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  parent: CMMobHuman
  id: CMUNightVisionScopeTestUser
  components:
  - type: NightVision
    innate: true
    state: Half
    green: false
";

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public async Task NightVisionOnlyAppliesWhileScoped(bool innate, bool unwield)
    {
        EntityUid user = default;
        EntityUid gun = default;
        EntityUid scope = default;

        await Server.WaitAssertion(() =>
        {
            (user, gun, scope) = SpawnHeldScope(innate);

            var scopeComp = SEntMan.GetComponent<ScopeComponent>(scope);
            Assert.That(scopeComp.ZoomLevels, Has.Count.EqualTo(1));
            Assert.That(scopeComp.ZoomLevels[0].Zoom, Is.EqualTo(1.5f));
            Assert.That(SEntMan.GetComponent<NightVisionItemComponent>(scope).User, Is.Null);
            Assert.That(SEntMan.GetComponent<PowerCellDrawComponent>(scope).Enabled, Is.False);
            Assert.That(Server.System<SharedScopeSystem>().StartScoping((scope, scopeComp), user), Is.Not.Null);
        });

        await Pair.RunTicksSync(2);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<ScopingComponent>(user), Is.True);
            var vision = SEntMan.GetComponent<NightVisionComponent>(user);
            Assert.That(vision.State, Is.EqualTo(NightVisionState.Full));
            Assert.That(vision.Green, Is.True);
            Assert.That(vision.BlockScopes, Is.False);
            Assert.That(SEntMan.GetComponent<PowerCellDrawComponent>(scope).Enabled, Is.True);
            Assert.That(SEntMan.GetComponent<ContentEyeComponent>(user).TargetZoom, Is.EqualTo(new Vector2(1.5f)));

            if (unwield)
                Assert.That(Server.System<SharedWieldableSystem>().TryUnwield((gun, SEntMan.GetComponent<WieldableComponent>(gun)), user), Is.True);
            else
                Assert.That(Server.System<SharedScopeSystem>().Unscope((scope, SEntMan.GetComponent<ScopeComponent>(scope))), Is.True);
        });

        await Pair.RunTicksSync(2);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<ScopingComponent>(user), Is.False);
            Assert.That(SEntMan.GetComponent<ContentEyeComponent>(user).TargetZoom, Is.EqualTo(Vector2.One));
            Assert.That(SEntMan.GetComponent<NightVisionItemComponent>(scope).User, Is.Null);
            Assert.That(SEntMan.GetComponent<PowerCellDrawComponent>(scope).Enabled, Is.False);
            AssertOriginalVision(user, innate);

            SEntMan.DeleteEntity(gun);
            SEntMan.DeleteEntity(user);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task M96SIntegratedNightVisionWorksWithoutBatteryAndRestoresVision(bool innate)
    {
        EntityUid user = default;
        EntityUid gun = default;
        EntityUid scope = default;

        await Server.WaitAssertion(() =>
        {
            user = SEntMan.SpawnEntity(innate ? "CMUNightVisionScopeTestUser" : "CMMobHuman", MapCoordinates.Nullspace);
            gun = SEntMan.SpawnEntity("CMM96SSniperRifle", MapCoordinates.Nullspace);
            var holder = SEntMan.GetComponent<AttachableHolderComponent>(gun);
            Assert.That(Server.System<AttachableHolderSystem>().TryGetAttachable((gun, holder), "rmc-aslot-rail", out var attachment), Is.True);
            scope = attachment.Owner;
            Assert.That(Server.System<SharedHandsSystem>().TryPickupAnyHand(user, gun), Is.True);
            Assert.That(Server.System<SharedWieldableSystem>().TryWield((gun, SEntMan.GetComponent<WieldableComponent>(gun)), user), Is.True);
            Assert.That(SEntMan.HasComponent<BatteryComponent>(scope), Is.False);
            Assert.That(SEntMan.HasComponent<PowerCellSlotComponent>(scope), Is.False);
            Assert.That(SEntMan.HasComponent<PowerCellDrawComponent>(scope), Is.False);
            var scopeComp = SEntMan.GetComponent<ScopeComponent>(scope);
            Assert.That(scopeComp.ZoomLevels.Select(level => level.Zoom), Is.EqualTo(new[] { 1.6f, 1f }));
            Assert.That(Server.System<SharedScopeSystem>().StartScoping((scope, scopeComp), user), Is.Not.Null);
        });

        await Pair.RunTicksSync(2);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<NightVisionItemComponent>(scope).User, Is.EqualTo(user));
            var vision = SEntMan.GetComponent<NightVisionComponent>(user);
            Assert.That(vision.Green, Is.True);
            Assert.That(vision.State, Is.EqualTo(NightVisionState.Full));
            Assert.That(SEntMan.GetComponent<ContentEyeComponent>(user).TargetZoom, Is.EqualTo(new Vector2(1.6f)));
            Assert.That(Server.System<SharedWieldableSystem>().TryUnwield((gun, SEntMan.GetComponent<WieldableComponent>(gun)), user), Is.True);
        });

        await Pair.RunTicksSync(2);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<ScopingComponent>(user), Is.False);
            Assert.That(SEntMan.GetComponent<NightVisionItemComponent>(scope).User, Is.Null);
            AssertOriginalVision(user, innate);
            SEntMan.DeleteEntity(gun);
            SEntMan.DeleteEntity(user);
        });
    }

    [Test]
    public async Task M96SDerivedScopesInheritBatteryFreeNightVision()
    {
        await Server.WaitAssertion(() =>
        {
            string[] scopes =
            [
                "RMCM96SIntegratedScope",
                "RMCXM43E1IntegratedScope",
                "RMCType88IntegratedScope",
                "RMCL112IntegratedScope",
                "RMCVultureScope",
            ];
            foreach (var scopeId in scopes)
            {
                var prototype = SProtoMan.Index<EntityPrototype>(scopeId);
                var factory = SEntMan.ComponentFactory;
                Assert.That(prototype.TryComp<NightVisionItemComponent>(out var vision, factory), Is.True, scopeId);
                Assert.That(vision!.Green, Is.True, scopeId);
                Assert.That(vision.RestorePreviousState, Is.True, scopeId);
                Assert.That(vision.EnableOnEquip, Is.False, scopeId);
                Assert.That(vision.ActionId, Is.Null, scopeId);
                Assert.That(prototype.TryComp<BatteryComponent>(out _, factory), Is.False, scopeId);
                Assert.That(prototype.TryComp<PowerCellSlotComponent>(out _, factory), Is.False, scopeId);
                Assert.That(prototype.TryComp<PowerCellDrawComponent>(out _, factory), Is.False, scopeId);
            }
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task BatteryDrainsOnlyWhileScopedAndRechargesWithoutLosingOpticalZoom(bool innate)
    {
        EntityUid user = default;
        EntityUid gun = default;
        EntityUid scope = default;
        EntityUid cell = default;
        EntityUid charger = default;
        float initialCharge = 0;
        float stoppedCharge = 0;

        await Server.WaitAssertion(() =>
        {
            (user, gun, scope) = SpawnHeldScope(innate);
            Assert.That(Server.System<PowerCellSystem>().TryGetBatteryFromSlot(scope, out var battery), Is.True);
            cell = battery!.Value.Owner;
            initialCharge = Server.System<SharedBatterySystem>().GetCharge(cell);
            var draw = SEntMan.GetComponent<PowerCellDrawComponent>(scope);
            Assert.That(initialCharge / draw.DrawRate, Is.EqualTo(300f).Within(0.001f),
                "a full battery must last five minutes of active use");
        });

        await Pair.RunTicksSync(Pair.SecondsToTicks(1f));

        await Server.WaitAssertion(() =>
        {
            Assert.That(Server.System<SharedBatterySystem>().GetCharge(cell), Is.EqualTo(initialCharge),
                "holding the scope must not drain its battery");
            Assert.That(Server.System<SharedScopeSystem>().StartScoping((scope, SEntMan.GetComponent<ScopeComponent>(scope)), user), Is.Not.Null);
        });

        await Pair.RunTicksSync(Pair.SecondsToTicks(1f));

        await Server.WaitAssertion(() =>
        {
            var draw = SEntMan.GetComponent<PowerCellDrawComponent>(scope);
            var charge = Server.System<SharedBatterySystem>().GetCharge(cell);
            Assert.That(initialCharge - charge, Is.EqualTo(draw.DrawRate).Within(draw.DrawRate * 0.1f));
            Assert.That(Server.System<SharedScopeSystem>().Unscope((scope, SEntMan.GetComponent<ScopeComponent>(scope))), Is.True);
            stoppedCharge = Server.System<SharedBatterySystem>().GetCharge(cell);
        });

        await Pair.RunTicksSync(Pair.SecondsToTicks(1f));

        await Server.WaitAssertion(() =>
        {
            Assert.That(Server.System<SharedBatterySystem>().GetCharge(cell), Is.EqualTo(stoppedCharge),
                "unscoping must stop battery drain");
            var draw = SEntMan.GetComponent<PowerCellDrawComponent>(scope);
            Server.System<SharedBatterySystem>().SetCharge(cell, draw.DrawRate * 1.5f);
            Assert.That(Server.System<SharedScopeSystem>().StartScoping((scope, SEntMan.GetComponent<ScopeComponent>(scope)), user), Is.Not.Null);
        });

        await Pair.RunTicksSync(2);
        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.GetComponent<NightVisionItemComponent>(scope).User, Is.EqualTo(user)));

        await Pair.RunTicksSync(Pair.SecondsToTicks(2f));

        await Server.WaitAssertion(() =>
        {
            Assert.That(Server.System<SharedBatterySystem>().GetCharge(cell), Is.Zero);
            Assert.That(SEntMan.HasComponent<ScopingComponent>(user), Is.True);
            Assert.That(SEntMan.GetComponent<ContentEyeComponent>(user).TargetZoom, Is.EqualTo(new Vector2(1.5f)));
            Assert.That(SEntMan.GetComponent<PowerCellDrawComponent>(scope).Enabled, Is.False);
            Assert.That(SEntMan.GetComponent<NightVisionItemComponent>(scope).User, Is.Null);
            AssertOriginalVision(user, innate);
            Assert.That(Server.System<SharedScopeSystem>().Unscope((scope, SEntMan.GetComponent<ScopeComponent>(scope))), Is.True);
        });

        await Pair.RunTicksSync(2);

        await Server.WaitAssertion(() =>
            Assert.That(Server.System<SharedScopeSystem>().StartScoping((scope, SEntMan.GetComponent<ScopeComponent>(scope)), user), Is.Not.Null));

        await Pair.RunTicksSync(2);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<ContentEyeComponent>(user).TargetZoom, Is.EqualTo(new Vector2(1.5f)),
                "an empty battery must not prevent use of the optical zoom");
            Assert.That(SEntMan.GetComponent<NightVisionItemComponent>(scope).User, Is.Null);
            AssertOriginalVision(user, innate);
            Assert.That(Server.System<SharedScopeSystem>().Unscope((scope, SEntMan.GetComponent<ScopeComponent>(scope))), Is.True);

            var holder = SEntMan.GetComponent<AttachableHolderComponent>(gun);
            Assert.That(Server.System<AttachableHolderSystem>().Detach((gun, holder), scope, user), Is.True);
            charger = SEntMan.SpawnEntity("RMCRecharger", MapCoordinates.Nullspace);
            var chargerComp = SEntMan.GetComponent<ChargerComponent>(charger);
            chargerComp.Portable = true;
            SEntMan.Dirty(charger, chargerComp);
            Assert.That(Server.System<ItemSlotsSystem>().TryInsert(charger, "charger_slot", scope, null), Is.True,
                "the standard recharger must accept a detached night-vision scope");
        });

        await Pair.RunTicksSync(Pair.SecondsToTicks(1f));

        await Server.WaitAssertion(() =>
        {
            Assert.That(Server.System<SharedBatterySystem>().GetCharge(cell), Is.GreaterThan(0f));
            Assert.That(Server.System<ItemSlotsSystem>().TryEject(charger, "charger_slot", null, out _), Is.True);
            var holder = SEntMan.GetComponent<AttachableHolderComponent>(gun);
            Assert.That(Server.System<AttachableHolderSystem>().Attach((gun, holder), scope, user), Is.True);
            Assert.That(Server.System<SharedScopeSystem>().StartScoping((scope, SEntMan.GetComponent<ScopeComponent>(scope)), user), Is.Not.Null);
        });

        await Pair.RunTicksSync(2);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<NightVisionItemComponent>(scope).User, Is.EqualTo(user),
                "recharging must restore night vision when next scoped in");
            Assert.That(SEntMan.GetComponent<NightVisionComponent>(user).Green, Is.True);
            SEntMan.DeleteEntity(gun);
            SEntMan.DeleteEntity(user);
            SEntMan.DeleteEntity(charger);
        });
    }

    [Test]
    public async Task FactionVendorsHaveRequestedScopeStock()
    {
        await Server.WaitAssertion(() =>
        {
            (string Vendor, int Amount)[] vendors =
            [
                ("AU14ProdigyRequisitionVendor", 5),
                ("AU14WYPMCRequisitionVendor", 5),
                ("CMUWYGuardequipmentvendor", 5),
                ("AU14VAIPORequisitionVendor", 5),
                ("AU14LACNRequisitionVendor", 5),
                ("AU14CMBCIURequisitionVendor", 5),
                ("AU14CMBequipmentvendor", 5),
                ("AU14USCMRequisitionVendor", 15),
                ("AU14UPPRequisitionVendor", 15),
                ("AU14RMCRequisitionVendor", 15),
                ("AU14HAZOPSRequisitionVendor", 15),
                ("RuMCBEARRequisitionVendor", 3),
                ("AU14CLFWeaponsVendor", 3),
                ("AU14CLFObjectiveWeaponsVendor", 3),
                ("AU14CWPSequipmentvendor", 3),
                ("AU14NSPAequipmentvendor", 3),
                ("AU14PaPequipmentvendor", 3),
            ];
            foreach (var (vendorId, amount) in vendors)
            {
                var prototype = SProtoMan.Index<EntityPrototype>(vendorId);
                Assert.That(prototype.TryComp<CMAutomatedVendorComponent>(out var vendor, SEntMan.ComponentFactory), Is.True);
                var entries = vendor!.Sections.SelectMany(section => section.Entries)
                    .Where(entry => entry.Id == "CMUAttachmentNightVisionScope").ToArray();
                Assert.That(entries, Has.Length.EqualTo(1), vendorId);
                Assert.That(entries[0].Amount, Is.EqualTo(amount), vendorId);
                Assert.That(entries[0].Spawn, Is.EqualTo(1), vendorId);
            }
        });
    }

    [Test]
    public async Task PlatoonAsrsSellsFiveScopesFor2500ButColonyAndCorporateDoNot()
    {
        await Server.WaitAssertion(() =>
        {
            EntProtoId crateId = "CMUCrateAttachmentNightVisionScope";
            string[] catalogs =
            [
                "USCMCargoCatalog",
                "UPPCargoCatalog",
                "RMCCargoCatalog",
                "HAZOPSCargoCatalog",
                "ProdigyCargoCatalog",
                "VAIPOCargoCatalog",
                "LACNCargoCatalog",
                "CMBCIUCargoCatalog",
                "WYPMCCargoCatalog",
                "BEARCargoCatalog",
            ];
            foreach (var catalogId in catalogs)
            {
                var prototype = SProtoMan.Index<EntityPrototype>(catalogId);
                Assert.That(prototype.TryComp<RequisitionsComputerComponent>(out var catalog, SEntMan.ComponentFactory), Is.True);
                var entries = catalog!.Categories.SelectMany(category => category.Entries)
                    .Where(entry => entry.Crate == crateId).ToArray();
                Assert.That(entries, Has.Length.EqualTo(1), catalogId);
                Assert.That(entries[0].Cost, Is.EqualTo(2500), catalogId);
            }

            string[] excluded = ["CMASRSConsole", "CMASRSConsoleColony", "AU14CorporateASRSConsole"];
            foreach (var consoleId in excluded)
            {
                var prototype = SProtoMan.Index<EntityPrototype>(consoleId);
                Assert.That(prototype.TryComp<RequisitionsComputerComponent>(out var catalog, SEntMan.ComponentFactory), Is.True);
                Assert.That(catalog!.Categories.SelectMany(category => category.Entries)
                    .Any(entry => entry.Crate == crateId), Is.False, consoleId);
            }
        });

        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var crate = SEntMan.SpawnEntity("CMUCrateAttachmentNightVisionScope", map.GridCoords);
            var storage = SEntMan.GetComponent<EntityStorageComponent>(crate);
            var contents = storage.Contents.ContainedEntities;
            Assert.That(contents, Has.Count.EqualTo(5));
            foreach (var scope in contents)
            {
                Assert.That(SEntMan.GetComponent<MetaDataComponent>(scope).EntityPrototype?.ID,
                    Is.EqualTo("CMUAttachmentNightVisionScope"));
                Assert.That(Server.System<PowerCellSystem>().TryGetBatteryFromSlot(scope, out var battery), Is.True);
                Assert.That(Server.System<SharedBatterySystem>().GetCharge(battery!.Value.AsNullable()),
                    Is.EqualTo(battery.Value.Comp.MaxCharge), "purchased scopes must arrive fully charged");
            }
            SEntMan.DeleteEntity(crate);
        });
    }

    private (EntityUid User, EntityUid Gun, EntityUid Scope) SpawnHeldScope(bool innate)
    {
        var user = SEntMan.SpawnEntity(innate ? "CMUNightVisionScopeTestUser" : "CMMobHuman", MapCoordinates.Nullspace);
        var gun = SEntMan.SpawnEntity("RMCWeaponRifleM54C", MapCoordinates.Nullspace);
        var scope = SEntMan.SpawnEntity("CMUAttachmentNightVisionScope", MapCoordinates.Nullspace);
        var holder = SEntMan.GetComponent<AttachableHolderComponent>(gun);
        Assert.That(Server.System<AttachableHolderSystem>().Attach((gun, holder), scope, user), Is.True);
        Assert.That(Server.System<SharedHandsSystem>().TryPickupAnyHand(user, gun), Is.True);
        Assert.That(Server.System<SharedWieldableSystem>().TryWield((gun, SEntMan.GetComponent<WieldableComponent>(gun)), user), Is.True);
        return (user, gun, scope);
    }

    private void AssertOriginalVision(EntityUid user, bool innate)
    {
        if (!innate)
        {
            Assert.That(SEntMan.HasComponent<NightVisionComponent>(user), Is.False);
            return;
        }

        var vision = SEntMan.GetComponent<NightVisionComponent>(user);
        Assert.That(vision.Innate, Is.True);
        Assert.That(vision.State, Is.EqualTo(NightVisionState.Half));
        Assert.That(vision.Green, Is.False);
    }
}
