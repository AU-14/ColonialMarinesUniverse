#pragma warning disable RA0002 // Exercise emitter strength and scan deadlines through the real system update.
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Xenonids.Pheromones;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.CMU14.Xenomorphs;

[TestFixture]
public sealed class CMUFrenzyPheromonesTest : GameTest
{
    [Test]
    public async Task OverlappingFrenzyRefreshesOnlyWhenFinalStrengthChanges()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var pheromones = Server.System<SharedXenoPheromonesSystem>();
            var now = Server.Resolve<IGameTiming>().CurTime;
            var xeno = SEntMan.SpawnEntity("CMXenoDrone", map.GridCoords);
            var probe = SEntMan.AddComponent<CMUFrenzyRefreshProbeComponent>(xeno);
            var speed = SEntMan.GetComponent<MovementSpeedModifierComponent>(xeno);
            var originalSpeed = speed.WalkSpeedModifier;

            EntityUid Emitter(int strength)
            {
                var uid = SEntMan.SpawnEntity(null, map.GridCoords);
                var settings = SEntMan.AddComponent<XenoPheromonesComponent>(uid);
                settings.PheromonesMultiplier = strength;
                settings.NextPheromonesPlasmaUse = now + TimeSpan.FromMinutes(1);
                var obj = SEntMan.AddComponent<XenoPheromonesObjectComponent>(uid);
                obj.Pheromones = XenoPheromones.Frenzy;
                pheromones.TryActivatePheromonesObject((uid, obj));
                return uid;
            }

            var weak = Emitter(1);
            pheromones.Update(0);
            var weakSpeed = speed.WalkSpeedModifier;
            Assert.That(weakSpeed, Is.GreaterThan(originalSpeed));

            var strong = Emitter(3);
            pheromones.Update(0);
            var strongSpeed = speed.WalkSpeedModifier;
            var applied = SEntMan.GetComponent<XenoFrenzyPheromonesComponent>(xeno);
            Assert.That(applied.Multiplier.Int(), Is.EqualTo(3), "overlapping auras use the strongest emitter, not their sum");
            Assert.That(strongSpeed, Is.GreaterThan(weakSpeed));

            probe.Refreshes = 0;
            for (var i = 0; i < 60; i++)
                pheromones.Update(0);
            Assert.That(probe.Refreshes, Is.Zero, "unchanged overlapping emitters must not rebuild movement modifiers");

            SEntMan.GetComponent<XenoPheromonesComponent>(strong).PheromonesMultiplier = 2;
            pheromones.Update(0);
            Assert.That(probe.Refreshes, Is.EqualTo(1));
            Assert.That(applied.Multiplier.Int(), Is.EqualTo(2));
            Assert.That(speed.WalkSpeedModifier, Is.GreaterThan(weakSpeed).And.LessThan(strongSpeed));

            pheromones.DeactivatePheromones(strong);
            pheromones.Update(0);
            Assert.That(probe.Refreshes, Is.EqualTo(2));
            Assert.That(speed.WalkSpeedModifier, Is.EqualTo(weakSpeed),
                "losing the stronger emitter must reveal the weaker emitter");

            Server.System<SharedTransformSystem>().SetCoordinates(weak, map.GridCoords.Offset(new Vector2(30, 0)));
            SEntMan.GetComponent<XenoPheromonesComponent>(weak).NextPheromonesPlasmaUse = now;
            pheromones.Update(0);
            Assert.That(SEntMan.HasComponent<XenoFrenzyPheromonesComponent>(xeno), Is.False,
                "a due spatial scan must remove receivers that have left the aura");
            Assert.That(speed.WalkSpeedModifier, Is.EqualTo(originalSpeed));
        });
    }
}

[RegisterComponent]
public sealed partial class CMUFrenzyRefreshProbeComponent : Component
{
    public int Refreshes;
}

public sealed class CMUFrenzyRefreshProbeSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<CMUFrenzyRefreshProbeComponent, RefreshMovementSpeedModifiersEvent>(OnRefresh);
    }

    private void OnRefresh(Entity<CMUFrenzyRefreshProbeComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        ent.Comp.Refreshes++;
    }
}
