using System.Linq;
using Content.Server._CMU14.Yautja;
using Content.Server.Examine;
using Content.Shared._CMU14.Yautja;
using Content.Shared.Ghost;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class YautjaScalpTest
{
    [Test]
    public async Task ScalpPrototypeMatchesCmss13StaticFacts()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var client = pair.Client;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var scalp = entMan.SpawnEntity("CMUYautjaScalp", MapCoordinates.Nullspace);

            try
            {
                var meta = entMan.GetComponent<MetaDataComponent>(scalp);
                var item = entMan.GetComponent<ItemComponent>(scalp);

                Assert.Multiple(() =>
                {
                    Assert.That(meta.EntityName, Is.EqualTo("scalp"),
                        "CMSS13 /obj/item/scalp source name.");
                    Assert.That(meta.EntityDescription, Is.EqualTo("This is the scalp of an irrelevant human."),
                        "Static mapload scalps keep a local generic metadata description.");
                    Assert.That(entMan.GetComponent<YautjaScalpComponent>(scalp).TrueDescription,
                        Is.EqualTo("This is the scalp of an irrelevant human."),
                        "CMSS13 /obj/item/scalp mapload true_desc fallback.");
                    Assert.That(item.Size.Id, Is.EqualTo("Small"),
                        "CMSS13 /obj/item/scalp inherited local item size mapping.");
                    Assert.That(item.HeldPrefix, Is.EqualTo("scalp"),
                        "CMSS13 /obj/item/scalp item_state local held-prefix mapping.");
                    Assert.That(entMan.HasComponent<YautjaScalpComponent>(scalp), Is.True,
                        "CMSS13 scalp item should keep the local scalp marker.");
                    Assert.That(entMan.HasComponent<YautjaTrophyComponent>(scalp), Is.True,
                        "Local scalp remains trophy-display compatible; CMSS13 scalp scoring is runtime-driven.");
                    Assert.That(entMan.HasComponent<YautjaTechItemComponent>(scalp), Is.False,
                        "CMSS13 /obj/item/scalp does not set flags_item = ITEM_PREDATOR.");
                });
            }
            finally
            {
                if (!entMan.Deleted(scalp))
                    entMan.DeleteEntity(scalp);
            }
        });

        await client.WaitAssertion(() =>
        {
            var prototypes = client.ResolveDependency<IPrototypeManager>();
            var factory = client.ResolveDependency<IComponentFactory>();
            var prototype = prototypes.Index<EntityPrototype>("CMUYautjaScalp");

            Assert.That(prototype.TryGetComponent<SpriteComponent>(out var sprite, factory), Is.True);
            Assert.That(sprite!.BaseRSI?.Path, Is.EqualTo(new ResPath("/Textures/_CMU14/Yautja/yautja_items.rsi")));
            Assert.That(sprite.AllLayers.Select(layer => layer.RsiState.Name),
                Is.EqualTo(new[] { "scalp_1", "scalp_1_blood" }),
                "CMSS13 /obj/item/scalp default icon_state plus blood overlay.");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RuntimeScalpBiographyAndTakenByTextMatchCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var metadata = entMan.System<MetaDataSystem>();
            var marks = entMan.System<YautjaMarkSystem>();
            var trophies = entMan.System<YautjaTrophySystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var prey = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new(1, 0)));
            var honoringHunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new(2, 0)));
            var gearHunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new(3, 0)));
            var bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);
            var honorBracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords.Offset(new(2, 0)));
            var gearBracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords.Offset(new(3, 0)));
            EntityUid scalp = default;

            try
            {
                var inventory = entMan.System<InventorySystem>();
                entMan.EnsureComponent<YautjaComponent>(hunter);
                entMan.EnsureComponent<YautjaComponent>(honoringHunter);
                entMan.EnsureComponent<YautjaComponent>(gearHunter);
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(inventory.TryEquip(honoringHunter, honorBracer, "gloves", silent: true, force: true), Is.True);
                Assert.That(inventory.TryEquip(gearHunter, gearBracer, "gloves", silent: true, force: true), Is.True);
                metadata.SetEntityName(hunter, "A'ke Ret");
                metadata.SetEntityName(prey, "Guan Thwei");
                metadata.SetEntityName(honoringHunter, "Ki'cte Pa");
                metadata.SetEntityName(gearHunter, "N'dui Tkeh");
                entMan.EnsureComponent<YautjaHonorWorthComponent>(prey).LifeKillsTotal = 6;

                Assert.That(marks.TryMark((bracer, entMan.GetComponent<YautjaBracerComponent>(bracer)), hunter, prey, YautjaMarkKind.Prey, null), Is.True);
                Assert.That(marks.TryMark((honorBracer, entMan.GetComponent<YautjaBracerComponent>(honorBracer)), honoringHunter, prey, YautjaMarkKind.Honored, "spared civilians"), Is.True);
                Assert.That(marks.TryMark((gearBracer, entMan.GetComponent<YautjaBracerComponent>(gearBracer)), gearHunter, prey, YautjaMarkKind.GearCarrier, null), Is.True);

                scalp = trophies.SpawnRuntimeScalp(prey, hunter);
                var scalpMeta = entMan.GetComponent<MetaDataComponent>(scalp);
                var scalpComp = entMan.GetComponent<YautjaScalpComponent>(scalp);

                Assert.Multiple(() =>
                {
                    Assert.That(scalpMeta.EntityName, Is.EqualTo("Guan Thwei's scalp"),
                        "CMSS13 /obj/item/scalp/Initialize() names runtime scalps after scalpee.real_name.");
                    Assert.That(scalpMeta.EntityDescription, Is.Empty,
                        "Runtime scalp true_desc must not live in local metadata, because metadata examine text is visible to ordinary humans.");
                    Assert.That(scalpComp.TrueDescription, Does.Contain("This is the scalp of an uncommonly destructive human."),
                        "CMSS13 scalp true_desc uses the 5-9 life_kills_total worth branch.");
                    Assert.That(scalpComp.TrueDescription, Does.Contain("Guan Thwei was honored for 'spared civilians'"),
                        "CMSS13 scalp biography includes honored reason text.");
                    Assert.That(scalpComp.TrueDescription, Does.Contain("killed after N'dui Tkeh marked him as a thief of Yautja equipment"),
                        "CMSS13 scalp biography includes gear-carrier hunter text.");
                    Assert.That(scalpComp.TrueDescription, Does.Contain("This trophy was taken by A'ke Ret after a successful hunt."),
                        "CMSS13 scalp keeps dishonorable 5-9-kill scalps at worth 1, so the hunter-only line is the ordinary successful hunt text.");
                });
            }
            finally
            {
                foreach (var uid in new[] { hunter, prey, honoringHunter, gearHunter, bracer, honorBracer, gearBracer, scalp })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RuntimeScalpExamineShowsTrueDescOnlyToYautjaAndGhostsLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var metadata = entMan.System<MetaDataSystem>();
            var trophies = entMan.System<YautjaTrophySystem>();
            var examine = entMan.System<ExamineSystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var prey = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new(1, 0)));
            var ordinary = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new(2, 0)));
            var ghost = entMan.SpawnEntity(null, map.GridCoords.Offset(new(3, 0)));
            EntityUid scalp = default;

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                entMan.EnsureComponent<GhostComponent>(ghost);
                metadata.SetEntityName(prey, "Guan Thwei");
                entMan.EnsureComponent<YautjaHonorWorthComponent>(prey).LifeKillsTotal = 2;

                scalp = trophies.SpawnRuntimeScalp(prey, hunter);
                var scalpComp = entMan.GetComponent<YautjaScalpComponent>(scalp);

                var hunterText = examine.GetExamineText(scalp, hunter).ToMarkup();
                var ghostText = examine.GetExamineText(scalp, ghost).ToMarkup();
                var ordinaryText = examine.GetExamineText(scalp, ordinary).ToMarkup();
                var warning = Loc.GetString("cmu-yautja-scalp-non-yautja-examine");

                Assert.Multiple(() =>
                {
                    Assert.That(hunterText, Does.Contain(scalpComp.TrueDescription),
                        "CMSS13 /obj/item/scalp/get_examine_text() shows true_desc to Yautja.");
                    Assert.That(ghostText, Does.Contain(scalpComp.TrueDescription),
                        "CMSS13 observers can inspect the scalp true_desc.");
                    Assert.That(ordinaryText, Does.Contain(warning),
                        "CMSS13 hides true_desc from ordinary viewers behind the joke warning.");
                    Assert.That(ordinaryText, Does.Not.Contain("This is the scalp of a notable human."),
                        "Ordinary local examine must not leak the runtime true_desc via metadata.");
                });
            }
            finally
            {
                foreach (var uid in new[] { hunter, prey, ordinary, ghost, scalp })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RuntimeScalpHairColorTintsOnlyBaseLayerLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();
        var hairColor = new Color((byte) 0x31, (byte) 0x7f, (byte) 0xa4);
        EntityUid hunter = default;
        EntityUid prey = default;
        EntityUid scalp = default;
        NetEntity scalpNet = default;

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var trophies = entMan.System<YautjaTrophySystem>();

            hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            prey = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new(1, 0)));
            entMan.EnsureComponent<YautjaComponent>(hunter);
            var appearance = entMan.GetComponent<HumanoidAppearanceComponent>(prey);
            appearance.CachedHairColor = hairColor;
            entMan.Dirty(prey, appearance);

            scalp = trophies.SpawnRuntimeScalp(prey, hunter);
            scalpNet = entMan.GetNetEntity(scalp);
        });

        await pair.RunTicksSync(5);

        await client.WaitAssertion(() =>
        {
            var entMan = client.EntMan;
            var sprites = entMan.System<SpriteSystem>();

            Assert.That(entMan.TryGetEntity(scalpNet, out var clientScalp), Is.True);
            var sprite = entMan.GetComponent<SpriteComponent>(clientScalp.Value);
            Assert.That(sprites.TryGetLayer((clientScalp.Value, sprite), 0, out var baseLayer, false), Is.True);
            Assert.That(sprites.TryGetLayer((clientScalp.Value, sprite), 1, out var bloodLayer, false), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(baseLayer!.Color, Is.EqualTo(hairColor),
                    "CMSS13 scalp icon is tinted from the scalpee hair color.");
                Assert.That(bloodLayer!.Color, Is.EqualTo(Color.White),
                    "CMSS13 blood overlay is reset_color, so local blood overlay must remain untinted.");
            });
        });

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            foreach (var uid in new[] { hunter, prey, scalp })
            {
                if (uid != default && !entMan.Deleted(uid))
                    entMan.DeleteEntity(uid);
            }
        });

        await pair.CleanReturnAsync();
    }
}
