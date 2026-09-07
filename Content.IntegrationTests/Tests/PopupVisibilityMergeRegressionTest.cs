using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Stealth;
using Content.Shared._RMC14.Xenonids.Invisibility;
using Content.Shared.Popups;
using Robust.Shared.GameObjects;
using ServerPopupSystem = Content.Server.Popups.PopupSystem;
using ClientPopupSystem = Content.Client.Popups.PopupSystem;

namespace Content.IntegrationTests.Tests;

[TestFixture]
[TestOf(typeof(ServerPopupSystem))]
public sealed class PopupVisibilityMergeRegressionTest : GameTest
{
    [Test]
    public async Task RecipientInvisibilitySuppressesOthersWithoutSuppressingRecipient()
    {
        var map = await Pair.CreateTestMap();
        var session = ServerSession!;
        var originalAttached = session.AttachedEntity;
        EntityUid display = default;
        EntityUid observer = default;
        EntityUid visibleRecipient = default;
        EntityUid entityInvisibleRecipient = default;
        EntityUid xenoInvisibleRecipient = default;

        try
        {
            await Server.WaitPost(() =>
            {
                display = SEntMan.SpawnEntity(null, map.GridCoords);
                observer = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
                visibleRecipient = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
                entityInvisibleRecipient = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
                xenoInvisibleRecipient = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);

                // The display entity is deliberately invisible so a display-keyed gate fails the positive controls.
                SEntMan.EnsureComponent<EntityActiveInvisibleComponent>(display);
                SEntMan.EnsureComponent<EntityActiveInvisibleComponent>(entityInvisibleRecipient);
                SEntMan.EnsureComponent<XenoActiveInvisibleComponent>(xenoInvisibleRecipient);
                Server.PlayerMan.SetAttachedEntity(session, observer);
            });
            await Pair.RunUntilSynced();

            await SendPopup(popup => popup.PopupEntity(
                "popup-merge-visible-self",
                "popup-merge-visible-others",
                display,
                visibleRecipient,
                PopupType.MediumXeno));
            await AssertPopup("popup-merge-visible-others", true, PopupType.MediumXeno);
            await AssertPopup("popup-merge-visible-self", false);

            await SendPopup(popup => popup.PopupEntity(
                "popup-merge-entity-invisible-self-unattached",
                "popup-merge-entity-invisible-others",
                display,
                entityInvisibleRecipient));
            await AssertPopup("popup-merge-entity-invisible-others", false);
            await AssertPopup("popup-merge-entity-invisible-self-unattached", false);

            await SendPopup(popup => popup.PopupEntity(
                "popup-merge-xeno-invisible-self-unattached",
                "popup-merge-xeno-invisible-others",
                display,
                xenoInvisibleRecipient));
            await AssertPopup("popup-merge-xeno-invisible-others", false);
            await AssertPopup("popup-merge-xeno-invisible-self-unattached", false);

            await Attach(session, entityInvisibleRecipient);
            await SendPopup(popup => popup.PopupEntity(
                "popup-merge-invisible-recipient",
                "popup-merge-invisible-recipient-others",
                display,
                entityInvisibleRecipient));
            await AssertPopup("popup-merge-invisible-recipient", true);
            await AssertPopup("popup-merge-invisible-recipient-others", false);

            await Attach(session, observer);
            await SendPopup(popup => popup.PopupEntity(
                "popup-merge-null-self-unused",
                "popup-merge-null-others",
                display,
                null));
            await AssertPopup("popup-merge-null-others", true);
            await AssertPopup("popup-merge-null-self-unused", false);

#pragma warning disable CS0618 // This regression deliberately covers the retained legacy prediction overload.
            await SendPopup(popup => popup.PopupPredicted(
                "popup-merge-legacy-visible",
                display,
                visibleRecipient));
            await AssertPopup("popup-merge-legacy-visible", true);

            await SendPopup(popup => popup.PopupPredicted(
                "popup-merge-legacy-entity-invisible",
                display,
                entityInvisibleRecipient));
            await AssertPopup("popup-merge-legacy-entity-invisible", false);

            await SendPopup(popup => popup.PopupPredicted(
                "popup-merge-legacy-xeno-invisible",
                display,
                xenoInvisibleRecipient));
            await AssertPopup("popup-merge-legacy-xeno-invisible", false);

            await SendPopup(popup => popup.PopupPredicted(
                "popup-merge-legacy-null",
                display,
                null));
            await AssertPopup("popup-merge-legacy-null", true);
#pragma warning restore CS0618
        }
        finally
        {
            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(session, originalAttached));
        }
    }

    private async Task Attach(Robust.Shared.Player.ICommonSession session, EntityUid entity)
    {
        await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(session, entity));
        await Pair.RunUntilSynced();
    }

    private async Task SendPopup(Action<ServerPopupSystem> send)
    {
        await Server.WaitPost(() => send(Server.System<ServerPopupSystem>()));
        await Pair.RunTicksSync(5);
    }

    private async Task AssertPopup(string message, bool expected, PopupType? type = null)
    {
        await Client.WaitAssertion(() =>
        {
            var labels = CEntMan.System<ClientPopupSystem>().WorldLabels;
            var present = labels.Any(label => label.Text == message && (type == null || label.Type == type));
            Assert.That(present, Is.EqualTo(expected), message);
        });
    }
}
