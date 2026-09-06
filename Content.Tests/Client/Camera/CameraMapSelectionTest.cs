using System.Collections.Generic;
using System.Numerics;
using Content.Client.Camera;
using Content.Client._RMC14.Camera;
using Content.Shared._RMC14.Camera;
using Content.Shared.Camera;
using Content.Shared.SurveillanceCamera;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.UnitTesting;

namespace Content.Tests.Client.Camera;

[TestFixture]
public sealed class CameraMapSelectionTest : RobustUnitTest
{
    public override UnitTestProject Project => UnitTestProject.Client;

    [OneTimeSetUp]
    public void Setup()
    {
        IoCManager.Resolve<IUserInterfaceManager>().InitializeTesting();
        var proxyType = typeof(IUserInterfaceManager).Assembly.GetType(
            "Robust.Client.UserInterface.XAML.Proxy.IXamlProxyManager")!;
        var proxy = IoCManager.ResolveType(proxyType);
        proxyType.GetMethod("Initialize")!.Invoke(proxy, null);
    }

    [Test]
    public void RmcEditorControlRebindsReusedCameraRowAndMultipleMemberships()
    {
        var firstNetwork = new NetEntity(1);
        var secondNetwork = new NetEntity(2);
        var firstCamera = new NetEntity(10);
        var secondCamera = new NetEntity(20);
        var control = new RMCCameraNetworkEditorControl();

        control.SetState(EditorState(1,
            [Network(firstNetwork, "First", RMCCameraNetworkEditorOrigin.Seeded)],
            [new RMCCameraNetworkEditorCameraUiData(firstCamera, "First camera", [firstNetwork])]));
        var reusedRow = (RMCCameraNetworkEditorCameraRow) control.CameraRows.GetChild(0);

        control.SetState(EditorState(9,
            [
                Network(firstNetwork, "First", RMCCameraNetworkEditorOrigin.Seeded),
                Network(secondNetwork, "Second", RMCCameraNetworkEditorOrigin.Owned),
            ],
            [new RMCCameraNetworkEditorCameraUiData(secondCamera, "Second camera", [secondNetwork])]));
        var currentRow = (RMCCameraNetworkEditorCameraRow) control.CameraRows.GetChild(0);
        currentRow.Activate();
        control.CameraNameEdit.Text = "Second camera renamed";
        control.SetCameraMembership(firstNetwork, true);
        var message = control.BuildSaveCameraMessage();

        Assert.Multiple(() =>
        {
            Assert.That(currentRow, Is.SameAs(reusedRow));
            Assert.That(control.SelectedCamera, Is.EqualTo(secondCamera));
            Assert.That(message, Is.Not.Null);
            Assert.That(message!.Revision, Is.EqualTo(9));
            Assert.That(message.Camera, Is.EqualTo(secondCamera));
            Assert.That(message.Name, Is.EqualTo("Second camera renamed"));
            Assert.That(message.Networks, Is.EquivalentTo(new[] { firstNetwork, secondNetwork }));
        });
    }

    [Test]
    public void RmcEditorControlBuildsCommandsFromLatestRevisionAndSelection()
    {
        var seeded = new NetEntity(1);
        var control = new RMCCameraNetworkEditorControl();
        control.SetState(EditorState(3,
            [Network(seeded, "Seeded", RMCCameraNetworkEditorOrigin.Seeded)], []));
        control.SelectNetwork(seeded);
        control.NetworkNameEdit.Text = "Renamed subnet";

        Assert.Multiple(() =>
        {
            Assert.That(control.BuildCreateNetworkMessage("Created subnet").Revision, Is.EqualTo(3));
            Assert.That(control.BuildRenameNetworkMessage()!.Revision, Is.EqualTo(3));
            Assert.That(control.BuildRenameNetworkMessage()!.Network, Is.EqualTo(seeded));
            Assert.That(control.BuildRenameNetworkMessage()!.Name, Is.EqualTo("Renamed subnet"));
            Assert.That(control.BuildSetHiddenMessage(true)!.Network, Is.EqualTo(seeded));
        });

        control.SetState(EditorState(7,
            [Network(seeded, "Server update", RMCCameraNetworkEditorOrigin.Seeded)], []));
        control.SelectNetwork(seeded);
        Assert.That(control.BuildSetHiddenMessage(false)!.Revision, Is.EqualTo(7));
    }

    [Test]
    public void RmcEditorControlShowsHiddenSeededAndOwnedNetworkActions()
    {
        var seeded = new NetEntity(1);
        var owned = new NetEntity(2);
        var control = new RMCCameraNetworkEditorControl();
        control.SetState(EditorState(4,
        [
            Network(seeded, "Hidden seeded", RMCCameraNetworkEditorOrigin.Seeded, true),
            Network(owned, "Owned", RMCCameraNetworkEditorOrigin.Owned),
        ], []));

        control.SelectNetwork(seeded);
        Assert.Multiple(() =>
        {
            Assert.That(control.HideNetworkButton.Visible, Is.True);
            Assert.That(control.DeleteNetworkButton.Visible, Is.False);
            Assert.That(control.BuildSetHiddenMessage(false)!.Hidden, Is.False);
        });

        control.SelectNetwork(owned);
        Assert.Multiple(() =>
        {
            Assert.That(control.HideNetworkButton.Visible, Is.False);
            Assert.That(control.DeleteNetworkButton.Visible, Is.True);
            Assert.That(control.BuildDeleteNetworkMessage()!.Network, Is.EqualTo(owned));
        });
    }

