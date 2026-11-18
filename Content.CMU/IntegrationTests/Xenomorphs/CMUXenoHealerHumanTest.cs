using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Actions;
using Content.Shared._RMC14.Xenonids.Heal;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Injuries.Wounds;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Xenomorphs;

[TestFixture]
public sealed class CMUXenoHealerHumanTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false };

    [TestCase(100, 100, 25, 50)]
    [TestCase(0, 0, 0, 0)] // Surface bleeding alone is treatable.
    public async Task SalveHealsHumanWithinDamageCapsAndStopsBleeding(
        double initialBrute,
        double initialBurn,
        double expectedBrute,
        double expectedBurn)
    {
        var map = await Pair.CreateTestMap();
        EntityUid healer = default;
        EntityUid human = default;

        await Server.WaitAssertion(() =>
        {
            healer = SEntMan.SpawnEntity("CMXenoDroneHealer", map.GridCoords);
            human = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new System.Numerics.Vector2(1, 0)));

            var hive = SEntMan.SpawnEntity("CMXenoHive", map.GridCoords);
            var hives = Server.System<SharedXenoHiveSystem>();
            hives.SetHive(healer, hive);

            var damage = new DamageSpecifier
            {
                DamageDict =
                {
                    ["Blunt"] = FixedPoint2.New(initialBrute),
                    ["Heat"] = FixedPoint2.New(initialBurn),
                },
            };
            Server.System<DamageableSystem>().TryChangeDamage(human, damage, ignoreResistances: true);

            var actions = Server.System<SharedActionsSystem>();
            var salve = actions.GetActions(healer).Select(a => actions.GetEvent(a)).OfType<XenoApplySalveActionEvent>().Single();
            bool Apply()
            {
                salve.Target = human;
                salve.Handled = false;
                SEntMan.EventBus.RaiseLocalEvent(healer, salve);
                return salve.Handled;
            }

            Assert.That(Apply(), Is.False);
            hives.SetHiveIndividualAlly(human, hive, true);

            var parts = Server.System<CMUMedicalBodyIndexSystem>().GetBodyParts(human).ToArray();
#pragma warning disable RA0002
            foreach (var (part, _) in parts)
                SEntMan.EnsureComponent<BodyPartWoundComponent>(part).ExternalBleeding = ExternalBleedTier.Severe;
#pragma warning restore RA0002

            Assert.That(Apply(), Is.True);

            var healed = Server.System<DamageableSystem>().GetAllDamage(human).DamageDict;
            Assert.That(healed.GetValueOrDefault("Blunt"), Is.EqualTo(FixedPoint2.New(expectedBrute)));
            Assert.That(healed.GetValueOrDefault("Heat"), Is.EqualTo(FixedPoint2.New(expectedBurn)));
            Assert.That(healed.GetValueOrDefault("Poison"), Is.EqualTo(FixedPoint2.New(5)));
            Assert.That(
                Server.System<DamageableSystem>().GetAllDamage(healer).DamageDict.GetValueOrDefault("Blunt"),
                Is.EqualTo(FixedPoint2.New(75)));

            if (expectedBrute == 0 && expectedBurn == 0)
            {
                Assert.That(Apply(), Is.False, "Poison alone is not treatable with salve.");
                Assert.That(Server.System<DamageableSystem>().GetAllDamage(healer).DamageDict["Blunt"],
                    Is.EqualTo(FixedPoint2.New(75)));
            }

            foreach (var (part, _) in parts)
                Assert.That(SEntMan.GetComponent<BodyPartWoundComponent>(part).ExternalBleeding, Is.EqualTo(ExternalBleedTier.None));
        });
    }
}
