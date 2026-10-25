using System.IO;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Photography;
using Content.Shared.CMU14.Photography;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.IntegrationTests.CMU14.Photography;

[TestFixture]
[TestOf(typeof(CMUPhotoSystem))]
public sealed class CMUPhotoTest : GameTest
{
    private static byte[] Png(int size)
    {
        using var image = new Image<Rgba32>(size, size, new Rgba32(40, 200, 90, 255));
        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        return stream.ToArray();
    }

    private Entity<CMUPhotoCameraComponent> SpawnCamera(EntityCoordinates coordinates)
    {
        var uid = SEntMan.SpawnEntity("CMUPhotoCamera", coordinates);
        return (uid, SEntMan.GetComponent<CMUPhotoCameraComponent>(uid));
    }

    /// <summary>Clicks the camera on a tile, as a player holding it would.</summary>
    private AfterInteractEvent Click(EntityUid user, Entity<CMUPhotoCameraComponent> camera, EntityCoordinates target)
    {
        // Let every click through the shutter's cooldown.
        camera.Comp.NextPhoto = TimeSpan.Zero;
        var ev = new AfterInteractEvent(user, camera, null, target, true);
        SEntMan.EventBus.RaiseLocalEvent(camera, ev);
        return ev;
    }

    [Test]
    [Description("Only a PNG of the frame's exact size, for a request the server made, becomes a print in the photographer's hand, listing who was in frame.")]
    public async Task CapturesBecomePrintsInHand()
    {
        var map = await Pair.CreateTestMap();
        var session = ServerSession!;

        EntityUid user = default, subject = default;
        await Server.WaitAssertion(() =>
        {
            user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            subject = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            // Area markers sit on every mapped tile; nobody can see them.
            SEntMan.SpawnEntity("AU14AreaStableGarrisonJournalistRoof", map.GridCoords);
            Server.PlayerMan.SetAttachedEntity(session, user);
        });

        // A new mob's identity name is filled in on a later tick.
        await Server.WaitRunTicks(5);

        await Server.WaitAssertion(() =>
        {
            var photos = SEntMan.System<CMUPhotoSystem>();
            var hands = SEntMan.System<SharedHandsSystem>();

            var camera = SpawnCamera(map.GridCoords);
            // Printed at once, so the print can be checked in this tick.
            camera.Comp.PrintDelay = TimeSpan.Zero;
            var frame = camera.Comp.Size * CMUPhotoLimits.TilePixels;

            var wrong = photos.RequestCapture(session, user, camera, map.GridCoords);
            Assert.That(photos.TryAcceptCapture(session, wrong, Png(frame + 1), out _), Is.False,
                "A capture of the wrong size was printed.");

            var right = photos.RequestCapture(session, user, camera, map.GridCoords);
            Assert.That(photos.TryAcceptCapture(session, right, Png(frame), out var print), Is.True,
                "A capture of the frame's size was refused.");
            Assert.That(photos.TryAcceptCapture(session, right, Png(frame), out _), Is.False,
                "The same request printed twice.");

            var photo = SEntMan.GetComponent<CMUPhotoComponent>(print);
            Assert.Multiple(() =>
            {
                Assert.That(photos.HasPhoto(photo.Id));
                Assert.That(photo.Size, Is.EqualTo(camera.Comp.Size));
                Assert.That(photo.Photographer, Is.EqualTo(SEntMan.GetComponent<MetaDataComponent>(user).EntityName));
                Assert.That(photo.Contents, Does.Contain(SEntMan.GetComponent<MetaDataComponent>(subject).EntityName),
                    "A person standing in the frame is missing from the print's description.");
                Assert.That(photo.Contents, Does.Not.Contain("Albacetrie - Journalist Office Roof"),
                    "An invisible area marker was described as being in the photo.");
                Assert.That(photo.Thumbnail, Is.Not.Empty, "The print has no thumbnail for its sprite.");
                Assert.That(Image.Identify(photo.Thumbnail).Width, Is.EqualTo(CMUPhotoLimits.ThumbnailPixels));
            });

            Assert.That(hands.IsHolding(user, print), "The print did not come out into the photographer's hand.");
        });
    }

