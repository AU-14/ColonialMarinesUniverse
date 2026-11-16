#pragma warning disable RA0002 // Sets a sensor tower's faction directly instead of driving the claim dialog.

using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Yautja.BadBlood;
using Content.Shared._RMC14.Communications;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Power;
using Content.Shared._RMC14.Rules;
using Content.Shared._RMC14.Sensor;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// ARCH-503, ARCH-504, FLOW-511: the marine objective chain. The gate latches with any two parts and stays latched,
// the reactor hold pauses instead of resetting, the uplink needs the gate and a signature, and each signature avenue
// latches. docs/GraphAudit-BadBlood-ObjectivesPlan.md
[TestFixture]
public sealed class BadBloodObjectiveChainTest : GameTest
{
    private const string Totem = "CMUBadBloodTotem";
    private const string BadBloodBracer = "CMUYautjaBadBloodBracer";
    private const string Wirecutter = "Wirecutter";
    private const string Wrench = "Wrench";
    private static readonly EntProtoId<SkillDefinitionComponent> EngineerSkill = "RMCSkillEngineer";

    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task GateLatchesWithTwoOfThreeAndStaysLatched()
    {
        var (map, party) = await SetUp();
        var spawned = new List<EntityUid>();
        EntityUid sensor = default;
        EntityUid comms = default;

        try
        {
            await Server.WaitAssertion(() =>
            {
                Server.System<BadBloodChainStageProbeSystem>().Reset();
                comms = BadBloodTestHelpers.SpawnUplinkSite(Server, BadBloodTestSiteKind.Comms, At(map, 10));
                spawned.Add(comms);
                // On, but switched on by nobody: no faction, so it feeds no one's tactical map and does not count
                spawned.Add(BadBloodTestHelpers.SpawnUplinkSite(Server, BadBloodTestSiteKind.Sensor, At(map, 16)));
            });

            await Pair.RunSeconds(3);

            await Server.WaitAssertion(() =>
            {
                var chain = Chain();
                Assert.Multiple(() =>
                {
                    Assert.That(chain.CommsMet, Is.True);
                    Assert.That(chain.SensorMet, Is.False, "a sensor tower with no faction is not GOVFOR's");
                    Assert.That(chain.PowerMet, Is.False);
                    Assert.That(chain.GateMet, Is.False, "one part of three does not open the gate");
                    Assert.That(SComp<BadBloodUplinkSiteComponent>(comms).Kind, Is.EqualTo(BadBloodUplinkSiteKind.Comms),
                        "a planet comms tower becomes an uplink site");
                });

                sensor = BadBloodTestHelpers.SpawnUplinkSite(Server, BadBloodTestSiteKind.Sensor, At(map, 20));
                spawned.Add(sensor);
                SComp<SensorTowerComponent>(sensor).Faction = BadBloodObjectiveChainSystem.GovforIffFaction;
            });

            await Pair.RunSeconds(3);

            await Server.WaitAssertion(() =>
            {
                var chain = Chain();
                var probe = Server.System<BadBloodChainStageProbeSystem>();
                Assert.Multiple(() =>
                {
                    Assert.That(chain.SensorMet, Is.True);
                    Assert.That(chain.GateMet, Is.True, "two parts open the gate");
                    Assert.That(probe.Count(BadBloodObjectiveStage.GatePart), Is.EqualTo(2));
                    Assert.That(probe.Count(BadBloodObjectiveStage.GateMet), Is.EqualTo(1));
                    Assert.That(SComp<BadBloodUplinkSiteComponent>(sensor).Kind, Is.EqualTo(BadBloodUplinkSiteKind.Sensor));
                });

                SComp<CommunicationsTowerComponent>(comms).State = CommunicationsTowerState.Off;
                SEntMan.DeleteEntity(sensor);
            });

            await Pair.RunSeconds(3);

            await Server.WaitAssertion(() =>
            {
                var chain = Chain();
                Assert.Multiple(() =>
                {
                    Assert.That(chain.GateMet, Is.True, "the gate stays latched when the towers go down");
                    Assert.That(chain.CommsMet, Is.True);
                    Assert.That(Server.System<BadBloodChainStageProbeSystem>().Count(BadBloodObjectiveStage.GateMet),
                        Is.EqualTo(1), "the gate is announced once");
                });
            });
        }
        finally
        {
            await Cleanup(party, spawned);
        }
    }

