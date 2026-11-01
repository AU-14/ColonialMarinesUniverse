# Recovered Yautja bracer experiments

Human characters with research skill can work on a bracer lying directly on a floor. Examine it to find the research entry point. Right-click it and choose **Bracer experiments > Inspect bracer**. Five numbered contacts surround the actual item in the world: its sprite, terrain, decals and lighting stay visible, and the controls follow the camera. Click a contact to read its clue in the compact inspection panel. Compare the contact's physical clues with the procedures: click the selected procedure to open the searchable list, or **Recovered** to read completed field records. These menus expand within the panel. The required compound property, tool and work time stay visible alongside interlock progress and core strain. Add chemicals from a beaker directly onto the bracer (10 units per use, 30-unit sample well), then use the indicated tool on it. The center of the inspection controls lets clicks through to the real item. Picking the bracer up closes inspection.

The bracer must remain on the floor and within reach for the whole operation. Moving, taking damage, changing tools, picking up the bracer or losing required reagents interrupts the work without consuming the recipe. Only one operator can work at a time. The internal sample reservoir uses normal chemical reactions, so use the bracer's right-click purge action to clear incompatible leftovers before starting another mixture. There is no separate sample or help menu in inspection.

Each new discovery prints notes and awards research credits and sample data to the nearest research terminal's faction. Repeating a completed experiment still produces its effect or output but earns no more research. All 20 procedures are available immediately and can be attempted in any order.

A new result matching one of the five seals releases that seal and resets core strain to zero. The five chosen procedures therefore need no additional cooling experiment when begun with a cool core. At 8 core strain, the procedure vents the capacitor and fails. Field-kit sealant dissolution removes 2 strain; plasma exchanger quenching removes 5. Plasma reservoir excitation restores 450 charge. Field-medicine catalysis produces a usable stabilizer gel, and the bio-mask quarry sweep reports nearby living signatures.

Eight procedures consume 5u of a named standard chemical, including familiar dispenser stock, medicine and synthesized silicone oil. These do not require XRF identification. The other twelve consume 5u of an XRF-identified, round-generated tier-3 (or higher) compound with the listed property. Research contracts supply that starting chemistry; the synthesis simulator can refine or combine its properties. Standard chemicals cannot substitute for a generated requirement. Matching generated compounds may serve more than one procedure. Inspection labels requirements as Standard or Research; the full research requirement is available on hover. The bracer consumes only the matching reagent, after successful tool work.

| Procedure | Tool | Required chemical or research property | Strain |
| --- | --- | --- | --- |
| Gauntlet circuit tomography | Screwdriver | Standard: ethanol | 0 |
| Vocal mimicry buffer | Multitool | Psychostimulating 3+ | 1 |
| Field-kit sealant dissolution | Screwdriver | Standard: sulphuric acid | -2 |
| Plasma exchanger quench | Wrench | Standard: water | -5 |
| Wrist-computer bus bridge | Multitool | Standard: copper | 1 |
| Plasma reservoir excitation | Wrench | Electrogenetic 3+ | 2 |
| Bio-mask spectral alignment | Screwdriver | Standard: silver | 1 |
| Optical camouflage phase test | Multitool | Hypometabolic 2+ | 2 |
| Hunt-recording recovery | Screwdriver | Psychostimulating 2+ | 1 |
| Bio-mask quarry sweep | Multitool | Focusing 3+ | 1 |
| Hunter biometrics assay | Screwdriver | Neuroinhibiting 2+ | 1 |
| Field-medicine catalyst | Lit welder | Standard: bicaridine | 2 |
| Camouflage calibration phantom | Multitool | Hallucinogenic 2+ | 2 |
| Remote equipment command link | Wrench | Standard: silicone oil | 2 |
| Smart-disc recall calibration | Wrench | Hyperdensificating 1+ | 2 |
| Caster containment bleed | Lit welder | Hyperthermic 3+ | 3 |
| Targeting optic overload | Screwdriver | Standard: phosphorus | 2 |
| Tamper circuit inversion | Multitool | Disrupting 2+ | 3 |
| Gauntlet containment interlock | Wrench | Anticorrosive 2+ | -2 |
| Hunter identity cipher | Multitool | Neuroshielding 1+ | 2 |

