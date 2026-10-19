using Content.IntegrationTests.Pair;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.CMU14.Threats.Mobs.Wendigo.Feeding;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Tools.Components;
using Content.Shared.Traits.Assorted;
using Content.Shared.Verbs;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Threats.Wendigo;

[TestFixture]
public sealed class CMUWendigoFeedingTest
{
    private const string Lab = "CMUWendigoLab";
    private const string Natural = "AU14Wendigo";
    private const string Human = "CMMobHuman";
    private const string HumanMeat = "FoodMeatHuman";
    private const string OtherFood = "FoodBreadPlainSlice";

    private static readonly ProtoId<DamageTypePrototype>[] BruteTypes = ["Blunt", "Slash", "Piercing"];

    [TestCase(Lab)]
    [TestCase(Natural)]
    public async Task WendigoEatsAndHealsOnHumanFlesh(string wendigoProto)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.EntMan;

        EntityUid wendigo = default, bread = default, meat = default;
        FixedPoint2 before = default;
        await server.WaitPost(() =>
        {
            wendigo = entMan.SpawnEntity(wendigoProto, map.GridCoords);
            bread = entMan.SpawnEntity(OtherFood, map.GridCoords);
            meat = entMan.SpawnEntity(HumanMeat, map.GridCoords);
            // Natural regeneration would blur how much the meal healed.
            entMan.RemoveComponent<XenoRegenComponent>(wendigo);
            var wound = new DamageSpecifier { DamageDict = { ["Blunt"] = 100, ["Slash"] = 100, ["Piercing"] = 100 } };
            entMan.System<DamageableSystem>().TryChangeDamage(wendigo, wound, ignoreResistances: true);
        });
        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var ingestion = entMan.System<IngestionSystem>();
            Assert.That(entMan.HasComponent<CMUWendigoFleshHealComponent>(wendigo), Is.True);
            Assert.That(ingestion.CanIngest(wendigo, bread), Is.True, "a Wendigo must accept ordinary food");
            Assert.That(ingestion.CanIngest(wendigo, meat), Is.True, "a Wendigo must accept human meat");
        });

        // Ordinary food goes through the normal ingestion path and is consumed.
        await Eat(pair, wendigo, bread);
        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.Deleted(bread), Is.True, "the Wendigo never finished the bread");
            before = Brute(entMan, wendigo);
        });

        // Finishing a piece of human meat heals the configured amount.
        await Eat(pair, wendigo, meat);
        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.Deleted(meat), Is.True, "the Wendigo never finished the human meat");
            var heal = entMan.GetComponent<CMUWendigoFleshHealComponent>(wendigo).Heal;
            var expected = -SumBrute(heal).Float();
            Assert.That((before - Brute(entMan, wendigo)).Float(), Is.GreaterThanOrEqualTo(expected - 1f),
                "finishing human meat did not heal the Wendigo");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LabWendigoButchersDeadHumanIntoMeat()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.EntMan;

        EntityUid wendigo = default, corpse = default;
        var meatBefore = 0;
        await server.WaitPost(() =>
        {
            wendigo = entMan.SpawnEntity(Lab, map.GridCoords);
            corpse = entMan.SpawnEntity(Human, map.GridCoords);
            entMan.System<MobStateSystem>().ChangeMobState(corpse, MobState.Dead);
            // Like the ape, a Wendigo carves only past the defib window.
            entMan.EnsureComponent<UnrevivableComponent>(corpse);
            meatBefore = CountMeat(entMan);
        });
        await pair.RunTicksSync(5);

        // Claws are the slicing tool: the empty-handed butcher verb, the same path the Space Ape uses.
        await server.WaitAssertion(() =>
        {
            var verbs = entMan.System<SharedVerbSystem>();
            var label = Loc.GetString(entMan.GetComponent<ToolRefinableComponent>(corpse).VerbText!);
            var verb = verbs.GetLocalVerbs(corpse, wendigo, typeof(InteractionVerb)).Single(v => v.Text == label);
            Assert.That(verb.Disabled, Is.False, "the Wendigo's claws must be able to butcher a dead human");
            verbs.ExecuteVerb(verb, wendigo, corpse);
        });
        await pair.RunTicksSync(pair.SecondsToTicks(10));

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.Deleted(corpse), Is.True, "the corpse was not butchered");
            Assert.That(CountMeat(entMan) - meatBefore, Is.GreaterThanOrEqualTo(6), "butchering left no human meat");
        });

        await pair.CleanReturnAsync();
    }

    private static async Task Eat(TestPair pair, EntityUid eater, EntityUid food)
    {
        var entMan = pair.Server.EntMan;
        await pair.Server.WaitAssertion(() =>
            Assert.That(entMan.System<IngestionSystem>().TryIngest(eater, food), Is.True, "ingestion refused to start"));

        // No player is attached, so bites repeat on their own until the piece is gone.
        for (var i = 0; i < 20; i++)
        {
            await pair.RunTicksSync(pair.SecondsToTicks(1));
            var gone = false;
            await pair.Server.WaitPost(() => gone = entMan.Deleted(food));
            if (gone)
                return;
        }
    }

    private static FixedPoint2 Brute(IEntityManager entMan, EntityUid uid)
    {
        // The heal is Brute only, so the total tracks it; the component's damage dict is access-restricted.
        return entMan.System<DamageableSystem>().GetTotalDamage(uid);
    }

    private static FixedPoint2 SumBrute(DamageSpecifier damage)
    {
        var dict = damage.DamageDict;
        var total = FixedPoint2.Zero;
        foreach (var type in BruteTypes)
        {
            if (dict.TryGetValue(type, out var value))
                total += value;
        }

        return total;
    }

    private static int CountMeat(IEntityManager entMan)
    {
        return entMan.EntityQuery<MetaDataComponent>()
            .Count(meta => !meta.Deleted && meta.EntityPrototype?.ID == HumanMeat);
    }
}
