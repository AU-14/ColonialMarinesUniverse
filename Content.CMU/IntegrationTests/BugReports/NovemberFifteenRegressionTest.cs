using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared._RMC14.Armor;
using Content.Shared.Chemistry.Reagent;
using Content.Shared._RMC14.Emplacements;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Vendors;
using Content.Shared._RMC14.Xenonids.Eye;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.CMU14.util;
using Content.Shared.CMU14.Weapons.Safety;
using Content.Shared.CMU14.Xenomorphs.Pathogen.MycotoxinInject;
using Content.Shared.CMU14.Xenomorphs.Pathogen.Overmind;
using Content.Shared.CMU14.Xenomorphs.Pathogen.Walker;
using Content.Shared.Inventory;
using Content.Shared.Roles;
using Content.Shared.Verbs;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.CMU14.BugReports;

[TestFixture]
public sealed class NovemberFifteenRegressionTest : GameTest
{
    private void SwapForm(EntityUid overmind, bool incorporeal)
    {
        var ev = new CMUXenoOvermindFormChangedEvent(incorporeal);
        SEntMan.EventBus.RaiseLocalEvent(overmind, ref ev);
    }

    // BUG-1415: the overmind's eye form isn't a queen eye action, so moving the eye broke every build do-after
    [Test]
    public async Task OvermindEyeFormCountsAsARemoteEye()
    {
        var map = await Pair.CreateTestMap();
        EntityUid overmind = default;
        await Server.WaitPost(() =>
        {
            SEntMan.SpawnEntity("CMUPathogenHive", map.GridCoords);
            overmind = SEntMan.SpawnEntity("CMU14XenoOvermind", map.GridCoords);
        });
        await Server.WaitRunTicks(5);

        await Server.WaitAssertion(() =>
        {
            var eye = SEntMan.System<QueenEyeSystem>();
            Assert.That(SEntMan.GetComponent<CMUXenoOvermindComponent>(overmind).Eye, Is.Not.Null);
            Assert.That(eye.IsInQueenEye(overmind), Is.False, "the overmind never had a queen eye action eye");
            Assert.That(eye.IsMovingRemoteEye(overmind), Is.True);

            SwapForm(overmind, false);
            Assert.That(eye.IsMovingRemoteEye(overmind), Is.False, "in the physical form the body moves itself");
        });
    }

    // BUG-1411: form swaps respawned the form actions, so swapping twice reset blight scream
    [Test]
    public async Task OvermindFormSwapKeepsActionCooldowns()
    {
        var map = await Pair.CreateTestMap();
        EntityUid overmind = default;
        await Server.WaitPost(() =>
        {
            SEntMan.SpawnEntity("CMUPathogenHive", map.GridCoords);
            overmind = SEntMan.SpawnEntity("CMU14XenoOvermind", map.GridCoords);
        });
        await Server.WaitRunTicks(5);

        await Server.WaitAssertion(() =>
        {
            var actions = SEntMan.System<SharedActionsSystem>();
            var timing = Server.ResolveDependency<IGameTiming>();
            var comp = SEntMan.GetComponent<CMUXenoOvermindComponent>(overmind);

            SwapForm(overmind, false);
            var scream = comp.PhysicalFormActionEntities["CMU14ActionXenoPathogenBlightWave"];
            actions.SetCooldown(scream, TimeSpan.FromSeconds(60));

            SwapForm(overmind, true);
            SwapForm(overmind, false);

            var regranted = comp.PhysicalFormActionEntities["CMU14ActionXenoPathogenBlightWave"];
            Assert.That(regranted, Is.Not.EqualTo(scream), "the action entity is respawned on every swap");
            var cooldown = SEntMan.GetComponent<ActionComponent>(regranted).Cooldown;
            Assert.That(cooldown, Is.Not.Null);
            Assert.That(cooldown!.Value.End, Is.GreaterThan(timing.CurTime + TimeSpan.FromSeconds(50)));
        });
    }

