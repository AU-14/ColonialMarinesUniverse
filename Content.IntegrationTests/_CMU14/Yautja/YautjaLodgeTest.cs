using System.Collections.Generic;
using System.Linq;
using Content.Shared._RMC14.Construction;
using Content.Shared._RMC14.Construction.Prototypes;
using Content.Shared._RMC14.Dialog;
using Content.Shared.Actions.Components;
using Content.Shared.CMU14.Yautja;
using Content.Shared.CMU14.Yautja.Lodge;
using Content.Shared.Doors.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Maps;
using Content.Shared.Verbs;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class YautjaLodgeTest
{
    [Test]
    public async Task RunedRecipesNeedTheHunterSkillAndSpawn()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var protos = server.ProtoMan;
            var sheet = protos.Index<EntityPrototype>("CMUSheetRunedSandstone");
            Assert.That(sheet.TryComp<RMCConstructionItemComponent>(out var item, server.EntMan.ComponentFactory), Is.True);

            var recipes = new List<RMCConstructionPrototype>();
            foreach (var id in item!.Buildable!)
            {
                var recipe = protos.Index(id);
                if (recipe.Listed is { } listed)
                    recipes.AddRange(listed.Select(protos.Index));
                else
                    recipes.Add(recipe);
            }

            // 2 doors, 5 walls, 15 floors, brazier, torch, chair, bed, 3 statues
            Assert.That(recipes, Has.Count.EqualTo(29));
            foreach (var recipe in recipes)
            {
                Assert.Multiple(() =>
                {
                    Assert.That(recipe.Skill?.Id, Is.EqualTo("RMCSkillAntagonist"), recipe.ID);
                    Assert.That(recipe.RequiredSkillLevel, Is.EqualTo(2), recipe.ID);
                    Assert.That(recipe.MaterialCost, Is.GreaterThan(0), recipe.ID);
                });

                var spawned = server.EntMan.SpawnEntity(recipe.Prototype, map.GridCoords);
                server.EntMan.DeleteEntity(spawned);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TempleFloorApplierLaysTheTile()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitPost(() => server.EntMan.SpawnEntity("CMUYautjaTileApplierDecoDiamond", map.GridCoords));
        await pair.RunTicksSync(2);

        await server.WaitAssertion(() =>
        {
            var tile = server.System<SharedMapSystem>().GetTileRef(map.Grid, map.GridCoords);
            var def = (ContentTileDefinition) server.ResolveDependency<ITileDefinitionManager>()[tile.Tile.TypeId];
            Assert.That(def.ID, Is.EqualTo("CMUYautjaTileTempleDecoDiamond"));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task WallTorchMountsInFrontOfTheWall()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var mapSystem = server.System<SharedMapSystem>();
            var grid = map.Grid;
            mapSystem.SetTile(grid, new Vector2i(0, 1), map.Tile.Tile);

            var wall = entMan.SpawnEntity("CMUYautjaTempleWall", mapSystem.GridTileToLocal(grid, grid, new Vector2i(0, 1)));
            var user = entMan.SpawnEntity("CMMobHuman", mapSystem.GridTileToLocal(grid, grid, new Vector2i(0, 0)));
            var frame = entMan.SpawnEntity("CMUYautjaWallTorchFrame", map.GridCoords);

            var ev = new AfterInteractEvent(user, frame, wall, entMan.GetComponent<TransformComponent>(wall).Coordinates, true);
            entMan.EventBus.RaiseLocalEvent(frame, ev);

            Assert.That(ev.Handled, Is.True);
            Assert.That(entMan.IsQueuedForDeletion(frame) || entMan.Deleted(frame), Is.True);

            var torches = new List<EntityUid>();
            var all = entMan.AllEntityQueryEnumerator<MetaDataComponent, TransformComponent>();
            while (all.MoveNext(out var uid, out var meta, out var xform))
            {
                if (meta.EntityPrototype?.ID == "CMUYautjaWallTorch" && xform.GridUid == grid.Owner)
                    torches.Add(uid);
            }

            Assert.That(torches, Has.Count.EqualTo(1));
            var pos = entMan.GetComponent<TransformComponent>(torches[0]).LocalPosition;
            Assert.Multiple(() =>
            {
                Assert.That(pos.X, Is.EqualTo(0.5f).Within(0.01f));
                // wall tile centre is y 1.5, pushed 0.55 toward the user on the south side
                Assert.That(pos.Y, Is.EqualTo(0.95f).Within(0.01f));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TempleDoorOnlyAnswersWristbladesAndHoldsItsLock()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid door = default;
        EntityUid hunter = default;
        EntityUid human = default;
        EntityUid wristBlades = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var hands = entMan.System<SharedHandsSystem>();

            door = entMan.SpawnEntity("CMUYautjaTempleDoor", map.GridCoords);
            hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            human = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            entMan.EnsureComponent<YautjaComponent>(hunter);

            var bracer = entMan.SpawnEntity("CMUYautjaBracer", map.GridCoords);
            var holder = entMan.SpawnEntity("CMUYautjaWristBladesAttachment", map.GridCoords);
            var action = entMan.SpawnEntity("CMUActionYautjaToggleWristBlades", map.GridCoords);
            Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);
            Assert.That(hands.TryPickupAnyHand(hunter, holder), Is.True);
            entMan.GetComponent<YautjaBracerComponent>(bracer).Charge = 300;

            entMan.EventBus.RaiseLocalEvent(bracer, new InteractUsingEvent(hunter, holder, bracer, map.GridCoords));
            RaiseDialogOption(entMan, bracer, hunter, "Left");
            wristBlades = entMan.GetComponent<YautjaStoredGearComponent>(holder).AttachedWeapon!.Value;

            var actionComp = entMan.GetComponent<ActionComponent>(action);
            entMan.EventBus.RaiseLocalEvent(bracer, new YautjaToggleWristBladesActionEvent
            {
                Performer = hunter,
                Action = (action, actionComp),
            });
            Assert.That(hands.GetActiveItem(hunter), Is.EqualTo(wristBlades));

            // built locked like cmss13, so the blades bounce off until it's unlocked
            Assert.That(entMan.GetComponent<YautjaTempleDoorComponent>(door).Locked, Is.True);
            entMan.EventBus.RaiseLocalEvent(door, new InteractUsingEvent(hunter, wristBlades, door, map.GridCoords));
            Assert.That(entMan.GetComponent<DoorComponent>(door).State, Is.EqualTo(DoorState.Closed));

            var unlock = entMan.System<SharedVerbSystem>()
                .GetLocalVerbs(door, hunter, typeof(AlternativeVerb))
                .Single(verb => verb.Text == Loc.GetString("cmu-yautja-temple-door-verb-unlock"));
            unlock.Act!.Invoke();
            Assert.That(entMan.GetComponent<YautjaTempleDoorComponent>(door).Locked, Is.False);

            // someone without wristblades gets nothing, even with the door unlocked
            var junk = entMan.SpawnEntity("CMUSheetRunedSandstone1", map.GridCoords);
            Assert.That(hands.TryPickupAnyHand(human, junk), Is.True);
            var humanTry = new InteractUsingEvent(human, junk, door, map.GridCoords);
            entMan.EventBus.RaiseLocalEvent(door, humanTry);
            Assert.That(humanTry.Handled, Is.False);
            Assert.That(entMan.GetComponent<DoorComponent>(door).State, Is.EqualTo(DoorState.Closed));
            Assert.That(entMan.System<SharedVerbSystem>().GetLocalVerbs(door, human, typeof(AlternativeVerb))
                .Any(verb => verb.Text == Loc.GetString("cmu-yautja-temple-door-verb-lock")), Is.False);

            entMan.EventBus.RaiseLocalEvent(door, new InteractUsingEvent(hunter, wristBlades, door, map.GridCoords));
            Assert.That(entMan.GetComponent<DoorComponent>(door).State, Is.EqualTo(DoorState.Opening));
        });

        await pair.RunTicksSync(pair.SecondsToTicks(3));

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            Assert.That(entMan.GetComponent<DoorComponent>(door).State, Is.EqualTo(DoorState.Open));

            // bolted open: closing gets refused
            var lockVerb = entMan.System<SharedVerbSystem>()
                .GetLocalVerbs(door, hunter, typeof(AlternativeVerb))
                .Single(verb => verb.Text == Loc.GetString("cmu-yautja-temple-door-verb-lock"));
            lockVerb.Act!.Invoke();
            Assert.That(entMan.System<Content.Shared.Doors.Systems.SharedDoorSystem>().TryClose(door), Is.False);
            Assert.That(entMan.GetComponent<DoorComponent>(door).State, Is.EqualTo(DoorState.Open));
        });

        await pair.CleanReturnAsync();
    }

    private static void RaiseDialogOption(IEntityManager entMan, EntityUid console, EntityUid user, string optionText)
    {
        var dialog = entMan.GetComponent<DialogComponent>(console);
        var optionIndex = dialog.Options
            .Select((option, index) => (option, index))
            .Single(pair => pair.option.Text == optionText)
            .index;

        entMan.EventBus.RaiseLocalEvent(console, new DialogOptionBuiMsg(optionIndex)
        {
            Actor = user,
            UiKey = DialogUiKey.Key,
        });
    }
}
