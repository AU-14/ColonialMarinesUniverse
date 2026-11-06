using System.Linq;
using System.Numerics;
using Content.Client.CMU14.Round.Antags.EarlyWarning;
using Content.Server.CMU14.Round.Antags.EarlyWarning;
using Content.Server.CMU14.Round.Antags.EarlyWarning.Abilities;
using Content.Shared._RMC14.Projectiles.Reflect;
using Content.Shared._RMC14.Smoke;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.CMU14.Round.Antags.EarlyWarning.Abilities;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Projectiles;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.IntegrationTests._CMU14.Round.Antags;

/// <summary>
/// Playtest 4: permanent Scour from one-of-a-kind gear, Magnetic Resonance as a real reflection of IFF
/// bullets, the respawn (death turned back, EMP, two levels lost, moved away from people), lowering a
/// level, THE LIGHT's cables reaching the sprite, and the Overclock numbers.
/// </summary>
[TestFixture]
public sealed class EWJoePlaytest4Test
{
    private const string ColonyJoe = "AU14MobWorkingJoeColony";
    private const string Human = "CMMobHuman";
    private const string ScoutCloak = "RMCBackpackScout";
    private const string PyroArmor = "CMArmorM35";
    private const string Ghillie = "CMArmorM45";
    private const string PlainArmor = "CMArmorM3Medium";
    private const string Baton = "CMStunbaton";
    private const string Bullet = "CMBulletSentry10x50mm";
    private const string Cell = "PowerCellMedium";

    [Test]
    public async Task ScourOfOneOfAKindGearIsPermanent()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;
        var timing = server.ResolveDependency<IGameTiming>();
        EntityUid joe = default;

        await server.WaitAssertion(() =>
        {
            var scour = entities.System<EWJoeScourSystem>();
            joe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            var comp = entities.EnsureComponent<EWJoeComponent>(joe);

            var cloak = entities.SpawnEntity(ScoutCloak, map.GridCoords);
            var pyro = entities.SpawnEntity(PyroArmor, map.GridCoords);
            var ghillie = entities.SpawnEntity(Ghillie, map.GridCoords);
            var plain = entities.SpawnEntity(PlainArmor, map.GridCoords);
            var baton = entities.SpawnEntity(Baton, map.GridCoords);

            // the Joe's optics already see in the dark: goggles are not food for Scour
            var goggles = entities.SpawnEntity("CMGlassesM42ScoutSight", map.GridCoords);
            Assert.That(scour.Classify(goggles), Is.Null, "night-vision goggles give a Joe nothing");

            Assert.Multiple(() =>
            {
                Assert.That(scour.IsOneOfAKind(cloak), "the scout cloak is one of a kind");
                Assert.That(scour.IsOneOfAKind(pyro), "so is the pyro armor");
                Assert.That(scour.IsOneOfAKind(ghillie), "and the sniper's ghillie");
                Assert.That(scour.IsOneOfAKind(plain), Is.False, "plain armor is not");
                Assert.That(scour.IsOneOfAKind(baton), Is.False, "nor a baton");
            });

            // a timed armor buff first, then the ghillie makes the armor kind permanent
            scour.Scour((joe, comp), plain, scour.Classify(plain)!.Value);
            var buffs = entities.GetComponent<EWJoeScourComponent>(joe);
            Assert.That(buffs.Expiries[EWJoeScourKind.Armor], Is.LessThan(TimeSpan.MaxValue), "plain armor is timed");

            scour.Scour((joe, comp), ghillie, scour.Classify(ghillie)!.Value);
            scour.Scour((joe, comp), cloak, scour.Classify(cloak)!.Value);
            scour.Scour((joe, comp), pyro, scour.Classify(pyro)!.Value);
            scour.Scour((joe, comp), baton, scour.Classify(baton)!.Value);

            // a later timed scour of a permanent kind does not shorten it
            var plain2 = entities.SpawnEntity(PlainArmor, map.GridCoords);
            scour.Scour((joe, comp), plain2, EWJoeScourKind.Armor);

            Assert.Multiple(() =>
            {
                Assert.That(buffs.Permanent, Is.EquivalentTo(new[] { EWJoeScourKind.Armor, EWJoeScourKind.Cloak, EWJoeScourKind.Pyro }));
                Assert.That(buffs.Expiries[EWJoeScourKind.Armor], Is.EqualTo(TimeSpan.MaxValue), "the ghillie's armor stays");
                Assert.That(buffs.Expiries[EWJoeScourKind.Cloak], Is.EqualTo(TimeSpan.MaxValue));
                Assert.That(buffs.Expiries[EWJoeScourKind.Pyro], Is.EqualTo(TimeSpan.MaxValue));
                Assert.That(buffs.Expiries[EWJoeScourKind.Baton], Is.LessThan(TimeSpan.MaxValue), "the baton is timed");
            });

            // the baton's time runs out
            buffs.Expiries[EWJoeScourKind.Baton] = timing.CurTime;
        });

