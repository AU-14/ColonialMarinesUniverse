using Content.Shared.Damage.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Server.Examine;
using Content.Server.Destructible;
using Content.Server.Power.Components;
using Content.Server.Construction.Components;
using Content.Server._RMC14.TacticalMap;
using Content.Shared.CMU14.Yautja;
using Content.Shared._RMC14.NightVision;
using Content.Shared._RMC14.TacticalMap;
using Content.Shared.Body.Part;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Lathe;
using Content.Shared.Materials;
using Content.Shared.Power.Components;
using Content.Shared.UserInterface;
using Content.Shared.Wires;
using Content.Shared._RMC14.Xenonids.Acid;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using DrawDepth = Content.Shared.DrawDepth.DrawDepth;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class YautjaMachineSourceParityTest
{

    [Test]
    public async Task YautjaCauldronRuntimeMatchesCmss13BubblerAttackBy()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid human = default;
        EntityUid cauldron = default;
        EntityUid crowbar = default;
        EntityUid rawLimb = default;
        EntityUid cancelLimb = default;
        EntityUid boiledLimb = default;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;

                hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
                human = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
                cauldron = entMan.SpawnEntity("CMUYautjaStructureYautjaMachinesVat", map.GridCoords.Offset(new Vector2(2, 0)));
                crowbar = entMan.SpawnEntity("CMCrowbar", map.GridCoords);
                rawLimb = entMan.SpawnEntity("CMUPartHumanLeftArm", map.GridCoords.Offset(new Vector2(2, 1)));
                cancelLimb = entMan.SpawnEntity("CMUPartHumanLeftArm", map.GridCoords.Offset(new Vector2(2, 2)));
                boiledLimb = entMan.SpawnEntity("CMUPartHumanLeftArm", map.GridCoords.Offset(new Vector2(2, 3)));

                entMan.EnsureComponent<YautjaComponent>(hunter);
                entMan.EnsureComponent<YautjaFlayedComponent>(cancelLimb);
                entMan.EnsureComponent<YautjaFlayedComponent>(boiledLimb);
            });

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var examine = entMan.System<ExamineSystem>();

                Assert.That(entMan.HasComponent<YautjaCauldronComponent>(cauldron), Is.True,
                    "CMSS13 /obj/structure/machinery/prop/yautja/bubbler has custom get_examine_text() and attackby() behavior.");
                Assert.That(entMan.GetComponent<BodyPartComponent>(rawLimb).Body, Is.Null,
                    "The test fixture must model CMSS13 /obj/item/limb as a detached local body-part item.");

                var hunterText = examine.GetExamineText(cauldron, hunter).ToMarkup();
                var humanText = examine.GetExamineText(cauldron, human).ToMarkup();

                Assert.Multiple(() =>
                {
                    Assert.That(hunterText, Does.Contain("You can use this machine to clean the skin off limbs, and turn them into bones for your armor."));
                    Assert.That(hunterText, Does.Contain("You first need to find a limb. Then you use a ceremonial dagger to prepare it."));
                    Assert.That(hunterText, Does.Contain("After preparing the limb, you put it into the cauldron, removing the flesh, leaving you with a bone."));
                    Assert.That(hunterText, Does.Contain("You will then clean and polish the resulting bones with a polishing rag, making it ready to be attached to your armor."));
                    Assert.That(humanText, Does.Not.Contain("clean the skin off limbs"),
                        "CMSS13 adds the cauldron instructions only when HAS_TRAIT(user, TRAIT_YAUTJA_TECH).");
                });

                var nonTech = InteractWithCauldron(entMan, human, rawLimb, cauldron);
                var nonLimb = InteractWithCauldron(entMan, hunter, crowbar, cauldron);
                var unflayed = InteractWithCauldron(entMan, hunter, rawLimb, cauldron);

                Assert.Multiple(() =>
                {
                    Assert.That(nonTech.Handled, Is.True,
                        "CMSS13 cauldron attackby() handles non-tech users with a denial notice.");
                    Assert.That(nonLimb.Handled, Is.True,
                        "CMSS13 cauldron attackby() handles non-limb items with a denial notice.");
                    Assert.That(unflayed.Handled, Is.True,
                        "CMSS13 cauldron attackby() handles unflayed limbs with a not-ready notice.");
                    Assert.That(ActiveCauldronDoAfters(entMan, human), Is.Zero);
                    Assert.That(ActiveCauldronDoAfters(entMan, hunter), Is.Zero);
                    AssertCauldronVisual(entMan, cauldron, "vat");
                });
            });

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;

                var started = InteractWithCauldron(entMan, hunter, cancelLimb, cauldron);
                var busy = InteractWithCauldron(entMan, hunter, boiledLimb, cauldron);

                Assert.Multiple(() =>
                {
                    Assert.That(started.Handled, Is.True,
                        "CMSS13 sets icon_state = vat_boiling and starts do_after(user, 15 SECONDS, INTERRUPT_NONE, BUSY_ICON_HOSTILE, current_limb).");
                    Assert.That(busy.Handled, Is.True,
                        "CMSS13 user.action_busy returns after handling the cauldron attackby().");
                    Assert.That(ActiveCauldronDoAfters(entMan, hunter), Is.EqualTo(1));
                    AssertActiveCauldronDoAfter(entMan, hunter, cancelLimb, cauldron);
                    AssertCauldronVisual(entMan, cauldron, "vat_boiling");
                });

                CancelActiveCauldronDoAfter(entMan, hunter);
            });

            await pair.ReallyBeIdle(10);

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;

                Assert.Multiple(() =>
                {
                    Assert.That(ActiveCauldronDoAfters(entMan, hunter), Is.Zero);
                    Assert.That(entMan.Deleted(cancelLimb), Is.False,
                        "CMSS13 failed do_after pulls current_limb back out of the cauldron instead of deleting it.");
                    Assert.That(entMan.Deleted(boiledLimb), Is.False);
                    AssertCauldronVisual(entMan, cauldron, "vat");
                });
            });

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;

                var started = InteractWithCauldron(entMan, hunter, boiledLimb, cauldron);

                Assert.Multiple(() =>
                {
                    Assert.That(started.Handled, Is.True);
                    Assert.That(ActiveCauldronDoAfters(entMan, hunter), Is.EqualTo(1));
                    AssertActiveCauldronDoAfter(entMan, hunter, boiledLimb, cauldron);
                    AssertCauldronVisual(entMan, cauldron, "vat_boiling");
                });
            });

            await pair.RunTicksSync(pair.SecondsToTicks(15.5f));

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var trophies = entMan.EntityQuery<YautjaTrophyComponent, MetaDataComponent>()
                    .Where(trophy => trophy.Item1.Kind == YautjaTrophyKind.HumanLeftArmBone)
                    .ToList();

                Assert.Multiple(() =>
                {
                    Assert.That(ActiveCauldronDoAfters(entMan, hunter), Is.Zero);
                    Assert.That(entMan.Deleted(boiledLimb) || entMan.IsQueuedForDeletion(boiledLimb), Is.True,
                        "CMSS13 qdel(current_limb) after creating current_limb.bone_type at get_turf(src).");
                    Assert.That(trophies, Has.Count.EqualTo(1),
                        "CMSS13 creates exactly one skeleton accessory from current_limb.bone_type.");
                    Assert.That(trophies.Single().Item2.EntityPrototype?.ID, Is.EqualTo("CMUYautjaHumanLeftArmBoneTrophy"));
                    AssertCauldronVisual(entMan, cauldron, "vat");
                });
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                foreach (var uid in new[] { hunter, human, cauldron, crowbar, rawLimb, cancelLimb, boiledLimb })
                {
                    if (uid != default && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }

                var query = entMan.EntityQueryEnumerator<YautjaTrophyComponent>();
                while (query.MoveNext(out var uid, out var trophy))
                {
                    if (trophy.Kind == YautjaTrophyKind.HumanLeftArmBone && !entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }

        await pair.CleanReturnAsync();
    }

    private static InteractUsingEvent InteractWithCauldron(
        IEntityManager entMan,
        EntityUid user,
        EntityUid used,
        EntityUid cauldron)
    {
        var interact = new InteractUsingEvent(
            user,
            used,
            cauldron,
            entMan.GetComponent<TransformComponent>(cauldron).Coordinates);

        entMan.EventBus.RaiseLocalEvent(cauldron, interact);
        return interact;
    }

    private static int ActiveCauldronDoAfters(IEntityManager entMan, EntityUid user)
    {
        return entMan.TryGetComponent(user, out DoAfterComponent? doAfter)
            ? doAfter.DoAfters.Values.Count(active =>
                !active.Cancelled &&
                !active.Completed &&
                active.Args.Event is YautjaCauldronBoilDoAfterEvent)
            : 0;
    }

    private static void AssertActiveCauldronDoAfter(
        IEntityManager entMan,
        EntityUid user,
        EntityUid limb,
        EntityUid cauldron)
    {
        var active = entMan.GetComponent<DoAfterComponent>(user).DoAfters.Values.Single(doAfter =>
            !doAfter.Cancelled &&
            !doAfter.Completed &&
            doAfter.Args.Event is YautjaCauldronBoilDoAfterEvent);

        Assert.Multiple(() =>
        {
            Assert.That(active.Args.Delay, Is.EqualTo(TimeSpan.FromSeconds(15)),
                "CMSS13 cauldron do_after waits exactly 15 seconds.");
            Assert.That(active.Args.EventTarget, Is.EqualTo(cauldron),
                "The local completion event must return to the cauldron machinery.");
            Assert.That(active.Args.Target, Is.EqualTo(limb),
                "CMSS13 passes current_limb as the do_after target.");
            Assert.That(active.Args.Used, Is.Null,
                "Local Used is reserved for hand-held tools; CMSS13 passes current_limb as the do_after target and uses INTERRUPT_NONE.");
        });
    }

    private static void CancelActiveCauldronDoAfter(IEntityManager entMan, EntityUid user)
    {
        var doAfterSystem = entMan.System<SharedDoAfterSystem>();
        var doAfterComp = entMan.GetComponent<DoAfterComponent>(user);
        var active = doAfterComp.DoAfters.Values.Single(doAfter =>
            !doAfter.Cancelled &&
            !doAfter.Completed &&
            doAfter.Args.Event is YautjaCauldronBoilDoAfterEvent);

        doAfterSystem.Cancel(active.Id, doAfterComp);
    }

    private static void AssertCauldronVisual(IEntityManager entMan, EntityUid cauldron, string state)
    {
        var appearance = entMan.System<SharedAppearanceSystem>();
        Assert.That(appearance.TryGetData<string>(cauldron, YautjaCauldronVisuals.State, out var actual), Is.True,
            "Yautja cauldron runtime state must be available to GenericVisualizer.");
        Assert.That(actual, Is.EqualTo(state));
    }

}
