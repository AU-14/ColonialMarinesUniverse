#pragma warning disable RA0002 // Inspect public-operation outcomes and configure isolated test anatomy.
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Pair;
using Content.Server.CMU14.Medical.Diagnostics;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Medical.Scanner;
using Content.Shared.Body;
using Content.Shared.CMU14.Medical.Anatomy.Organs;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Events;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Heart;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Lungs;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Diagnostics;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Utility;
using ClientPopupSystem = Content.Client.Popups.PopupSystem;
using ClientVerbSystem = Content.Client.Verbs.VerbSystem;

namespace Content.IntegrationTests.CMU14.Medical.Diagnostics;

[TestFixture]
public sealed class StethoscopeInteractionContractTest
{
    [TestCase("held", 1)]
    [TestCase("held", 2)]
    [TestCase("worn", 1)]
    [TestCase("worn", 2)]
    [TestCase("innate", 2)]
    public async Task RealEntryPointsReportCurrentOrgansOnceAfterTheExamination(string route, int skill)
    {
        await WithPatient(route != "held", skill, async (pair, scene) =>
        {
            await Start(pair, scene, route);
            await pair.Server.WaitAssertion(() =>
            {
                AssertPending(scene);
                // Injure after the menu/interaction: a previously cached healthy
                // aggregate readout must not be used at completion.
                InjureOrgan<HeartComponent>(scene.Entities, scene.Patient, OrganDamageStage.Dead);
                InjureOrgan<LungsComponent>(scene.Entities, scene.Patient, OrganDamageStage.Damaged);
            });
            await pair.Client.WaitAssertion(() => Assert.That(Results(pair, scene), Is.Empty));
            await pair.RunTicksSync(pair.SecondsToTicks(3));
            await pair.Server.WaitAssertion(() => Assert.That(scene.Entities.HasComponent<CMUStethoscopeExaminationComponent>(scene.Medic), Is.False));
            await pair.Client.WaitAssertion(() =>
            {
                var results = Results(pair, scene);
                Assert.That(results, Has.Count.EqualTo(1));
                Assert.That(results[0].Tooltip, Is.EqualTo(route != "held"));
                Assert.That(results[0].Text, Does.Contain(Loc.GetString("cmu-medical-stethoscope-no-pulse")));
                Assert.That(results[0].Text, Does.Contain(skill >= 2
                    ? Loc.GetString("cmu-medical-stethoscope-lungs-precise", ("stage", $"{0.6f:F2}"))
                    : Loc.GetString("cmu-medical-stethoscope-lungs-qualitative", ("description", "wet"))));
                AssertNoLocalFallback(pair, scene);
            });
        });
    }