        await pair.RunTicksSync((int) Math.Ceiling(2.5 / timing.TickPeriod.TotalSeconds));

        await server.WaitAssertion(() =>
        {
            var buffs = entities.GetComponent<EWJoeScourComponent>(joe);
            Assert.Multiple(() =>
            {
                Assert.That(buffs.Expiries.ContainsKey(EWJoeScourKind.Baton), Is.False, "the timed buff expired");
                Assert.That(buffs.Expiries.Keys, Is.EquivalentTo(new[] { EWJoeScourKind.Armor, EWJoeScourKind.Cloak, EWJoeScourKind.Pyro }),
                    "the permanent ones did not");
                Assert.That(buffs.BulletArmor, Is.GreaterThan(0));
            });

            // stand-down still takes everything
            entities.System<EWJoeScourSystem>().Expire(joe, "test");
            Assert.That(entities.HasComponent<EWJoeScourComponent>(joe), Is.False);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ResonanceTurnsBackAnIffBulletInsteadOfZeroingIt()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var projectiles = entities.System<SharedProjectileSystem>();
            var physics = entities.System<SharedPhysicsSystem>();
            var iffSys = entities.System<GunIFFSystem>();
            var damageable = entities.System<DamageableSystem>();

            (EntityUid Bullet, ProjectileComponent Projectile) Fire(EntityUid target)
            {
                var bullet = entities.SpawnEntity(Bullet, map.GridCoords);
                iffSys.GiveAmmoIFF(bullet, "FactionMarine", true);
                physics.SetLinearVelocity(bullet, new Vector2(10, 0));
                var projectile = entities.GetComponent<ProjectileComponent>(bullet);
                projectiles.ProjectileCollide((bullet, projectile, entities.GetComponent<PhysicsComponent>(bullet)), target);
                return (bullet, projectile);
            }

            // the Joe: every bullet is turned back, IFF or not
            var joe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            entities.EnsureComponent<EWJoeComponent>(joe);
            entities.AddComponent(joe, new EWJoeResonanceComponent { Chance = 1f });

            var (bullet, projectile) = Fire(joe);
            Assert.Multiple(() =>
            {
                Assert.That(damageable.GetTotalDamage(joe), Is.EqualTo(Content.Shared.FixedPoint.FixedPoint2.Zero), "the Joe takes nothing");
                Assert.That(entities.GetComponent<PhysicsComponent>(bullet).LinearVelocity.X, Is.LessThan(0f), "the bullet flies back");
                Assert.That(entities.HasComponent<RMCReflectedProjectileComponent>(bullet), "marked as reflected");
                Assert.That(entities.GetComponent<ProjectileIFFComponent>(bullet).Enabled, Is.False, "its IFF is cleared, so it can strike its own side");
                Assert.That(projectile.Damage.GetTotal(), Is.GreaterThan(Content.Shared.FixedPoint.FixedPoint2.Zero), "and it still hurts");
            });

            // control: RMC's own reflective only zeroes an IFF bullet; this is what playtest 4 saw
            var rmc = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            entities.AddComponent(rmc, new RMCReflectiveComponent { Chance = 1f });
            var (_, zeroed) = Fire(rmc);
            Assert.That(zeroed.Damage.GetTotal(), Is.EqualTo(Content.Shared.FixedPoint.FixedPoint2.Zero),
                "RMC's reflective zeroes IFF bullets (why the Joe does not use it)");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RespawnTurnsBackDeathPulsesDemotesAndMovesAway()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;
        var timing = server.ResolveDependency<IGameTiming>();

        EntityUid joe = default, watcher = default, floorCell = default, heldCell = default;

        await server.WaitAssertion(() =>
        {
            var maps = entities.System<SharedMapSystem>();
            var rule = entities.System<EWJoeRuleSystem>();
            var respawn = entities.System<EWJoeRespawnSystem>();
            var hands = entities.System<SharedHandsSystem>();

            // a 31x31 floor with the only player in one corner
            var origin = maps.CoordinatesToTile(map.Grid.Owner, map.Grid.Comp, map.GridCoords);
            for (var x = 0; x <= 30; x++)
            {
                for (var y = 0; y <= 30; y++)
                {
                    maps.SetTile(map.Grid, origin + new Vector2i(x, y), map.Tile.Tile);
                }
            }

            watcher = entities.SpawnEntity(Human, map.GridCoords);
            server.PlayerMan.SetAttachedEntity(pair.Player!, watcher);

            var joeTile = maps.GridTileToLocal(map.Grid.Owner, map.Grid.Comp, origin + new Vector2i(1, 0));
            joe = entities.SpawnEntity(ColonyJoe, joeTile);
            var comp = entities.EnsureComponent<EWJoeComponent>(joe);
            rule.ApplyLevel((joe, comp), 4);
            Assert.That(respawn.Grant((joe, comp)), "respawn granted");

            floorCell = entities.SpawnEntity(Cell, joeTile);
            heldCell = entities.SpawnEntity(Cell, joeTile);
            Assert.That(hands.TryPickupAnyHand(joe, heldCell), "the Joe holds a cell");

            // a lethal blow
            var protos = server.ResolveDependency<IPrototypeManager>();
            entities.System<DamageableSystem>().TryChangeDamage(joe,
                new DamageSpecifier(protos.Index<DamageTypePrototype>("Blunt"), 1000), ignoreResistances: true);

            Assert.Multiple(() =>
            {
                Assert.That(entities.System<MobStateSystem>().IsDead(joe), Is.False, "the death is turned back");
                Assert.That(comp.RespawnReady, Is.False, "the respawn is spent");
            });
        });

        await pair.RunTicksSync(3);

        await server.WaitAssertion(() =>
        {
            var comp = entities.GetComponent<EWJoeComponent>(joe);
            var xforms = entities.System<SharedTransformSystem>();
            var batteries = entities.System<SharedBatterySystem>();
            var damageable = entities.System<DamageableSystem>();
            var distance = Vector2.Distance(xforms.GetWorldPosition(joe), xforms.GetWorldPosition(watcher));

            Assert.Multiple(() =>
            {
                Assert.That(entities.System<MobStateSystem>().IsAlive(joe), "alive");
                Assert.That(damageable.GetTotalDamage(joe), Is.EqualTo(Content.Shared.FixedPoint.FixedPoint2.Zero), "mended");
                Assert.That(comp.Level, Is.EqualTo(2), "two levels lost");
                Assert.That(comp.Respawns, Is.EqualTo(1));
                Assert.That(entities.HasComponent<EWJoeHeatComponent>(joe), Is.False, "level 3's heat went with it");
                Assert.That(entities.HasComponent<EWJoeRespawningComponent>(joe), Is.False);
                Assert.That(distance, Is.GreaterThanOrEqualTo(comp.RespawnClearance), "put down away from the only player");
                Assert.That(batteries.GetCharge(floorCell), Is.LessThan(entities.GetComponent<Content.Shared.Power.Components.BatteryComponent>(floorCell).MaxCharge),
                    "the pulse drained a cell where it fell");
                Assert.That(batteries.GetCharge(heldCell), Is.EqualTo(entities.GetComponent<Content.Shared.Power.Components.BatteryComponent>(heldCell).MaxCharge),
                    "the Joe's own cell is spared");
            });

            // no copy left: the next lethal blow sticks
            var protos = server.ResolveDependency<IPrototypeManager>();
            damageable.TryChangeDamage(joe, new DamageSpecifier(protos.Index<DamageTypePrototype>("Blunt"), 1000), ignoreResistances: true);
            Assert.That(entities.System<MobStateSystem>().IsDead(joe), "the second death is final");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ADeathThatSkipsTheThresholdIsNotUndone()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;
        EntityUid joe = default;
        var before = Vector2.Zero;

        await server.WaitAssertion(() =>
        {
            joe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            var comp = entities.EnsureComponent<EWJoeComponent>(joe);
            entities.System<EWJoeRuleSystem>().ApplyLevel((joe, comp), 4);
            entities.System<EWJoeRespawnSystem>().Grant((joe, comp));
            before = entities.System<SharedTransformSystem>().GetWorldPosition(joe);

            // what an execution or a suicide does: a lethal blow, then Dead set directly
            var protos = server.ResolveDependency<IPrototypeManager>();
            entities.System<DamageableSystem>().TryChangeDamage(joe,
                new DamageSpecifier(protos.Index<DamageTypePrototype>("Blunt"), 1000), ignoreResistances: true);
            entities.System<MobStateSystem>().ChangeMobState(joe, MobState.Dead);
        });

        await pair.RunTicksSync(3);

        await server.WaitAssertion(() =>
        {
            var comp = entities.GetComponent<EWJoeComponent>(joe);
            Assert.Multiple(() =>
            {
                Assert.That(entities.System<MobStateSystem>().IsDead(joe), "the death stands");
                Assert.That(comp.Respawns, Is.Zero, "no respawn ran");
                Assert.That(comp.Level, Is.EqualTo(4), "no levels were taken");
                Assert.That(entities.System<SharedTransformSystem>().GetWorldPosition(joe), Is.EqualTo(before), "not moved");
                Assert.That(entities.HasComponent<EWJoeRespawningComponent>(joe), Is.False);
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LoweringALevelTakesItsGrantsAndReturnsWhatTheyReplaced()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var rule = entities.System<EWJoeRuleSystem>();
            var protos = server.ResolveDependency<IPrototypeManager>();
            var joe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            var comp = entities.EnsureComponent<EWJoeComponent>(joe);

            // the downgrade removes actions by prototype, so no prototype may be granted by two levels
            var table = protos.Index<EWJoeLevelsPrototype>(comp.LevelTable);
            var ids = table.Levels.SelectMany(l => l.Actions).Select(a => a.Id).ToList();
            Assert.That(ids, Is.Unique, "each action is granted by one level only");

            bool Holds(string id) => comp.GrantedActions.Any(a => entities.EntityExists(a)
                && entities.GetComponent<MetaDataComponent>(a).EntityPrototype?.ID == id);

            rule.ApplyLevel((joe, comp), 5);
            var arsenal = entities.System<EWJoeArsenalSystem>();
            Assert.That(arsenal.TrySummonMachete((joe, comp), out var machete), "the level-3 machete is drawn");
            Assert.That(rule.LowerLevel((joe, comp), 3), "5 to 3");
            Assert.That(entities.IsQueuedForDeletion(machete) || entities.Deleted(machete), Is.False, "level 3 is kept, so is the machete");
            Assert.Multiple(() =>
            {
                Assert.That(comp.Level, Is.EqualTo(3));
                Assert.That(Holds("CMUActionEWJoeSaturate"), Is.False, "Saturate gone");
                Assert.That(Holds("CMUActionEWJoeZLeap"), Is.False, "Z-Leap gone");
                Assert.That(entities.HasComponent<EWJoeLightComponent>(joe), Is.False, "THE LIGHT gone");
                Assert.That(entities.HasComponent<EWJoeResonanceComponent>(joe), Is.False, "Resonance gone");
                Assert.That(entities.HasComponent<EWJoeHeatComponent>(joe), "level 3 kept");
                Assert.That(Holds("CMUActionEWJoeOverclock"), "level 3 actions kept");
            });

            Assert.That(rule.LowerLevel((joe, comp), 1), "3 to 1");
            Assert.Multiple(() =>
            {
                Assert.That(entities.IsQueuedForDeletion(machete) || entities.Deleted(machete), "the undroppable machete goes with its level");
                Assert.That(entities.HasComponent<Content.Shared.CMU14.Round.Antags.ColonyBounty.ColonyBountyComponent>(joe), "the wanted record stays");
                Assert.That(Holds("CMUActionEWJoeJudgement"), Is.False, "Judgement gone");
                Assert.That(Holds("CMUActionEWJoeTestOfFaith"), "Test of Faith comes back below Judgement's level");
                Assert.That(entities.HasComponent<EWJoeHeatComponent>(joe), Is.False);
                Assert.That(Holds("CMUActionEWJoeWheelEngine"), Is.False, "the Engine wheel goes with its abilities");
            });

            Assert.That(rule.LowerLevel((joe, comp), 1), Is.False, "not below the current level");

            // climbing back grants it all again, and Judgement takes Test of Faith again
            rule.ApplyLevel((joe, comp), 5);
            Assert.Multiple(() =>
            {
                Assert.That(comp.Level, Is.EqualTo(5));
                Assert.That(Holds("CMUActionEWJoeSaturate"));
                Assert.That(Holds("CMUActionEWJoeJudgement"));
                Assert.That(Holds("CMUActionEWJoeTestOfFaith"), Is.False);
                Assert.That(entities.HasComponent<EWJoeLightComponent>(joe));
                Assert.That(entities.HasComponent<EWJoeHeatComponent>(joe));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public void CablesEndInsideTheSpriteAtFullEnvelope()
    {
        const float radius = 900f;
        const float contact = 12f;

        Assert.Multiple(() =>
        {
            Assert.That(EWJoeCablesOverlay.EndDistance(1f, radius, contact), Is.EqualTo(contact).Within(0.01f),
                "at full the tips sit inside the sprite's edge");
            Assert.That(EWJoeCablesOverlay.EndDistance(0f, radius, contact), Is.GreaterThan(radius * 0.8f), "at nothing they start near the edge");
            Assert.That(EWJoeCablesOverlay.EndDistance(0.5f, radius, contact), Is.LessThan(EWJoeCablesOverlay.EndDistance(0.25f, radius, contact)),
                "they close in as the envelope fills");
        });
    }

    [Test]
    public async Task OverclockRunsCoolerAndItsCloudIsSmaller()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var protos = server.ResolveDependency<IPrototypeManager>();
            var factory = server.ResolveDependency<IComponentFactory>();
            Assert.That(new EWJoeHeatComponent().OverclockHeat, Is.EqualTo(8f), "heat per second while overclocked");
            Assert.That(protos.Index<EntityPrototype>("CMUEWJoeSmoke").TryGetComponent(out EvenSmokeComponent? smoke, factory)
                && smoke.Range == 3, "a 7-tile cloud");
        });

        await pair.CleanReturnAsync();
    }
}
