# SPDX-License-Identifier: LicenseRef-AdvancedAtkinsonatorv2-Proprietary
# Copyright (c) 2026 wray-git. All rights reserved.
# Proprietary - reuse only with the Author's prior written authorization. See LICENSE-AdvancedAtkinsonatorv2.md.
# Examine line shown on any entity a player constructed.
construction-player-built-examine = Built by [color=cyan]{ $name }[/color].

# Build-partner verbs (right-click another player).
build-partner-add-verb = Add as Build Partner
build-partner-remove-verb = Remove Build Partner
build-partner-added = { $name } can now include your builds in their saves.
build-partner-removed = { $name } can no longer include your builds in their saves.

# Saving builds.
saved-build-success = Saved build "{ $name }" ({ $count } entities).
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
saved-build-window-selected = Highlighted: { $count }
saved-build-window-name = Build name…
saved-build-window-save = Save Build
saved-build-window-open-folder = Open Saved Builds Folder

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

# Admin/Player build-mode toggle at the top of the construction menu.
gmod-construction-menu-mode-admin = Building: Admin (instant)
gmod-construction-menu-mode-player = Building: Player (ghosts)

# Placement controls hint (top-left).
saved-build-controls-mode-admin = Mode: Admin (instant, free)
saved-build-controls-mode-player = Mode: Build (ghosts + materials)
saved-build-controls-gridalign = Alt (toggle): Grid-aligned ({ $state })
saved-build-controls-rotate = { $key }: Rotate
saved-build-controls-place = Left Click: Place
saved-build-controls-cancel = Right Click: Cancel
