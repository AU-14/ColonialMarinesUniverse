using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Actions;
using Content.Shared._RMC14.Gibbing;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.Reaper;
using Content.Shared.Actions;
using Content.Shared.CMU14.Xenomorphs.Reaper;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs.Systems;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Xenomorphs;

// Seed resource balances and disable passive gains without waiting for unrelated harvesting.
#pragma warning disable RA0002
[TestFixture]
public sealed class CMUReaperRevivalTest : GameTest
{
    private EntityUid _caster;
    private EntityUid _corpse;
    private EntityUid _hive;
    private EntityUid? _originalAttached;

    private async Task Prepare(float duration)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            _originalAttached = ServerSession!.AttachedEntity;
            _hive = SEntMan.SpawnEntity("CMXenoHive", map.GridCoords);
            _caster = SEntMan.SpawnEntity("CMXenoReaper", map.GridCoords);
            _corpse = SEntMan.SpawnEntity("CMXenoDrone", map.GridCoords.Offset(new Vector2(1, 0)));
            // These scenarios need intact bodies; target deletion exercises the destroyed-corpse path.
            SEntMan.RemoveComponent<RMCGibOnDeathComponent>(_corpse);
            SEntMan.RemoveComponent<RMCGibOnDeathComponent>(_caster);
            var hive = Server.System<SharedXenoHiveSystem>();
            hive.SetHive(_caster, _hive);
            hive.SetHive(_corpse, _hive);
            Server.PlayerMan.SetAttachedEntity(ServerSession, _caster);
            var reaper = SEntMan.GetComponent<XenoReaperComponent>(_caster);
            reaper.FleshResin = 600;
            reaper.PassiveGainMaxFleshResin = 0;
            SEntMan.GetComponent<CMUReaperRevivalComponent>(_caster).ChargeTime = TimeSpan.FromSeconds(duration);
            Server.System<DamageableSystem>().TryChangeDamage(_corpse,
                new DamageSpecifier { DamageDict = { ["Blunt"] = 10000 } }, true);
            Assert.That(Server.System<MobStateSystem>().IsDead(_corpse), Is.True);
        });
        await Pair.RunTicksSync(5);
    }

    private CMUReaperReviveActionEvent Cast(EntityUid caster)
    {
        var action = Server.System<SharedRMCActionsSystem>().GetActionsWithEvent<CMUReaperReviveActionEvent>(caster).Single();
        var ev = new CMUReaperReviveActionEvent { Target = _corpse };
        Server.System<SharedActionsSystem>().PerformAction(caster, action, ev);
        return ev;
    }

    [Test]
    public async Task AcceptedRevivalSpendsUpfrontAndReplicatesCompletion()
    {
        await Prepare(0.5f);
        NetEntity targetNet = default;
        NetEntity effectNet = default;
        await Server.WaitAssertion(() =>
        {
            Assert.That(Cast(_caster).Handled, Is.True);
            targetNet = SEntMan.GetNetEntity(_corpse);
            var effect = SEntMan.GetComponent<CMUReaperRevivingComponent>(_corpse).Effect;
            effectNet = SEntMan.GetNetEntity(effect);
            Assert.That(SEntMan.GetComponent<XenoReaperComponent>(_caster).FleshResin, Is.EqualTo(100));
            var action = Server.System<SharedRMCActionsSystem>().GetActionsWithEvent<CMUReaperReviveActionEvent>(_caster).Single();
            Assert.That(action.Comp.Cooldown!.Value.End, Is.GreaterThan(SGameTiming.CurTime + TimeSpan.FromMinutes(9)));
            Assert.That(Server.System<MobStateSystem>().IsDead(_corpse), Is.True, "charging must not revive early");
        });
        await Pair.RunTicksSync(5);
        await Client.WaitAssertion(() =>
        {
            Assert.That(CEntMan.GetComponent<CMUReaperRitualComponent>(CEntMan.GetEntity(effectNet)).State,
                Is.EqualTo(CMUReaperCastState.Charging));
        });
        await Pair.RunTicksSync(50);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Server.System<MobStateSystem>().IsAlive(_corpse), Is.True);
            Assert.That(SEntMan.HasComponent<CMUReaperRevivingComponent>(_corpse), Is.False);
            Assert.That(SEntMan.GetComponent<XenoReaperComponent>(_caster).FleshResin, Is.EqualTo(100));
        });
        await Client.WaitAssertion(() =>
        {
            Assert.That(Client.System<MobStateSystem>().IsAlive(CEntMan.GetEntity(targetNet)), Is.True);
            Assert.That(CEntMan.GetComponent<CMUReaperRitualComponent>(CEntMan.GetEntity(effectNet)).State,
                Is.EqualTo(CMUReaperCastState.Succeeded));
        });
    }

    [TestCase("move")]
    [TestCase("damage")]
    [TestCase("death")]
    [TestCase("target-deleted")]
    public async Task InterruptedRevivalKeepsCostAndCooldownWithoutReviving(string reason)
    {
        await Prepare(2);
        EntityUid effect = default;
        await Server.WaitAssertion(() =>
        {
            Assert.That(Cast(_caster).Handled, Is.True);
            effect = SEntMan.GetComponent<CMUReaperRevivingComponent>(_corpse).Effect;
        });
        await Pair.RunTicksSync(5);
        await Server.WaitPost(() =>
        {
            switch (reason)
            {
                case "move":
                    var transform = Server.System<SharedTransformSystem>();
                    transform.SetCoordinates(_caster, SEntMan.GetComponent<TransformComponent>(_caster).Coordinates.Offset(new Vector2(3, 0)));
                    break;
                case "damage":
                case "death":
                    Server.System<DamageableSystem>().TryChangeDamage(_caster,
                        new DamageSpecifier { DamageDict = { ["Blunt"] = reason == "death" ? 10000 : 10 } }, true);
                    break;
                case "target-deleted":
                    SEntMan.DeleteEntity(_corpse);
                    break;
            }
        });
        await Pair.RunTicksSync(10);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<CMUReaperRitualComponent>(effect).State, Is.EqualTo(CMUReaperCastState.Cancelled));
            Assert.That(SEntMan.GetComponent<XenoReaperComponent>(_caster).FleshResin, Is.EqualTo(100));
            var action = Server.System<SharedRMCActionsSystem>().GetActionsWithEvent<CMUReaperReviveActionEvent>(_caster).Single();
            Assert.That(action.Comp.Cooldown!.Value.End, Is.GreaterThan(SGameTiming.CurTime + TimeSpan.FromMinutes(9)));
            if (reason != "target-deleted")
            {
                Assert.That(Server.System<MobStateSystem>().IsDead(_corpse), Is.True);
                Assert.That(SEntMan.HasComponent<CMUReaperRevivingComponent>(_corpse), Is.False);
            }
        });
        await Pair.RunTicksSync(150);
        await Server.WaitAssertion(() =>
        {
            if (reason != "target-deleted")
                Assert.That(Server.System<MobStateSystem>().IsDead(_corpse), Is.True, "a cancelled timer must never complete later");
            Assert.That(SEntMan.EntityExists(effect), Is.False, "interrupted effects must be cleaned up");
        });
    }

    [Test]
    public async Task StaleCorpseIsRejectedButNewDeathResetsRevivalWindow()
    {
        await Prepare(0.8f);
        await Server.WaitPost(() =>
            SEntMan.GetComponent<CMUReaperRevivalComponent>(_caster).MaximumDeathAge = TimeSpan.FromSeconds(0.5));
        await Pair.RunTicksSync(40);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Cast(_caster).Handled, Is.False);
            Assert.That(SEntMan.GetComponent<XenoReaperComponent>(_caster).FleshResin, Is.EqualTo(600));
            var action = Server.System<SharedRMCActionsSystem>().GetActionsWithEvent<CMUReaperReviveActionEvent>(_caster).Single();
            Assert.That(action.Comp.Cooldown, Is.Null);

            var damage = Server.System<DamageableSystem>();
            var thresholds = Server.System<MobThresholdSystem>();
            damage.ClearAllDamage(_corpse);
            thresholds.SetAllowRevives(_corpse, true);
            Assert.That(Server.System<MobStateSystem>().IsAlive(_corpse), Is.True);
            Assert.That(SEntMan.HasComponent<CMUReaperCorpseComponent>(_corpse), Is.False);
            thresholds.SetAllowRevives(_corpse, false);
            damage.TryChangeDamage(_corpse, new DamageSpecifier { DamageDict = { ["Blunt"] = 10000 } }, true);
            Assert.That(Cast(_caster).Handled, Is.True, "a new death must start a new eligibility window");
        });
        await Pair.RunTicksSync(65);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Server.System<MobStateSystem>().IsAlive(_corpse), Is.True,
                "a ritual accepted while fresh must finish even after the admission window expires");
        });
    }

    [Test]
    public async Task RejectedAndCompetingCastsDoNotSpendFlesh()
    {
        await Prepare(2);
        await Server.WaitAssertion(() =>
        {
            var reaper = SEntMan.GetComponent<XenoReaperComponent>(_caster);
            reaper.FleshResin = 499;
            Assert.That(Cast(_caster).Handled, Is.False);
            Assert.That(reaper.FleshResin, Is.EqualTo(499));
            var action = Server.System<SharedRMCActionsSystem>().GetActionsWithEvent<CMUReaperReviveActionEvent>(_caster).Single();
            Assert.That(action.Comp.Cooldown, Is.Null);
            reaper.FleshResin = 600;
            Assert.That(Cast(_caster).Handled, Is.True);
            var competitor = SEntMan.SpawnEntity("CMXenoReaper", SEntMan.GetComponent<TransformComponent>(_caster).Coordinates);
            Server.System<SharedXenoHiveSystem>().SetHive(competitor, _hive);
            var other = SEntMan.GetComponent<XenoReaperComponent>(competitor);
            other.FleshResin = 600;
            Assert.That(Cast(competitor).Handled, Is.False);
            Assert.That(other.FleshResin, Is.EqualTo(600));
        });
    }

    [TearDown]
    public async Task RestoreSession()
    {
        await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(ServerSession!, _originalAttached));
    }
}