    [Test]
    public void RmcEditorControlKeepsUnassignedCameraSearchable()
    {
        var camera = new NetEntity(30);
        var control = new RMCCameraNetworkEditorControl();
        control.SetState(EditorState(1, [],
            [new RMCCameraNetworkEditorCameraUiData(camera, "Unassigned north hall", [])]));

        control.FilterCameras("north");
        Assert.That(control.CameraRows.GetChild(0).Visible, Is.True);
        control.FilterCameras("south");
        Assert.That(control.CameraRows.GetChild(0).Visible, Is.False);
        control.FilterCameras(string.Empty);
        ((RMCCameraNetworkEditorCameraRow) control.CameraRows.GetChild(0)).Activate();

        Assert.Multiple(() =>
        {
            Assert.That(control.SelectedCamera, Is.EqualTo(camera));
            Assert.That(control.BuildSaveCameraMessage()!.Networks, Is.Empty);
        });
    }

    [Test]
    public void RmcEditorControlDisplaysTargetedServerError()
    {
        var control = new RMCCameraNetworkEditorControl();
        control.SetState(EditorState(5, [], []));
        control.SetPending(true);

        control.ShowResult(new RMCCameraNetworkEditorResultBuiMsg(
            RMCCameraNetworkEditorError.AccessDenied,
            5));
        control.SetState(EditorState(5, [], []));

        Assert.Multiple(() =>
        {
            Assert.That(control.Pending, Is.False);
            Assert.That(control.ErrorLabel.Visible, Is.True);
            Assert.That(control.ErrorLabel.Text, Is.Not.Empty);
        });

        control.SetState(EditorState(6, [], []));
        Assert.That(control.ErrorLabel.Visible, Is.False);
    }

    [Test]
    public void RmcNetworkSelectorUsesLocalizedLabelsStableIdsAndSelectedNetworkMessage()
    {
        var first = new NetEntity(1);
        var second = new NetEntity(2);
        var selector = new OptionButton();

        var state = new CameraSessionDirectoryUiData(null, null,
            [
                new CameraSessionNetworkUiData(first, "Localized network A"),
                new CameraSessionNetworkUiData(second, "Localized network B"),
            ],
            second,
            [],
            false);
        RMCCameraBui.PopulateNetworkSelector(selector, state);
        var message = RMCCameraBui.GetNetworkSelectionMessage(
            new OptionButton.ItemSelectedEventArgs(0, selector));

        Assert.Multiple(() =>
        {
            Assert.That(selector.ItemCount, Is.EqualTo(2));
            Assert.That(SelectorItemText(selector, 0), Is.EqualTo("Localized network A"));
            Assert.That(SelectorItemText(selector, 1), Is.EqualTo("Localized network B"));
            Assert.That(selector.SelectedMetadata, Is.EqualTo(second));
            Assert.That(message.Network, Is.EqualTo(first));
        });
    }

    [Test]
    public void ReusedRmcCameraButtonSelectsItsUpdatedCamera()
    {
        var binding = new RmcCameraRowBinding();
        var first = new NetEntity(10);
        var movedCamera = new NetEntity(20);

        binding.Bind(first);
        binding.Bind(movedCamera);

        Assert.That(binding.CreateSelectionMessage().Camera, Is.EqualTo(movedCamera));
    }

    [Test]
    public void KeepsExistingGridSelection()
    {
        var first = new NetEntity(1);
        var second = new NetEntity(2);
        var state = State(null, first, second);

        var selected = CameraMapSelection.SelectGrid(state, second);

        Assert.That(selected, Is.EqualTo(second));
    }

    [Test]
    public void KeepsExplicitGridSelectionWhenActiveCameraChangesGrid()
    {
        var first = new NetEntity(1);
        var second = new NetEntity(2);
        var activeCamera = new NetEntity(20);
        var state = new CameraMapUiState(null,
        [
            new CameraMapGridUiData(first, "first",
            [new CameraMapMarkerUiData(new NetEntity(10), Vector2.Zero, "first-camera", true)]),
            new CameraMapGridUiData(second, "second",
            [new CameraMapMarkerUiData(activeCamera, Vector2.One, "second-camera", true)]),
        ]);

        var selected = CameraMapSelection.SelectGrid(state, first, activeCamera);

        Assert.That(selected, Is.EqualTo(first));
    }

    [Test]
    public void UsesActiveCameraGridForInitialSelection()
    {
        var first = new NetEntity(1);
        var second = new NetEntity(2);
        var activeCamera = new NetEntity(20);
        var state = new CameraMapUiState(null,
        [
            new CameraMapGridUiData(first, "first", []),
            new CameraMapGridUiData(second, "second",
                [new CameraMapMarkerUiData(activeCamera, Vector2.One, "camera", true)]),
        ]);

        Assert.That(CameraMapSelection.SelectGrid(state, null, activeCamera), Is.EqualTo(second));
    }

