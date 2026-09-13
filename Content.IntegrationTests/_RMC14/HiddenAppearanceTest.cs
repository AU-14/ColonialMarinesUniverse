#pragma warning disable RA0002 // Integration regression intentionally inspects restricted component state.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server.Humanoid;
using Content.Shared._RMC14.CCVar;
using Content.Shared._RMC14.Humanoid;
using Content.Shared._RMC14.UniformAccessories;
using Content.Shared.Body;
using Content.Shared.CCVar;
using Content.Shared.Clothing;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Client.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;
using ClientHiddenAppearanceSystem = Content.Client._RMC14.Humanoid.HiddenAppearanceSystem;
using ServerHiddenAppearanceSystem = Content.Server._RMC14.Humanoid.HiddenAppearanceSystem;

namespace Content.IntegrationTests._RMC14;

[TestOf(typeof(ClientHiddenAppearanceSystem))]
public sealed class HiddenAppearanceTest : InteractionTest
{
    private static readonly Color HiddenEyeColor = Color.Cyan;
    private static readonly Color HiddenSkinColor = Color.Teal;
    private static readonly Color HiddenHairColor = Color.Magenta;
    private static readonly Color AuthoritativeEyeColor = Color.Yellow;
    private static readonly Color AuthoritativeSkinColor = Color.Orange;

    protected override string PlayerPrototype => "CMXenoDrone";

    [SidedDependency(Side.Server)] private BodySystem _serverBody = default!;
    [SidedDependency(Side.Server)] private SharedContainerSystem _serverContainers = default!;
    [SidedDependency(Side.Server)] private HumanoidOrganAppearanceSystem _serverOrganAppearance = default!;
    [SidedDependency(Side.Server)] private ServerHiddenAppearanceSystem _serverHidden = default!;
    [SidedDependency(Side.Server)] private InventorySystem _serverInventory = default!;
    [SidedDependency(Side.Server)] private IRobustSerializer _serverSerializer = default!;
    [SidedDependency(Side.Server)] private SharedVisualBodySystem _serverVisualBody = default!;

    [SidedDependency(Side.Client)] private BodySystem _clientBody = default!;
    [SidedDependency(Side.Client)] private ClientHiddenAppearanceSystem _clientHidden = default!;
    [SidedDependency(Side.Client)] private IRobustSerializer _clientSerializer = default!;
    [SidedDependency(Side.Client)] private SpriteSystem _clientSprite = default!;

    private EntityUid _serverTarget;
    private NetEntity _target;

    [Test]
    public async Task StateAndViewerArrivalOrderRestoresCanonicalAppearanceWithoutAuthorityMutation()
    {
        await SpawnTarget();

        var serverBefore = CaptureAuthoritativeState(
            SEntMan,
            _serverBody,
            _serverSerializer,
            _serverTarget);
        var clientBefore = CaptureAuthoritativeState(
            CEntMan,
            _clientBody,
            _clientSerializer,
            ClientTarget);

        await AssertClientOverride(active: false, markingLayers: 0);

        // The Xeno viewer is attached before the hidden state arrives.
        await SetHiddenAppearance(duplicateHair: false, includeUnderwear: true);
        var singleHairCount = await GetClientMarkingLayerCount();
        Assert.That(singleHairCount, Is.GreaterThan(0));

        // Replacing the snapshot must clear old local keys. Two identical marking IDs must still produce two layers.
        await SetHiddenAppearance(duplicateHair: true, includeUnderwear: true);
        var duplicateHairCount = await GetClientMarkingLayerCount();
        Assert.That(duplicateHairCount, Is.EqualTo(singleHairCount + 1));

        await SetHiddenAppearance(duplicateHair: true, includeUnderwear: true);
        await AssertClientOverride(active: true, markingLayers: duplicateHairCount);

        EntityUid ordinaryViewer = default;
        await Server.WaitPost(() =>
        {
            ordinaryViewer = SEntMan.SpawnEntity("CMMobHuman", SEntMan.GetCoordinates(PlayerCoords));
            Server.PlayerMan.SetAttachedEntity(ServerSession!, ordinaryViewer);
        });
        await RunTicks(5);

        // The state exists before this non-whitelisted viewer attaches, but must not affect presentation.
        await AssertClientOverride(active: false, markingLayers: 0);

        await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(ServerSession!, SPlayer));
        await RunTicks(5);
        await AssertClientOverride(active: true, markingLayers: duplicateHairCount);

