using System.Linq;
using Content.Shared._CMU14.Yautja;
using Content.Shared._RMC14.Actions;
using Content.Shared._RMC14.Armor;
using Content.Shared._RMC14.IdentityManagement;
using Content.Shared._RMC14.Stun;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.ClawSharpness;
using Content.Shared._RMC14.Xenonids.Damage;
using Content.Shared._RMC14.Xenonids.Evolution;
using Content.Shared._RMC14.Xenonids.Parasite;
using Content.Shared._RMC14.Xenonids.Plasma;
using Content.Shared._RMC14.Xenonids.Stab;
using Content.Shared.Damage;
using Content.Shared.Explosion.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Speech;
using Content.Shared.StatusIcon.Components;
using Content.Shared.Weapons.Melee;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using ClientSpriteComponent = Robust.Client.GameObjects.SpriteComponent;

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class YautjaAbominationTest
{
    [Test]
    public async Task InfectedYautjaGestatesPredalienLarva()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        EntityUid hunter = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            hunter = entMan.SpawnEntity("CMUMobYautja", MapCoordinates.Nullspace);
            entMan.EnsureComponent<VictimInfectedComponent>(hunter);
        });

        await server.WaitRunTicks(35);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;

            try
            {
                var infected = entMan.GetComponent<VictimInfectedComponent>(hunter);

                Assert.That(entMan.HasComponent<YautjaComponent>(hunter), Is.True);
                Assert.That(entMan.HasComponent<YautjaAbominationHostComponent>(hunter), Is.True);
                Assert.That(infected.BurstSpawn.Id, Is.EqualTo("CMUXenoPredalienLarva"));
            }
            finally
            {
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PredalienLarvaCanImmediatelyEvolveOnlyIntoAbomination()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var larva = entMan.SpawnEntity("CMUXenoPredalienLarva", MapCoordinates.Nullspace);

            try
            {
                Assert.That(entMan.HasComponent<YautjaAbominationLarvaComponent>(larva), Is.True);
                Assert.That(entMan.HasComponent<RestrictEvolveOffWeedsComponent>(larva), Is.False);

                var xeno = entMan.GetComponent<XenoComponent>(larva);
                Assert.That(xeno.Role.Id, Is.EqualTo("CMUXenoPredalienLarva"));
                Assert.That(xeno.Tier, Is.EqualTo(0));
                Assert.That(xeno.CountedInSlots, Is.False);
                Assert.That(xeno.BypassTierCount, Is.True);

                var evolution = entMan.GetComponent<XenoEvolutionComponent>(larva);
                Assert.That(evolution.RequiresGranter, Is.False);
                Assert.That(evolution.CanEvolveWithoutGranter, Is.True);
                Assert.That(evolution.Max, Is.EqualTo(FixedPoint2.Zero));
                Assert.That(evolution.EvolvesToWithoutPoints.Select(id => id.Id), Is.EquivalentTo(new[] { "CMUXenoAbomination" }));

                var regen = entMan.GetComponent<XenoRegenComponent>(larva);
                Assert.That(regen.HealOffWeeds, Is.True);
            }
            finally
            {
                if (!entMan.Deleted(larva))
                    entMan.DeleteEntity(larva);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PredalienLarvaUsesCustomSprite()
    {
        await using var pair = await PoolManager.GetServerClient();
        var client = pair.Client;
        var prototypeManager = client.ResolveDependency<IPrototypeManager>();
        var componentFactory = client.ResolveDependency<IComponentFactory>();

        await client.WaitAssertion(() =>
        {
            Assert.That(prototypeManager.TryIndex<EntityPrototype>("CMUXenoPredalienLarva", out var prototype), Is.True);
            Assert.That(prototype!.TryGetComponent<ClientSpriteComponent>(out var sprite, componentFactory), Is.True);
            Assert.That(sprite.BaseRSI?.Path.ToString(), Does.EndWith("/Textures/_CMU14/Yautja/predalien_larva.rsi"));
            Assert.That(sprite.BaseRSI?.TryGetState("alive", out _), Is.True);
            Assert.That(sprite.BaseRSI?.TryGetState("running", out _), Is.True);
            Assert.That(sprite.BaseRSI?.TryGetState("sleeping", out _), Is.True);
            Assert.That(sprite.BaseRSI?.TryGetState("crit", out _), Is.True);
            Assert.That(sprite.BaseRSI?.TryGetState("dead", out _), Is.True);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AbominationHasCm13InspiredStatsActionsAndDishonoredMark()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var abomination = entMan.SpawnEntity("CMUXenoAbomination", MapCoordinates.Nullspace);

            try
            {
                Assert.That(entMan.HasComponent<YautjaAbominationComponent>(abomination), Is.True);
                Assert.That(entMan.HasComponent<XenoEvolutionComponent>(abomination), Is.False);
                Assert.That(entMan.HasComponent<StatusIconComponent>(abomination), Is.True);

                var mark = entMan.GetComponent<YautjaMarkComponent>(abomination);
                Assert.That(mark.Marks[YautjaMarkKind.Dishonored], Is.EqualTo(abomination));

                var xeno = entMan.GetComponent<XenoComponent>(abomination);
                Assert.That(xeno.Role.Id, Is.EqualTo("CMUXenoAbomination"));
                Assert.That(xeno.Tier, Is.EqualTo(1));
                Assert.That(xeno.CountedInSlots, Is.False);
                Assert.That(xeno.BypassTierCount, Is.True);
                Assert.That(xeno.EmoteSounds?.Id, Is.EqualTo("CMUPredalien"));
                Assert.That(xeno.ActionIds.Select(id => id.Id), Is.SupersetOf(new[]
                {
                    "CMUActionYautjaAbominationRush",
                    "CMUActionYautjaAbominationRoar",
                    "CMUActionYautjaAbominationSmash",
                    "CMUActionYautjaAbominationFrenzy",
                    "CMUActionYautjaAbominationToggleFrenzy",
                }));

                var thresholds = entMan.GetComponent<MobThresholdsComponent>(abomination).Thresholds;
                Assert.That(thresholds[FixedPoint2.New(650)], Is.EqualTo(MobState.Critical));
                Assert.That(thresholds[FixedPoint2.New(750)], Is.EqualTo(MobState.Dead));

                var armor = entMan.GetComponent<CMArmorComponent>(abomination);
                Assert.That(armor.XenoArmor, Is.EqualTo(30));
                Assert.That(armor.ExplosionArmor, Is.EqualTo(100));
                Assert.That(entMan.GetComponent<ExplosionResistanceComponent>(abomination).DamageCoefficient, Is.EqualTo(0.25f).Within(0.001f));
                Assert.That(entMan.GetComponent<XenoClawsComponent>(abomination).ClawType, Is.EqualTo(XenoClawType.VerySharp));

                var melee = entMan.GetComponent<MeleeWeaponComponent>(abomination);
                Assert.That(melee.AttackRate, Is.EqualTo(1.15f).Within(0.001f));
                Assert.That(melee.Damage.GetTotal(), Is.EqualTo(FixedPoint2.New(38)));
                Assert.That(entMan.HasComponent<XenoTailStabComponent>(abomination), Is.True);

                var movement = entMan.GetComponent<MovementSpeedModifierComponent>(abomination);
                Assert.That(movement.BaseWalkSpeed, Is.EqualTo(3f).Within(0.001f));
                Assert.That(movement.BaseSprintSpeed, Is.EqualTo(5.2f).Within(0.001f));

                Assert.That(entMan.GetComponent<RMCSizeComponent>(abomination).Size, Is.EqualTo(RMCSizes.Big));
                Assert.That(entMan.GetComponent<RMCXenoDamageVisualsComponent>(abomination).Prefix, Is.EqualTo("predalien"));
                Assert.That(entMan.GetComponent<FixedIdentityComponent>(abomination).Name, Is.EqualTo("cmu-xeno-abomination-name"));

                var plasma = entMan.GetComponent<XenoPlasmaComponent>(abomination);
                Assert.That(plasma.Plasma, Is.EqualTo(FixedPoint2.Zero));
                Assert.That(plasma.MaxPlasma, Is.EqualTo(0));

                var speech = entMan.GetComponent<SpeechComponent>(abomination);
                Assert.That(speech.SpeechSounds?.Id, Is.EqualTo("CMUPredalienSpeech"));

                var order = entMan.GetComponent<RMCActionOrderComponent>(abomination);
                Assert.That(order.Id.Id, Is.EqualTo("CMUXenoAbomination"));
            }
            finally
            {
                if (!entMan.Deleted(abomination))
                    entMan.DeleteEntity(abomination);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AbominationSmashAndFrenzyCannotHitOutOfRangeTargets()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var mapManager = server.ResolveDependency<IMapManager>();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var map = entMan.System<SharedMapSystem>();

            map.CreateMap(out var mapId);
            var grid = mapManager.CreateGridEntity(mapId);
            map.SetTile(grid, Vector2i.Zero, new Tile(1));
            map.SetTile(grid, new Vector2i(10, 0), new Tile(1));

            var abomination = entMan.SpawnEntity("CMUXenoAbomination", map.GridTileToLocal(grid, grid.Comp, Vector2i.Zero));
            var farTarget = entMan.SpawnEntity("CMMobHuman", map.GridTileToLocal(grid, grid.Comp, new Vector2i(10, 0)));

            try
            {
                var rmcActions = entMan.System<SharedRMCActionsSystem>();
                var smashAction = rmcActions.GetActionsWithEvent<YautjaAbominationSmashActionEvent>(abomination).Single();
                var frenzyAction = rmcActions.GetActionsWithEvent<YautjaAbominationFrenzyActionEvent>(abomination).Single();

                var smash = new YautjaAbominationSmashActionEvent
                {
                    Performer = abomination,
                    Action = smashAction,
                    Target = farTarget,
                };

                entMan.EventBus.RaiseLocalEvent(abomination, smash);
                Assert.That(entMan.GetComponent<DamageableComponent>(farTarget).TotalDamage, Is.EqualTo(FixedPoint2.Zero));

                var frenzy = new YautjaAbominationFrenzyActionEvent
                {
                    Performer = abomination,
                    Action = frenzyAction,
                    Target = farTarget,
                };

                entMan.EventBus.RaiseLocalEvent(abomination, frenzy);
                Assert.That(entMan.GetComponent<DamageableComponent>(farTarget).TotalDamage, Is.EqualTo(FixedPoint2.Zero));
            }
            finally
            {
                if (!entMan.Deleted(abomination))
                    entMan.DeleteEntity(abomination);

                if (!entMan.Deleted(farTarget))
                    entMan.DeleteEntity(farTarget);

                if (!entMan.Deleted(grid))
                    entMan.DeleteEntity(grid);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AbominationToggleFrenzyModeUpdatesFrenzyActionIcon()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var abomination = entMan.SpawnEntity("CMUXenoAbomination", MapCoordinates.Nullspace);

            try
            {
                var rmcActions = entMan.System<SharedRMCActionsSystem>();
                var frenzyAction = rmcActions.GetActionsWithEvent<YautjaAbominationFrenzyActionEvent>(abomination).Single();
                var toggleAction = rmcActions.GetActionsWithEvent<YautjaAbominationToggleFrenzyModeActionEvent>(abomination).Single();

                AssertRsiState(frenzyAction.Comp.Icon, "rav_eviscerate");

                var toggle = new YautjaAbominationToggleFrenzyModeActionEvent
                {
                    Performer = abomination,
                    Action = toggleAction,
                };

                entMan.EventBus.RaiseLocalEvent(abomination, toggle);

                Assert.That(entMan.GetComponent<YautjaAbominationComponent>(abomination).FrenzyAreaMode, Is.True);
                AssertRsiState(frenzyAction.Comp.Icon, "spin_slash");
                Assert.That(toggleAction.Comp.Toggled, Is.True);

                toggle = new YautjaAbominationToggleFrenzyModeActionEvent
                {
                    Performer = abomination,
                    Action = toggleAction,
                };

                entMan.EventBus.RaiseLocalEvent(abomination, toggle);

                Assert.That(entMan.GetComponent<YautjaAbominationComponent>(abomination).FrenzyAreaMode, Is.False);
                AssertRsiState(frenzyAction.Comp.Icon, "rav_eviscerate");
                Assert.That(toggleAction.Comp.Toggled, Is.False);
            }
            finally
            {
                if (!entMan.Deleted(abomination))
                    entMan.DeleteEntity(abomination);
            }
        });

        await pair.CleanReturnAsync();
    }

    private static void AssertRsiState(SpriteSpecifier? icon, string state)
    {
        Assert.That(icon, Is.TypeOf<SpriteSpecifier.Rsi>());
        Assert.That(((SpriteSpecifier.Rsi) icon!).RsiState, Is.EqualTo(state));
    }
}
