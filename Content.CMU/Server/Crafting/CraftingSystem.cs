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
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Crafting;

public sealed class CraftingSystem : SharedCraftingSystem
{
    private static readonly EntProtoId<SkillDefinitionComponent> EngineerSkill = "RMCSkillEngineer";

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
    [Dependency] private readonly IGameTiming _timing = default!;

    // Who is crafting right now, and the do-after that can be cancelled.
    private readonly Dictionary<EntityUid, DoAfterId> _active = new();

    // Who is playing the game of a recipe: nothing has been used up yet, the result of the game says how the craft turns out.
    private readonly Dictionary<EntityUid, Playing> _playing = new();

    private TimeSpan _nextCleanup;

    // Stage is which of the games of the recipe is being played, Scores what the games before it gave.
    private readonly record struct Playing(string Recipe, int Stage, float Scores, TimeSpan Started, TimeSpan Shortest, TimeSpan Deadline);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<CraftingMenuRequestEvent>(OnMenuRequest);
        SubscribeNetworkEvent<CraftingSelectEvent>(OnSelect);
        SubscribeNetworkEvent<CraftingCancelEvent>(OnCancel);
        SubscribeNetworkEvent<CraftingMinigameResultEvent>(OnMinigameResult);
        SubscribeLocalEvent<DoAfterComponent, CraftDoAfterEvent>(OnCraftDoAfter);
    }

    public bool IsCrafting(EntityUid user) => _active.ContainsKey(user) || _playing.ContainsKey(user);

    // The games of those who walked away from them without a word are dropped after a while.
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_playing.Count == 0 || _timing.CurTime < _nextCleanup)
            return;

        _nextCleanup = _timing.CurTime + TimeSpan.FromSeconds(5);

        List<EntityUid>? stale = null;
        foreach (var (user, playing) in _playing)
        {
            if (_timing.CurTime > playing.Deadline || Deleted(user))
                (stale ??= new List<EntityUid>()).Add(user);
        }

        if (stale != null)
        {
            foreach (var user in stale)
                _playing.Remove(user);
        }
    }

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

        // A recipe with a game is played instead of waited for; whoever cannot be asked to play (nobody is at the controls) only waits.
        if (recipe.Minigames.Count > 0 && TryStartMinigame(user, recipe))
            return true;

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
        if (_playing.Remove(user))
        {
            _popup.PopupEntity(Loc.GetString("au14-craft-cancelled"), user, user, PopupType.SmallCaution);
            return true;
        }

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

        Complete(user, recipe, CraftOutcome.Normal);
    }

    #endregion

    #region Games

    // Asks the player to play: nothing is used up now, and the craft is made when the result comes back. False when nobody can be asked.
    private bool TryStartMinigame(EntityUid user, CraftingRecipePrototype recipe)
    {
        if (!TryComp<ActorComponent>(user, out var actor))
            return false;

        SendGame(user, actor, recipe, 0, 0f);
        return true;
    }

    // Asks for one of the games of the recipe: the first, or the next after one that was played.
    private void SendGame(EntityUid user, ActorComponent actor, CraftingRecipePrototype recipe, int stage, float scores)
    {
        var kind = recipe.Minigames[stage];
        var stages = recipe.Minigames.Count;

        var engineer = _skills.GetSkill(user, EngineerSkill);
        var ease = Math.Clamp(1f + 0.12f * engineer, 1f, 1.5f);
        var seconds = (float) recipe.Time.TotalSeconds;

        // Several games one after another are a little shorter each.
        var shorter = stages > 1 ? 0.75f : 1f;

        // The rhythm is a few presses (one for each kind of ingredient and one more); the other games run for a time that follows the time of the recipe.
        var steps = Math.Clamp(recipe.Consumables.Count + 1, 3, 6);
        var duration = kind switch
        {
            CraftMinigameKind.Hold => Math.Clamp(seconds * 0.7f * shorter, 4.5f, 8f),
            CraftMinigameKind.Trace => Math.Clamp(seconds * 0.8f * shorter, 5f, 10f),
            CraftMinigameKind.Polish => Math.Clamp(seconds * 0.8f * shorter, 5f, 10f),
            CraftMinigameKind.Drill => Math.Clamp(seconds * 0.7f * shorter, 5f, 9f),
            _ => 0f,
        };

        // The shortest a game can take: a result that comes sooner was not played.
        var (shortest, longest) = kind switch
        {
            CraftMinigameKind.Rhythm => (steps * 0.7f, steps * 6f + 4f),
            CraftMinigameKind.Center => (steps * 0.8f, steps * 8f + 4f),
            // The needle runs the seam in a time of its own, a few seconds whatever the recipe.
            CraftMinigameKind.Stitch => (3f, 14f),
            _ => (duration * 0.8f, duration + 4f),
        };

        var now = _timing.CurTime;
        _playing[user] = new Playing(recipe.ID, stage, scores, now, TimeSpan.FromSeconds(shortest), now + TimeSpan.FromSeconds(longest + 20));

        // Every game of a recipe has a track of its own, and always the same one.
        var seed = CraftMinigames.StableHash(recipe.ID) + stage * 7919;
        RaiseNetworkEvent(new CraftingMinigameStartEvent(recipe.ID, kind, seed, steps, duration, ease, stage, stages), actor.PlayerSession);
    }
    private void OnMinigameResult(CraftingMinigameResultEvent msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is { } user)
            TryFinishMinigame(user, msg.Recipe, msg.Score);
    }

    // The game is over: the craft turns out as well as the game went. A game that was not played (it came back too soon) counts for nothing.
    public bool TryFinishMinigame(EntityUid user, string recipeId, float score)
    {
        if (!_playing.TryGetValue(user, out var playing) || playing.Recipe != recipeId)
            return false;

        _playing.Remove(user);

        if (!_proto.TryIndex<CraftingRecipePrototype>(recipeId, out var recipe) || !_actionBlocker.CanInteract(user, null))
            return false;

        if (float.IsNaN(score) || _timing.CurTime - playing.Started < playing.Shortest)
            score = 0f;

        var total = playing.Scores + Math.Clamp(score, 0f, 1f);

        // Another game of the recipe is next; the craft is made after the last one, as well as the games went on the average.
        if (playing.Stage + 1 < recipe.Minigames.Count && TryComp<ActorComponent>(user, out var actor))
        {
            SendGame(user, actor, recipe, playing.Stage + 1, total);
            return true;
        }

        Complete(user, recipe, CraftMinigames.OutcomeOf(total / Math.Min(playing.Stage + 1, recipe.Minigames.Count)));
        return true;
    }

    #endregion

    #region Making

    // Uses up what the recipe asks for and makes what it makes. A craft that went badly uses up half and makes nothing; one that went well makes a little more, or spares
    // a little of the materials.
    private void Complete(EntityUid user, CraftingRecipePrototype recipe, CraftOutcome outcome)
    {
        // Things may have been put away while the craft was going on, so look again before using anything up.
        var available = GetAvailable(user);
        if (!_skills.HasAllSkills(user, recipe.Skills) ||
            !HasTools(available, recipe, out _) ||
            !TryClaim(available, recipe, out var claims, out _))
        {
            _popup.PopupEntity(Loc.GetString("au14-craft-failed"), user, user, PopupType.SmallCaution);
            return;
        }

        var name =Loc.GetString(recipe.Name);

        if (outcome == CraftOutcome.Failed)
        {
            // Half of each is lost, the rest stays where it was.
            for (var i = 0; i < claims.Count; i++)
                claims[i] = (claims[i].Entity, (claims[i].Amount + 1) / 2);
        }

        var spared = false;
        if (outcome == CraftOutcome.Good && recipe.OutputCount <= 1)
        {
            // A single thing cannot come out larger, so one of the materials is spared instead (only where there is more than one of it).
            for (var i = 0; i < claims.Count; i++)
            {
                if (claims[i].Amount < 2)
                    continue;

                claims[i] = (claims[i].Entity, claims[i].Amount - 1);
                spared = true;
                break;
            }
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

        if (outcome == CraftOutcome.Failed)
        {
            _popup.PopupEntity(Loc.GetString("au14-craft-minigame-failed", ("name", name)), user, user, PopupType.MediumCaution);
            return;
        }

        // One more of the product when the craft makes several of it.
        var count = outcome == CraftOutcome.Good && recipe.OutputCount > 1 ? recipe.OutputCount + 1 : recipe.OutputCount;

        var coords = Transform(user).Coordinates;
        var result = Spawn(recipe.Output, coords);
        if (TryComp<StackComponent>(result, out var resultStack))
        {
            _stack.SetCount((result, resultStack), count);
        }
        else
        {
            for (var i = 1; i < count; i++)
                _hands.TryPickupAnyHand(user, Spawn(recipe.Output, coords));
        }

        // A structure that can be carried comes out of the craft in the hands, to be put down where it is wanted.
        if (TryComp<PortableStructureComponent>(result, out var portable))
            _portable.TryPickUp((result, portable), user);
        else
            _hands.TryPickupAnyHand(user, result);

        if (outcome == CraftOutcome.Good && (count > recipe.OutputCount || spared))
            _popup.PopupEntity(Loc.GetString("au14-craft-minigame-good", ("name", name)), user, user, PopupType.Medium);
        else
            _popup.PopupEntity(Loc.GetString("au14-craft-done", ("name", name)), user, user, PopupType.Medium);
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