    [Test]
    [Description("A ghost standing in the frame is recorded on the print, faintly, and kept out of its caption; ghosts outside the frame and admin ghosts are not.")]
    public async Task GhostsInTheFrameAreCaught()
    {
        var map = await Pair.CreateTestMap();
        var session = ServerSession!;

        EntityUid user = default, inside = default;
        await Server.WaitAssertion(() =>
        {
            user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            Server.PlayerMan.SetAttachedEntity(session, user);
            inside = SEntMan.SpawnEntity("MobObserver", map.GridCoords.Offset(new System.Numerics.Vector2(1, 0)));
            SEntMan.SpawnEntity("MobObserver", map.GridCoords.Offset(new System.Numerics.Vector2(6, 0)));
            SEntMan.SpawnEntity("AdminObserver", map.GridCoords);
        });

        await Server.WaitRunTicks(5);

        await Server.WaitAssertion(() =>
        {
            var photos = SEntMan.System<CMUPhotoSystem>();
            var camera = SpawnCamera(map.GridCoords);
            camera.Comp.PrintDelay = TimeSpan.Zero;
            Assert.That(camera.Comp.Size, Is.EqualTo(3), "The test frames a 3x3 area.");

            var request = photos.RequestCapture(session, user, camera, map.GridCoords);
            Assert.That(photos.TryAcceptCapture(session, request, Png(camera.Comp.Size * CMUPhotoLimits.TilePixels), out var print));

            var photo = SEntMan.GetComponent<CMUPhotoComponent>(print);
            Assert.That(photo.Ghosts, Has.Count.EqualTo(1), "Only the player ghost inside the frame should be caught.");
            var ghost = photo.Ghosts[0];
            Assert.Multiple(() =>
            {
                Assert.That(ghost.Prototype.Id, Is.EqualTo("MobObserver"));
                Assert.That(ghost.Opacity, Is.InRange(CMUPhotoLimits.GhostMinOpacity, CMUPhotoLimits.GhostMaxOpacity));
                Assert.That(ghost.Offset.X, Is.EqualTo(1).Within(0.01f), "The ghost was recorded in the wrong place.");
                Assert.That(ghost.Offset.Y, Is.EqualTo(0).Within(0.01f), "The ghost was recorded in the wrong place.");
                Assert.That(photo.Contents, Does.Not.Contain(SEntMan.GetComponent<MetaDataComponent>(inside).EntityName),
                    "A ghost was named in the caption.");
            });
        });
    }

    [Test]
    [Description("The print comes out of the camera a moment after the shutter, not in the same tick.")]
    public async Task PrintComesOutAfterTheShutter()
    {
        var map = await Pair.CreateTestMap();
        var session = ServerSession!;

        EntityUid user = default;
        await Server.WaitAssertion(() =>
        {
            user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            Server.PlayerMan.SetAttachedEntity(session, user);

            var photos = SEntMan.System<CMUPhotoSystem>();
            var camera = SpawnCamera(map.GridCoords);
            camera.Comp.PrintDelay = TimeSpan.FromSeconds(0.5);
            var request = photos.RequestCapture(session, user, camera, map.GridCoords);

            Assert.That(photos.TryAcceptCapture(session, request, Png(camera.Comp.Size * CMUPhotoLimits.TilePixels), out var print),
                "A good capture was refused.");
            Assert.That(print, Is.EqualTo(EntityUid.Invalid), "The print came out with the shutter instead of after it.");
        });

        await Server.WaitRunTicks(60);

        await Server.WaitAssertion(() =>
        {
            var hands = SEntMan.System<SharedHandsSystem>();
            var held = false;
            foreach (var item in hands.EnumerateHeld(user))
                held |= SEntMan.HasComponent<CMUPhotoComponent>(item);

            Assert.That(held, "The delayed print never came out into the photographer's hand.");
        });
    }