Each bracer chooses five distinct experiments at map initialization and saves that challenge on the device. The five numbered clues appear on the bracer's diagnostic contacts and in printed discovery notes. Each clue describes physical evidence on the device: residue, damaged surfaces, changing traces and tool fittings. There are no observation headings or instructions proposing a solution. These details suggest the chemical behavior and tool without naming a property, reagent, required level or unresolved procedure. Similar chemical requirements are distinguished by subsystem and fitting; stronger requirements appear as higher thresholds in the diagnostic traces. Released seals instead show the solved procedure name. Picking up the bracer, changing operators, selecting a different procedure and interrupted work do not reroll the challenge.

Completing all five chosen experiments automatically unlocks the device and authorizes the final operator. Repeating a solved procedure or completing an unrelated experiment cannot substitute for another seal. Hunter identity cipher is one of the 20 possible experiments, not a mandatory final step or an authorization shortcut. Full access includes the bracer interface, optical cloak, stored gear, weapon recall and self-destruct controls, with their existing safety restrictions. Activating a recovered disc while holding it claims it for the reconstructed operator; Call Disc returns nearby discs through the normal bracer control. Another trained human can choose **Bind recovered access key** while the unlocked bracer lies on the floor. This grants no Yautja species, rank, clan membership or honor privileges.

Modern/Retro cloak presets remain selected through the Yautja editor's Technology section. Plasma laser, muzzle flash, bolt and impact use blue for Stun/Immobilizer and red for Plasma Bolt/Eradicator.

## Automatic visual showcase

Open `Content.CMU/Tools/Yautja/show-bracer-experiments.csx` in an editor and copy its entire contents. Open the in-game console with `~`, enter `scsi`, paste the copied code, then press **Ctrl+Enter**. Do not paste the filename. Do not use `#load`: this engine build crashes in its syntax highlighter when it tries to display loaded scripts, even though they compile successfully.

Close the scripting window and console during the 15-second countdown. It creates a separate metal-floor stage and cycles through the 20 actual game effects, bracer replies and sounds, one every 10 seconds. Each production effect lasts eight seconds: a 2.4-second build-up, release with its own audio cue, a sustained projection and a separate shutdown cue during the final collapse. Both live experiments and this script call the same presentation method. The sequence runs once; set `repeat = true` to loop or change `firstEffect` to start at a later effect.

The script targets `CMU_client1` by default. Change `viewerName` for another connected account. It temporarily gives that player an observer so a living body is not exposed to the test stage. Paste the contents of `stop-bracer-experiments.csx` to cancel the timers, return to the previous body and remove the stage. Running the showcase again also cleans up its previous run.

This is a visual preview: it emits the production effects and plays their configured responses and sounds without consuming reagents or awarding discoveries, research credits or authorization.

## Lore and presentation

The wrist computer is the controller for recovered hunting equipment. IllFonic's licensed [Predator technology descriptions](https://predator.illfonic.com/the-predator/) establish light-bending camouflage, plasma weaponry and a guided returning smart disc. Its [game description](https://predator.illfonic.com/about/) also describes heat-sensitive vision, hunting scanners, mimicry and the struggle to recover alien equipment. The [AVP production gauntlet castings](https://propstore.com/product/avp-alien-vs-predator-2004/predator-wrist-gauntlet-production-castings/) connect the wrist prop to camouflage and self-destruction in the film.

The laboratory procedures, substitute chemicals, projected diagnostic formations and five randomized interlocks are CMU fiction built around those functions. The research window labels explanations as human analysis. A projection of a hunter or smart disc is diagnostic imagery, not a newly invented physical summon or a claim that the films show these experiments. The bracer's responses are recovered status messages and recordings. A forged salvage override grants mechanical access, not Yautja identity, rank, honor or acceptance.

The research guide and printed salvage records frame the bracer as a particular hunter's equipment. Damaged voice buffers, a stored wearer silhouette, return-command markings and an unresolved owner glyph provide a small forensic trail as researchers work. These fragments are original CMU flavor, not quotations or translations claimed from a film. The notes explicitly distinguish human procedure names from the undeciphered script and explain why a successful recipe on one recovered bracer may not release a seal on another.

The twenty formation paths remain intact. Their carriers now use four source shaders: an animated material ribbon, a noise-driven field, a sprite-masked projection and local screen refraction. Eight material families distinguish plasma, discharge, projection, vapor, cryogenic flow, optical interference, magnetic circulation and thermal scanning. Glyphs and control channels use restrained red/amber; the cloak and energy tests retain their cool spectral channels. Three-band optics sample blue, red and amber rather than a rainbow prism. The effect entity owns a real world light, synchronized to its server timestamp, and three audio phases. Both production experiments and the showcase call the same presentation method.
