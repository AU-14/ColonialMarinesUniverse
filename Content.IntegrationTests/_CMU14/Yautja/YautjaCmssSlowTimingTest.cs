using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Slow;
using Content.Shared._RMC14.Xenonids.Abduct;
using Content.Shared._RMC14.Xenonids.Charge;
using Content.Shared._RMC14.Xenonids.Crippling;
using Content.Shared._RMC14.Xenonids.Doom;
using Content.Shared._RMC14.Xenonids.Fling;
using Content.Shared._RMC14.Xenonids.HighGallop;
using Content.Shared._RMC14.Xenonids.MeleeSlow;
using Content.Shared._RMC14.Xenonids.ScissorCut;
using Content.Shared._RMC14.Xenonids.Stomp;
using Content.Shared._RMC14.Xenonids.Tail_Lash;
using Content.Shared._RMC14.Xenonids.TailJab;
using Content.Shared._RMC14.Xenonids.TailTrip;
using Content.Shared.CMU14.Yautja.Slow;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.CMU14.Yautja;

// preds get cmss13's tick-jittered slow timings from xenos, marines keep the RMC numbers
[TestFixture]
public sealed class YautjaCmssSlowTimingTest : GameTest
{
    // windows from replaying cmss13's life tick (2s) + xeno_slow effect tick (1s) over every phase
    [TestCase(false, 2f, 0f, 2f, 4f)] // berserker apprehend, warrior fling
    [TestCase(true, 3f, 0f, 4f, 6f)] // lurker crippling strike
    [TestCase(true, 4f, 0f, 6f, 8f)] // king doom
    [TestCase(false, 8f, 0f, 14f, 16f)] // charger headbutt
    [TestCase(false, 0f, 3.5f, 6.5f, 10.5f)] // crusher slash and charge, lurker on a downed target
    [TestCase(false, 0f, 2.5f, 4.5f, 8.5f)] // tail lash, high gallop
    [TestCase(false, 1f, 2.5f, 6f, 10f)] // single target abduct
    [TestCase(true, 0f, 3f, 6f, 9f)] // empowered scissor cut
    [TestCase(true, 0f, 1f, 2f, 5f)] // charger crush underneath
    public void RollMatchesCmssWindow(bool super, float ticks, float effect, float min, float max)
    {
        var slow = new YautjaCmssSlow { Super = super, Ticks = ticks, Effect = TimeSpan.FromSeconds(effect) };
        var lo = float.MaxValue;
        var hi = 0f;
        var spread = new HashSet<float>();

        for (var life = 0.01f; life < 2f; life += 0.05f)
        {
            for (var eff = 0.01f; eff < 1f; eff += 0.05f)
            {
                var rolled = CMUYautjaSlowSystem.Roll(slow, life, eff);
                lo = MathF.Min(lo, rolled);
                hi = MathF.Max(hi, rolled);
                spread.Add(MathF.Round(rolled, 1));
            }
        }

        Assert.Multiple(() =>
        {
            Assert.That(lo, Is.GreaterThanOrEqualTo(min - 0.05f), "rolled shorter than cmss13 can");
            Assert.That(hi, Is.LessThanOrEqualTo(max + 0.05f), "rolled longer than cmss13 can");
            Assert.That(hi - lo, Is.GreaterThan((max - min) * 0.8f), "the roll lost cmss13's spread");
            Assert.That(spread.Count, Is.GreaterThan(5), "the roll is flat");
        });
    }