    [Test]
    public async Task ReactorHoldPausesWhileDownAndResumes()
    {
        var (map, party) = await SetUp();
        var spawned = new List<EntityUid>();
        EntityUid reactor = default;
        var heldAtBreak = TimeSpan.Zero;

        try
        {
            await Server.WaitAssertion(() =>
            {
                Assert.That(Chain().ReactorHold, Is.EqualTo(TimeSpan.FromMinutes(5)), "the YAML default");
                Chain().ReactorHold = TimeSpan.FromSeconds(4);
                reactor = BadBloodTestHelpers.SpawnReactor(Server, At(map, 10));
                spawned.Add(reactor);
            });

            await Pair.RunSeconds(2.5f);

            await Server.WaitAssertion(() =>
            {
                var chain = Chain();
                Assert.That(chain.ReactorHeld, Is.GreaterThan(TimeSpan.Zero));
                Assert.That(chain.PowerMet, Is.False);
                heldAtBreak = chain.ReactorHeld;

                Server.System<SharedRMCPowerSystem>().FullyDestroy((reactor, SComp<RMCFusionReactorComponent>(reactor)));
            });

            await Pair.RunSeconds(3);

            await Server.WaitAssertion(() =>
            {
                var chain = Chain();
                Assert.That(chain.ReactorHeld, Is.EqualTo(heldAtBreak), "the hold pauses while no reactor runs");
                Assert.That(chain.PowerMet, Is.False);

                spawned.Add(BadBloodTestHelpers.SpawnReactor(Server, At(map, 14)));
            });

            await Pair.RunSeconds(4);

            await Server.WaitAssertion(() =>
            {
                var chain = Chain();
                Assert.That(chain.ReactorHeld, Is.GreaterThanOrEqualTo(chain.ReactorHold),
                    "the hold resumes from where it paused");
                Assert.That(chain.PowerMet, Is.True);
            });
        }
        finally
        {
            await Cleanup(party, spawned);
        }
    }

    [Test]
    public async Task UplinkNeedsGateAndSignatureAndLocksTheCommsTower()
    {
        var (map, party) = await SetUp();
        var spawned = new List<EntityUid>();
        EntityUid comms = default;
        EntityUid marine = default;

        try
        {
            await Server.WaitAssertion(() =>
            {
                Server.System<BadBloodChainStageProbeSystem>().Reset();
                comms = BadBloodTestHelpers.SpawnUplinkSite(Server, BadBloodTestSiteKind.Comms, At(map, 10));
                spawned.Add(comms);
                marine = BadBloodTestHelpers.SpawnMarineNear(Server, At(map, 10), new Vector2(-1, 0));
                spawned.Add(marine);
            });

            await Pair.RunSeconds(2);

            await Server.WaitAssertion(() =>
            {
                var chains = Server.System<BadBloodObjectiveChainSystem>();
                var chain = Chain();
                Assert.That(chains.IsSiteWorking(comms), Is.True);

                chain.GateMet = false;
                chain.Signature = BadBloodSignature.Bracer;
                Assert.That(chains.CanUplink(marine, comms, out var reason), Is.False);
                Assert.That(reason, Is.EqualTo("cmu-bad-blood-obj-uplink-no-gate"));
                Assert.That(chains.TryStartUplink(marine, comms), Is.False);

                chain.GateMet = true;
                chain.Signature = BadBloodSignature.None;
                Assert.That(chains.CanUplink(marine, comms, out reason), Is.False);
                Assert.That(reason, Is.EqualTo("cmu-bad-blood-obj-uplink-no-signature"));

                chain.Signature = BadBloodSignature.Totem;
                Assert.That(chains.CanUplink(party.Grunts[0], comms, out reason), Is.False, "a Bad Blood cannot uplink");

                var site = SComp<BadBloodUplinkSiteComponent>(comms);
                Assert.That(site.UplinkTime, Is.EqualTo(TimeSpan.FromSeconds(90)));
                site.UplinkTime = TimeSpan.FromSeconds(1);
                Assert.That(chains.TryStartUplink(marine, comms), Is.True);
                Assert.That(chains.IsUplinked(), Is.False);
            });

            await Pair.RunSeconds(2);

            await Server.WaitAssertion(() =>
            {
                var chains = Server.System<BadBloodObjectiveChainSystem>();
                var probe = Server.System<BadBloodChainStageProbeSystem>();
                Assert.Multiple(() =>
                {
                    Assert.That(chains.IsUplinked(), Is.True);
                    Assert.That(chains.GetLockSite(), Is.EqualTo(comms));
                    Assert.That(probe.Count(BadBloodObjectiveStage.Uplink), Is.EqualTo(1));
                    Assert.That(probe.LastDetail(BadBloodObjectiveStage.Uplink), Is.EqualTo("Comms"));
                    Assert.That(chains.GetWorkingSites(), Does.Contain(comms));
                });

                Assert.That(chains.TryStartUplink(marine, comms), Is.False, "the uplink runs once");
            });
        }
        finally
        {
            await Cleanup(party, spawned);
        }
    }

