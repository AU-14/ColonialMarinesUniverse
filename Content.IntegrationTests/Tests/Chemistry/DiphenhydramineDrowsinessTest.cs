#nullable enable

using Content.Shared.Chemistry.Reagent;
using Content.Shared.EntityEffects;
using Content.Shared.EntityEffects.Effects.StatusEffects;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Components;
using Content.Shared.StatusEffectNew;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Chemistry;

[TestFixture]
[TestOf(typeof(ModifyStatusEffect))]
public sealed class DiphenhydramineDrowsinessTest
{
    private static readonly ProtoId<ReagentPrototype> Diphenhydramine = "Diphenhydramine";

    [Test]
    public async Task RepeatedMetabolismDoesNotAccumulateDrowsiness()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.ResolveDependency<IEntityManager>();
            var protoMan = server.ResolveDependency<IPrototypeManager>();
            var status = entMan.System<SharedStatusEffectsSystem>();
            var reagent = protoMan.Index(Diphenhydramine);

            var effect = reagent.Metabolisms!.Values
                .SelectMany(entry => entry.Effects)
                .OfType<ModifyStatusEffect>()
                .Single(entry => entry.EffectProto == "StatusEffectDrowsiness");

            Assert.Multiple(() =>
            {
                Assert.That(effect.Type, Is.EqualTo(StatusEffectMetabolismType.Add));
                Assert.That(effect.Refresh, Is.True);
            });

            var target = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            entMan.AddComponent<MobStateComponent>(target);
            var args = new EntityEffectReagentArgs(
                target,
                entMan,
                null,
                null,
                FixedPoint2.New(1),
                reagent,
                null,
                FixedPoint2.New(1));

            effect.Effect(args);
            Assert.That(status.TryGetTime(target, effect.EffectProto, out var first), Is.True);
            Assert.That(first.EndEffectTime, Is.Not.Null);

            effect.Effect(args);
            Assert.That(status.TryGetTime(target, effect.EffectProto, out var second), Is.True);
            Assert.That(second.EndEffectTime, Is.EqualTo(first.EndEffectTime));
        });

        await pair.CleanReturnAsync();
    }
}