    [Test]
    public void ActiveCameraGridFallsBackWhenCameraIsMissing()
    {
        var first = new NetEntity(1);
        var second = new NetEntity(2);
        var missingCamera = new NetEntity(20);
        var state = State(null, first, second);

        var selected = CameraMapSelection.SelectGrid(state, second, missingCamera);

        Assert.That(selected, Is.EqualTo(second));
    }

    [Test]
    public void PrefersConsoleGridInitially()
    {
        var first = new NetEntity(1);
        var console = new NetEntity(2);
        var state = State(console, first, console);

        var selected = CameraMapSelection.SelectGrid(state, null);

        Assert.That(selected, Is.EqualTo(console));
    }

    [Test]
    public void FallsBackToFirstServerOrderedGrid()
    {
        var first = new NetEntity(1);
        var second = new NetEntity(2);
        var state = State(new NetEntity(3), first, second);

        var selected = CameraMapSelection.SelectGrid(state, null);

        Assert.That(selected, Is.EqualTo(first));
    }

    [Test]
    public void ReturnsNullForNoGrids()
    {
        var state = new CameraMapUiState(null, []);

        var selected = CameraMapSelection.SelectGrid(state, new NetEntity(1));

        Assert.That(selected, Is.Null);
    }

    [Test]
    public void TwoViewersDoNotShareGridSelection()
    {
        var first = new NetEntity(1);
        var second = new NetEntity(2);
        var state = State(null, first, second);

        var firstViewerSelection = CameraMapSelection.SelectGrid(state, first);
        var secondViewerSelection = CameraMapSelection.SelectGrid(state, second);

        Assert.Multiple(() =>
        {
            Assert.That(firstViewerSelection, Is.EqualTo(first));
            Assert.That(secondViewerSelection, Is.EqualTo(second));
        });
    }

    [Test]
    public void RightClickSelectsOnlyActiveCameraMarkers()
    {
        var active = new CameraMapMarkerUiData(new NetEntity(10), Vector2.Zero, "active", true);
        var inactive = new CameraMapMarkerUiData(new NetEntity(11), Vector2.One, "inactive", false);

        Assert.Multiple(() =>
        {
            Assert.That(CameraMapSelection.IsRightClickSelectable(active), Is.True);
            Assert.That(CameraMapSelection.IsRightClickSelectable(inactive), Is.False);
        });
    }

    [Test]
    public void InvalidMarkersUseRedStatusAndAreNotSelectable()
    {
        var invalid = new CameraMapMarkerUiData(new NetEntity(12), Vector2.Zero, "invalid",
            CameraMapMarkerStatus.Invalid);

        Assert.Multiple(() =>
        {
            Assert.That(CameraMapSelection.IsRightClickSelectable(invalid), Is.False);
            Assert.That(CameraMapSelection.GetMarkerColor(invalid.Status, false), Is.EqualTo(Color.Red));
        });
    }

    [Test]
    public void CameraMapRetriesGridBindingAfterEntityReplicates()
    {
        var gridNetEntity = new NetEntity(42);
        var resolvedGrid = EntityUid.Invalid;
        var binding = new CameraMapGridBinding(_ => resolvedGrid);
        binding.Select(gridNetEntity);

        Assert.That(binding.TryResolve(out _), Is.False);

        resolvedGrid = new EntityUid(123);

        Assert.Multiple(() =>
        {
            Assert.That(binding.TryResolve(out var grid), Is.True);
            Assert.That(grid, Is.EqualTo(resolvedGrid));
            Assert.That(binding.ResolvedGrid, Is.EqualTo(resolvedGrid));
        });

        resolvedGrid = EntityUid.Invalid;

        Assert.Multiple(() =>
        {
            Assert.That(binding.TryResolve(out _), Is.False);
            Assert.That(binding.ResolvedGrid, Is.Null);
        });
    }

    private static CameraMapUiState State(NetEntity? consoleGrid, params NetEntity[] grids)
    {
        var data = new List<CameraMapGridUiData>();
        foreach (var grid in grids)
        {
            data.Add(new CameraMapGridUiData(grid, grid.ToString(), []));
        }

        return new CameraMapUiState(consoleGrid, data);
    }

    private static RMCCameraNetworkEditorUiState EditorState(
        uint revision,
        List<RMCCameraNetworkEditorNetworkUiData> networks,
        List<RMCCameraNetworkEditorCameraUiData> cameras)
    {
        return new RMCCameraNetworkEditorUiState(revision, networks, cameras);
    }

    private static RMCCameraNetworkEditorNetworkUiData Network(
        NetEntity id,
        string name,
        RMCCameraNetworkEditorOrigin origin,
        bool hidden = false)
    {
        return new RMCCameraNetworkEditorNetworkUiData(id, name, origin, hidden);
    }

    private static string SelectorItemText(OptionButton selector, int index)
    {
        var container = selector.OptionsScroll.GetChild(0).GetChild(1);
        return ((Button) container.GetChild(index)).Text;
    }
}
