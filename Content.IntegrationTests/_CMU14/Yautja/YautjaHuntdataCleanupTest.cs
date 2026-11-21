using System.Numerics;
using System.Linq;
using Content.Client.UserInterface.Systems.Chat;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Chat;
using Content.Shared.GameTicking;
using Content.Shared.Inventory;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Server.CMU14.Yautja;
using Content.Client.Popups;
using Robust.Client.UserInterface;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class YautjaHuntdataCleanupTest
{
    [TestCase(YautjaMarkKind.Honored, "spared civilians")]
    [TestCase(YautjaMarkKind.Dishonored, "stole hunter gear")]
    [TestCase(YautjaMarkKind.GearCarrier, null)]
    public async Task OneOwnerMarksCannotBeOverwrittenLikeCmss13Huntdata(YautjaMarkKind kind, string? reason)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var marks = entMan.System<YautjaMarkSystem>();

            var firstHunter = SpawnHunterWithBracer(entMan, map.GridCoords, out var firstBracer);
            var secondHunter = SpawnHunterWithBracer(entMan, map.GridCoords.Offset(new Vector2(1, 0)), out var secondBracer);
            var target = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(2, 0)));

            try
            {
                Assert.That(
                    marks.TryMark((firstBracer, entMan.GetComponent<YautjaBracerComponent>(firstBracer)), firstHunter, target, kind, reason),
                    Is.True);

                Assert.That(
                    marks.TryMark((secondBracer, entMan.GetComponent<YautjaBracerComponent>(secondBracer)), secondHunter, target, kind, "second hunter"),
                    Is.False,
                    $"CMSS13 huntdata only has one {kind} owner field and rejects a second hunter instead of overwriting it.");

                Assert.That(marks.IsMarkedBy(target, kind, firstHunter), Is.True,
                    "The original hunter_data owner link must remain intact.");
                Assert.That(marks.IsMarkedBy(target, kind, secondHunter), Is.False,
                    "A rejected repeat mark must not replace the original hunter_data owner link.");
            }
            finally
            {
                DeleteAll(entMan, firstHunter, secondHunter, target, firstBracer, secondBracer);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task GenericMarksClearOnRoundRestartLikeCmss13CleanData()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var marks = entMan.System<YautjaMarkSystem>();

            var hunter = SpawnHunterWithBracer(entMan, map.GridCoords, out var bracer);
            var prey = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
            var honored = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(2, 0)));
            var dishonored = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(3, 0)));
            var gearCarrier = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(4, 0)));
            var bracerComp = entMan.GetComponent<YautjaBracerComponent>(bracer);

            try
            {
                Assert.That(marks.TryMark((bracer, bracerComp), hunter, prey, YautjaMarkKind.Prey, null), Is.True);
                Assert.That(marks.TryMark((bracer, bracerComp), hunter, honored, YautjaMarkKind.Honored, "worthy"), Is.True);
                Assert.That(marks.TryMark((bracer, bracerComp), hunter, dishonored, YautjaMarkKind.Dishonored, "thief"), Is.True);
                Assert.That(marks.TryMark((bracer, bracerComp), hunter, gearCarrier, YautjaMarkKind.GearCarrier, null), Is.True);

                entMan.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent());

                Assert.That(marks.IsMarkedBy(prey, YautjaMarkKind.Prey, hunter), Is.False,
                    "CMSS13 huntdata.clean_data() clears hunted/prey links during round-scoped cleanup.");
                Assert.That(marks.IsMarkedBy(honored, YautjaMarkKind.Honored, hunter), Is.False,
                    "CMSS13 huntdata.clean_data() clears honored links during round-scoped cleanup.");
                Assert.That(marks.IsMarkedBy(dishonored, YautjaMarkKind.Dishonored, hunter), Is.False,
                    "CMSS13 huntdata.clean_data() clears dishonored links during round-scoped cleanup.");
                Assert.That(marks.IsMarkedBy(gearCarrier, YautjaMarkKind.GearCarrier, hunter), Is.False,
                    "CMSS13 huntdata.clean_data() clears gear-carrier links during round-scoped cleanup.");
            }
            finally
            {
                DeleteAll(entMan, hunter, prey, honored, dishonored, gearCarrier, bracer);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HunterDeletionClearsOwnedGenericMarksLikeCmss13CleanData()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var marks = entMan.System<YautjaMarkSystem>();

            var hunter = SpawnHunterWithBracer(entMan, map.GridCoords, out var bracer);
            var prey = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
            var honored = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(2, 0)));
            var dishonored = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(3, 0)));
            var gearCarrier = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(4, 0)));
            var bracerComp = entMan.GetComponent<YautjaBracerComponent>(bracer);

            try
            {
                Assert.That(marks.TryMark((bracer, bracerComp), hunter, prey, YautjaMarkKind.Prey, null), Is.True);
                Assert.That(marks.TryMark((bracer, bracerComp), hunter, honored, YautjaMarkKind.Honored, "worthy"), Is.True);
                Assert.That(marks.TryMark((bracer, bracerComp), hunter, dishonored, YautjaMarkKind.Dishonored, "thief"), Is.True);
                Assert.That(marks.TryMark((bracer, bracerComp), hunter, gearCarrier, YautjaMarkKind.GearCarrier, null), Is.True);

                entMan.DeleteEntity(hunter);

                Assert.That(marks.TryGetMarkOwner(prey, YautjaMarkKind.Prey, out _), Is.False,
                    "CMSS13 hunter_data.clean_data() clears prey.hunter when the hunter owner is deleted.");
                Assert.That(marks.TryGetMarkOwner(honored, YautjaMarkKind.Honored, out _), Is.False,
                    "CMSS13 hunter_data.clean_data() clears target honored_set links from the deleted hunter's honored_targets list.");
                Assert.That(marks.TryGetMarkOwner(dishonored, YautjaMarkKind.Dishonored, out _), Is.False,
                    "CMSS13 hunter_data.clean_data() clears target dishonored_set links from the deleted hunter's dishonored_targets list.");
                Assert.That(marks.TryGetMarkOwner(gearCarrier, YautjaMarkKind.GearCarrier, out _), Is.False,
                    "CMSS13 hunter_data.clean_data() clears target gear_set links from the deleted hunter's gear_targets list.");
            }
            finally
            {
                DeleteAll(entMan, hunter, prey, honored, dishonored, gearCarrier, bracer);
            }
        });

        await pair.CleanReturnAsync();
    }

    [TestCase(YautjaMarkKind.Honored)]
    [TestCase(YautjaMarkKind.Dishonored)]
    public async Task OtherHunterCannotUnmarkHonorOrDishonorLikeCmss13(YautjaMarkKind kind)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid ownerHunter = default;
        EntityUid otherHunter = default;
        EntityUid target = default;
        EntityUid ownerBracer = default;
        EntityUid otherBracer = default;
        EntityUid? previousAttached = null;

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var marks = entMan.System<YautjaMarkSystem>();
                var ui = entMan.System<SharedUserInterfaceSystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                ownerHunter = SpawnHunterWithBracer(entMan, map.GridCoords, out ownerBracer);
                otherHunter = SpawnHunterWithBracer(entMan, map.GridCoords.Offset(new Vector2(1, 0)), out otherBracer);
                target = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(2, 0)));
                server.PlayerMan.SetAttachedEntity(session, otherHunter);

                Assert.That(
                    marks.TryMark((ownerBracer, entMan.GetComponent<YautjaBracerComponent>(ownerBracer)), ownerHunter, target, kind, "source owner"),
                    Is.True);

                Assert.That(marks.TryOpenMarkPanel((otherBracer, entMan.GetComponent<YautjaBracerComponent>(otherBracer)), otherHunter), Is.True);
                ui.RaiseUiMessage(
                    otherBracer,
                    YautjaMarkUIKey.Key,
                    YautjaTestMarkMessages.Unmark(entMan, otherHunter, target, kind));

                Assert.That(marks.IsMarkedBy(target, kind, ownerHunter), Is.True,
                    "CMSS13 only lets the original living hunter undo their honored/dishonored mark.");
                Assert.That(marks.IsMarkedBy(target, kind, otherHunter), Is.False,
                    "A failed unmark attempt must not transfer ownership to the acting hunter.");
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var labels = client.EntMan.System<PopupSystem>().WorldLabels
                    .Select(label => label.Text)
                    .ToList();
                var joinedLabels = string.Join("\n", labels);

                Assert.That(
                    labels.Any(label => label.Contains("You cannot undo the actions of a living brother or sister!", StringComparison.OrdinalIgnoreCase)),
                    Is.True,
                    $"CMSS13 unmark_honored()/unmark_dishonored() denies non-owner living hunters with the source text.\nActual labels:\n{joinedLabels}");
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);
                DeleteAll(entMan, ownerHunter, otherHunter, target, ownerBracer, otherBracer);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task GearCarrierTransitionsBroadcastAndOtherHunterCanUnmarkLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid ownerHunter = default;
        EntityUid otherHunter = default;
        EntityUid target = default;
        EntityUid ownerBracer = default;
        EntityUid otherBracer = default;
        EntityUid? previousAttached = null;

        const string OwnerName = "A'ke Ret";
        const string OtherName = "Ki'cte Pa";
        const string TargetName = "Guan Thwei";

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var marks = entMan.System<YautjaMarkSystem>();
                var metadata = entMan.System<MetaDataSystem>();
                var ui = entMan.System<SharedUserInterfaceSystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                ownerHunter = SpawnHunterWithBracer(entMan, map.GridCoords, out ownerBracer);
                otherHunter = SpawnHunterWithBracer(entMan, map.GridCoords.Offset(new Vector2(1, 0)), out otherBracer);
                target = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(2, 0)));
                metadata.SetEntityName(ownerHunter, OwnerName);
                metadata.SetEntityName(otherHunter, OtherName);
                metadata.SetEntityName(target, TargetName);
                server.PlayerMan.SetAttachedEntity(session, otherHunter);

                Assert.That(
                    marks.TryMark((ownerBracer, entMan.GetComponent<YautjaBracerComponent>(ownerBracer)), ownerHunter, target, YautjaMarkKind.GearCarrier, null),
                    Is.True);

                Assert.That(marks.TryOpenMarkPanel((otherBracer, entMan.GetComponent<YautjaBracerComponent>(otherBracer)), otherHunter), Is.True);
                ui.RaiseUiMessage(
                    otherBracer,
                    YautjaMarkUIKey.Key,
                    YautjaTestMarkMessages.Unmark(entMan, otherHunter, target, YautjaMarkKind.GearCarrier));

                Assert.That(marks.TryGetMarkOwner(target, YautjaMarkKind.GearCarrier, out _), Is.False,
                    "CMSS13 unmark_gear() lets a living Yautja unmark an existing gear carrier; it does not require the original marker.");
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var labels = client.EntMan.System<PopupSystem>().WorldLabels
                    .Select(label => label.Text)
                    .ToList();
                var joinedLabels = string.Join("\n", labels);

                Assert.Multiple(() =>
                {
                    Assert.That(
                        labels.Any(label => label.Contains($"{OwnerName} has marked {TargetName} as a Gear Carrier!", StringComparison.OrdinalIgnoreCase)),
                        Is.True,
                        $"CMSS13 mark_gear() broadcasts the gear-carrier transition.\nActual labels:\n{joinedLabels}");

                    Assert.That(
                        labels.Any(label => label.Contains($"{OtherName} has un-marked {TargetName} as a Gear Carrier!", StringComparison.OrdinalIgnoreCase)),
                        Is.True,
                        $"CMSS13 unmark_gear() broadcasts the acting hunter, not necessarily the original marker.\nActual labels:\n{joinedLabels}");
                });
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);
                DeleteAll(entMan, ownerHunter, otherHunter, target, ownerBracer, otherBracer);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TrophyClaimOfHuntedPreyUsesCmss13ClaimTextAndClearsPrey()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        EntityUid hunter = default;
        EntityUid prey = default;
        EntityUid bracer = default;
        EntityUid? previousAttached = null;

        const string HunterName = "A'ke Ret";
        const string PreyName = "Guan Thwei";

        try
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var inventory = entMan.System<InventorySystem>();
                var marks = entMan.System<YautjaMarkSystem>();
                var metadata = entMan.System<MetaDataSystem>();
                var mobState = entMan.System<MobStateSystem>();
                var trophies = entMan.System<YautjaTrophySystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;

                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                prey = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
                bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);

                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);
                metadata.SetEntityName(hunter, HunterName);
                metadata.SetEntityName(prey, PreyName);
                server.PlayerMan.SetAttachedEntity(session, hunter);

                Assert.That(
                    marks.TryMark((bracer, entMan.GetComponent<YautjaBracerComponent>(bracer)), hunter, prey, YautjaMarkKind.Prey, null),
                    Is.True);
                mobState.ChangeMobState(prey, MobState.Dead);

                Assert.That(trophies.TryHarvestTrophy(hunter, prey, YautjaTrophyKind.HumanSkull, out _), Is.True);
                Assert.That(marks.TryGetMarkOwner(prey, YautjaMarkKind.Prey, out _), Is.False,
                    "CMSS13 trophy claim clears hunter_data.prey and the prey's hunter/hunted fields.");
            });

            await pair.ReallyBeIdle(10);

            await client.WaitAssertion(() =>
            {
                var labels = client.EntMan.System<PopupSystem>().WorldLabels
                    .Select(label => label.Text)
                    .ToList();
                var joinedLabels = string.Join("\n", labels);

                Assert.Multiple(() =>
                {
                    Assert.That(
                        labels.Any(label => label.Contains($"You have claimed {PreyName} as your trophy.", StringComparison.OrdinalIgnoreCase)),
                        Is.True,
                        $"CMSS13 butcher/trophy prey completion tells the hunter with the source text.\nActual labels:\n{joinedLabels}");
                    Assert.That(
                        labels.Any(label => label.Contains($"{HunterName} has claimed {PreyName} as their trophy.", StringComparison.OrdinalIgnoreCase)),
                        Is.True,
                        $"CMSS13 butcher/trophy prey completion broadcasts the claim to Yautja with the source text.\nActual labels:\n{joinedLabels}");
                });
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);
                DeleteAll(entMan, hunter, prey, bracer);
            });
        }

        await pair.CleanReturnAsync();
    }

    private static EntityUid SpawnHunterWithBracer(IEntityManager entMan, EntityCoordinates coordinates, out EntityUid bracer)
    {
        var hunter = entMan.SpawnEntity("CMMobHuman", coordinates);
        bracer = entMan.SpawnEntity("CMUYautjaBracer", coordinates);
        entMan.EnsureComponent<YautjaComponent>(hunter);
        Assert.That(entMan.System<InventorySystem>().TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);
        return hunter;
    }

    private static void DeleteAll(IEntityManager entMan, params EntityUid[] uids)
    {
        foreach (var uid in uids)
        {
            if (uid != default && !entMan.Deleted(uid))
                entMan.DeleteEntity(uid);
        }
    }
}
