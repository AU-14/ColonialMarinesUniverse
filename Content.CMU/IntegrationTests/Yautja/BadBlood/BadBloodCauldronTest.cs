#pragma warning disable RA0002 // tests read component data directly
using Content.Server.CMU14.Yautja;
using Content.Shared.Body.Part;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts.Events;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Interaction;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// COUPLING-101: the cauldron only claims an interaction it accepts, and never boils a Yautja's own limb
[TestFixture]
public sealed class BadBloodCauldronTest
{
    private const string Cauldron = "CMUYautjaStructureYautjaMachinesVat";

    [Test]
    public async Task CauldronLeavesUnacceptedItemsUnhandledAndRefusesYautjaLimbs()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var machines = entMan.System<YautjaMachineSystem>();
            var hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            var cauldron = entMan.SpawnEntity(Cauldron, map.GridCoords.Offset(new Vector2(1, 0)));

            var yautjaLimb = SeverArm(entMan, entMan.SpawnEntity("CMUMobYautja", map.GridCoords.Offset(new Vector2(0, 2))));
            var humanLimb = SeverArm(entMan, entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(0, 3))));
            entMan.EnsureComponent<YautjaFlayedComponent>(yautjaLimb);
            entMan.EnsureComponent<YautjaFlayedComponent>(humanLimb);

            Assert.Multiple(() =>
            {
                Assert.That(machines.IsYautjaOriginPart(yautjaLimb), Is.True);
                Assert.That(machines.IsYautjaOriginPart(humanLimb), Is.False);
                UseOnCauldron(entMan, hunter, yautjaLimb, cauldron);
                Assert.That(ActiveDoAfters(entMan, hunter), Is.Zero, "A limb cut from a Yautja must be refused.");
                UseOnCauldron(entMan, hunter, humanLimb, cauldron);
                Assert.That(ActiveDoAfters(entMan, hunter), Is.EqualTo(1), "A prepared human limb is still accepted.");
            });
        });

        await pair.CleanReturnAsync();
    }

    private static int ActiveDoAfters(IEntityManager entMan, EntityUid user)
    {
        return entMan.TryGetComponent(user, out Content.Shared.DoAfter.DoAfterComponent? comp)
            ? System.Linq.Enumerable.Count(comp.DoAfters.Values, d => !d.Cancelled && !d.Completed)
            : 0;
    }

    private static bool UseOnCauldron(IEntityManager entMan, EntityUid user, EntityUid used, EntityUid cauldron)
    {
        var use = new InteractUsingEvent(user, used, cauldron, entMan.GetComponent<TransformComponent>(cauldron).Coordinates);
        entMan.EventBus.RaiseLocalEvent(cauldron, use);
        return use.Handled;
    }

    private static EntityUid SeverArm(IEntityManager entMan, EntityUid body)
    {
        Assert.That(entMan.System<CMUMedicalBodyIndexSystem>().TryGetBodyPart(body,
            new CMUMedicalBodyPartKey(BodyPartType.Arm, BodyPartSymmetry.Left), out var arm), Is.True);

        var sever = new BodyPartSeverAttemptEvent(body, arm, BodyPartType.Arm, Surgical: true);
        entMan.EventBus.RaiseLocalEvent(arm, ref sever);
        Assert.That(sever.Succeeded, Is.True);
        Assert.That(sever.DetachedBody, Is.Not.Null);
        return sever.DetachedBody!.Value;
    }
}
