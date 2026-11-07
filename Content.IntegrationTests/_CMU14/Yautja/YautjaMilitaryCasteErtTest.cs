using System.Collections.Generic;
using System.Linq;
using Content.Server.CMU14.Yautja;
using Content.Server.Ghost.Roles.Components;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.Actions;
using Content.Shared.CMU14.Threats;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Inventory;
using Content.Shared.Mind;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Nutrition.Prototypes;
using Content.Shared.Roles;
using Content.Shared.Storage;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using ThirdPartySystem = Content.Server.CMU14.Ops.ThirdParty.ThirdPartySystem;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class YautjaMilitaryCasteErtTest
{
    private static readonly ProtoId<ThirdPartyPrototype> DeathsquadParty = "CMUYautjaMilitaryCaste";

    [Test]
    public async Task AdminCallSpawnsTheSquadAboardTheMilitaryCasteShuttle()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var party = prototypes.Index(DeathsquadParty);
            var spawn = prototypes.Index<PartySpawnPrototype>(party.PartySpawn);

            Assert.That(entMan.System<ThirdPartySystem>().SpawnThirdPartyImmediately(party, spawn, true), Is.True);

            EntityUid? shuttle = null;
            var consoles = entMan.EntityQueryEnumerator<YautjaShuttleConsoleComponent, TransformComponent>();
            while (consoles.MoveNext(out var uid, out _, out var xform))
            {
                if (entMan.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID == "CMUYautjaMilitaryCasteShuttleConsole")
                    shuttle = xform.GridUid;
            }

            Assert.That(shuttle, Is.Not.Null, "the squad should arrive on its own copy of the hunter shuttle");

            var enforcers = 0;
            var soldiers = 0;
            var bodies = entMan.EntityQueryEnumerator<YautjaMilitaryCasteComponent, TransformComponent>();
            while (bodies.MoveNext(out var uid, out var caste, out var xform))
            {
                Assert.That(xform.GridUid, Is.EqualTo(shuttle));
                Assert.That(entMan.HasComponent<GhostRoleComponent>(uid), Is.True);
                if (caste.Caste == YautjaMilitaryCaste.Enforcer)
                    enforcers++;
                else
                    soldiers++;
            }

            Assert.Multiple(() =>
            {
                Assert.That(enforcers, Is.EqualTo(1));
                Assert.That(soldiers, Is.EqualTo(7));
            });
        });

        await pair.CleanReturnAsync();
    }

}
