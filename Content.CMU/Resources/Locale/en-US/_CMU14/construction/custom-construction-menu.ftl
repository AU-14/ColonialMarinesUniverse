# SPDX-License-Identifier: AGPL-3.0-only
# Copyright (c) 2026 wray-git
# SPDX-License-Identifier: AGPL-3.0-only
## In-game construction menu editor (world right-click > Construction)

verb-categories-construction = Construction

construction-category-au14-custom = Custom

construction-menu-verb-add = Add to Construction Menu
construction-menu-verb-add-message = Permanently add this item to the construction menu (applies next restart).
construction-menu-verb-remove = Remove from Construction Menu
construction-menu-verb-remove-message = Remove this item from the construction menu (applies next restart).
construction-menu-verb-change-recipe = Change Recipe
construction-menu-verb-change-recipe-message = Change the spawnlist, category, or recipe for this menu item (applies next restart).
construction-menu-verb-change-recipe-disabled = This item is not in the construction menu. Add it first.

## Add / Change dialogs

construction-menu-dialog-add-title = Add { $item } to Construction Menu
construction-menu-dialog-change-title = Change Recipe — { $item }
construction-menu-dialog-spawnlist = Spawnlist (default: { $default })
construction-menu-dialog-category = Category (default: { $default })
construction-menu-dialog-recipe = Recipe, e.g. { $example }  (Material:Amount, separate steps with >, tools: weld/wrench/screw/pry/cut)
construction-menu-dialog-spawnlist-current = Spawnlist (current: { $current })
construction-menu-dialog-category-current = Category (current: { $current })
construction-menu-dialog-recipe-current = Recipe (current: { $current })

## Result popups

construction-menu-verb-added = Added { $item } to "{ $category }". Recipe: { $recipe }. Applies next restart.
construction-menu-verb-recipe-changed = Updated { $item }. Recipe: { $recipe }. Applies next restart.
construction-menu-verb-removed = Removed { $item } from the construction menu. Applies next restart.

## Editor window

construction-editor-title = Construction Menu Editor
construction-editor-title-add = Add to Construction Menu
construction-editor-title-edit = Change Recipe
construction-editor-spawnlist = Spawnlist
construction-editor-category = Category
construction-editor-new-spawnlist = New spawnlist name…
construction-editor-new-category = New category name…
construction-editor-add-new = Add new…
construction-editor-confirm = Confirm
construction-editor-material-custom = Custom…
construction-editor-material-notfound = Material "{ $material }" not found — pick a valid one.
construction-editor-steps = Recipe steps
construction-editor-material = Custom stack id (e.g. Steel)
construction-editor-amount = Amt
construction-editor-doafter = Sec
construction-editor-add-material = + Material
construction-editor-add-tool = + Tool
construction-editor-remove-step = Remove last
construction-editor-clear-steps = Clear
construction-editor-ok = Save (next restart)
construction-editor-cancel = Cancel
construction-editor-health = Health
construction-editor-health-placeholder = blank = inherit
construction-editor-danger = Danger zone - bulk removal
construction-editor-remove-include-all = Include ALL entities under this spawnlist/category
construction-editor-remove-group = Remove spawnlist/category
construction-editor-remove-confirm = Confirm removal
construction-editor-remove-need-check = Check "Include all entities" first to confirm this destructive action.
construction-editor-remove-warning = WARNING: permanently removes EVERY recipe in { $spawnlist } / { $category }. Wait 3 seconds...
construction-editor-remove-ready = Ready - click Confirm to permanently remove every recipe in { $spawnlist } / { $category }.
construction-menu-group-removed = Removed { $count } recipes from { $spawnlist } / { $category }. Applies next restart.
construction-editor-step-material = { $amount } x { $material }  ({ $sec }s)
construction-editor-step-tool = Tool: { $tool }  ({ $sec }s)

## Deconstruction steps (structures only)

construction-editor-deconstruct-steps = Deconstruction steps (structures, default: crowbar)
construction-editor-add-deconstruct-tool = + Tool
construction-editor-pick-deconstruct-entity-tool = + Custom Tool…
construction-editor-remove-deconstruct-step = Remove last
construction-editor-clear-deconstruct-steps = Clear

construction-menu-verb-add-failed = Failed to add the item to the construction menu.
construction-menu-verb-remove-failed = Failed to remove the item from the construction menu.
construction-menu-verb-bad-recipe = Could not parse that recipe. Use e.g. "Steel:4 > weld > Steel:2".