    [Test]
    public async Task BracerTinkerLatchesAndTheInsertedBracerCannotBeScuttled()
    {
        var (map, party) = await SetUp();
        var spawned = new List<EntityUid>();
        EntityUid sensor = default;
        EntityUid marine = default;
        EntityUid bracer = default;
        EntityUid wirecutter = default;
        EntityUid wrench = default;

        try
        {
            await Server.WaitAssertion(() =>
            {
                Server.System<BadBloodChainStageProbeSystem>().Reset();
                sensor = BadBloodTestHelpers.SpawnUplinkSite(Server, BadBloodTestSiteKind.Sensor, At(map, 10));
                spawned.Add(sensor);
                marine = BadBloodTestHelpers.SpawnMarineNear(Server, At(map, 10), new Vector2(-1, 0));
                spawned.Add(marine);
                bracer = SEntMan.SpawnEntity(BadBloodBracer, At(map, 9));
                spawned.Add(bracer);
                wirecutter = SEntMan.SpawnEntity(Wirecutter, At(map, 9));
                spawned.Add(wirecutter);
                wrench = SEntMan.SpawnEntity(Wrench, At(map, 9));
                spawned.Add(wrench);
            });

            await Pair.RunSeconds(2);

            await Server.WaitAssertion(() =>
            {
                var chains = Server.System<BadBloodObjectiveChainSystem>();
                var scuttles = Server.System<BadBloodScuttleSystem>();
                Chain().BracerStepTime = TimeSpan.FromSeconds(1);

                Assert.That(chains.IsBadBloodBracer(bracer), Is.True);
                Assert.That(chains.IsSiteWorking(sensor), Is.True, "a sensor tower that is On is a working site");

                // a scuttle already counting down is stopped by the insert
                Assert.That(scuttles.TryStartScuttle(bracer), Is.True);
                Assert.That(chains.TryInsertBracer(party.Grunts[0], sensor, bracer), Is.False, "a Bad Blood cannot tinker");
                Assert.That(chains.TryInsertBracer(marine, sensor, bracer), Is.True);

                Assert.Multiple(() =>
                {
                    Assert.That(SEntMan.HasComponent<BadBloodScuttleComponent>(bracer), Is.False);
                    Assert.That(chains.IsInUplinkSite(bracer), Is.True);
                    Assert.That(scuttles.TryStartScuttle(bracer), Is.False, "a bracer in an uplink site cannot be scuttled");
                    Assert.That(SComp<BadBloodUplinkSiteComponent>(sensor).BracerStage, Is.EqualTo(1));
                });

                // wrong order: the wrench does nothing before the cut
                var hands = Server.System<SharedHandsSystem>();
                Assert.That(hands.TryPickupAnyHand(marine, wrench), Is.True);
                Assert.That(chains.TryTinkerStep(marine, sensor, wrench), Is.False);
                Assert.That(hands.TryDrop(marine, wrench), Is.True);

                Assert.That(hands.TryPickupAnyHand(marine, wirecutter), Is.True);
                Assert.That(chains.TryTinkerStep(marine, sensor, wirecutter), Is.True);
            });

            // an unskilled marine works tools slower than the base step time
            await Pair.RunSeconds(4);

            await Server.WaitAssertion(() =>
            {
                var chains = Server.System<BadBloodObjectiveChainSystem>();
                var hands = Server.System<SharedHandsSystem>();
                Assert.That(SComp<BadBloodUplinkSiteComponent>(sensor).BracerStage, Is.EqualTo(2));
                Assert.That(Chain().Signature, Is.EqualTo(BadBloodSignature.None));

                Assert.That(hands.TryDrop(marine, wirecutter), Is.True);
                Assert.That(hands.TryPickupAnyHand(marine, wrench), Is.True);
                Assert.That(chains.TryTinkerStep(marine, sensor, wrench), Is.True);
            });

            await Pair.RunSeconds(4);

            await Server.WaitAssertion(() =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(Chain().Signature, Is.EqualTo(BadBloodSignature.Bracer));
                    Assert.That(SEntMan.Deleted(bracer), Is.True, "the bracer is used up");
                    Assert.That(Server.System<BadBloodChainStageProbeSystem>().LastDetail(BadBloodObjectiveStage.Signature),
                        Is.EqualTo("Bracer"));
                });
            });
        }
        finally
        {
            await Cleanup(party, spawned);
        }
    }

    [Test]
    public async Task TotemHoldPausesAndIsKeptAcrossReconsecration()
    {
        var (map, party) = await SetUp();
        var spawned = new List<EntityUid>();
        EntityUid totem = default;
        var heldAtPause = TimeSpan.Zero;

        try
        {
            await Server.WaitAssertion(() =>
            {
                Assert.That(Chain().TotemHold, Is.EqualTo(TimeSpan.FromMinutes(10)), "the YAML default");
                Chain().TotemHold = TimeSpan.FromSeconds(4);

                totem = SEntMan.SpawnEntity(Totem, At(map, 30));
                spawned.Add(totem);
                Assert.That(SComp<BadBloodTotemComponent>(totem).Registered, Is.True);
                spawned.Add(BadBloodTestHelpers.SpawnMarineNear(Server, At(map, 30)));

                Server.System<BadBloodTotemSystem>().SetDesecrated((totem, SComp<BadBloodTotemComponent>(totem)), true);
            });

            await Pair.RunSeconds(2.5f);

            await Server.WaitAssertion(() =>
            {
                var chain = Chain();
                Assert.That(chain.TotemHeld.GetValueOrDefault(totem), Is.GreaterThan(TimeSpan.Zero));
                Assert.That(chain.Signature, Is.EqualTo(BadBloodSignature.None));
                heldAtPause = chain.TotemHeld[totem];

                Server.System<BadBloodTotemSystem>().SetDesecrated((totem, SComp<BadBloodTotemComponent>(totem)), false);
            });

            await Pair.RunSeconds(3);

            await Server.WaitAssertion(() =>
            {
                var chain = Chain();
                Assert.That(chain.TotemHeld[totem], Is.EqualTo(heldAtPause), "a reconsecrated Totem keeps the hold");
                Assert.That(chain.Signature, Is.EqualTo(BadBloodSignature.None));

                Server.System<BadBloodTotemSystem>().SetDesecrated((totem, SComp<BadBloodTotemComponent>(totem)), true);
            });

            await Pair.RunSeconds(4);

            await Server.WaitAssertion(() =>
            {
                Assert.That(Chain().Signature, Is.EqualTo(BadBloodSignature.Totem), "re-desecrating resumes the hold");
            });
        }
        finally
        {
            await Cleanup(party, spawned);
        }
    }

    [Test]
    public async Task StarChartLatchesTheLodgeSignatureOnce()
    {
        var (map, party) = await SetUp();
        var spawned = new List<EntityUid>();

        try
        {
            await Server.WaitAssertion(() =>
            {
                var chains = Server.System<BadBloodObjectiveChainSystem>();
                var probe = Server.System<BadBloodChainStageProbeSystem>();
                probe.Reset();

                var marine = BadBloodTestHelpers.SpawnMarineNear(Server, At(map, 10));
                spawned.Add(marine);
                var chart = SEntMan.SpawnEntity(null, At(map, 10));
                spawned.Add(chart);

                // no Lodge stands, so there is nothing revealed to survey
                Assert.That(chains.CanSurvey(marine, out var reason), Is.False);
                Assert.That(reason, Is.EqualTo("cmu-bad-blood-obj-survey-hidden"));

                var recovered = new BadBloodStarChartRecoveredEvent(chart, marine);
                SEntMan.EventBus.RaiseEvent(EventSource.Local, ref recovered);
                Assert.That(Chain().Signature, Is.EqualTo(BadBloodSignature.Lodge));

                // a second avenue does not change or re-announce it
                SEntMan.EventBus.RaiseEvent(EventSource.Local, ref recovered);
                Assert.That(Chain().Signature, Is.EqualTo(BadBloodSignature.Lodge));
                Assert.That(probe.Count(BadBloodObjectiveStage.Signature), Is.EqualTo(1));
            });
        }
        finally
        {
            await Cleanup(party, spawned);
        }
    }

    // NEW-5: a tower holding a half-tinkered bracer that breaks down is still repaired by the RMC handler, and NEW-6:
    // a signature latched another way drops the stranded bracer at its site and leaves the site to the RMC handlers
    [Test]
    public async Task BrokenSiteWithABracerIsStillRepairedAndALaterSignatureEjectsTheBracer()
    {
        var (map, party) = await SetUp();
        var spawned = new List<EntityUid>();
        EntityUid sensor = default;
        EntityUid marine = default;
        EntityUid bracer = default;
        EntityUid wrench = default;

        try
        {
            await Server.WaitAssertion(() =>
            {
                sensor = BadBloodTestHelpers.SpawnUplinkSite(Server, BadBloodTestSiteKind.Sensor, At(map, 10));
                spawned.Add(sensor);
                marine = BadBloodTestHelpers.SpawnMarineNear(Server, At(map, 10), new Vector2(-1, 0));
                spawned.Add(marine);
                bracer = SEntMan.SpawnEntity(BadBloodBracer, At(map, 9));
                spawned.Add(bracer);
                wrench = SEntMan.SpawnEntity(Wrench, At(map, 9));
                spawned.Add(wrench);
            });

            await Pair.RunSeconds(2);

            await Server.WaitAssertion(() =>
            {
                var chains = Server.System<BadBloodObjectiveChainSystem>();
                Assert.That(chains.TryInsertBracer(marine, sensor, bracer), Is.True);

                // the tower breaks down to its wrench stage with the bracer still in it
                Server.System<SensorTowerSystem>().SensorTowerIncrementalDestroy((sensor, SComp<SensorTowerComponent>(sensor)));
                Assert.That(SComp<SensorTowerComponent>(sensor).State, Is.EqualTo(SensorTowerState.Wrench));
                Assert.That(chains.IsSiteWorking(sensor), Is.False);

                Server.System<SkillsSystem>().SetSkill(marine, EngineerSkill, 2);
                Assert.That(Server.System<SharedHandsSystem>().TryPickupAnyHand(marine, wrench), Is.True);

                var use = new InteractUsingEvent(marine, wrench, sensor, At(map, 10));
                SEntMan.EventBus.RaiseLocalEvent(sensor, use);
                Assert.That(use.Handled, Is.True, "the RMC repair took the wrench");
                Assert.That(SComp<BadBloodUplinkSiteComponent>(sensor).BracerStage, Is.EqualTo(1),
                    "the bracer step was not touched");
            });

            // the RMC wrench repair is 15 s, faster for a trained engineer
            await Pair.RunSeconds(18);

            await Server.WaitAssertion(() =>
            {
                Assert.That(SComp<SensorTowerComponent>(sensor).State, Is.EqualTo(SensorTowerState.Off),
                    "the RMC repair finished");

                // a Lodge signature latches while the bracer is still in the tower
                var chart = SEntMan.SpawnEntity(null, At(map, 12));
                spawned.Add(chart);
                var recovered = new BadBloodStarChartRecoveredEvent(chart, marine);
                SEntMan.EventBus.RaiseEvent(EventSource.Local, ref recovered);

                var chains = Server.System<BadBloodObjectiveChainSystem>();
                Assert.Multiple(() =>
                {
                    Assert.That(Chain().Signature, Is.EqualTo(BadBloodSignature.Lodge));
                    Assert.That(SEntMan.Deleted(bracer), Is.False, "the stranded bracer is dropped, not deleted");
                    Assert.That(chains.IsInUplinkSite(bracer), Is.False, "the slot is emptied");
                    Assert.That(SComp<BadBloodUplinkSiteComponent>(sensor).BracerStage, Is.EqualTo(0));
                });

                // after the signature a bracer use on a site is not taken over (an On tower does nothing with it)
                SComp<SensorTowerComponent>(sensor).State = SensorTowerState.On;
                var insert = new InteractUsingEvent(marine, bracer, sensor, At(map, 10));
                SEntMan.EventBus.RaiseLocalEvent(sensor, insert);
                Assert.That(insert.Handled, Is.False);
                Assert.That(chains.IsInUplinkSite(bracer), Is.False);
            });
        }
        finally
        {
            await Cleanup(party, spawned);
        }
    }

    // a planet map with a floor strip, and a running Bad Blood rule whose party stands at the west end
    private async Task<(TestMapData Map, BadBloodTestParty Party)> SetUp()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitPost(() =>
        {
            SEntMan.EnsureComponent<RMCPlanetComponent>(map.MapUid);
            var maps = Server.System<SharedMapSystem>();
            for (var x = -2; x <= 40; x++)
            {
                maps.SetTile(map.Grid, map.Tile.GridIndices + new Vector2i(x, 0), map.Tile.Tile);
            }
        });

        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1);
        return (map, party);
    }

    private static EntityCoordinates At(TestMapData map, int x)
    {
        return map.GridCoords.Offset(new Vector2(x, 0));
    }

    // call on the server thread
    private BadBloodObjectiveChainComponent Chain()
    {
        Assert.That(Server.System<BadBloodObjectiveChainSystem>().TryGetChain(out var chain), Is.True,
            "the Bad Blood rule carries the objective chain");
        return chain.Comp;
    }

    private async Task Cleanup(BadBloodTestParty party, List<EntityUid> spawned)
    {
        await Server.WaitPost(() =>
        {
            for (var i = spawned.Count - 1; i >= 0; i--)
            {
                if (SEntMan.EntityExists(spawned[i]))
                    SEntMan.DeleteEntity(spawned[i]);
            }
        });

        await BadBloodTestHelpers.EndBadBloodRule(Pair, party);
        await Server.WaitPost(() =>
        {
            foreach (var uid in party.All)
            {
                if (SEntMan.EntityExists(uid))
                    SEntMan.DeleteEntity(uid);
            }
        });
    }
}

// records the objective-chain stage broadcasts for the tests. global state on a pooled server: call Reset first
public sealed class BadBloodChainStageProbeSystem : EntitySystem
{
    private readonly List<(BadBloodObjectiveStage Stage, string Detail)> _stages = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<BadBloodObjectiveStageEvent>(OnStage);
    }

    public void Reset()
    {
        _stages.Clear();
    }

    public int Count(BadBloodObjectiveStage stage)
    {
        return _stages.Count(s => s.Stage == stage);
    }

    public string? LastDetail(BadBloodObjectiveStage stage)
    {
        for (var i = _stages.Count - 1; i >= 0; i--)
        {
            if (_stages[i].Stage == stage)
                return _stages[i].Detail;
        }

        return null;
    }

    private void OnStage(ref BadBloodObjectiveStageEvent ev)
    {
        _stages.Add((ev.Stage, ev.Detail));
    }
}
