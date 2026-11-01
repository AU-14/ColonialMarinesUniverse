using Content.Server.CMU14.Round.Antags.EarlyWarning;
using Content.Shared._RMC14.Armor;
using Content.Shared._RMC14.Slow;
using Content.Shared._RMC14.Stamina;
using Content.Shared._RMC14.Weapons.Melee;
using Content.Shared._RMC14.Xenonids.Zoom;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.Inventory;
using Content.Shared.Medical;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Round.Antags;

/// <summary>
/// Early Warning Joe phase E0: the level table grants the level-0 kit, levels shed slowdown and add
/// armor, removing the antag component takes every grant back, and a reset key cannot revive the body.
/// </summary>
[TestFixture]
public sealed class EWJoeLevelZeroTest
{
    private const string ColonyJoe = "AU14MobWorkingJoeColony";
    private const string ResetKey = "RMCSynthResetKeySeegson";

    [Test]
    public async Task LevelZeroGrantsKitAndLevelsShedSlowdown()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var entities = pair.Server.EntMan;

        var joe = EntityUid.Invalid;
        var grantedActions = new List<EntityUid>();

        await pair.Server.WaitAssertion(() =>
        {
            joe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            var system = entities.System<EWJoeRuleSystem>();
            var ewJoe = entities.EnsureComponent<EWJoeComponent>(joe);
            var slowdown = entities.GetComponent<RMCInnateSlowdownComponent>(joe);
            var baseSlowdown = slowdown.Slowdown;

            system.ApplyLevel((joe, ewJoe), 0);
            grantedActions.AddRange(ewJoe.GrantedActions);

            var armorAtZero = new CMGetArmorEvent(SlotFlags.All);
            entities.EventBus.RaiseLocalEvent(joe, ref armorAtZero);

            Assert.Multiple(() =>
            {
                Assert.That(ewJoe.Level, Is.EqualTo(0));
                Assert.That(ewJoe.GrantedActions, Has.Count.EqualTo(3),
                    "Level 0 grants Test of Faith, Optical Zoom and Disrupt.");
                Assert.That(entities.HasComponent<XenoZoomComponent>(joe), Is.True);
                Assert.That(entities.TryGetComponent(joe, out StunOnHitComponent? stun), Is.True);
                Assert.That(stun!.Duration, Is.LessThanOrEqualTo(TimeSpan.FromSeconds(1.5)),
                    "The RMC StunOnHit default of 12 s must be overridden (balance A-3).");
                Assert.That(entities.GetComponent<RMCStaminaDamageOnHitComponent>(joe).Damage, Is.EqualTo(20));
                Assert.That(slowdown.Slowdown, Is.EqualTo(baseSlowdown),
                    "Level 0 sheds no slowdown.");
            });

            // levels never go down: re-applying a lower level must change nothing
            system.ApplyLevel((joe, ewJoe), 0);
            Assert.That(ewJoe.GrantedActions, Has.Count.EqualTo(3),
                "Re-applying an already applied level must not duplicate grants.");

            Assert.That(system.TryAdvanceLevel((joe, ewJoe)), Is.True);

            var armorAtOne = new CMGetArmorEvent(SlotFlags.All);
            entities.EventBus.RaiseLocalEvent(joe, ref armorAtOne);

            Assert.Multiple(() =>
            {
                Assert.That(ewJoe.Level, Is.EqualTo(1));
                Assert.That(slowdown.Slowdown,
                    Is.EqualTo(baseSlowdown - ewJoe.SlowdownShedPerLevel).Within(0.001f),
                    "Each level sheds innate slowdown (balance A-4).");
                Assert.That(armorAtOne.Melee - armorAtZero.Melee, Is.EqualTo(ewJoe.ArmorPerLevel),
                    "Sculpted Form adds melee armor per level on top of whatever the Joe wears.");
                Assert.That(armorAtOne.Bullet - armorAtZero.Bullet, Is.EqualTo(ewJoe.ArmorPerLevel),
                    "Sculpted Form adds bullet armor per level on top of whatever the Joe wears.");
            });

            entities.RemoveComponent<EWJoeComponent>(joe);
            Assert.Multiple(() =>
            {
                Assert.That(entities.HasComponent<StunOnHitComponent>(joe), Is.False,
                    "Removing the antag takes its granted components back.");
                Assert.That(entities.HasComponent<RMCStaminaDamageOnHitComponent>(joe), Is.False);
                Assert.That(entities.HasComponent<XenoZoomComponent>(joe), Is.False);
                Assert.That(slowdown.Slowdown, Is.EqualTo(baseSlowdown).Within(0.001f),
                    "Removing the antag restores the mob's own slowdown.");
            });
        });

        // granted action entities are queued for deletion on cleanup
        await pair.RunTicksSync(2);

        await pair.Server.WaitAssertion(() =>
        {
            try
            {
                Assert.That(grantedActions, Is.Not.Empty);
                foreach (var action in grantedActions)
                {
                    Assert.That(entities.Deleted(action), Is.True,
                        "Removing the antag deletes its granted action entities, not just detaches them.");
                }
            }
            finally
            {
                entities.DeleteEntity(joe);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ResetKeyCannotReviveAnEarlyWarningJoe()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var entities = pair.Server.EntMan;

        await pair.Server.WaitAssertion(() =>
        {
            var plainJoe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            var ewJoe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            var medic = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            var key = entities.SpawnEntity(ResetKey, map.GridCoords);
            try
            {
                entities.EnsureComponent<EWJoeComponent>(ewJoe);

                var plainZap = new TargetBeforeDefibrillatorZapsEvent(medic, key, plainJoe);
                entities.EventBus.RaiseLocalEvent(plainJoe, plainZap);
                Assert.That(plainZap.Cancelled, Is.False,
                    "An ordinary Working Joe can still be rebooted with a reset key.");

                var ewZap = new TargetBeforeDefibrillatorZapsEvent(medic, key, ewJoe);
                entities.EventBus.RaiseLocalEvent(ewJoe, ewZap);
                Assert.That(ewZap.Cancelled, Is.True,
                    "An Early Warning Joe refuses the reset key (D3).");
            }
            finally
            {
                entities.DeleteEntity(plainJoe);
                entities.DeleteEntity(ewJoe);
                entities.DeleteEntity(medic);
                entities.DeleteEntity(key);
            }
        });

        await pair.CleanReturnAsync();
    }
}
