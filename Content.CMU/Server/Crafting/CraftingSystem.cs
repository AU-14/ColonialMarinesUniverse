// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using System.Linq;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.ActionBlocker;
using Content.Server.CMU14.Portable;
using Content.Shared.CMU14.Crafting;
using Content.Shared.CMU14.Portable;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Tools;
using Content.Shared.Tools.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Crafting;

// Crafting from the menu: checks the recipe (skill, tools, ingredients), runs the do-after, then takes the ingredients and gives the result.
public sealed class CraftingSystem : SharedCraftingSystem
{
    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly PortableStructureSystem _portable = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SkillsSystem _skills = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly SharedToolSystem _tool = default!;

    // Who is crafting right now, and the do-after that can be cancelled.
    private readonly Dictionary<EntityUid, DoAfterId> _active = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<CraftingMenuRequestEvent>(OnMenuRequest);
        SubscribeNetworkEvent<CraftingSelectEvent>(OnSelect);
        SubscribeNetworkEvent<CraftingCancelEvent>(OnCancel);
        SubscribeLocalEvent<DoAfterComponent, CraftDoAfterEvent>(OnCraftDoAfter);
    }

    public bool IsCrafting(EntityUid user) => _active.ContainsKey(user);

    #region Menu

    private void OnMenuRequest(CraftingMenuRequestEvent msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } user)
            return;

        var menu = BuildMenu(user);
        if (menu == null)
        {
            _popup.PopupEntity(Loc.GetString("au14-craft-nothing"), user, user, PopupType.SmallCaution);
            return;
        }

        RaiseNetworkEvent(menu, args.SenderSession);
    }

    // What the radial menu shows: only the recipes the crafter has the skills and the consumables for.
    // While a craft is going on the only thing to show is the cancel button.
    public CraftingMenuEvent? BuildMenu(EntityUid user)
    {
        if (IsCrafting(user))
            return new CraftingMenuEvent(new List<string>(), canCancel: true);

        var recipes = GetAvailableRecipes(user).Select(recipe => recipe.ID).ToList();
        return recipes.Count == 0 ? null : new CraftingMenuEvent(recipes, canCancel: false);
    }

    public List<CraftingRecipePrototype> GetAvailableRecipes(EntityUid user)
    {
        var available = GetAvailable(user);
        var result = new List<CraftingRecipePrototype>();

        foreach (var recipe in _proto.EnumeratePrototypes<CraftingRecipePrototype>().OrderBy(recipe => recipe.ID))
        {
            if (_skills.HasAllSkills(user, recipe.Skills) && TryClaim(available, recipe, out _, out _))
                result.Add(recipe);
        }

        return result;
    }

    private void OnSelect(CraftingSelectEvent msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is { } user)
            TryStartCraft(user, msg.Recipe);
    }

    private void OnCancel(CraftingCancelEvent msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is { } user)
            TryCancelCraft(user);
    }

    #endregion

    #region Crafting

    // Picking a recipe crafts it on its own: the consumables stay with the crafter until the craft is done.
    public bool TryStartCraft(EntityUid user, string recipeId)
    {
        if (!_proto.TryIndex<CraftingRecipePrototype>(recipeId, out var recipe) || !_actionBlocker.CanInteract(user, null))
            return false;

        if (IsCrafting(user))
        {
            _popup.PopupEntity(Loc.GetString("au14-craft-busy"), user, user, PopupType.SmallCaution);
            return false;
        }

        if (!_skills.HasAllSkills(user, recipe.Skills))
        {
            _popup.PopupEntity(Loc.GetString("au14-craft-skill"), user, user, PopupType.SmallCaution);
            return false;
        }

        var available = GetAvailable(user);
        if (!TryClaim(available, recipe, out _, out var missing))
        {
            _popup.PopupEntity(Loc.GetString("au14-craft-missing", ("items", string.Join(", ", missing))), user, user, PopupType.SmallCaution);
            return false;
        }

        if (!HasTools(available, recipe, out var missingTools))
        {
            _popup.PopupEntity(Loc.GetString("au14-craft-no-tool", ("tools", string.Join(", ", missingTools))), user, user, PopupType.SmallCaution);
            return false;
        }

        var args = new DoAfterArgs(EntityManager, user, recipe.Time, new CraftDoAfterEvent(recipe.ID), user)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false,
        };

        if (!_doAfter.TryStartDoAfter(args, out var id))
            return false;

        _active[user] = id.Value;
        _popup.PopupEntity(Loc.GetString("au14-craft-started", ("name", Loc.GetString(recipe.Name))), user, user);
        return true;
    }

    // Stops the craft that is in progress; nothing has been used up yet, so nothing is lost.
    public bool TryCancelCraft(EntityUid user)
    {
        if (!_active.TryGetValue(user, out var id))
            return false;

        _doAfter.Cancel(id);
        return true;
    }

    private void OnCraftDoAfter(Entity<DoAfterComponent> ent, ref CraftDoAfterEvent args)
    {
        var user = args.User;
        _active.Remove(user);

        if (args.Cancelled)
        {
            _popup.PopupEntity(Loc.GetString("au14-craft-cancelled"), user, user, PopupType.SmallCaution);
            return;
        }

        if (args.Handled)
            return;

        args.Handled = true;

        if (!_proto.TryIndex<CraftingRecipePrototype>(args.Recipe, out var recipe))
            return;

        // Things may have been put away while the craft was going on, so look again before using anything up.
        var available = GetAvailable(user);
        if (!_skills.HasAllSkills(user, recipe.Skills) ||
            !HasTools(available, recipe, out _) ||
            !TryClaim(available, recipe, out var claims, out var missing))
        {
            _popup.PopupEntity(Loc.GetString("au14-craft-failed"), user, user, PopupType.SmallCaution);
            return;
        }

        foreach (var (entity, amount) in claims)
        {
            var usedUp = true;
            if (TryComp<StackComponent>(entity, out var stack))
            {
                _stack.TryUse((entity, stack), amount);
                usedUp = stack.Count <= 0;
            }
            else
            {
                QueueDel(entity);
            }

            // What is used up does not stay in the hand until the end of the tick: the craft may need both hands.
            if (usedUp && _hands.IsHolding(user, entity))
                _hands.TryDrop(user, entity, checkActionBlocker: false, doDropInteraction: false);
        }

        var coords = Transform(user).Coordinates;
        var result = Spawn(recipe.Output, coords);
        if (TryComp<StackComponent>(result, out var resultStack))
        {
            _stack.SetCount((result, resultStack), recipe.OutputCount);
        }
        else
        {
            for (var i = 1; i < recipe.OutputCount; i++)
                _hands.TryPickupAnyHand(user, Spawn(recipe.Output, coords));
        }

        // A structure that can be carried comes out of the craft in the hands, to be put down where it is wanted.
        if (TryComp<PortableStructureComponent>(result, out var portable))
            _portable.TryPickUp((result, portable), user);
        else
            _hands.TryPickupAnyHand(user, result);

        _popup.PopupEntity(Loc.GetString("au14-craft-done", ("name", Loc.GetString(recipe.Name))), user, user, PopupType.Medium);
    }

    #endregion

    #region Consumables and tools

    // Everything the crafter has on them: in hands, worn, and inside whatever they carry.
    private List<EntityUid> GetAvailable(EntityUid user)
    {
        var found = new List<EntityUid>();

        foreach (var held in _hands.EnumerateHeld(user))
            AddWithContents(held, found);

        if (_inventory.TryGetContainerSlotEnumerator(user, out var slots))
        {
            while (slots.MoveNext(out var slot))
            {
                if (slot.ContainedEntity is { } worn)
                    AddWithContents(worn, found);
            }
        }

        return found;
    }

    private void AddWithContents(EntityUid entity, List<EntityUid> found)
    {
        if (found.Contains(entity))
            return;

        found.Add(entity);

        if (!TryComp<ContainerManagerComponent>(entity, out var manager))
            return;

        foreach (var container in manager.Containers.Values)
        {
            foreach (var inside in container.ContainedEntities)
                AddWithContents(inside, found);
        }
    }

    // Looks for the consumables of a recipe. When something is short, says what, and nothing is claimed.
    private bool TryClaim(
        List<EntityUid> available,
        CraftingRecipePrototype recipe,
        out List<(EntityUid Entity, int Amount)> claims,
        out List<string> missing)
    {
        claims = new List<(EntityUid, int)>();
        missing = new List<string>();

        var remaining = new Dictionary<EntityUid, int>();
        foreach (var entity in available)
            remaining[entity] = TryComp<StackComponent>(entity, out var stack) ? stack.Count : 1;

        foreach (var consumable in recipe.Consumables)
        {
            var have = 0;
            var matching = new List<EntityUid>();
            foreach (var entity in available)
            {
                if (remaining[entity] > 0 && Matches(consumable, entity))
                {
                    matching.Add(entity);
                    have += remaining[entity];
                }
            }

            if (have < consumable.Amount)
            {
                missing.Add($"{ConsumableName(consumable)} {have}/{consumable.Amount}");
                continue;
            }

            var left = consumable.Amount;
            foreach (var entity in matching)
            {
                if (left <= 0)
                    break;

                var take = Math.Min(remaining[entity], left);
                remaining[entity] -= take;
                left -= take;
                claims.Add((entity, take));
            }
        }

        if (missing.Count == 0)
            return true;

        claims.Clear();
        return false;
    }

    // The tools only have to be on the crafter; they are never used up.
    private bool HasTools(List<EntityUid> available, CraftingRecipePrototype recipe, out List<string> missing)
    {
        missing = new List<string>();

        foreach (var quality in recipe.Tools)
        {
            if (available.Any(entity => _tool.HasQuality(entity, quality)))
                continue;

            missing.Add(_proto.TryIndex(quality, out var proto) ? Loc.GetString(proto.ToolName) : quality.Id);
        }

        return missing.Count == 0;
    }

    private bool Matches(CraftConsumable consumable, EntityUid entity)
    {
        if (consumable.Item is { } item)
            return MetaData(entity).EntityPrototype?.ID == item.Id;

        return consumable.Stack is { } stackType &&
               TryComp<StackComponent>(entity, out var stack) &&
               stack.StackTypeId == stackType;
    }

    private string ConsumableName(CraftConsumable consumable)
    {
        if (consumable.Item is { } item)
            return _proto.TryIndex(item, out var proto) ? proto.Name : item.Id;

        if (consumable.Stack is { } stackType)
            return _proto.TryIndex(stackType, out var stack) ? Loc.GetString(stack.Name) : stackType.Id;

        return string.Empty;
    }

    #endregion
}
