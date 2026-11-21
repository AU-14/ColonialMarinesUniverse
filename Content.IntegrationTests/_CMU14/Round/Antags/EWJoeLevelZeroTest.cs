using Content.Server.CMU14.Round.Antags.EarlyWarning;
using Content.Shared._RMC14.Armor;
using Content.Shared._RMC14.Slow;
using Content.Shared._RMC14.Stamina;
using Content.Shared._RMC14.Weapons.Melee;
using Content.Shared._RMC14.Xenonids.Zoom;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.CMU14.Round.Antags.EarlyWarning.Abilities;
using Content.Shared.Inventory;
using Content.Shared.Medical;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests._CMU14.Round.Antags;

/// <summary>
/// Early Warning Joe phase E0: the level table grants the level-0 kit, levels shed slowdown and add
/// armor, removing the antag component takes every grant back, and (revised D3) only a defibrillator zap revives the body, see EWJoeReviveRadioTest.
/// </summary>
[TestFixture]
public sealed class EWJoeLevelZeroTest
{
    private const string ColonyJoe = "AU14MobWorkingJoeColony";

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
                Assert.That(ewJoe.GrantedActions, Has.Count.EqualTo(7),
                    "Level 0 grants Test of Faith, Optical Zoom, Disrupt, Offer, Signal Status and the Gaze and Machine wheels.");
                Assert.That(entities.HasComponent<EWJoeOpticalZoomComponent>(joe), Is.True,
                    "Optical Zoom is the bio-mask style zoom (playtest 3).");
                Assert.That(entities.HasComponent<XenoZoomComponent>(joe), Is.False);
                Assert.That(entities.HasComponent<StunOnHitComponent>(joe), Is.False,
                    "Unarmed hits no longer stun (playtest 3).");
                Assert.That(entities.GetComponent<RMCStaminaDamageOnHitComponent>(joe).Damage, Is.EqualTo(20));
                Assert.That(slowdown.Slowdown, Is.EqualTo(baseSlowdown),
                    "Level 0 sheds no slowdown.");
            });

            // levels never go down: re-applying a lower level must change nothing
            system.ApplyLevel((joe, ewJoe), 0);
            Assert.That(ewJoe.GrantedActions, Has.Count.EqualTo(7),
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
                Assert.That(armorAtOne.Melee - armorAtZero.Melee, Is.EqualTo(ewJoe.MeleeArmorPerLevel),
                    "Sculpted Form adds melee armor per level on top of whatever the Joe wears.");
                Assert.That(armorAtOne.Bullet - armorAtZero.Bullet, Is.EqualTo(ewJoe.BulletArmorPerLevel),
                    "Sculpted Form adds bullet armor per level on top of whatever the Joe wears.");
                // rebalance RB-01/RB-02: bullet and melee armor scale separately (L5: 20 bullet, 10 melee)
                Assert.That(ewJoe.BulletArmorPerLevel, Is.EqualTo(4), "RB-01: +4 bullet armor per level");
                Assert.That(ewJoe.MeleeArmorPerLevel, Is.EqualTo(2), "RB-02: +2 melee armor per level");
            });

            entities.RemoveComponent<EWJoeComponent>(joe);
            Assert.Multiple(() =>
            {
                Assert.That(entities.HasComponent<StunOnHitComponent>(joe), Is.False,
                    "Removing the antag takes its granted components back.");
                Assert.That(entities.HasComponent<RMCStaminaDamageOnHitComponent>(joe), Is.False);
                Assert.That(entities.HasComponent<EWJoeOpticalZoomComponent>(joe), Is.False);
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

}