    [TestCase("drop")]
    [TestCase("remove worn")]
    [TestCase("skill")]
    [TestCase("replace tool")]
    [TestCase("replace patient marker")]
    [TestCase("replace patient body")]
    [TestCase("queue patient")]
    [TestCase("queue tool")]
    [TestCase("range")]
    [TestCase("cancel")]
    [TestCase("disable")]
    public async Task InterruptedExaminationPublishesNothingAndDoesNotFallBack(string interruption)
    {
        var worn = interruption == "remove worn";
        await WithPatient(worn, 2, async (pair, scene) =>
        {
            await Start(pair, scene, worn ? "worn" : "held");
            await pair.Server.WaitAssertion(() =>
            {
                AssertPending(scene);
                var entities = scene.Entities;
                switch (interruption)
                {
                    case "drop":
                        Assert.That(entities.System<SharedHandsSystem>().TryDrop(scene.Medic, scene.Tool), Is.True);
                        break;
                    case "remove worn":
                        Assert.That(entities.System<InventorySystem>().TryUnequip(scene.Medic, "neck", silent: true, force: true), Is.True);
                        break;
                    case "skill":
                        entities.System<SkillsSystem>().SetSkill(scene.Medic, "RMCSkillMedical", 0);
                        break;
                    case "replace tool":
                        entities.RemoveComponent<RMCStethoscopeComponent>(scene.Tool);
                        entities.AddComponent<RMCStethoscopeComponent>(scene.Tool);
                        break;
                    case "replace patient marker":
                        entities.RemoveComponent<CMUHumanMedicalComponent>(scene.Patient);
                        entities.AddComponent<CMUHumanMedicalComponent>(scene.Patient);
                        break;
                    case "replace patient body":
                        entities.RemoveComponent<BodyComponent>(scene.Patient);
                        entities.AddComponent<BodyComponent>(scene.Patient);
                        break;
                    case "queue patient":
                        entities.QueueDeleteEntity(scene.Patient);
                        break;
                    case "queue tool":
                        entities.QueueDeleteEntity(scene.Tool);
                        break;
                    case "range":
                        entities.System<SharedTransformSystem>().SetCoordinates(scene.Patient,
                            new EntityCoordinates(scene.Coordinates.EntityId, scene.Coordinates.Position + new Vector2(10, 0)));
                        break;
                    case "cancel":
                        var active = entities.GetComponent<DoAfterComponent>(scene.Medic).DoAfters.Values
                            .Single(value => value.Args.Event is CMUStethoscopeDoAfterEvent && !value.Completed && !value.Cancelled);
                        entities.System<SharedDoAfterSystem>().Cancel(scene.Medic, active.Index);
                        break;
                    case "disable":
                        pair.Server.ResolveDependency<IConfigurationManager>().SetCVar(CMUMedicalCCVars.DiagnosticsEnabled, false);
                        break;
                }
            });
            await pair.RunTicksSync(pair.SecondsToTicks(3));
            await pair.Server.WaitAssertion(() => Assert.That(scene.Entities.HasComponent<CMUStethoscopeExaminationComponent>(scene.Medic), Is.False));
            await pair.Client.WaitAssertion(() =>
            {
                Assert.That(Results(pair, scene), Is.Empty);
                AssertNoLocalFallback(pair, scene);
            });
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task TemporarilyLosingTheWornToolOrSkillCancelsTheOriginalAttempt(bool loseSkill)
    {
        await WithPatient(true, 2, async (pair, scene) =>
        {
            await Start(pair, scene, "worn");
            await pair.Server.WaitAssertion(() =>
            {
                AssertPending(scene);
                if (loseSkill)
                    scene.Entities.System<SkillsSystem>().SetSkill(scene.Medic, "RMCSkillMedical", 0);
                else
                    Assert.That(scene.Entities.System<InventorySystem>()
                        .TryUnequip(scene.Medic, "neck", silent: true, force: true), Is.True);
            });
            // Restore availability well before the original deadline. Completion
            // validation alone would let that interrupted examination succeed.
            await pair.RunTicksSync(3);
            await pair.Server.WaitAssertion(() =>
            {
                Assert.That(scene.Entities.HasComponent<CMUStethoscopeExaminationComponent>(scene.Medic), Is.False);
                if (loseSkill)
                    scene.Entities.System<SkillsSystem>().SetSkill(scene.Medic, "RMCSkillMedical", 2);
                else
                    Assert.That(scene.Entities.System<InventorySystem>()
                        .TryEquip(scene.Medic, scene.Tool, "neck", silent: true, force: true), Is.True);
            });
            await pair.RunTicksSync(pair.SecondsToTicks(3));
            await pair.Client.WaitAssertion(() => Assert.That(Results(pair, scene), Is.Empty));
            await Start(pair, scene, "worn");
            await pair.Server.WaitAssertion(() => AssertPending(scene));
            await pair.RunTicksSync(pair.SecondsToTicks(3));
            await pair.Client.WaitAssertion(() => Assert.That(Results(pair, scene), Has.Count.EqualTo(1)));
        });
    }

    [TestCase("held", false)]
    [TestCase("worn", false)]
    [TestCase("held", true)]
    [TestCase("innate", true)]
    public async Task DisabledOrNonCmuPatientUsesOneImmediateRmcFallback(string route, bool nonCmu)
    {
        await WithPatient(route != "held", 2, async (pair, scene) =>
        {
            await pair.Server.WaitAssertion(() =>
            {
                if (nonCmu)
                    scene.Entities.RemoveComponent<CMUHumanMedicalComponent>(scene.Patient);
                else
                    pair.Server.ResolveDependency<IConfigurationManager>().SetCVar(CMUMedicalCCVars.DiagnosticsEnabled, false);
            });
            await pair.RunUntilSynced();
            await Start(pair, scene, route);
            await pair.RunTicksSync(8);
            await pair.Server.WaitAssertion(() => Assert.That(scene.Entities.HasComponent<CMUStethoscopeExaminationComponent>(scene.Medic), Is.False));
            await pair.Client.WaitAssertion(() =>
            {
                var results = Results(pair, scene);
                Assert.That(results, Has.Count.EqualTo(1));
                Assert.That(results[0].Text, Is.EqualTo(scene.Fallback));
                Assert.That(results[0].Tooltip, Is.EqualTo(route != "held"));
            });
        });
    }

    [Test]
    public async Task StaleNetworkVerbCannotSelectAReplacementTool()
    {
        await WithPatient(true, 2, async (pair, scene) =>
        {
            Verb oldVerb = default!;
            await pair.Client.WaitAssertion(() => oldVerb = GetVerb(pair, scene, "worn"));
            await pair.Server.WaitAssertion(() =>
            {
                var entities = scene.Entities;
                Assert.That(entities.System<InventorySystem>().TryUnequip(scene.Medic, "neck", silent: true, force: true), Is.True);
                var replacement = entities.SpawnEntity("ClothingNeckStethoscope", scene.Coordinates);
                scene.Cleanup.Add(replacement);
                Assert.That(entities.System<InventorySystem>().TryEquip(scene.Medic, replacement, "neck", silent: true, force: true), Is.True);
            });
            // Send the saved menu command through the real authenticated client
            // verb API. Server re-resolution must fail the old IconEntity identity.
            await pair.Client.WaitPost(() => pair.Client.EntMan.System<ClientVerbSystem>().ExecuteVerb(scene.PatientNet, oldVerb));
            await pair.RunTicksSync(pair.SecondsToTicks(3));
            await pair.Server.WaitAssertion(() => Assert.That(scene.Entities.HasComponent<CMUStethoscopeExaminationComponent>(scene.Medic), Is.False));
            await pair.Client.WaitAssertion(() => Assert.That(Results(pair, scene), Is.Empty));
        });
    }

    [Test]
    public async Task SimultaneousExamineAndInheritedListeningShareOneAttempt()
    {
        await WithPatient(true, 2, async (pair, scene) =>
        {
            await pair.Client.WaitPost(() =>
            {
                var verbs = pair.Client.EntMan.System<ClientVerbSystem>();
                verbs.ExecuteVerb(scene.PatientNet, GetVerb(pair, scene, "worn"));
                verbs.ExecuteVerb(scene.PatientNet, GetVerb(pair, scene, "innate"));
            });
            await pair.RunTicksSync(8);
            await pair.Server.WaitAssertion(() => AssertPending(scene));
            await pair.RunTicksSync(pair.SecondsToTicks(3));
            await pair.Client.WaitAssertion(() => Assert.That(Results(pair, scene), Has.Count.EqualTo(1)));
        });
    }

    [Test]
    public async Task ReplacingDoAfterStateAllowsAFreshExaminationWithoutCompletingTheOldOne()
    {
        await WithPatient(false, 2, async (pair, scene) =>
        {
            await Start(pair, scene, "held");
            await pair.Server.WaitAssertion(() =>
            {
                AssertPending(scene);
                scene.Entities.RemoveComponent<DoAfterComponent>(scene.Medic);
                scene.Entities.AddComponent<DoAfterComponent>(scene.Medic);
            });
            await Start(pair, scene, "held");
            await pair.Server.WaitAssertion(() => AssertPending(scene));
            await pair.RunTicksSync(pair.SecondsToTicks(3));
            await pair.Client.WaitAssertion(() => Assert.That(Results(pair, scene), Has.Count.EqualTo(1)));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task UnskilledGetsOneDenialWhileOutOfRangeGetsNoClinicalDetails(bool outOfRange)
    {
        await WithPatient(false, outOfRange ? 2 : 0, async (pair, scene) =>
        {
            await pair.Server.WaitAssertion(() =>
            {
                if (outOfRange)
                    scene.Entities.System<SharedTransformSystem>().SetCoordinates(scene.Patient,
                        new EntityCoordinates(scene.Coordinates.EntityId, scene.Coordinates.Position + new Vector2(10, 0)));
                // Even a caller supplying canReach=true must pass current server range checks.
                scene.Entities.System<SharedInteractionSystem>().InteractDoAfter(scene.Medic, scene.Tool, scene.Patient,
                    scene.Coordinates, canReach: true);
                Assert.That(scene.Entities.HasComponent<CMUStethoscopeExaminationComponent>(scene.Medic), Is.False);
            });
            await pair.RunTicksSync(pair.SecondsToTicks(3));
            await pair.Client.WaitAssertion(() =>
            {
                var results = Results(pair, scene);
                Assert.That(results, Has.Count.EqualTo(outOfRange ? 0 : 1));
                if (!outOfRange)
                    Assert.That(results[0].Text, Is.EqualTo(Loc.GetString("rmc-stethoscope-unskilled")));
            });
        });
    }

    private static async Task Start(TestPair pair, Scene scene, string route)
    {
        if (route == "held")
        {
            await pair.Client.WaitAssertion(() =>
            {
                var entities = pair.Client.EntMan;
                var medic = entities.GetEntity(scene.MedicNet);
                var patient = entities.GetEntity(scene.PatientNet);
                entities.System<SharedInteractionSystem>().InteractDoAfter(medic, entities.GetEntity(scene.ToolNet), patient,
                    entities.GetComponent<TransformComponent>(patient).Coordinates, canReach: true);
                AssertNoLocalFallback(pair, scene);
            });
            await pair.Server.WaitPost(() => scene.Entities.System<SharedInteractionSystem>().InteractDoAfter(scene.Medic,
                scene.Tool, scene.Patient, scene.Coordinates, canReach: true));
        }
        else
        {
            await pair.Client.WaitPost(() => pair.Client.EntMan.System<ClientVerbSystem>()
                .ExecuteVerb(scene.PatientNet, GetVerb(pair, scene, route)));
            await pair.RunTicksSync(8);
        }
    }

    private static Verb GetVerb(TestPair pair, Scene scene, string route)
    {
        var entities = pair.Client.EntMan;
        return entities.System<SharedVerbSystem>().GetLocalVerbs(entities.GetEntity(scene.PatientNet),
                entities.GetEntity(scene.MedicNet), route == "innate" ? typeof(InnateVerb) : typeof(ExamineVerb))
            .Single(verb => verb.Text == Loc.GetString(route == "innate" ? "stethoscope-verb" : "rmc-stethoscope-verb-text"));
    }

    private static void AssertPending(Scene scene)
    {
        Assert.That(scene.Entities.HasComponent<CMUStethoscopeExaminationComponent>(scene.Medic), Is.True);
        var pending = scene.Entities.GetComponent<DoAfterComponent>(scene.Medic).DoAfters.Values
            .Where(value => !value.Cancelled && !value.Completed).ToArray();
        Assert.That(pending, Has.Length.EqualTo(1));
        Assert.That(pending[0].Args.Event, Is.TypeOf<CMUStethoscopeDoAfterEvent>());
        Assert.That(pending[0].Args.Target, Is.EqualTo(scene.Patient));
        Assert.That(pending[0].Args.Used, Is.EqualTo(scene.Tool));
    }

    private static void InjureOrgan<T>(IEntityManager entities, EntityUid patient, OrganDamageStage stage) where T : Component
    {
        Assert.That(entities.System<CMUMedicalBodyIndexSystem>().TryGetOrgan<T>(patient, out var organ), Is.True);
        var health = entities.GetComponent<OrganHealthComponent>(organ);
        var ev = new OrganDamagedEvent(patient, organ,
            new DamageSpecifier { DamageDict = { ["Blunt"] = health.Current - health.StageThresholds[stage] } }, OrganDamageSource.Direct);
        entities.EventBus.RaiseLocalEvent(organ, ref ev, broadcast: true);
        Assert.That(health.Stage, Is.EqualTo(stage));
    }

    private static List<StethoscopeNetworkOutput> Results(TestPair pair, Scene scene)
        => pair.Client.EntMan.System<StethoscopeNetworkProbeSystem>().Outputs
            .Where(value => value.Target == scene.PatientNet && (value.Text == scene.Fallback || value.Text == Loc.GetString("rmc-stethoscope-unskilled") ||
                value.Text.Contains("Lungs", StringComparison.Ordinal) || value.Text.Contains("lungs", StringComparison.Ordinal)))
            .ToList();

    private static void AssertNoLocalFallback(TestPair pair, Scene scene)
        => Assert.That(pair.Client.EntMan.System<ClientPopupSystem>().WorldLabels.Any(label => label.Text == scene.Fallback), Is.False);

    private static async Task WithPatient(bool worn, int skill, Func<TestPair, Scene, Task> test)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var map = await pair.CreateTestMap();
        var entities = pair.Server.EntMan;
        var player = pair.Player!;
        var originalActor = player.AttachedEntity;
        var cfg = pair.Server.ResolveDependency<IConfigurationManager>();
        var enabled = cfg.GetCVar(CMUMedicalCCVars.Enabled);
        var diagnostics = cfg.GetCVar(CMUMedicalCCVars.DiagnosticsEnabled);
        var scene = new Scene { Entities = entities, Coordinates = map.GridCoords };
        try
        {
            await pair.Server.WaitAssertion(() =>
            {
                cfg.SetCVar(CMUMedicalCCVars.Enabled, true);
                cfg.SetCVar(CMUMedicalCCVars.DiagnosticsEnabled, true);
                scene.Patient = entities.SpawnEntity("CMMobHuman", map.GridCoords);
                scene.Medic = entities.SpawnEntity("CMMobHuman", map.GridCoords);
                scene.Tool = entities.SpawnEntity("ClothingNeckStethoscope", map.GridCoords);
                scene.Cleanup.AddRange([scene.Patient, scene.Medic, scene.Tool]);
                entities.System<SkillsSystem>().SetSkill(scene.Medic, "RMCSkillMedical", skill);
                if (worn)
                    Assert.That(entities.System<InventorySystem>().TryEquip(scene.Medic, scene.Tool, "neck", silent: true, force: true), Is.True);
                else
                    Assert.That(entities.System<SharedHandsSystem>().TryPickupAnyHand(scene.Medic, scene.Tool), Is.True);
                pair.Server.PlayerMan.SetAttachedEntity(player, scene.Medic);
                scene.PatientNet = entities.GetNetEntity(scene.Patient);
                scene.MedicNet = entities.GetNetEntity(scene.Medic);
                scene.ToolNet = entities.GetNetEntity(scene.Tool);
                var fallback = new FormattedMessage();
                fallback.AddMarkupOrThrow(Loc.GetString("rmc-stethoscope-normal", ("target", scene.Patient)));
                scene.Fallback = fallback.ToString();
            });
            await pair.RunUntilSynced();
            await pair.Client.WaitPost(() => pair.Client.EntMan.System<StethoscopeNetworkProbeSystem>().Outputs.Clear());
            await test(pair, scene);
        }
        finally
        {
            await pair.Server.WaitPost(() =>
            {
                pair.Server.PlayerMan.SetAttachedEntity(player, originalActor);
                cfg.SetCVar(CMUMedicalCCVars.Enabled, enabled);
                cfg.SetCVar(CMUMedicalCCVars.DiagnosticsEnabled, diagnostics);
                foreach (var uid in scene.Cleanup)
                    if (entities.EntityExists(uid)) entities.DeleteEntity(uid);
            });
            await pair.RunUntilSynced();
            await pair.Client.WaitPost(() => pair.Client.EntMan.System<StethoscopeNetworkProbeSystem>().Outputs.Clear());
        }
        await pair.CleanReturnAsync();
    }

    private sealed class Scene
    {
        public IEntityManager Entities = default!;
        public EntityCoordinates Coordinates;
        public EntityUid Medic, Patient, Tool;
        public NetEntity MedicNet, PatientNet, ToolNet;
        public string Fallback = string.Empty;
        public readonly List<EntityUid> Cleanup = new();
    }
}

public readonly record struct StethoscopeNetworkOutput(NetEntity Target, string Text, bool Tooltip);

public sealed partial class StethoscopeNetworkProbeSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;
    public readonly List<StethoscopeNetworkOutput> Outputs = new();

    public override void Initialize()
    {
        if (!_net.IsClient) return;
        SubscribeNetworkEvent<PopupEntityEvent>(OnPopup);
        SubscribeNetworkEvent<ExamineSystemMessages.ExamineInfoResponseMessage>(OnExamine);
    }

    private void OnPopup(PopupEntityEvent args) => Outputs.Add(new(args.Uid, args.Message, false));
    private void OnExamine(ExamineSystemMessages.ExamineInfoResponseMessage args)
        => Outputs.Add(new(args.EntityUid, args.Message.ToString(), true));
}
