# Hybrisa window shutter source-frame motion

Draft models: `CMU3DHybrisaWindowShutter` and `CMU3DHybrisaWindowShutterOpen`.

Source artwork: `Resources/Textures/_RMC14/Structures/Doors/Shutters/Hybrisa/window_shutter.rsi`.
The RSI metadata credits CM-SS13 and licenses the artwork under CC-BY-SA-3.0:
https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/doors/hybrisashutters.dmi

Opening and closing each contain six 0.1-second source frames in each of four directions.
Canonical geometry follows the south-facing silhouettes and colors, using shallow steel bands.
Two stable poses plus twelve transition poses are stored in each model. The two exports have
different default poses and each contains Opening and Closing clips: four exported clips represent
two distinct transitions, not four independent motions.

The Door prototype's visual animation lasts one second. After the six-frame strip reaches its end,
the last frame holds until that duration ends. Gameplay collision uses its separate 0.4 + 0.4 second
phases. The native adapter reads the existing DoorSystem Base sprite frame, including owner-driven
completion and interruption; it does not add a second animation timer.

Physical height, thickness, roll mechanism, reverse and side reconstruction remain inferred. The
directional sprites are supplied for comparison; the four directions are not individually reconstructed.
Damage, power, welding, crushing and additional overlays are not certified. The existing stable geometry
and saved map placements are unchanged. The contact audit records existing wall/door/window fitting
issues; animation/export validation does not imply clear mounting or visual approval.

All resources remain drafts under the source artwork's license. Full native interactive verification
and final fidelity review remain outstanding.
