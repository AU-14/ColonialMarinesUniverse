using System.Numerics;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Actions;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Heal;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.Plasma;
using Content.Shared._RMC14.Xenonids.Rest;
using Content.Shared.CCVar;
using Content.Shared.CMU14.Xenomorphs.Regen;
using Content.Shared.Actions;
using Content.Shared.Alert;
using Content.Shared.Alert.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.CMU14.Xenomorphs;

#pragma warning disable RA0002
[TestFixture, NonParallelizable]
public sealed class CMUXenoRegenPoolTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    private EntityUid _xeno;
    private EntityUid _hive;

    private async Task Prepare(string prototype = "CMXenoDrone")
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            Server.CfgMan.SetCVar(CCVars.CMUXenoRegenPoolEnabled, true);
            _hive = SEntMan.SpawnEntity("CMXenoHive", map.GridCoords);
            _xeno = SEntMan.SpawnEntity(prototype, map.GridCoords);
            Server.System<SharedXenoHiveSystem>().SetHive(_xeno, _hive);
        });
    }

    private void ChangeDamage(int amount)
    {
        Server.System<DamageableSystem>().TryChangeDamage(_xeno,
            new DamageSpecifier { DamageDict = { ["Blunt"] = amount } }, true);
    }

    [Test]
    public async Task CrossingsConsumeChargesWithGraceAndBlockOnlyPassiveHealth()
    {
        await Prepare();
        await Server.WaitAssertion(() =>
        {
            var pool = SEntMan.GetComponent<CMUXenoRegenPoolComponent>(_xeno);
            Assert.That(pool.Charges, Is.EqualTo(3));
            ChangeDamage(250);
            Assert.That(pool.Charges, Is.EqualTo(3), "exactly 50% is not below 50%");
            ChangeDamage(1);
            Assert.That(pool.Charges, Is.EqualTo(2));
            ChangeDamage(-1);
            ChangeDamage(1);
            Assert.That(pool.Charges, Is.EqualTo(2), "crossings during grace are ignored");
        });
        await Pair.RunSeconds(5.1f);
        await Server.WaitAssertion(() =>
        {
            var pool = SEntMan.GetComponent<CMUXenoRegenPoolComponent>(_xeno);
            Assert.That(pool.Charges, Is.EqualTo(2), "remaining below half does not consume another charge");
            ChangeDamage(-1);
            ChangeDamage(1);
            Assert.That(pool.Charges, Is.EqualTo(1));
        });
        await Pair.RunSeconds(5.1f);
        await Server.WaitAssertion(() =>
        {
            var pool = SEntMan.GetComponent<CMUXenoRegenPoolComponent>(_xeno);
            ChangeDamage(-1);
            ChangeDamage(1);
            Assert.That(pool.Charges, Is.Zero);
            Server.System<XenoSystem>().SetHealOffWeeds(_xeno, true);
            var plasma = SEntMan.GetComponent<XenoPlasmaComponent>(_xeno);
            Server.System<XenoPlasmaSystem>().SetPlasma((_xeno, plasma), 0);
        });
        await Pair.RunSeconds(2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Server.System<DamageableSystem>().GetTotalDamage(_xeno).Float(), Is.EqualTo(251).Within(0.01));
            Assert.That(SEntMan.GetComponent<XenoPlasmaComponent>(_xeno).Plasma.Float(), Is.GreaterThan(0),
                "depleted health regeneration must not block plasma regeneration");
            ChangeDamage(-10);
            Assert.That(Server.System<DamageableSystem>().GetTotalDamage(_xeno).Float(), Is.EqualTo(241).Within(0.01),
                "other healing remains available");
            Server.CfgMan.SetCVar(CCVars.CMUXenoRegenPoolEnabled, false);
        });
        await Pair.RunSeconds(2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Server.System<DamageableSystem>().GetTotalDamage(_xeno).Float(), Is.LessThan(241));
            Server.CfgMan.SetCVar(CCVars.CMUXenoRegenPoolEnabled, true);
        });
    }

    [TestCase("HiveClusterXeno")]
    [TestCase("HiveCoreXeno")]
    [TestCase("HiveRecoveryNodeXeno")]
    [TestCase("CMXenoQueen")]
    [TestCase("CMU14PathogenCluster")]
    [TestCase("CMU14PathogenBlightCore")]
    [TestCase("CMU14PathogenRecoveryNode")]
    [TestCase("CMU14XenoOvermind")]
    public async Task RestingNearFriendlySourceRechargesOneChargePerTenSeconds(string sourcePrototype)
    {
        await Prepare();
        await Server.WaitAssertion(() =>
        {
            var source = SEntMan.SpawnEntity(sourcePrototype, SEntMan.GetComponent<TransformComponent>(_xeno).Coordinates.Offset(new Vector2(5, 0)));
            Server.System<SharedXenoHiveSystem>().SetHive(source, _hive);
            SEntMan.GetComponent<CMUXenoRegenPoolComponent>(_xeno).Charges = 0;
            SEntMan.AddComponent<XenoRestingComponent>(_xeno);
        });
        await Pair.RunSeconds(9.5f);
        await Server.WaitAssertion(() => Assert.That(SEntMan.GetComponent<CMUXenoRegenPoolComponent>(_xeno).Charges, Is.Zero));
        await Pair.RunSeconds(0.7f);
        await Server.WaitAssertion(() => Assert.That(SEntMan.GetComponent<CMUXenoRegenPoolComponent>(_xeno).Charges, Is.EqualTo(1)));
        await Pair.RunSeconds(10.1f);
        await Server.WaitAssertion(() => Assert.That(SEntMan.GetComponent<CMUXenoRegenPoolComponent>(_xeno).Charges, Is.EqualTo(2)));
    }

    [Test]
    public async Task RechargeRequiresContinuousRestAndFriendlySourceInRange()
    {
        await Prepare();
        EntityUid source = default;
        await Server.WaitAssertion(() =>
        {
            var coordinates = SEntMan.GetComponent<TransformComponent>(_xeno).Coordinates;
            source = SEntMan.SpawnEntity("HiveCoreXeno", coordinates.Offset(new Vector2(6.1f, 0)));
            Server.System<SharedXenoHiveSystem>().SetHive(source, _hive);
            SEntMan.GetComponent<CMUXenoRegenPoolComponent>(_xeno).Charges = 0;
            SEntMan.AddComponent<XenoRestingComponent>(_xeno);
        });
        await Pair.RunSeconds(11);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<CMUXenoRegenPoolComponent>(_xeno).Charges, Is.Zero);
            var coordinates = SEntMan.GetComponent<TransformComponent>(_xeno).Coordinates;
            Server.System<SharedTransformSystem>().SetCoordinates(source, coordinates.Offset(new Vector2(5.9f, 0)));
        });
        await Pair.RunSeconds(6);
        await Server.WaitAssertion(() => SEntMan.RemoveComponent<XenoRestingComponent>(_xeno));
        await Pair.RunSeconds(1);
        await Server.WaitAssertion(() => SEntMan.AddComponent<XenoRestingComponent>(_xeno));
        await Pair.RunSeconds(5);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<CMUXenoRegenPoolComponent>(_xeno).Charges, Is.Zero,
                "interrupted rest must not retain progress");
            var otherHive = SEntMan.SpawnEntity("CMXenoHive", SEntMan.GetComponent<TransformComponent>(_xeno).Coordinates);
            Server.System<SharedXenoHiveSystem>().SetHive(source, otherHive);
        });
        await Pair.RunSeconds(11);
        await Server.WaitAssertion(() => Assert.That(SEntMan.GetComponent<CMUXenoRegenPoolComponent>(_xeno).Charges, Is.Zero));
    }

    [Test]
    public async Task SalveRestoresPoolOnlyWhenItActuallyHeals()
    {
        await Prepare();
        await Server.WaitAssertion(() =>
        {
            var coordinates = SEntMan.GetComponent<TransformComponent>(_xeno).Coordinates;
            var healer = SEntMan.SpawnEntity("CMXenoDroneHealer", coordinates.Offset(new Vector2(1, 0)));
            Server.System<SharedXenoHiveSystem>().SetHive(healer, _hive);
            var pool = SEntMan.GetComponent<CMUXenoRegenPoolComponent>(_xeno);
            pool.Charges = 0;
            ChangeDamage(100);
            var action = Server.System<SharedRMCActionsSystem>().GetActionsWithEvent<XenoApplySalveActionEvent>(healer).Single();
            var ev = new XenoApplySalveActionEvent { Target = _xeno };
            Server.System<SharedActionsSystem>().PerformAction(healer, action, ev);
            Assert.That(ev.Handled, Is.True);
            Assert.That(pool.Charges, Is.Zero, "applying salve without healing yet must not refill");
        });
        await Pair.RunSeconds(1.2f);
        await Server.WaitAssertion(() => Assert.That(SEntMan.GetComponent<CMUXenoRegenPoolComponent>(_xeno).Charges, Is.EqualTo(3)));
    }

    [Test]
    public async Task NeomorphsInheritPoolAndAnEmptySalveDoesNotRefillIt()
    {
        await Prepare("CMU14XenoNeomorph");
        await Server.WaitAssertion(() =>
        {
            var pool = SEntMan.GetComponent<CMUXenoRegenPoolComponent>(_xeno);
            Assert.That(pool.MaxCharges, Is.EqualTo(3));
            pool.Charges = 0;
            var healing = SEntMan.EnsureComponent<XenoBeingHealedComponent>(_xeno);
            healing.HealStacks.Add(new XenoHealStack
            {
                Charges = 1,
                HealAmount = 10,
                CMURefillRegenPool = true,
            });
        });
        await Pair.RunSeconds(0.2f);
        await Server.WaitAssertion(() => Assert.That(SEntMan.GetComponent<CMUXenoRegenPoolComponent>(_xeno).Charges, Is.Zero));
    }

    [TestCase("CMXenoDrone", "XenoWeeds")]
    [TestCase("CMU14XenoNeomorph", "CMU14XenoMyceliumWeeds")]
    public async Task DepletedRegenOnFriendlyWeedsIsSeventyFivePercent(string prototype, string weedsPrototype)
    {
        await Prepare(prototype);
        EntityUid full = default;
        await Server.WaitAssertion(() =>
        {
            var coordinates = SEntMan.GetComponent<TransformComponent>(_xeno).Coordinates;
            full = SEntMan.SpawnEntity(prototype, coordinates);
            var weeds = SEntMan.SpawnEntity(weedsPrototype, coordinates);
            var hive = Server.System<SharedXenoHiveSystem>();
            hive.SetHive(full, _hive);
            hive.SetHive(weeds, _hive);
            var damage = new DamageSpecifier { DamageDict = { ["Blunt"] = 100 } };
            Server.System<DamageableSystem>().TryChangeDamage(_xeno, damage, true);
            Server.System<DamageableSystem>().TryChangeDamage(full, damage, true);
            SEntMan.GetComponent<CMUXenoRegenPoolComponent>(_xeno).Charges = 0;
        });

        await Pair.RunSeconds(3.2f);
        await Server.WaitAssertion(() =>
        {
            var damage = Server.System<DamageableSystem>();
            var fullHealing = 100f - damage.GetTotalDamage(full).Float();
            var depletedHealing = 100f - damage.GetTotalDamage(_xeno).Float();
            Assert.That(fullHealing, Is.GreaterThan(0), "friendly weeds must heal an unspent xeno");
            Assert.That(depletedHealing, Is.EqualTo(fullHealing * 0.75f).Within(0.08f),
                "depleted health regeneration on weeds must be reduced by 25%");
        });
    }

    [Test]
    public async Task ChargeAlertAndExamineAreAvailableOnlyWhenEnabled()
    {
        await Prepare();
        await Server.WaitAssertion(() =>
        {
            var coordinates = SEntMan.GetComponent<TransformComponent>(_xeno).Coordinates;
            var neo = SEntMan.SpawnEntity("CMU14XenoNeomorph", coordinates);
            var human = SEntMan.SpawnEntity("CMMobHuman", coordinates);
            var alerts = Server.System<AlertsSystem>();
            var pool = SEntMan.GetComponent<CMUXenoRegenPoolComponent>(_xeno);
            Assert.That(alerts.IsShowingAlert(_xeno, pool.Alert), Is.True);

            var prototype = Server.ResolveDependency<IPrototypeManager>().Index(pool.Alert);
            var counter = new GetGenericAlertCounterAmountEvent(prototype, _xeno);
            SEntMan.EventBus.RaiseLocalEvent(_xeno, ref counter);
            Assert.That(counter.Amount, Is.EqualTo(3));

            var examine = new ExaminedEvent(new FormattedMessage(), _xeno, neo, true, false);
            SEntMan.EventBus.RaiseLocalEvent(_xeno, examine);
            Assert.That(examine.GetTotalMessage().ToString(), Does.Contain("3 / 3"));

            var humanExamine = new ExaminedEvent(new FormattedMessage(), _xeno, human, true, false);
            SEntMan.EventBus.RaiseLocalEvent(_xeno, humanExamine);
            Assert.That(humanExamine.GetTotalMessage().ToString(), Does.Not.Contain("Regeneration charges"));

            Server.CfgMan.SetCVar(CCVars.CMUXenoRegenPoolEnabled, false);
            Assert.That(alerts.IsShowingAlert(_xeno, pool.Alert), Is.False);
            var disabledExamine = new ExaminedEvent(new FormattedMessage(), _xeno, neo, true, false);
            SEntMan.EventBus.RaiseLocalEvent(_xeno, disabledExamine);
            Assert.That(disabledExamine.GetTotalMessage().ToString(), Does.Not.Contain("Regeneration charges"));
        });
    }
}
#pragma warning restore RA0002
