using System.Linq;
using System.Numerics;
using Content.Server.CMU14.Yautja;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Yautja;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Yautja;

// full limb trophy chain like an actual round: butcher a real corpse -> DetachedBody carrier
// -> dagger -> cauldron -> bone. old tests just fed the cauldron a bare part from nullspace,
// which severance never actually produces. that's how this stayed broken
[TestFixture]
public sealed class YautjaLimbTrophyPipelineTest
{
    private static readonly (YautjaButcherProcedure Procedure, BodyPartType Type, BodyPartSymmetry Symmetry, YautjaTrophyKind Kind, string Bone)[] Limbs =
    [
        (YautjaButcherProcedure.LeftArm, BodyPartType.Arm, BodyPartSymmetry.Left, YautjaTrophyKind.HumanLeftArmBone, "CMUYautjaHumanLeftArmBoneTrophy"),
        (YautjaButcherProcedure.RightLeg, BodyPartType.Leg, BodyPartSymmetry.Right, YautjaTrophyKind.HumanRightLegBone, "CMUYautjaHumanRightLegBoneTrophy"),
        (YautjaButcherProcedure.LeftHand, BodyPartType.Hand, BodyPartSymmetry.Left, YautjaTrophyKind.HumanLeftHandBone, "CMUYautjaHumanLeftHandBoneTrophy"),
        (YautjaButcherProcedure.Head, BodyPartType.Head, BodyPartSymmetry.None, YautjaTrophyKind.HumanSkull, "CMUYautjaHumanSkullTrophy"),
    ];

