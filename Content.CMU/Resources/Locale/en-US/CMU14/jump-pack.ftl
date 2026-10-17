ent-CMUJumpPack = jump pack
    .desc = A back-mounted thruster pack. Boosts the wearer along a set heading, hops a level up, or hovers above the ground for a short time. Refuel it from a gas canister like any other tank. It can hold 5 L of gas.
ent-CMUJumpPackFilled = { ent-CMUJumpPack }
    .desc = { ent-CMUJumpPack.desc }
    .suffix = Filled

ent-CMUActionJumpPackBoost = Jump Pack Boost
    .desc = Fire the thrusters and dash six tiles along the set heading.
ent-CMUActionJumpPackDirection = Jump Pack Heading
    .desc = Rotate the boost heading clockwise. Alt-click the pack to switch between four and eight headings.
ent-CMUActionJumpPackJump = Jump Pack Leap
    .desc = Blast upward to the level above. Shares a cooldown with the boost.
ent-CMUActionJumpPackHover = Jump Pack Hover
    .desc = Rise one level and hover for up to twenty seconds. Use again to land early.

cmu-jump-pack-direction-north = north
cmu-jump-pack-direction-northeast = northeast
cmu-jump-pack-direction-east = east
cmu-jump-pack-direction-southeast = southeast
cmu-jump-pack-direction-south = south
cmu-jump-pack-direction-southwest = southwest
cmu-jump-pack-direction-west = west
cmu-jump-pack-direction-northwest = northwest
cmu-jump-pack-direction-invalid = nowhere

cmu-jump-pack-mode-four = four headings
cmu-jump-pack-mode-eight = eight headings

cmu-jump-pack-examine = The thrusters are aimed [color=cyan]{ $direction }[/color]. The heading dial is set to [color=cyan]{ $mode }[/color].
cmu-jump-pack-verb-toggle-mode = Switch to { $mode }
cmu-jump-pack-mode-set = Heading dial set to { $mode }. Thrusters aimed { $direction }.
cmu-jump-pack-direction-set = Thrusters aimed { $direction }.

cmu-jump-pack-no-fuel = The jump pack sputters. It's out of fuel!
cmu-jump-pack-no-room = There's no open sky above you!
cmu-jump-pack-jump-hovering = You're already hovering!
cmu-jump-pack-hover-start = The jump pack roars and lifts you off the ground.
cmu-jump-pack-hover-expired = The jump pack's thrusters overheat, lowering you back down.
cmu-jump-pack-hover-out-of-fuel = The jump pack runs dry, lowering you back down.
