# SPDX-License-Identifier: AGPL-3.0-only
# Copyright (c) 2026 wray-git
# SPDX-License-Identifier: AGPL-3.0-only
# Examine line shown on any entity a player constructed.
construction-player-built-examine = Built by [color=cyan]{ $name }[/color].

# Build-partner verbs (right-click another player).
build-partner-add-verb = Add as Build Partner
build-partner-remove-verb = Remove Build Partner
build-partner-added = { $name } can now include your builds in their saves.
build-partner-removed = { $name } can no longer include your builds in their saves.

# Saving builds.
saved-build-success = Saved build "{ $name }" ({ $count } entities, { $tiles } tiles).
saved-build-error-no-name = Give the build a name first.
saved-build-error-empty = Nothing you built (or a partner's) is in the selection.
saved-build-error-serialize = Failed to serialize that build.
saved-build-error-write = Failed to write the build file.

# Build-save selection panel (client).
saved-build-window-title = Save a Build
saved-build-window-range = Range
saved-build-window-size = Selection: { $size }x{ $size } tiles
saved-build-window-append = Append Range
saved-build-window-clear = Clear
saved-build-window-selected = Highlighted: { $count } entities, { $tiles } tiles
saved-build-window-multiz-help = Multi-Z saving is experimental:
    - Multi-Z builds only work when using "Place at Original".
    - Place the original build once on each Z-level: build one level, move to the next, then use "Place at Original" again.
    - For the best stability, make a separate save for each Z-level.
saved-build-window-name = Build name…
saved-build-window-save = Save Build
saved-build-window-open-folder = Open Saved Builds Folder
saved-build-window-include-tiles = Save tiles
saved-build-window-include-multiz = Capture other Z-levels (above/below)

# Saved Builds spawnlist in the construction menu.
gmod-construction-menu-saved-builds = Saved Builds
saved-build-card = { $name }  ({ $author } · { $count })
saved-build-detail-desc = By { $author }
    { $count } entities · { $source }
saved-build-none = No saved builds yet. Use the build-save tool to make one.
saved-build-place-button = Place Build
saved-build-placed = Placed build ({ $count } pieces).
saved-build-error-load = Couldn't load that saved build.
saved-build-error-nogrid = You can only place a build on a grid.
saved-build-error-noorigin = This build's original location no longer exists.
saved-build-error-notadmin = Only admins can place a build instantly. Build it with construction ghosts instead.
saved-build-place-original-button = Place at Original
saved-build-ghosts-placed = Placed { $count } construction ghosts — build them with materials.

# Saved-build management (delete + open folder).
gmod-construction-menu-delete-build = Delete Build
gmod-construction-menu-open-build-folder = Open Builds Folder
saved-build-deleted = Deleted that saved build.
saved-build-error-delete = Failed to delete that saved build.
saved-build-error-delete-notyours = You can only delete builds you saved. (Admins can delete any.)

# Build-mode dropdown at the top of the construction menu.
gmod-construction-menu-mode-admin = Building: Admin (instant)
gmod-construction-menu-mode-player = Building: Player (ghosts)
gmod-construction-menu-mode-mapper = Building: Mapper (any entity)

# Build partners window (the "Partners" button).
build-partner-window-title = Build Partners
build-partner-window-desc = Add a player to let them include YOUR built items in their saved builds.
build-partner-window-empty = No other players are online.
build-partner-window-add = Add
build-partner-window-remove = Remove
build-partner-window-clear-all = Clear all partners
build-partner-granted-to-you = { $name } added you as a build partner - you can now save their builds.
build-partner-revoked-from-you = { $name } removed you as a build partner.

# Saved-build window extra option (mapper mode) + detail-panel rename/delete.
saved-build-window-include-loose = Include loose items
gmod-construction-menu-rename-build = Rename
gmod-construction-menu-delete-build-confirm = Confirm Delete?

# Placement controls hint (top-left).
saved-build-controls-mode-admin = Mode: Admin (instant, free)
saved-build-controls-mode-player = Mode: Build (ghosts + materials)
saved-build-controls-gridalign = Alt (toggle): Grid-aligned ({ $state })
saved-build-controls-rotate = { $key }: Rotate
saved-build-controls-place = Left Click: Place
saved-build-controls-cancel = Right Click: Cancel

# Multi-z placement
saved-build-z-skipped = {$count} entities could not be placed - their z-level could not be created here.
