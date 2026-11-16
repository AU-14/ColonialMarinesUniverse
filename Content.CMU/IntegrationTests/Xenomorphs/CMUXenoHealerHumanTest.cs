#pragma warning disable RA0002
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Actions;
using Content.Shared._RMC14.Xenonids.Heal;
using Content.Shared._RMC14.Xenonids.Plasma;
using Content.Shared.Actions;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Injuries.Wounds;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Xenomorphs;

[TestFixture]
public sealed class CMUXenoHealerHumanTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task HealerDroneHealsCappedHumanDamageAndStopsBleeding()
    {
        var map = await Pair.CreateTestMap();
        EntityUid healer = default, human = default;

        await Server.WaitAssertion(() =>
        {
            healer = SEntMan.SpawnEntity("CMXenoDroneHealer", map.GridCoords);
            human = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new System.Numerics.Vector2(1, 0)));
        });
        await Pair.RunTicksSync(3);

        await Server.WaitAssertion(() =>
        {
            var damageable = Server.System<DamageableSystem>();
            damageable.TryChangeDamage(human, new DamageSpecifier
            {
                DamageDict =
                {
                    ["Blunt"] = 80,
                    ["Heat"] = 55,
                },
            }, ignoreResistances: true);

            var medicalIndex = Server.System<CMUMedicalBodyIndexSystem>();
            foreach (var part in medicalIndex.GetBodyParts(human))
                SEntMan.EnsureComponent<BodyPartWoundComponent>(part.Owner).ExternalBleeding = ExternalBleedTier.Arterial;

            var action = Server.System<SharedRMCActionsSystem>()
                .GetActionsWithEvent<XenoApplySalveActionEvent>(healer).Single();
            var ev = new XenoApplySalveActionEvent
            {
                Target = human,
                AllowHumanTargets = true,
                HumanBruteHeal = 75,
                HumanBurnHeal = 50,
                HumanToxinDamage = 5,
            };
            Server.System<SharedActionsSystem>().PerformAction(healer, action, ev);

            Assert.That(ev.Handled, Is.True);
            var healed = damageable.GetAllDamage(human).DamageDict;
            Assert.That(healed["Blunt"], Is.EqualTo(5));
            Assert.That(healed["Heat"], Is.EqualTo(5));
            Assert.That(healed["Poison"], Is.EqualTo(5));

            var plasma = SEntMan.GetComponent<XenoPlasmaComponent>(healer).Plasma;
            Assert.That(plasma, Is.EqualTo(800));
            Assert.That(damageable.GetAllDamage(healer).DamageDict["Blunt"], Is.EqualTo(75));

            foreach (var part in medicalIndex.GetBodyParts(human))
                Assert.That(SEntMan.GetComponent<BodyPartWoundComponent>(part.Owner).ExternalBleeding,
                    Is.EqualTo(ExternalBleedTier.None));
        });
    }
}
