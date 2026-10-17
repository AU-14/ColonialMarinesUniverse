# Shared zNetwork command text
cmu-cmd-znetwork-entity-hint = zNetwork net entity
cmu-cmd-znetwork-entity-missing = Unable to find entity { $entity }.
cmu-cmd-znetwork-component-missing = Target entity does not have CMUZLevelsNetworkComponent: { $entity }.

# znetwork-delete
cmd-znetwork-delete-desc = Deletes all maps in the selected zNetwork and the zNetwork entity itself.
cmd-znetwork-delete-help = znetwork-delete <zNetwork entity>
cmu-cmd-znetwork-delete-success = ZNetwork and all its maps deleted.

# znetwork-combine
cmd-znetwork-combine-desc = Connects multiple maps into a common z-level network. Fails if any map is already part of a z-level network.
cmd-znetwork-combine-help = znetwork-combine <mapId> <mapId> [...]
cmu-cmd-znetwork-combine-map-hint = Map ID in order from ground to sky
cmu-cmd-znetwork-combine-not-enough = Not enough maps to form a network of levels.
cmu-cmd-znetwork-combine-parse-map = Cannot parse '{ $value }' into a MapId.
cmu-cmd-znetwork-combine-nullspace = Nullspace cannot be added to a zNetwork.
cmu-cmd-znetwork-combine-map-missing = Map { $mapId } does not exist.
cmu-cmd-znetwork-combine-duplicate = Duplicate map: { $mapId }.
cmu-cmd-znetwork-combine-name = Combined zNetwork: { $id }
cmu-cmd-znetwork-combine-success = Created z-level network! Z-Network entity: { $network }
cmu-cmd-znetwork-combine-partial-failure = Created z-level network { $network }, but something went wrong!

# znetwork-mapping
cmd-znetwork-mapping-desc = Loads an existing game map prototype as a zNetwork for mapping.
cmd-znetwork-mapping-help = znetwork-mapping <gameMapPrototype>
cmu-cmd-znetwork-mapping-hint = GameMapPrototype with CMU Z-level map set
cmu-cmd-znetwork-mapping-unknown-prototype = Unknown GameMapPrototype { $prototype }.
cmu-cmd-znetwork-mapping-network-name = Mapping zNetwork: { $map }
cmu-cmd-znetwork-mapping-cleanup = Unloaded all maps created by the failed zNetwork load.
cmu-cmd-znetwork-mapping-default-load-failed = Failed to load default zNetwork map: { $path }!
cmu-cmd-znetwork-mapping-map-name = Mapping { $map }
cmu-cmd-znetwork-mapping-map-depth-name = Mapping { $map } [{ $depth }]
cmu-cmd-znetwork-mapping-depth-load-failed = Failed to load zNetwork map (depth { $depth }): { $path }!
cmu-cmd-znetwork-mapping-map-disappeared = For some reason a map does not exist after loading! MapId: { $mapId }
cmu-cmd-znetwork-mapping-network-failed = Failed to create zNetwork from loaded maps!

# znetwork-initialize
cmd-znetwork-initialize-desc = Initializes all maps in a zNetwork. This does not apply all components from the game map prototype.
cmd-znetwork-initialize-help = znetwork-initialize <zNetwork entity>
cmu-cmd-znetwork-initialize-map-component-missing = Map entity { $map } does not have MapComponent.
cmu-cmd-znetwork-initialize-map-missing = Map with ID { $mapId } does not exist.
cmu-cmd-znetwork-initialize-already = Map with ID { $mapId } is already initialized.
cmu-cmd-znetwork-initialize-success = Map with ID { $mapId } has been initialized.

# znetwork-variantize
cmd-znetwork-variantize-desc = Applies random tile variations across all maps in a zNetwork.
cmd-znetwork-variantize-help = znetwork-variantize <zNetwork entity>
cmu-cmd-znetwork-variantize-grid-missing = Entity '{ $entity }' does not exist or is not a grid.