    [Test]
    [Description("A roll gives ten shots, counted down by each photo; an empty camera clicks without asking for a picture.")]
    public async Task FilmRunsOut()
    {
        var map = await Pair.CreateTestMap();
        var session = ServerSession!;

        await Server.WaitAssertion(() =>
        {
            var photos = SEntMan.System<CMUPhotoSystem>();
            var user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            Server.PlayerMan.SetAttachedEntity(session, user);
            var camera = SpawnCamera(map.GridCoords);

            Assert.That(camera.Comp.MaxShots, Is.EqualTo(10), "A roll should hold ten shots, as in SS13.");
            Assert.That(camera.Comp.Shots, Is.EqualTo(camera.Comp.MaxShots), "A new camera should come loaded.");

            var pending = photos.PendingCaptures;
            for (var shot = 1; shot <= camera.Comp.MaxShots; shot++)
            {
                Assert.That(Click(user, camera, map.GridCoords).Handled, "The camera ignored a click.");
                Assert.That(camera.Comp.Shots, Is.EqualTo(camera.Comp.MaxShots - shot), $"Shot {shot} did not use film.");
            }

            Assert.That(photos.PendingCaptures, Is.EqualTo(pending + camera.Comp.MaxShots), "Not every shot asked for a picture.");

            var empty = Click(user, camera, map.GridCoords);
            Assert.Multiple(() =>
            {
                Assert.That(empty.Handled, "An empty camera's click went through to whatever was under it.");
                Assert.That(camera.Comp.Shots, Is.Zero, "An empty camera's shots went below zero.");
                Assert.That(photos.PendingCaptures, Is.EqualTo(pending + camera.Comp.MaxShots),
                    "An empty camera still took a photo.");
            });
        });
    }

    [Test]
    [Description("A shot the photographer's client could not capture gives its film back.")]
    public async Task UncapturedShotKeepsItsFilm()
    {
        var map = await Pair.CreateTestMap();
        var session = ServerSession!;

        await Server.WaitAssertion(() =>
        {
            var photos = SEntMan.System<CMUPhotoSystem>();
            var user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            Server.PlayerMan.SetAttachedEntity(session, user);
            var camera = SpawnCamera(map.GridCoords);

            camera.Comp.Shots = 3;
            var request = photos.RequestCapture(session, user, camera, map.GridCoords);
            camera.Comp.Shots--;
            Assert.That(photos.TryAcceptCapture(session, request, Array.Empty<byte>(), out _), Is.False);
            Assert.That(camera.Comp.Shots, Is.EqualTo(3), "The film for a picture that was never taken was used up.");
        });
    }

    [Test]
    [Description("A fresh roll of film reloads an empty camera to a full roll and is used up; a full camera refuses it.")]
    public async Task FilmReloadsTheCamera()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var camera = SpawnCamera(map.GridCoords);
            var spare = SEntMan.SpawnEntity("CMUCameraFilm", map.GridCoords);

            var full = new InteractUsingEvent(user, spare, camera, map.GridCoords);
            SEntMan.EventBus.RaiseLocalEvent(camera, full);
            Assert.Multiple(() =>
            {
                Assert.That(full.Handled, "The camera ignored the film.");
                Assert.That(SEntMan.Deleted(spare) || SEntMan.IsQueuedForDeletion(spare), Is.False,
                    "A roll was wasted on a camera that was already full.");
            });

            var film = SEntMan.SpawnEntity("CMUCameraFilm", map.GridCoords);
            camera.Comp.Shots = 0;
            var ev = new InteractUsingEvent(user, film, camera, map.GridCoords);
            SEntMan.EventBus.RaiseLocalEvent(camera, ev);

            Assert.Multiple(() =>
            {
                Assert.That(ev.Handled, "The camera ignored the film.");
                Assert.That(camera.Comp.Shots, Is.EqualTo(camera.Comp.MaxShots), "The camera was not reloaded to a full roll.");
                Assert.That(SEntMan.Deleted(film) || SEntMan.IsQueuedForDeletion(film), "The roll was not used up.");
            });
        });
    }
}
