cmu-cavern-stair-up = z-level stairs (up)
cmu-cavern-stair-up-desc = Build a supported stair and a landing on the level above. Excavates a pocket in generated rock.
cmu-cavern-stair-down = z-level stairs (down)
cmu-cavern-stair-down-desc = Open a stairwell with a supported landing and return stair on the level below.
cmu-cavern-ladder = deployable z-level ladder
cmu-cavern-ladder-desc = A portable ladder for an open shaft. Deploy on solid ground beside a supporting wall or beam.
cmu-caverns-description = Configure the next Insurgency round's caverns before the round starts.
cmu-caverns-help = cmu_caverns [status | reset | seed <positive integer|random> | preset <name> | biome <name> <weight 0-1000> | preview [depth] | size <48-160> | height <4-9> | variant <name|mixed> | layout <name|mixed>]. Pair a seed with a biome preset or custom weights. Zero disables a biome. Variant forces one biome for debugging. Caverns begin at Z-3. Overrides apply to Insurgency and reset with the round.
cmu-caverns-status = Insurgency caverns: seed={$seed}, region size={$size}, band height={$height}, geology={$variant}, layout={$layout}. Start: Z-3.
cmu-caverns-invalid = Invalid cavern option or value.
cmu-caverns-locked = Cavern settings can only change in the pre-round lobby.
cmu-caverns-generated = Caverns have already been generated in this lobby. Restart the round before changing their recipe.
cmu-caverns-world-seed = Generated world {$world}: seed {$seed}.
cmu-caverns-choices = Biome presets: {$presets}. Biomes: {$variants}. Layouts: {$layouts}. Keep layout mixed for varied chamber shapes.
cmu-caverns-biome-weights = Relative biome weights: {$weights}. Preset/biome commands clear a forced variant.
cmu-caverns-empty-palette = At least one biome must have a positive weight. The previous palette was kept.
cmu-caverns-preview-seed = Set a numeric seed before previewing, for example: cmu_caverns seed 12345.
cmu-caverns-preview-depth = Preview depth must be from Z-3 through Z-4096.
cmu-caverns-preview = Pending seed {$seed}, geology plan for the band containing Z{$depth}. Primary biomes in a 5x5 region window around the origin; each region is {$size} tiles wide. Columns X=-2 to 2; rows Y=2 to -2. This does not generate terrain or show the solid separator.
cmu-caverns-preview-legend = {$legend}. Counts include satellite chambers, which can have different biomes. Repeat the seed AND generation settings to reproduce this world.