    // BUG-1374: walkers raised by a corrupted neo joined the prime pathogen hive
    [Test]
    public async Task WalkersJoinTheInjectorsHive()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var hives = SEntMan.System<SharedXenoHiveSystem>();
            SEntMan.SpawnEntity("CMUPathogenHive", map.GridCoords);
            var corrupted = SEntMan.SpawnEntity("CMUCorruptedHive", map.GridCoords);
            var injector = SEntMan.SpawnEntity("CMU14XenoPopper", map.GridCoords);
            hives.SetHive(injector, corrupted);
            var victim = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);

            SEntMan.EventBus.RaiseEvent(EventSource.Local, new CMUMycotoxinInjectDoReanimateEvent(victim, injector));

            Assert.That(SEntMan.GetComponent<CMUPathogenWalkerComponent>(victim).Hive, Is.EqualTo(corrupted));
            Assert.That(hives.GetHive(victim)?.Owner, Is.EqualTo(corrupted));
        });
    }

    // BUG-1396: a gibbed walker dropped its bloom and anyone could wear it for free night vision
    [Test]
    public async Task WalkerBloomRotsAwayOnceItLeavesTheHead()
    {
        var map = await Pair.CreateTestMap();
        EntityUid bloom = default;
        await Server.WaitAssertion(() =>
        {
            var inventory = SEntMan.System<InventorySystem>();
            var human = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            bloom = SEntMan.SpawnEntity("CMU14ClothingHeadWalkerMarker", map.GridCoords);
            inventory.TryUnequip(human, "head", force: true);
            Assert.That(inventory.TryEquip(human, bloom, "head", force: true), Is.True);
            Assert.That(inventory.TryUnequip(human, "head"), Is.False, "a walker still can't take it off");
            Assert.That(inventory.TryUnequip(human, "head", force: true), Is.True);
        });
        await Server.WaitRunTicks(2);
        await Server.WaitAssertion(() => Assert.That(SEntMan.Deleted(bloom), Is.True));
    }

    // BUG-1401: 15u was exactly the 15u overdose line, and overdose starts at >=
    [TestCase("AU14PillDermaline", "CMDermaline")]
    [TestCase("AU14PillMeralyne", "CMMeralyne")]
    public async Task OnePillIsNotAnOverdose(string pill, string reagent)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var solutions = SEntMan.System<SharedSolutionContainerSystem>();
            var uid = SEntMan.SpawnEntity(pill, map.GridCoords);
            Assert.That(solutions.TryGetSolution(uid, "food", out _, out var solution), Is.True);
            var overdose = SProtoMan.Index<ReagentPrototype>(reagent).Overdose;
            Assert.That(overdose, Is.Not.Null);
            Assert.That(solution!.GetTotalPrototypeQuantity(reagent), Is.LessThan(overdose!.Value));
        });
    }

    // BUG-1391: armor is per zone, so the suit's 100 bio never reached the hands, feet or head
    [TestCase("RMCHandsVeteranCBRN")]
    [TestCase("RMCBootsCBRN")]
    [TestCase("AU14BootsCBRN")]
    [TestCase("AU14GlovesCBRNUPP")]
    [TestCase("AU14UPPCBRNHood")]
    public async Task MoppPiecesCarryTheirOwnBioArmor(string piece)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var uid = SEntMan.SpawnEntity(piece, map.GridCoords);
            Assert.That(SEntMan.GetComponent<CMArmorComponent>(uid).Bio, Is.GreaterThanOrEqualTo(75));
        });
    }

    // BUG-1408: the CMB tactical team carried breaching charges that need engineer 1
    [TestCase("CMUJobCMBTacticalLeader")]
    [TestCase("CMUJobCMBTacticalDeputy")]
    [TestCase("CMUJobCMBTacticalDeputyShotgun")]
    public async Task CmbTacticalTeamCanUseItsBreachingCharges(string job)
    {
        await Server.WaitAssertion(() =>
        {
            var proto = SProtoMan.Index<JobPrototype>(job);
            Assert.That(proto.RoundComponents.TryGetValue("Skills", out var entry), Is.True);
            var skills = (SkillsComponent) entry!.Component;
            Assert.That(skills.Skills.GetValueOrDefault("RMCSkillEngineer"), Is.GreaterThanOrEqualTo(1));
            Assert.That(skills.Skills.GetValueOrDefault("RMCSkillPolice"), Is.EqualTo(2), "still a CMB deputy");
        });
    }

    // BUG-1380 and BUG-1376: the para synth wasn't a synth and the para CO had no orders
    [Test]
    public async Task ParamarineSynthAndCommanderAreWiredUp()
    {
        await Server.WaitAssertion(() =>
        {
            var synth = SProtoMan.Index<JobPrototype>("CMUJobParamarineSynthetic");
            Assert.That(synth.IsSynthetic, Is.True);
            Assert.That(synth.Whitelisted, Is.True);
            Assert.That(synth.RoundComponents.ContainsKey("Synth"), Is.True);

            var commander = SProtoMan.Index<JobPrototype>("CMUJobParamarineCommander");
            Assert.That(commander.RoundComponents.ContainsKey("MarineOrders"), Is.True);
        });
    }

    // BUG-1409, BUG-1340, BUG-1365: gear the platoons and third parties were missing
    [Test]
    public async Task MissingGearIsHandedOut()
    {
        await Server.WaitAssertion(() =>
        {
            var rmc = SProtoMan.Index<PlatoonPrototype>("RMC");
            Assert.That(rmc.StartingGearOverrides.GetValueOrDefault("CMSatchelMarine").Id, Is.EqualTo("AU14SmallBackpackRMC"));

            var vendor = SProtoMan.Index("AU14HAZOPSMGequipmentvendor");
            Assert.That(vendor.TryGetComponent<CMAutomatedVendorComponent>(out var comp, SEntMan.ComponentFactory), Is.True);
            Assert.That(comp!.Sections.SelectMany(s => s.Entries).Any(e => e.Id == "RMCPowerCellSmartgun"), Is.True);

            var militsiya = SProtoMan.Index<StartingGearPrototype>("CMUGearUPPMilitsioner");
            Assert.That(militsiya.Equipment.ContainsKey("ears"), Is.True);
        });
    }
}

[TestFixture]
public sealed class NovemberFifteenMountSafetyTest : InteractionTest
{
    // BUG-1349: a deployed gun sits in its mount out of reach of the right click menu, so an M56D vended on
    // safety could never be fired
    [Test]
    public async Task MountedGunSafetyCanBeSwitchedFromTheMount()
    {
        await SpawnTarget("CMUM2CDoorGunDropship");
        var mount = STarget!.Value;
        await Server.WaitAssertion(() =>
        {
            var safety = SEntMan.System<CMUGunSafetySystem>();
            var verbs = SEntMan.System<SharedVerbSystem>();
            var gun = SEntMan.GetComponent<WeaponMountComponent>(mount).MountedEntity;
            Assert.That(gun, Is.Not.Null);
            safety.SetSafety(gun!.Value, true);

            var off = verbs.GetLocalVerbs(mount, SPlayer, typeof(Verb))
                .Single(v => v.Text == Loc.GetString("cmu-gun-safety-verb-off"));
            verbs.ExecuteVerb(off, SPlayer, mount);
            Assert.That(safety.IsOn(gun.Value), Is.False);
        });
    }
}
