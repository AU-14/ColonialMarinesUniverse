using Content.IntegrationTests.Fixtures;
using Content.Shared.Alert;
using Content.Shared.Alert.Components;
using Content.Shared.CCVar;
using Content.Shared.CMU14.Xenomorphs.Regen;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Xenomorphs;

[TestFixture, NonParallelizable]
public sealed class CMUXenoRegenPoolNetworkTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task ChargeAlertAndCounterReplicateToPlayer()
    {
        var map = await Pair.CreateTestMap();
        EntityUid xeno = default;
        NetEntity netXeno = default;
        await Server.WaitAssertion(() =>
        {
            Server.CfgMan.SetCVar(CCVars.CMUXenoRegenPoolEnabled, true);
            xeno = SEntMan.SpawnEntity("CMXenoDrone", map.GridCoords);
            netXeno = SEntMan.GetNetEntity(xeno);
            Server.PlayerMan.SetAttachedEntity(ServerSession!, xeno);
            Server.System<DamageableSystem>().TryChangeDamage(xeno,
                new DamageSpecifier { DamageDict = { ["Blunt"] = 251 } }, true);
        });
        await Pair.RunUntilSynced();
        await Client.WaitAssertion(() =>
        {
            var clientXeno = CEntMan.GetEntity(netXeno);
            var pool = CEntMan.GetComponent<CMUXenoRegenPoolComponent>(clientXeno);
            Assert.That(pool.Charges, Is.EqualTo(2));
            Assert.That(Client.System<AlertsSystem>().IsShowingAlert(clientXeno, pool.Alert), Is.True);
            var prototype = Client.ResolveDependency<IPrototypeManager>().Index(pool.Alert);
            var counter = new GetGenericAlertCounterAmountEvent(prototype, clientXeno);
            CEntMan.EventBus.RaiseLocalEvent(clientXeno, ref counter);
            Assert.That(counter.Amount, Is.EqualTo(2));
        });

        for (var expected = 1; expected >= 0; expected--)
        {
            await Pair.RunSeconds(5.1f);
            await Server.WaitAssertion(() =>
            {
                var damage = Server.System<DamageableSystem>();
                damage.TryChangeDamage(xeno, new DamageSpecifier { DamageDict = { ["Blunt"] = -1 } }, true);
                damage.TryChangeDamage(xeno, new DamageSpecifier { DamageDict = { ["Blunt"] = 1 } }, true);
            });
            await Pair.RunUntilSynced();
            await Client.WaitAssertion(() =>
            {
                var clientXeno = CEntMan.GetEntity(netXeno);
                var pool = CEntMan.GetComponent<CMUXenoRegenPoolComponent>(clientXeno);
                Assert.That(pool.Charges, Is.EqualTo(expected));
                var prototype = Client.ResolveDependency<IPrototypeManager>().Index(pool.Alert);
                var counter = new GetGenericAlertCounterAmountEvent(prototype, clientXeno);
                CEntMan.EventBus.RaiseLocalEvent(clientXeno, ref counter);
                Assert.That(counter.Amount, Is.EqualTo(expected));
            });
        }

        await Server.WaitAssertion(() => Server.CfgMan.SetCVar(CCVars.CMUXenoRegenPoolEnabled, false));
        await Pair.RunUntilSynced();
        await Client.WaitAssertion(() =>
        {
            var clientXeno = CEntMan.GetEntity(netXeno);
            var pool = CEntMan.GetComponent<CMUXenoRegenPoolComponent>(clientXeno);
            Assert.That(Client.System<AlertsSystem>().IsShowingAlert(clientXeno, pool.Alert), Is.False);
        });
    }
}