construction-menu-verb-invalid = Cannot save recipe: { $reason }
construction-menu-invalid-no-steps = the recipe needs at least one material step.
construction-menu-invalid-tool = tool steps ("{ $tool }") aren't supported yet — use material steps only. (The build path can't enforce tools without crashing.)
construction-menu-invalid-tool-item = tool steps ("{ $tool }") aren't supported for in-hand items — they only work for structures. Remove the tool step or pick a structure.
construction-menu-invalid-material = material "{ $material }" isn't a buildable material. Use a CM material (e.g. CMSteel, CMPlasteel, CMGlass, CMGlassReinforced, RMCWood, RMCPlastic).
construction-menu-invalid-entity = entity "{ $entity }" does not exist. Pick a real prototype from the selector.
construction-menu-invalid-deconstruct-material = deconstruction steps can only be tools (e.g. crowbar, welder) - you can't feed materials in to take something apart. Remove the material step.

## Custom material/tool selector + editor additions

construction-editor-pick-entity-material = + Custom Material…
construction-editor-pick-entity-tool = + Custom Tool (not consumed)…
construction-editor-step-entity-material = { $amount } x { $entity }  ({ $sec }s)
construction-editor-step-entity-tool = Tool (kept): { $entity }  ({ $sec }s)
construction-selector-title = Select an entity
construction-selector-search = Search entities…
construction-selector-select = Select

## Utilities → Admin Tools

gmod-construction-menu-admin-tools = Admin Tools
gmod-construction-menu-items-editor = Construction Items Editor
gmod-construction-menu-tiles-editor = Tiles Editor
gmod-construction-menu-lathe-editor = Lathe Editor
gmod-construction-menu-zlevel-toggles = Z-Level Toggles
construction-menu-editor-not-admin = You are not an admin - the editor won't open.

## In-menu detail panel: Change Recipe / Remove Item (admins; works for vanilla recipes too)

gmod-construction-menu-change-recipe = Change Recipe
gmod-construction-menu-remove-item = Remove Item
construction-menu-recipe-hidden = Removed "{ $recipe }" from the construction menu. Applies fully next restart.
construction-menu-recipe-already-hidden = "{ $recipe }" is already removed from the construction menu.
construction-menu-recipe-hide-failed = Failed to remove that recipe from the construction menu.

## Recipe chooser (entity already has recipes)

construction-chooser-title = Recipes for this item
construction-chooser-entry = { $spawnlist } / { $category }
construction-chooser-change = Change
construction-chooser-remove = Remove
construction-chooser-add-new = Add new recipe
construction-menu-verb-no-resources = Cannot edit the construction menu: no writable Resources directory found.

## Tiles editor

construction-tile-editor-title = Add Tile to Construction Menu
construction-tile-editor-tile = Tile
construction-tile-editor-search = Search tiles...
construction-tile-editor-main-category = Main category
construction-tile-editor-page-zlevel = Z-Level (Experimental)
construction-tile-editor-page-spawnlists = Spawnlists
construction-tile-editor-spawnlist = Spawnlist (Spawnlists page only)
construction-tile-editor-category = Category
construction-tile-editor-material = Material
construction-tile-editor-amount = Cost (sheets)
construction-tile-editor-selected = Selected tile: { $tile }
construction-tile-editor-none = (no tile selected)
construction-tile-editor-save = Save (next restart)
construction-tile-editor-cancel = Cancel
construction-menu-tile-invalid-tile = Tile "{ $tile }" is not a valid tile. Pick one from the list.
construction-menu-tile-added = Added tile { $tile } to "{ $category }". Applies next restart.

## Lathe editor

construction-lathe-editor-title = Add Lathe Recipe
construction-lathe-editor-lathe = Lathe
construction-lathe-editor-autolathe = Autolathe
construction-lathe-editor-armylathe = Armylathe
construction-lathe-editor-pick-item = Pick item to print...
construction-lathe-editor-selected = Item: { $item }
construction-lathe-editor-none = (no item selected)
construction-lathe-editor-steel = Steel cost
construction-lathe-editor-glass = Glass cost
construction-lathe-editor-plastic = Plastic cost
construction-lathe-editor-time = Print time (s)
construction-lathe-editor-save = Save (next restart)
construction-lathe-editor-cancel = Cancel
construction-menu-lathe-invalid-cost = Set at least one material cost (steel / glass / plastic).
construction-menu-lathe-added = Added { $item } to the { $lathe }. Applies next restart.
construction-menu-lathe-removed = Removed lathe recipe { $recipe }. Applies next restart.
construction-lathe-editor-existing = Existing added recipes (click to remove)
construction-lathe-editor-remove = Remove