        AssertAuthoritativeStateUnchanged(serverBefore, clientBefore);

        // Removing the component from a corpse is the canonical reveal path.
        await Server.WaitPost(() =>
        {
            Server.System<MobStateSystem>().ChangeMobState(_serverTarget, MobState.Dead);
            Assert.That(SEntMan.RemoveComponent<HiddenAppearanceComponent>(_serverTarget), Is.True);
        });
        await RunTicks(5);

        await AssertClientOverride(active: false, markingLayers: 0);
        AssertAuthoritativeStateUnchanged(serverBefore, clientBefore);

    }

    [Test]
    public async Task OrganLifecycleEquipmentVisibilityAndInventoryRefreshStayLocal()
    {
        await SpawnTarget();
        await SetHiddenAppearance(duplicateHair: true, includeUnderwear: true);
        var fullMarkingCount = await GetClientMarkingLayerCount();

        await AssertClientHairLayers(visible: true, expected: 2);

        EntityUid helmet = default;
        await Server.WaitPost(() =>
        {
            helmet = SEntMan.SpawnEntity("ClothingHeadHelmetBasic", SEntMan.GetCoordinates(TargetCoords));
            Assert.That(_serverInventory.TryEquip(
                    _serverTarget,
                    helmet,
                    "head",
                    silent: true,
                    force: true),
                Is.True);
        });
        await RunTicks(5);

        await AssertClientHairLayers(visible: false, expected: 2);

        NetEntity helmetNet = default;
        await Client.WaitPost(() => Client.System<HiddenInventoryVisualProbeSystem>().Watch(ClientTarget));
        await Server.WaitPost(() => helmetNet = SEntMan.GetNetEntity(helmet));
        await SetHiddenAppearance(duplicateHair: true, includeUnderwear: true);
        await Client.WaitAssertion(() =>
        {
            var probe = Client.System<HiddenInventoryVisualProbeSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(probe.Refreshes, Is.EqualTo(1));
                Assert.That(probe.LastItem, Is.EqualTo(helmetNet));
            });
        });

        await Server.WaitPost(() =>
            Assert.That(_serverInventory.TryUnequip(
                    _serverTarget,
                    "head",
                    silent: true,
                    force: true),
                Is.True));
        await RunTicks(5);
        await AssertClientHairLayers(visible: true, expected: 2);

        EntityUid hairOrgan = default;
        NetEntity hairOrganNet = default;
        Container organContainer = default!;
        await Server.WaitPost(() =>
        {
            hairOrgan = GetHairOrgan(SEntMan, _serverBody, _serverTarget);
            hairOrganNet = SEntMan.GetNetEntity(hairOrgan);
            organContainer = SEntMan.GetComponent<BodyComponent>(_serverTarget).Organs!;
            Assert.That(_serverContainers.Remove(hairOrgan, organContainer), Is.True);
        });
        await RunTicks(5);

        await AssertClientOverrideLessThan(active: true, fullMarkingCount);
        await AssertClientHairLayers(visible: false, expected: 0);
        await Client.WaitAssertion(() =>
        {
            var clientOrgan = ToClient(hairOrganNet);
            Assert.That(CEntMan.GetComponent<OrganComponent>(clientOrgan).Body, Is.Null);
        });

        await Server.WaitPost(() =>
            Assert.That(_serverContainers.Insert(hairOrgan, organContainer, force: true), Is.True));
        await RunTicks(5);

        await AssertClientOverride(active: true, markingLayers: fullMarkingCount);
        await AssertClientHairLayers(visible: true, expected: 2);

        await Server.WaitPost(() =>
        {
            _serverVisualBody.ApplyProfile(_serverTarget, new OrganProfileData
            {
                Sex = Sex.Female,
                EyeColor = AuthoritativeEyeColor,
                SkinColor = AuthoritativeSkinColor,
            });

            var category = SEntMan.GetComponent<OrganComponent>(hairOrgan).Category!.Value;
            _serverVisualBody.ApplyMarkings(_serverTarget, new()
            {
                [category] = new()
                {
                    [HumanoidVisualLayers.Hair] =
                    [
                        new Marking("HumanHairBob", 1).WithColor(Color.Blue),
                    ],
                },
            });
        });
        await RunTicks(5);

        await Client.WaitAssertion(() =>
        {
            var clientOrgan = ToClient(hairOrganNet);
            var visual = CEntMan.GetComponent<VisualOrganComponent>(clientOrgan);
            var markings = CEntMan.GetComponent<VisualOrganMarkingsComponent>(clientOrgan);
            var headLayer = _clientSprite.LayerMapGet(ClientTarget, HumanoidVisualLayers.Head);
            var sprite = CEntMan.GetComponent<SpriteComponent>(ClientTarget);

            Assert.Multiple(() =>
            {
                Assert.That(visual.Profile.SkinColor, Is.EqualTo(AuthoritativeSkinColor));
                Assert.That(markings.Markings[HumanoidVisualLayers.Hair].Single().MarkingId.Id,
                    Is.EqualTo("HumanHairBob"));
                Assert.That(sprite[headLayer].Color, Is.EqualTo(HiddenSkinColor));
            });
        });
        await AssertClientHairLayers(visible: true, expected: 2);

        var serverAfterStateUpdate = CaptureAuthoritativeState(
            SEntMan,
            _serverBody,
            _serverSerializer,
            _serverTarget);
        var clientAfterStateUpdate = CaptureAuthoritativeState(
            CEntMan,
            _clientBody,
            _clientSerializer,
            ClientTarget);

        await SetHiddenAppearance(duplicateHair: true, includeUnderwear: true);
        AssertAuthoritativeStateUnchanged(serverAfterStateUpdate, clientAfterStateUpdate);
    }

    [Test]
    public async Task IdentityAndNudityCVarTransitionsRefreshWithoutDuplicateOrAuthoritativeStateChanges()
    {
        await OverrideCVar(Side.Client, CCVars.AccessibilityClientCensorNudity, false);
        await OverrideCVar(Side.Server, CCVars.AccessibilityServerCensorNudity, false);
        await Client.WaitAssertion(() =>
        {
            var group = CProtoMan.Index<MarkingsGroupPrototype>("Human");
            Assert.Multiple(() =>
            {
                Assert.That(
                    group.Limits[HumanoidVisualLayers.UndergarmentTop].NudityDefault.Select(id => id.Id),
                    Is.EqualTo(new[] { "UndergarmentTopTanktop" }));
                Assert.That(
                    group.Limits[HumanoidVisualLayers.UndergarmentBottom].NudityDefault.Select(id => id.Id),
                    Is.EqualTo(new[] { "UndergarmentBottomBoxers" }));
            });
        });

        await SpawnTarget();
        await Client.WaitAssertion(() =>
            Assert.That(_clientHidden.HidePlayerIdentities, Is.True));
        await SetHiddenAppearance(duplicateHair: true, includeUnderwear: false);
        var uncensoredCount = await GetClientMarkingLayerCount();

        var serverBefore = CaptureAuthoritativeState(
            SEntMan,
            _serverBody,
            _serverSerializer,
            _serverTarget);
        var clientBefore = CaptureAuthoritativeState(
            CEntMan,
            _clientBody,
            _clientSerializer,
            ClientTarget);

        await OverrideCVar(Side.Client, CCVars.AccessibilityClientCensorNudity, true);
        await AssertClientOverride(active: true, markingLayers: uncensoredCount + 2);

        await OverrideCVar(Side.Server, CCVars.AccessibilityServerCensorNudity, true);
        await AssertClientOverride(active: true, markingLayers: uncensoredCount + 2);

        await OverrideCVar(Side.Client, CCVars.AccessibilityClientCensorNudity, false);
        await AssertClientOverride(active: true, markingLayers: uncensoredCount + 2);

        await OverrideCVar(Side.Server, CCVars.AccessibilityServerCensorNudity, false);
        await AssertClientOverride(active: true, markingLayers: uncensoredCount);

        await SetHiddenAppearance(duplicateHair: true, includeUnderwear: false);
        await AssertClientOverride(active: true, markingLayers: uncensoredCount);
        AssertAuthoritativeStateUnchanged(serverBefore, clientBefore);

        await OverrideCVar(Side.Server, RMCCVars.HidePlayerIdentities, false);
        await Client.WaitAssertion(() =>
            Assert.That(_clientHidden.HidePlayerIdentities, Is.False));
        await AssertClientOverride(active: false, markingLayers: 0);
        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.HasComponent<HiddenAppearanceComponent>(_serverTarget), Is.False));
        AssertAuthoritativeStateUnchanged(serverBefore, clientBefore);
    }

    [Test]
    public async Task UniformAccessoriesAreHiddenOnlyFromLocalXenosWhileIdentityHidingIsEnabled()
    {
        EntityUid serverUniform = default;
        NetEntity uniform = default;
        await Server.WaitPost(() =>
        {
            serverUniform = SEntMan.SpawnEntity("RMCJumpsuitMarinePatch", SEntMan.GetCoordinates(TargetCoords));
            uniform = SEntMan.GetNetEntity(serverUniform);
        });
        await RunTicks(5);

        var clientUniform = ToClient(uniform);
        await Client.WaitAssertion(() =>
        {
            Assert.That(_clientHidden.HidePlayerIdentities, Is.True);
            Assert.That(CEntMan.HasComponent<UniformAccessoryHolderComponent>(clientUniform), Is.True);
            Assert.That(AccessoryVisualCount(clientUniform), Is.Zero,
                "the local Xeno must not receive medal/accessory visuals while identity hiding is enabled");
        });

        EntityUid ordinaryViewer = default;
        await Server.WaitPost(() =>
        {
            ordinaryViewer = SEntMan.SpawnEntity("CMMobHuman", SEntMan.GetCoordinates(PlayerCoords));
            Server.PlayerMan.SetAttachedEntity(ServerSession!, ordinaryViewer);
        });
        await RunTicks(5);
        await Client.WaitAssertion(() =>
            Assert.That(AccessoryVisualCount(clientUniform), Is.EqualTo(1),
                "identity hiding must not suppress accessories for an ordinary local viewer"));

        await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(ServerSession!, SPlayer));
        await RunTicks(5);
        await OverrideCVar(Side.Server, RMCCVars.HidePlayerIdentities, false);
        await Client.WaitAssertion(() =>
        {
            Assert.That(_clientHidden.HidePlayerIdentities, Is.False);
            Assert.That(AccessoryVisualCount(clientUniform), Is.EqualTo(1),
                "disabling identity hiding must restore accessory visuals for the local Xeno");
        });

    }

    private EntityUid ClientTarget => ToClient(_target);

    private int AccessoryVisualCount(EntityUid holder)
    {
        var visuals = new GetEquipmentVisualsEvent(CPlayer, "jumpsuit");
        CEntMan.EventBus.RaiseLocalEvent(holder, visuals);
        return visuals.Layers.Count(layer =>
            layer.Item1.StartsWith("uniform-accessory-", StringComparison.Ordinal) ||
            layer.Item1.StartsWith($"enum.{nameof(UniformAccessoryLayer)}.", StringComparison.Ordinal));
    }

    private async Task SpawnTarget()
    {
        await Server.WaitPost(() =>
        {
            _serverTarget = SEntMan.SpawnEntity("CMMobHuman", SEntMan.GetCoordinates(TargetCoords));
            _target = SEntMan.GetNetEntity(_serverTarget);
        });
        await RunTicks(5);

        await Client.WaitAssertion(() =>
        {
            Assert.That(CEntMan.EntityExists(ClientTarget), Is.True);
            Assert.That(CEntMan.HasComponent<HiddenAppearanceComponent>(ClientTarget), Is.True);
        });
    }

    private async Task SetHiddenAppearance(bool duplicateHair, bool includeUnderwear)
    {
        await Server.WaitPost(() =>
        {
            Assert.That(_serverOrganAppearance.TryGetAppearance(
                    _serverTarget,
                    out _,
                    out _,
                    out var authoritativeMarkings),
                Is.True);

            var appearance = new HumanoidCharacterAppearance(
                HiddenEyeColor,
                HiddenSkinColor,
                authoritativeMarkings).Clone();
            Assert.That(_serverOrganAppearance.TryGetMarkings(
                    _serverTarget,
                    HumanoidVisualLayers.Hair,
                    out var hairOrgan,
                    out _,
                    out _),
                Is.True);
            if (!appearance.Markings.TryGetValue(hairOrgan, out var hairLayers))
            {
                hairLayers = new Dictionary<HumanoidVisualLayers, List<Marking>>();
                appearance.Markings[hairOrgan] = hairLayers;
            }

            if (!hairLayers.TryGetValue(HumanoidVisualLayers.Hair, out var hairSet))
            {
                hairSet = new List<Marking>();
                hairLayers[HumanoidVisualLayers.Hair] = hairSet;
            }

            hairSet.Clear();
            hairSet.Add(new Marking("HumanHairAfro", 1).WithColor(HiddenHairColor));
            if (duplicateHair)
                hairSet.Add(new Marking("HumanHairAfro", 1).WithColor(HiddenHairColor));

            if (!includeUnderwear)
            {
                foreach (var layers in appearance.Markings.Values)
                {
                    layers.Remove(HumanoidVisualLayers.UndergarmentTop);
                    layers.Remove(HumanoidVisualLayers.UndergarmentBottom);
                }
            }

            var profile = SEntMan.GetComponent<HumanoidProfileComponent>(_serverTarget);
            _serverHidden.SetHiddenAppearance(
                _serverTarget,
                new HiddenHumanoidAppearance(profile.Species, profile.Sex, appearance));
        });
        await RunTicks(5);
    }

    private async Task<int> GetClientMarkingLayerCount()
    {
        var result = 0;
        await Client.WaitAssertion(() =>
        {
            Assert.That(_clientHidden.IsLocalAppearanceOverrideActive(ClientTarget), Is.True);
            result = _clientHidden.LocalMarkingLayerCount(ClientTarget);
        });
        return result;
    }

    private async Task AssertClientOverride(bool active, int markingLayers)
    {
        await Client.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(_clientHidden.IsLocalAppearanceOverrideActive(ClientTarget), Is.EqualTo(active));
                Assert.That(
                    _clientHidden.LocalMarkingLayerCount(ClientTarget),
                    Is.EqualTo(markingLayers));
            });
        });
    }

    private async Task AssertClientOverrideLessThan(bool active, int markingLayers)
    {
        await Client.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(_clientHidden.IsLocalAppearanceOverrideActive(ClientTarget), Is.EqualTo(active));
                Assert.That(_clientHidden.LocalMarkingLayerCount(ClientTarget), Is.LessThan(markingLayers));
            });
        });
    }

    private async Task AssertClientHairLayers(bool visible, int expected)
    {
        await Client.WaitAssertion(() =>
        {
            var sprite = CEntMan.GetComponent<SpriteComponent>(ClientTarget);
            var matching = sprite.AllLayers.Count(layer =>
                layer.Color == HiddenHairColor && layer.Visible == visible);
            Assert.That(matching, Is.EqualTo(expected));
        });
    }

    private void AssertAuthoritativeStateUnchanged(
        IReadOnlyDictionary<string, byte[]> serverBefore,
        IReadOnlyDictionary<string, byte[]> clientBefore)
    {
        var serverAfter = CaptureAuthoritativeState(
            SEntMan,
            _serverBody,
            _serverSerializer,
            _serverTarget);
        var clientAfter = CaptureAuthoritativeState(
            CEntMan,
            _clientBody,
            _clientSerializer,
            ClientTarget);

        AssertStateBytesEqual(serverBefore, serverAfter, "server");
        AssertStateBytesEqual(clientBefore, clientAfter, "client");
    }

    private static IReadOnlyDictionary<string, byte[]> CaptureAuthoritativeState(
        IEntityManager entities,
        BodySystem bodySystem,
        IRobustSerializer serializer,
        EntityUid body)
    {
        var result = new Dictionary<string, byte[]>();
        AddState(result, entities, serializer, body, entities.GetComponent<HumanoidProfileComponent>(body));

        var bodyComponent = entities.GetComponent<BodyComponent>(body);
        foreach (var organ in bodyComponent.Organs!.ContainedEntities.OrderBy(uid => uid.Id))
        {
            AddState(result, entities, serializer, organ, entities.GetComponent<OrganComponent>(organ));
            if (entities.TryGetComponent(organ, out VisualOrganComponent? visual))
                AddState(result, entities, serializer, organ, visual);
            if (entities.TryGetComponent(organ, out VisualOrganMarkingsComponent? markings))
                AddState(result, entities, serializer, organ, markings);
        }

        // Ensure the body system agrees with the container graph whose states were fingerprinted.
        Assert.That(bodySystem.EnumerateOrgans<OrganComponent>(body).Select(ent => ent.Owner),
            Is.EquivalentTo(bodyComponent.Organs.ContainedEntities));
        return result;
    }

    private static void AddState(
        IDictionary<string, byte[]> result,
        IEntityManager entities,
        IRobustSerializer serializer,
        EntityUid uid,
        IComponent component)
    {
        var state = entities.GetComponentState(entities.EventBus, component, null, GameTick.Zero);
        Assert.That(state, Is.Not.Null);

        using var stream = new MemoryStream();
        serializer.Serialize(stream, state!);
        result.Add($"{uid.Id}:{component.GetType().FullName}", stream.ToArray());
    }

    private static void AssertStateBytesEqual(
        IReadOnlyDictionary<string, byte[]> before,
        IReadOnlyDictionary<string, byte[]> after,
        string side)
    {
        Assert.That(after.Keys, Is.EquivalentTo(before.Keys), $"{side} authoritative component set changed");
        foreach (var (key, value) in before)
            Assert.That(after[key], Is.EqualTo(value), $"{side} authoritative state changed for {key}");
    }

    private static EntityUid GetHairOrgan(IEntityManager entities, BodySystem bodySystem, EntityUid body)
    {
        foreach (var organ in bodySystem.EnumerateOrgans<VisualOrganMarkingsComponent>(body))
        {
            if (organ.Comp2.MarkingData.Layers.Contains(HumanoidVisualLayers.Hair))
                return organ.Owner;
        }

        Assert.Fail("Body has no organ that owns the Hair marking layer");
        return EntityUid.Invalid;
    }
}

[RegisterComponent]
public sealed partial class HiddenInventoryVisualProbeComponent : Component
{
    public int Refreshes;
    public NetEntity LastItem;
}

public sealed class HiddenInventoryVisualProbeSystem : EntitySystem
{
    private EntityUid? _watched;

    public int Refreshes => _watched is { } watched && TryComp(watched, out HiddenInventoryVisualProbeComponent? probe)
        ? probe.Refreshes
        : 0;

    public NetEntity LastItem => _watched is { } watched && TryComp(watched, out HiddenInventoryVisualProbeComponent? probe)
        ? probe.LastItem
        : default;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HiddenInventoryVisualProbeComponent, VisualsChangedEvent>(OnVisualsChanged);
    }

    public void Watch(EntityUid uid)
    {
        _watched = uid;
        var probe = EnsureComp<HiddenInventoryVisualProbeComponent>(uid);
        probe.Refreshes = 0;
        probe.LastItem = default;
    }

    private void OnVisualsChanged(Entity<HiddenInventoryVisualProbeComponent> ent, ref VisualsChangedEvent args)
    {
        if (ent.Owner != _watched)
            return;

        ent.Comp.Refreshes++;
        ent.Comp.LastItem = args.Item;
    }
}

#pragma warning restore RA0002
