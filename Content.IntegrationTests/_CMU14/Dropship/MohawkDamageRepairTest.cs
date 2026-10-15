using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Dropship.Fabricator;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.CMU14.Dropship.DirectFire;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Stacks;
using Robust.Shared.Containers;
using Content.Shared._RMC14.Dropship.AttachmentPoint;
using Content.Shared.Weapons.Ranged.Systems;

#pragma warning disable RA0002 // Advance arranged repair do-afters after checking their full configured delays.

namespace Content.IntegrationTests._CMU14.Dropship;

[TestFixture]
public sealed class MohawkDamageRepairTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Dirty = true };

    [Test]
    public async Task NewGunRequiresCompletedScrewdriverThenFactionProgramming()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            SEntMan.EnsureComponent<DropshipComponent>(map.Grid);
            var point = SEntMan.SpawnEntity("CMUMohawkM90Point", map.GridCoords);
            var chin = SEntMan.GetComponent<MohawkChinGunComponent>(point);
            var slots = SEntMan.GetComponent<DropshipWeaponPointComponent>(point);
            var weapon = Server.System<SharedContainerSystem>().GetContainer(point, slots.WeaponContainerSlotId).ContainedEntities.Single();
            chin.DefensiveFire = true;
            var system = Server.System<MohawkChinGunSystem>();
            Assert.That(system.CanOperate((point, chin), weapon, weapon, out _), Is.False);
            var actor = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(0, 0.5f)));
            var factions = SEntMan.GetComponent<Content.Shared.NPC.Components.NpcFactionMemberComponent>(actor);
            factions.Factions.Clear();
            factions.Factions.Add("OPFOR");
            var hands = Server.System<SharedHandsSystem>();
            var multitool = SEntMan.SpawnEntity("CMMultitool", map.GridCoords);
            Assert.That(hands.TryPickupAnyHand(actor, multitool), Is.True);
            SEntMan.EventBus.RaiseLocalEvent(point, new InteractUsingEvent(actor, multitool, point, map.GridCoords));
            Assert.That(chin.IffConfigured, Is.False, "Multitool cannot skip calibration.");
            hands.TryDrop(actor, multitool);

            void Use(EntityUid tool, bool cancel)
            {
                Assert.That(hands.TryPickupAnyHand(actor, tool), Is.True);
                SEntMan.EventBus.RaiseLocalEvent(point, new InteractUsingEvent(actor, tool, point, map.GridCoords));
                var after = SEntMan.GetComponent<DoAfterComponent>(actor).DoAfters.Values.Single(d => !d.Completed && !d.Cancelled);
                if (cancel)
                    Server.System<SharedDoAfterSystem>().Cancel(after);
                else
                {
                    after.StartTime = SGameTiming.CurTime - after.Args.Delay - TimeSpan.FromSeconds(1);
                    Server.System<SharedDoAfterSystem>().Update(0);
                    Assert.That(after.Completed, Is.True);
                }
                hands.TryDrop(actor, tool);
            }
            var screwdriver = SEntMan.SpawnEntity("CMScrewdriver", map.GridCoords);
            Use(screwdriver, true);
            Assert.That(chin.Calibrated, Is.False, "Interrupted calibration must not enable the gun.");
            Use(screwdriver, false);
            Assert.That(chin.Calibrated, Is.True);
            Assert.That(system.CanOperate((point, chin), weapon, weapon, out _), Is.False);
            Use(multitool, true);
            Assert.That(chin.IffConfigured, Is.False);
            Use(multitool, false);
            Assert.That(chin.IffConfigured, Is.True);
            Assert.That(SEntMan.GetComponent<Content.Shared._RMC14.Sentry.SentryTargetingComponent>(point).FriendlyFactions,
                Does.Contain("OPFOR"), "Programming takes the operator's faction, not the ship's.");
            Assert.That(system.CanOperate((point, chin), weapon, weapon, out _), Is.True);
        });
    }

    [TestCase("BulletRifle10x24mm")]
    [TestCase("BulletRifle10x24mmAP")]
    public async Task RealBulletsCollideWithTheExposedMountAndDisableWithoutDeletingIt(string bulletPrototype)
    {
        var map = await Pair.CreateTestMap();
        EntityUid point = default;
        await Server.WaitAssertion(() =>
        {
            point = SEntMan.SpawnEntity("CMUMohawkM90Point", map.GridCoords);
        });
        async Task Shoot()
        {
            await Server.WaitAssertion(() =>
            {
                var bullet = SEntMan.SpawnEntity(bulletPrototype, map.GridCoords.Offset(new Vector2(-3, 1)));
                Server.System<SharedGunSystem>().ShootProjectile(bullet, Vector2.UnitX, Vector2.Zero, null, speed: 20);
            });
            await Pair.RunSeconds(0.5f);
        }
        await Shoot();
        await Server.WaitAssertion(() =>
        {
            var damage = Server.System<DamageableSystem>();
            Assert.That(damage.GetTotalDamage(point), Is.GreaterThan(FixedPoint2.Zero));
            Assert.That(SEntMan.GetComponent<MohawkChinGunComponent>(point).Disabled, Is.False);
            damage.SetDamage(point, new DamageSpecifier { DamageDict = new() { ["Piercing"] = 1999 } });
        });
        await Shoot();
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.EntityExists(point), Is.True);
            Assert.That(SEntMan.GetComponent<MohawkChinGunComponent>(point).Disabled, Is.True);
        });
    }

    [Test]
    public async Task LowTierClawsCannotDamageButHigherTierClawsAndBulletDamageCanDisable()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            SEntMan.EnsureComponent<DropshipComponent>(map.Grid);
            var point = SEntMan.SpawnEntity("CMUMohawkM90Point", map.GridCoords);
            var chin = SEntMan.GetComponent<MohawkChinGunComponent>(point);
            var damage = Server.System<DamageableSystem>();
            var claw = SEntMan.SpawnEntity("CMXenoDrone", map.GridCoords.Offset(new Vector2(8, 0)));
            var xeno = SEntMan.GetComponent<XenoComponent>(claw);
            Assert.That(xeno.Tier, Is.EqualTo(1));
            var hit = new DamageSpecifier { DamageDict = new() { ["Slash"] = 50 } };
            damage.TryChangeDamage(point, hit, origin: claw, tool: claw);
            Assert.That(damage.GetTotalDamage(point), Is.EqualTo(FixedPoint2.Zero));
            xeno.Tier = 2;
            damage.TryChangeDamage(point, hit, origin: claw, tool: claw);
            Assert.That(damage.GetTotalDamage(point), Is.EqualTo(FixedPoint2.New(50)));
            Assert.That(chin.MaxHealth, Is.EqualTo(2000));
            var mount = SEntMan.GetComponent<DropshipWeaponPointComponent>(point);
            var weapon = Server.System<SharedContainerSystem>().GetContainer(point, mount.WeaponContainerSlotId).ContainedEntities.Single();
            chin.DefensiveFire = true;
            chin.FreeFireEnabled = true;
            damage.TryChangeDamage(point, new DamageSpecifier { DamageDict = new() { ["Piercing"] = 1949 } });
            Assert.That(chin.Disabled, Is.False, "1999 damage leaves exactly one HP.");
            damage.TryChangeDamage(point, new DamageSpecifier { DamageDict = new() { ["Piercing"] = 1 } });
            Assert.That(chin.Disabled, Is.True);
            Assert.That(chin.FreeFireEnabled, Is.True, "Damage pauses AI without disabling its toggle.");
            Assert.That(Server.System<MohawkChinGunSystem>().CanOperate((point, chin), weapon, weapon, out _), Is.False);
            damage.SetAllDamage(point, FixedPoint2.Zero);
            Assert.That(chin.Disabled, Is.True, "Ordinary healing must not bypass the costly staged rebuild.");
        });
    }

    [Test]
    public async Task DisabledMountRequiresOrderedPartsCableWeldingAndCalibration()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var point = SEntMan.SpawnEntity("CMUMohawkM90Point", map.GridCoords);
            SEntMan.EnsureComponent<DropshipComponent>(map.Grid).AttachmentPoints.Add(point);
            var chin = SEntMan.GetComponent<MohawkChinGunComponent>(point);
            var actor = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(0, 0.5f)));
            Server.System<SkillsSystem>().SetSkill(actor, "RMCSkillEngineer", 1);
            var damage = Server.System<DamageableSystem>();
            damage.TryChangeDamage(point, new DamageSpecifier { DamageDict = new() { ["Piercing"] = 2000 } });
            Assert.That(chin.Disabled, Is.True);
            var parts = SEntMan.SpawnEntity("CMUMohawkM90RepairParts", map.GridCoords);
            Assert.That(SEntMan.GetComponent<DropshipFabricatorPrintableComponent>(parts).Cost, Is.EqualTo(1150));
            Assert.That(SEntMan.GetComponent<DropshipFabricatorPrintableComponent>(parts).Category,
                Is.EqualTo(DropshipFabricatorPrintableComponent.CategoryType.Equipment));
            Assert.That(Server.System<DropshipFabricatorSystem>().Printables.Any(p => p.Id == "CMUMohawkM90RepairParts"), Is.True);
            var hands = Server.System<SharedHandsSystem>();
            Assert.That(hands.TryPickupAnyHand(actor, parts), Is.True);
            SEntMan.EventBus.RaiseLocalEvent(point, new InteractUsingEvent(actor, parts, point, map.GridCoords));
            Assert.That(chin.RepairStage, Is.EqualTo(MohawkChinGunRepairStage.Strip), "Parts cannot skip dismantling.");
            hands.TryDrop(actor, parts);

            void Repair(EntityUid item, MohawkChinGunRepairStage stage)
            {
                Assert.That(hands.TryPickupAnyHand(actor, item), Is.True);
                var interact = new InteractUsingEvent(actor, item, point, map.GridCoords);
                SEntMan.EventBus.RaiseLocalEvent(point, interact);
                Assert.That(interact.Handled, Is.True);
                var after = SEntMan.GetComponent<DoAfterComponent>(actor).DoAfters.Values.Single(d => !d.Cancelled && !d.Completed);
                Assert.That(((MohawkChinGunRepairDoAfterEvent) after.Args.Event).Stage, Is.EqualTo(stage));
                Assert.That(after.Args.Delay, Is.EqualTo(MohawkChinGunSystem.RepairDelay(stage)));
                after.StartTime = SGameTiming.CurTime - after.Args.Delay - TimeSpan.FromSeconds(1);
                Server.System<SharedDoAfterSystem>().Update(0);
                Assert.That(after.Completed, Is.True);
                if (!SEntMan.Deleted(item))
                    hands.TryDrop(actor, item);
            }
            Repair(SEntMan.SpawnEntity("CMWrench", map.GridCoords), MohawkChinGunRepairStage.Strip);
            Assert.That(chin.RepairStage, Is.EqualTo(MohawkChinGunRepairStage.Parts));
            Assert.That(hands.TryPickupAnyHand(actor, parts), Is.True);
            SEntMan.EventBus.RaiseLocalEvent(point, new InteractUsingEvent(actor, parts, point, map.GridCoords));
            var interrupted = SEntMan.GetComponent<DoAfterComponent>(actor).DoAfters.Values.Single(d => !d.Cancelled && !d.Completed);
            Assert.That(interrupted.Args.Delay, Is.EqualTo(TimeSpan.FromSeconds(120)));
            Server.System<SharedDoAfterSystem>().Cancel(interrupted);
            Assert.That(chin.RepairStage, Is.EqualTo(MohawkChinGunRepairStage.Parts));
            Assert.That(SEntMan.IsQueuedForDeletion(parts), Is.False, "An interrupted installation must not consume expensive parts.");
            hands.TryDrop(actor, parts);
            Repair(parts, MohawkChinGunRepairStage.Parts);
            Assert.That(chin.RepairStage, Is.EqualTo(MohawkChinGunRepairStage.Cable));
            Assert.That(SEntMan.IsQueuedForDeletion(parts), Is.True, "Successful installation consumes exactly one parts box.");
            var cable = SEntMan.SpawnEntity("RMCCableCoil30", map.GridCoords);
            Repair(cable, MohawkChinGunRepairStage.Cable);
            Assert.That(Server.System<SharedStackSystem>().GetCount(cable), Is.EqualTo(20));
            var welder = SEntMan.SpawnEntity("CMWelder", map.GridCoords);
            Server.System<ItemToggleSystem>().TryActivate(welder, actor, showPopup: false);
            Repair(welder, MohawkChinGunRepairStage.Weld);
            Assert.That(chin.Disabled, Is.True, "Welding alone cannot enable the turret.");
            Repair(SEntMan.SpawnEntity("CMScrewdriver", map.GridCoords), MohawkChinGunRepairStage.Calibrate);
            Assert.That(chin.Disabled, Is.False);
            Assert.That(damage.GetTotalDamage(point), Is.EqualTo(FixedPoint2.Zero));
        });
    }
}
