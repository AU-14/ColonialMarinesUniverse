using System.IO;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Photography;
using Content.Shared.CMU14.Photography;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Robust.Shared.GameObjects;
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
            Server.PlayerMan.SetAttachedEntity(session, user);
        });

        // A new mob's identity name is filled in on a later tick.
        await Server.WaitRunTicks(5);

        await Server.WaitAssertion(() =>
        {
            var photos = SEntMan.System<CMUPhotoSystem>();
            var hands = SEntMan.System<SharedHandsSystem>();

            var cameraUid = SEntMan.SpawnEntity("CMUPhotoCamera", map.GridCoords);
            var camera = new Entity<CMUPhotoCameraComponent>(cameraUid, SEntMan.GetComponent<CMUPhotoCameraComponent>(cameraUid));
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
            });

            Assert.That(hands.IsHolding(user, print), "The print did not come out into the photographer's hand.");
        });
    }

    [Test]
    [Description("A fresh roll of film reloads an empty camera to a full roll and is used up.")]
    public async Task FilmReloadsTheCamera()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var camera = SEntMan.SpawnEntity("CMUPhotoCamera", map.GridCoords);
            var film = SEntMan.SpawnEntity("CMUCameraFilm", map.GridCoords);
            var comp = SEntMan.GetComponent<CMUPhotoCameraComponent>(camera);
            comp.Shots = 0;

            var ev = new InteractUsingEvent(user, film, camera, map.GridCoords);
            SEntMan.EventBus.RaiseLocalEvent(camera, ev);

            Assert.Multiple(() =>
            {
                Assert.That(ev.Handled, "The camera ignored the film.");
                Assert.That(comp.Shots, Is.EqualTo(comp.MaxShots), "The camera was not reloaded to a full roll.");
                Assert.That(SEntMan.Deleted(film) || SEntMan.IsQueuedForDeletion(film), "The roll was not used up.");
            });
        });
    }
}