    [Test]
    [TestCaseSource(nameof(LimbCases))]
    public async Task ButcheredLimbBecomesBoneTrophyThroughDaggerAndCauldron(int index)
    {
        var limb = Limbs[index];

        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid victim = default;
        EntityUid dagger = default;
        EntityUid cauldron = default;
        EntityUid carrier = default;
        var victimName = string.Empty;

        try
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
                victim = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
                cauldron = entMan.SpawnEntity("CMUYautjaStructureYautjaMachinesVat", map.GridCoords.Offset(new Vector2(0, 1)));
                entMan.System<MobStateSystem>().ChangeMobState(victim, MobState.Dead);
                victimName = entMan.GetComponent<MetaDataComponent>(victim).EntityName;

                Assert.That(entMan.System<YautjaTrophySystem>().TryStartButcher(hunter, victim, limb.Procedure), Is.True);
            });

            // 9s butcher doafter plus a bit of slack
            await pair.RunTicksSync(pair.SecondsToTicks(13));

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var index2 = entMan.System<CMUMedicalBodyIndexSystem>();
                var body = entMan.System<SharedBodySystem>();

                Assert.That(index2.TryGetBodyPart(victim, new CMUMedicalBodyPartKey(limb.Type, limb.Symmetry), out _), Is.False,
                    "Butchering a limb must actually sever it from the corpse.");

                carrier = FindCarrier(entMan, body, limb.Type, limb.Symmetry);
                Assert.That(carrier, Is.Not.EqualTo(EntityUid.Invalid),
                    "Severance must drop the limb as a DetachedBody carrier.");
                Assert.That(entMan.GetComponent<MetaDataComponent>(carrier).EntityName, Does.Contain(victimName),
                    "A butchered limb keeps its victim's name, as CMSS13 names limbs \"<owner>'s <limb>\".");

                // severing flings the limb and on this tiny grid it slides forever, so just pick it up
                // like a player would. dagger in the active hand, limb in the other
                var hands = entMan.System<SharedHandsSystem>();
                dagger = entMan.SpawnEntity("CMUYautjaCeremonialDagger", map.GridCoords);
                Assert.That(hands.TryPickupAnyHand(hunter, dagger), Is.True);
                Assert.That(hands.TryPickupAnyHand(hunter, carrier, checkActionBlocker: false), Is.True,
                    "A severed limb carrier must be an item a hunter can pick up.");

                var flay = new AfterInteractEvent(hunter, dagger, carrier, map.GridCoords, true);
                entMan.EventBus.RaiseLocalEvent(dagger, flay);
                Assert.That(flay.Handled, Is.True, "The ceremonial dagger must accept a severed limb carrier.");
            });

            await pair.RunTicksSync(pair.SecondsToTicks(3));

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                Assert.That(entMan.HasComponent<YautjaFlayedComponent>(carrier), Is.True,
                    "The dagger's limb flay must finish on the carrier the hunter is holding.");

                var interact = new InteractUsingEvent(
                    hunter,
                    carrier,
                    cauldron,
                    entMan.GetComponent<TransformComponent>(cauldron).Coordinates);
                entMan.EventBus.RaiseLocalEvent(cauldron, interact);

                Assert.That(entMan.TryGetComponent(hunter, out DoAfterComponent? doAfter) &&
                            doAfter.DoAfters.Values.Any(active =>
                                !active.Cancelled &&
                                !active.Completed &&
                                active.Args.Event is YautjaCauldronBoilDoAfterEvent),
                    Is.True,
                    "The cauldron must start boiling a flayed severed limb.");
            });

            await pair.RunTicksSync(pair.SecondsToTicks(15.5f));

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var bones = entMan.EntityQuery<YautjaTrophyComponent, MetaDataComponent>()
                    .Where(t => t.Item1.Kind == limb.Kind)
                    .ToList();

                Assert.That(bones, Has.Count.EqualTo(1), "Exactly one bone trophy comes out of the cauldron.");
                var (trophy, meta) = bones.Single();
                var record = entMan.GetComponent<YautjaTrophyRecordComponent>(hunter);

                Assert.Multiple(() =>
                {
                    Assert.That(meta.EntityPrototype?.ID, Is.EqualTo(limb.Bone));
                    Assert.That(trophy.Hunter, Is.EqualTo(hunter));
                    Assert.That(trophy.SourceName, Does.Contain(victimName),
                        "The bone remembers whose limb it was.");
                    Assert.That(entMan.Deleted(carrier) || entMan.IsQueuedForDeletion(carrier), Is.True,
                        "The boiled limb (and every part carried with it) is consumed.");
                    if (limb.Kind == YautjaTrophyKind.HumanSkull)
                        Assert.That(record.HumanSkulls, Is.EqualTo(1), "A boiled skull counts on the hunter's trophy record.");
                    else
                        Assert.That(record.HumanBones, Is.EqualTo(1), "A boiled bone counts on the hunter's trophy record.");
                });
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                foreach (var uid in new[] { hunter, victim, dagger, cauldron, carrier })
                {
                    if (uid != default && entMan.EntityExists(uid) && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }

                var query = entMan.EntityQueryEnumerator<YautjaTrophyComponent>();
                while (query.MoveNext(out var uid, out _))
                    entMan.DeleteEntity(uid);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FinishedSkinButcheryLeavesTheVictimsGearBehind()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid victim = default;
        EntityUid uniform = default;

        try
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
                victim = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
                uniform = entMan.SpawnEntity("CMJumpsuitSPP", map.GridCoords);
                Assert.That(entMan.System<InventorySystem>().TryEquip(victim, uniform, "jumpsuit", force: true), Is.True);
                entMan.System<MobStateSystem>().ChangeMobState(victim, MobState.Dead);
            });

            // 4 skin stages, 7 / 6.5 / 7 / 9s
            foreach (var seconds in new[] { 7f, 6.5f, 7f, 9f })
            {
                await server.WaitAssertion(() =>
                {
                    Assert.That(server.EntMan.System<YautjaTrophySystem>()
                        .TryStartButcher(hunter, victim, YautjaButcherProcedure.Skin), Is.True);
                });
                await pair.RunTicksSync(pair.SecondsToTicks(seconds + 0.5f));
            }

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                Assert.Multiple(() =>
                {
                    Assert.That(entMan.Deleted(victim) || entMan.IsQueuedForDeletion(victim), Is.True,
                        "The final skin stage consumes the corpse.");
                    Assert.That(entMan.EntityExists(uniform) && !entMan.IsQueuedForDeletion(uniform), Is.True,
                        "Butchering a corpse must not destroy what it was wearing.");
                });
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                foreach (var uid in new[] { hunter, victim, uniform })
                {
                    if (uid != default && entMan.EntityExists(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    private static IEnumerable<int> LimbCases() => Enumerable.Range(0, Limbs.Length);

    private static EntityUid FindCarrier(IEntityManager entMan, SharedBodySystem body, BodyPartType type, BodyPartSymmetry symmetry)
    {
        var query = entMan.EntityQueryEnumerator<BodyComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out var bodyComp, out var meta))
        {
            if (meta.EntityPrototype?.ID != "DetachedBody" ||
                body.GetRootPartOrNull(uid, bodyComp) is not { } root ||
                root.BodyPart.PartType != type ||
                root.BodyPart.Symmetry != symmetry)
            {
                continue;
            }

            return uid;
        }

        return EntityUid.Invalid;
    }
}