    [Test]
    public async Task CastesCarryPredTimings()
    {
        await Server.WaitAssertion(() =>
        {
            var factory = SEntMan.ComponentFactory;
            Has<XenoCripplingStrikeComponent>("CMXenoLurker", c => c.YautjaSlow);
            Has<XenoCripplingStrikeComponent>("RMCXenoRavagerBerserker", c => c.YautjaSlow);
            Has<XenoMeleeSlowComponent>("RMCXenoCrusher", c => c.YautjaSlow);
            Has<XenoMeleeSlowComponent>("CMXenoLurker", c => c.YautjaSlow);
            Has<XenoChargeComponent>("RMCXenoCrusher", c => c.YautjaSlow);
            Has<XenoStompComponent>("RMCXenoCrusher", c => c.YautjaSlow);
            Has<XenoStompComponent>("RMCXenoCrusherCharger", c => c.YautjaSlowUnder);
            Has<XenoFlingComponent>("RMCXenoCrusherCharger", c => c.YautjaSlow);
            Has<XenoFlingComponent>("CMXenoWarrior", c => c.YautjaSlow);
            Has<XenoDoomComponent>("RMCXenoKing", c => c.YautjaSlow);
            Has<XenoTailTripComponent>("RMCXenoPraetorianDancer", c => c.YautjaSlow);
            Has<XenoAbductComponent>("RMCXenoPraetorianOppressor", c => c.YautjaSlow);
            Has<XenoTailLashComponent>("RMCXenoPraetorianOppressor", c => c.YautjaSlow);
            Has<XenoHighGallopComponent>("RMCXenoPraetorianValkyrie", c => c.YautjaSlow);
            Has<XenoScissorCutComponent>("CMXenoRavager", c => c.YautjaSlow);
            Has<XenoTailJabComponent>("RMCXenoLurkerVampire", c => c.YautjaSlow);

            // charger keeps the inherited marine slash slow but never slows a pred with it, like cmss13
            Assert.That(SProtoMan.Index<EntityPrototype>("RMCXenoCrusherCharger")
                    .TryGetComponent<XenoMeleeSlowComponent>(out var chargerSlash, factory), Is.True);
            Assert.That(chargerSlash!.SlowTime, Is.EqualTo(TimeSpan.FromSeconds(2)));
            Assert.That(chargerSlash.YautjaSlow, Is.Not.Null);
            Assert.That(CMUYautjaSlowSystem.Roll(chargerSlash.YautjaSlow!, 1f, 0.5f), Is.Zero);

            void Has<T>(string proto, Func<T, YautjaCmssSlow?> get) where T : IComponent, new()
            {
                Assert.That(SProtoMan.Index<EntityPrototype>(proto).TryGetComponent<T>(out var comp, factory), Is.True,
                    $"{proto} lost its {typeof(T).Name}");
                Assert.That(get(comp!), Is.Not.Null, $"{proto} {typeof(T).Name} has no pred slow timing");
            }
        });
    }

    [Test]
    public async Task WarriorFlingRollsCmssTimingOnPredsOnly()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var timing = Server.ResolveDependency<IGameTiming>();
            var warrior = SEntMan.SpawnEntity("CMXenoWarrior", map.GridCoords);
            var human = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
            var hunter = SEntMan.SpawnEntity("CMUMobYautja", map.GridCoords.Offset(new Vector2(-1, 0)));

            Fling(warrior, human);
            Fling(warrior, hunter);

            var now = timing.CurTime;
            var humanLeft = SEntMan.GetComponent<RMCSlowdownComponent>(human).ExpiresAt - now;
            var hunterLeft = SEntMan.GetComponent<RMCSlowdownComponent>(hunter).ExpiresAt - now;

            Assert.Multiple(() =>
            {
                Assert.That(humanLeft, Is.EqualTo(TimeSpan.FromSeconds(4)), "marines should keep the RMC fling slow");
                Assert.That(hunterLeft.TotalSeconds, Is.InRange(2.0, 4.0), "pred fling slow left cmss13's window");
            });
        });
    }

    // cmss13 takes most of the human slow off preds, so the long windows above aren't marine strength
    [TestCase(false)]
    [TestCase(true)]
    public async Task PredsAreSlowedLighterThanMarines(bool super)
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var human = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var hunter = SEntMan.SpawnEntity("CMUMobYautja", map.GridCoords.Offset(new Vector2(2, 0)));

            var humanKept = SpeedKept(human);
            var hunterKept = SpeedKept(hunter);

            Assert.Multiple(() =>
            {
                Assert.That(humanKept, Is.LessThan(1f), "the slow didn't slow a marine at all");
                Assert.That(hunterKept, Is.LessThan(1f), "the slow didn't slow a pred at all");
                Assert.That(hunterKept, Is.GreaterThan(humanKept), "preds should lose less speed to a slow than marines");
            });
        });

        float SpeedKept(EntityUid mob)
        {
            var speed = Server.System<MovementSpeedModifierSystem>();
            speed.RefreshMovementSpeedModifiers(mob);
            var before = SEntMan.GetComponent<MovementSpeedModifierComponent>(mob).CurrentSprintSpeed;

            var slow = Server.System<RMCSlowSystem>();
            if (super)
                slow.TrySuperSlowdown(mob, TimeSpan.FromSeconds(10));
            else
                slow.TrySlowdown(mob, TimeSpan.FromSeconds(10));

            speed.RefreshMovementSpeedModifiers(mob);
            return SEntMan.GetComponent<MovementSpeedModifierComponent>(mob).CurrentSprintSpeed / before;
        }
    }

    private void Fling(EntityUid warrior, EntityUid target)
    {
        var ev = new XenoFlingActionEvent { Performer = warrior, Target = target };
        SEntMan.EventBus.RaiseLocalEvent(warrior, ev);
        Assert.That(ev.Handled, Is.True, "the fling never went off");
    }
}
