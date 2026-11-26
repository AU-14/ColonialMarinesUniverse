using Content.Shared.CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.CMU14.Medical.Synth;
using Content.Shared.CMU14.Xenos;
using Content.Shared._RMC14.Synth;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests._CMU14.Round.Antags;

/// <summary>
/// N6 (STATE-01, ECS-02): the Early Warning Joe's markers. CMUNoSelfRepairPenalty makes self-repair take the
/// non-self delay, CMUIgnoreResinSlowdown keeps sticky resin from slowing it without granting fast resin's boost.
/// </summary>
[TestFixture]
public sealed class EWJoeSelfRepairAndResinTest
{
    [Test]
    public async Task NoSelfRepairPenaltyUsesTheNonSelfDelay()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var map = await pair.CreateTestMap();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var limbs = entities.System<SharedCMURoboticLimbSystem>();
            var circulation = entities.System<CMUSynthCirculationSystem>();

            var plain = SpawnSynth(entities, map.GridCoords);
            var joe = SpawnSynth(entities, map.GridCoords);
            entities.AddComponent<CMUNoSelfRepairPenaltyComponent>(joe);

            // robotic limb: someone else welding the Joe vs the Joe welding itself
            var robotic = new CMURoboticLimbComponent();
            var byOther = limbs.GetRepairDelay(plain, joe, robotic);
            Assert.That(limbs.GetRepairDelay(joe, joe, robotic), Is.EqualTo(byOther), "marked limb self-repair");
            Assert.That(limbs.GetRepairDelay(plain, plain, robotic), Is.GreaterThan(limbs.GetRepairDelay(joe, plain, robotic)),
                "a normal synth still pays the limb self-repair penalty");

            // synth organs
            Entity<CMUSynthCirculationComponent> joeCirc = (joe, entities.GetComponent<CMUSynthCirculationComponent>(joe));
            Entity<CMUSynthCirculationComponent> plainCirc = (plain, entities.GetComponent<CMUSynthCirculationComponent>(plain));
            Assert.That(circulation.GetOrganRepairDelay(joe, joeCirc), Is.EqualTo(circulation.GetOrganRepairDelay(plain, joeCirc)),
                "marked organ self-repair");
            Assert.That(circulation.GetOrganRepairDelay(plain, plainCirc), Is.EqualTo(plainCirc.Comp.OrganSelfRepairTime),
                "a normal synth still pays the organ self-repair penalty");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// FLOW-11: the synth chassis repair (welder and cable) also honours the marker. A marked synth repairing
    /// itself takes the non-self delay; an unmarked one still pays SelfRepairTime.
    /// </summary>
    [Test]
    public async Task NoSelfRepairPenaltySkipsChassisSelfRepairTime()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var map = await pair.CreateTestMap();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var synths = entities.System<SharedSynthSystem>();

            var plain = SpawnSynth(entities, map.GridCoords);
            var joe = SpawnSynth(entities, map.GridCoords);
            entities.AddComponent<CMUNoSelfRepairPenaltyComponent>(joe);

            Entity<SynthComponent> joeSynth = (joe, entities.GetComponent<SynthComponent>(joe));
            Entity<SynthComponent> plainSynth = (plain, entities.GetComponent<SynthComponent>(plain));

            var joeSelf = synths.GetRepairTime(joeSynth, joe);
            Assert.That(joeSelf, Is.EqualTo(synths.GetRepairTime(joeSynth, plain)),
                "a marked synth repairing its own chassis takes the non-self delay");
            Assert.That(joeSelf, Is.LessThan(joeSynth.Comp.SelfRepairTime),
                "the marked self-repair does not pay SelfRepairTime");
            Assert.That(synths.GetRepairTime(plainSynth, plain), Is.EqualTo(plainSynth.Comp.SelfRepairTime),
                "an unmarked synth still pays the chassis SelfRepairTime");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task IgnoreResinSlowdownSkipsStickyResinButNotFastResinRules()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        EntityUid control = default, plainSticky = default, joeSticky = default, joeFast = default;

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var stickyTile = map.GridCoords;
            var markedStickyTile = EWJoeTestHelpers.TileEast(entities, map, 3);
            var fastTile = EWJoeTestHelpers.TileEast(entities, map, 6);
            var bareTile = EWJoeTestHelpers.TileEast(entities, map, 9);

            entities.SpawnEntity("XenoStickyResin", stickyTile);
            entities.SpawnEntity("XenoStickyResin", markedStickyTile);
            entities.SpawnEntity("XenoFastResin", fastTile);

            control = entities.SpawnEntity("CMMobHuman", bareTile);
            plainSticky = entities.SpawnEntity("CMMobHuman", stickyTile);
            joeSticky = entities.SpawnEntity("CMMobHuman", markedStickyTile);
            joeFast = entities.SpawnEntity("CMMobHuman", fastTile);
            entities.AddComponent<CMUIgnoreResinSlowdownComponent>(joeSticky);
            entities.AddComponent<CMUIgnoreResinSlowdownComponent>(joeFast);
        });

        // let physics build the resin contacts
        await server.WaitRunTicks(10);

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var speed = entities.System<MovementSpeedModifierSystem>();
            float Walk(EntityUid uid)
            {
                speed.RefreshMovementSpeedModifiers(uid);
                return entities.GetComponent<MovementSpeedModifierComponent>(uid).CurrentWalkSpeed;
            }

            var baseline = Walk(control);
            Assert.That(Walk(plainSticky), Is.LessThan(baseline * 0.9f), "sticky resin still slows an unmarked human");
            Assert.That(Walk(joeSticky), Is.EqualTo(baseline).Within(0.01f), "the marker ignores sticky resin");
            Assert.That(Walk(joeFast), Is.EqualTo(baseline).Within(0.01f), "fast resin does not speed up the marked non-xeno");
        });

        await pair.CleanReturnAsync();
    }

    private static EntityUid SpawnSynth(IEntityManager entities, Robust.Shared.Map.EntityCoordinates coords)
    {
        var synth = entities.SpawnEntity("CMMobHuman", coords);
        entities.EnsureComponent<SynthComponent>(synth);
        return synth;
    }
}
