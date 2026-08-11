using System.Numerics;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Sticky.Components;
using Content.Shared._RMC14.Marines.Skills;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests._RMC14.Explosion;

[TestFixture]
public sealed class RMCExplosivePlantingTest
{
    [TestCase("RMCExplosiveBreachingCharge", 1.1f)]
    [TestCase("RMCExplosivePlastic", 5.1f)]
    public async Task ExplosiveCanBePlantedOnWall(string prototype, float delay)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid user = default;
        EntityUid explosive = default;
        EntityUid wall = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hands = entMan.System<SharedHandsSystem>();
            var interaction = entMan.System<SharedInteractionSystem>();
            var skills = entMan.System<SkillsSystem>();
            user = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            explosive = entMan.SpawnEntity(prototype, map.GridCoords);
            wall = entMan.SpawnEntity("CMWallMetal", map.GridCoords.Offset(Vector2.UnitX));

            skills.SetSkill(user, "RMCSkillEngineer", 1);
            Assert.That(hands.TryPickupAnyHand(user, explosive), Is.True);

            interaction.UserInteraction(user, entMan.GetComponent<TransformComponent>(wall).Coordinates, wall);
            Assert.That(entMan.GetComponent<DoAfterComponent>(user).DoAfters.Values.Any(x => x.Args.ForceVisible),
                Is.True);
        });

        await pair.RunSeconds(delay);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            Assert.That(entMan.GetComponent<StickyComponent>(explosive).StuckTo, Is.EqualTo(wall));

            entMan.DeleteEntity(user);
            entMan.DeleteEntity(wall);
        });

        await pair.CleanReturnAsync();
    }
}
