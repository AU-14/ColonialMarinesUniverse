using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server.Suicide;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared._RMC14.CCVar;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;

namespace Content.IntegrationTests.Tests.Commands;

[TestFixture]
[TestOf(typeof(SuicideSystem))]
public sealed class SuicideMergeRegressionTest : GameTest
{
    public override PoolSettings PoolSettings => new()
    {
        Connected = true,
        Dirty = true,
        DummyTicker = false,
    };

    [Test]
    [EnsureCVar(Side.Server, typeof(RMCCVars), nameof(RMCCVars.RMCEnableSuicide), false)]
    public async Task DisabledDefaultReturnsBeforeGhostOrLethalPath()
    {
        await Server.WaitAssertion(() =>
        {
            _ = Server.System<SuicideMergeProbeSystem>();
            var victim = ServerSession!.AttachedEntity!.Value;
            var probe = SEntMan.EnsureComponent<SuicideMergeProbeComponent>(victim);
            var mobState = SEntMan.GetComponent<MobStateComponent>(victim);
            var damageable = SEntMan.GetComponent<DamageableComponent>(victim);
            var damageSystem = Server.System<DamageableSystem>();
            var damageBefore = damageSystem.GetTotalDamage((victim, damageable));

            Assert.That(Server.CfgMan.GetCVar(RMCCVars.RMCEnableSuicide), Is.False,
                "the fork default keeps suicide disabled");
            Assert.That(Server.System<SuicideSystem>().Suicide(victim), Is.False);

            Assert.Multiple(() =>
            {
                Assert.That(Server.System<MobStateSystem>().IsAlive(victim, mobState), Is.True);
                Assert.That(damageSystem.GetTotalDamage((victim, damageable)), Is.EqualTo(damageBefore));
                Assert.That(probe.GhostEvents, Is.Zero,
                    "the disabled gate must return before the upstream ghost attempt");
                Assert.That(probe.SuicideEvents, Is.Zero,
                    "the disabled gate must return before the upstream lethal event");
                Assert.That(ServerSession.AttachedEntity, Is.EqualTo(victim));
            });
        });
    }

    [Test]
    [EnsureCVar(Side.Server, typeof(RMCCVars), nameof(RMCCVars.RMCEnableSuicide), true)]
    public async Task EnabledReachesInheritedGhostAndLethalPath()
    {
        await Server.WaitAssertion(() =>
        {
            _ = Server.System<SuicideMergeProbeSystem>();
            var victim = ServerSession!.AttachedEntity!.Value;
            var probe = SEntMan.EnsureComponent<SuicideMergeProbeComponent>(victim);
            var mobState = SEntMan.GetComponent<MobStateComponent>(victim);
            var damageable = SEntMan.GetComponent<DamageableComponent>(victim);
            var damageSystem = Server.System<DamageableSystem>();
            var damageBefore = damageSystem.GetTotalDamage((victim, damageable));

            Assert.That(Server.CfgMan.GetCVar(RMCCVars.RMCEnableSuicide), Is.True);
            Assert.That(Server.System<SuicideSystem>().Suicide(victim), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(Server.System<MobStateSystem>().IsDead(victim, mobState), Is.True);
                Assert.That(damageSystem.GetTotalDamage((victim, damageable)), Is.GreaterThan(damageBefore));
                Assert.That(probe.GhostEvents, Is.EqualTo(1));
                Assert.That(probe.SuicideEvents, Is.EqualTo(1),
                    "enabling the fork gate must reach the inherited lethal SuicideEvent path");
            });
        });
    }
}

[RegisterComponent]
public sealed partial class SuicideMergeProbeComponent : Component
{
    public int GhostEvents;
    public int SuicideEvents;
}

public sealed class SuicideMergeProbeSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SuicideMergeProbeComponent, SuicideGhostEvent>(OnGhost);
        SubscribeLocalEvent<SuicideMergeProbeComponent, SuicideEvent>(OnSuicide);
    }

    private static void OnGhost(Entity<SuicideMergeProbeComponent> ent, ref SuicideGhostEvent args)
    {
        ent.Comp.GhostEvents++;
    }

    private static void OnSuicide(Entity<SuicideMergeProbeComponent> ent, ref SuicideEvent args)
    {
        ent.Comp.SuicideEvents++;
    }
}
