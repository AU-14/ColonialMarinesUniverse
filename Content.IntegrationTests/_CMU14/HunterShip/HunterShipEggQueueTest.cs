using System;
using System.Linq;
using Content.Shared._RMC14.Xenonids.Egg;
using Content.Shared._RMC14.Xenonids.JoinXeno;
using Content.Server.Ghost.Roles.Components;
using NUnit.Framework;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.HunterShip;

[TestFixture]
public sealed class HunterShipEggQueueTest
{
    private static readonly Robust.Shared.Prototypes.EntProtoId CMXenoLarvaPrototype = "CMXenoLarva";

    [Test]
    public async Task AlphaAndForsakenEggsSpawnQueueableLarvae()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = server.EntMan.ComponentFactory;
            var eggs = prototypes.EnumeratePrototypes<EntityPrototype>()
                .Where(proto => !proto.Abstract &&
                                proto.ID.StartsWith("CMUHunterShip", StringComparison.Ordinal) &&
                                proto.ID.Contains("Egg", StringComparison.Ordinal) &&
                                proto.TryComp<XenoEggComponent>(out _, factory))
                .ToArray();

            Assert.That(eggs, Is.Not.Empty);
            foreach (var egg in eggs)
            {
                Assert.That(egg.TryComp<XenoEggComponent>(out var xenoEgg, factory), Is.True, egg.ID);
                Assert.That(xenoEgg!.Spawn.Id, Is.EqualTo("CMXenoLarva"), egg.ID);
                Assert.That(egg.TryComp<GhostRoleComponent>(out _, factory), Is.False,
                    $"Egg {egg.ID} must not carry a direct ghost role.");
            }

            var larva = prototypes.Index<EntityPrototype>(CMXenoLarvaPrototype);
            Assert.That(larva.TryComp<LarvaQueueableComponent>(out _, factory), Is.True,
                "CMXenoLarva must enter the standard larva queue.");
        });

        await pair.CleanReturnAsync();
    }
}
