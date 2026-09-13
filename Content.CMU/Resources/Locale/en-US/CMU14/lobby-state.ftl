# Heading for the lobby action panel once the round is running. Upstream blanks the countdown at
# that point, which left the panel's heading slot empty and the button grid hard against the top
# border; these give the panel the same two-line head in both states.
cmu-lobby-state-round-in-progress = Round in progress
cmu-lobby-state-round-elapsed = Round time: {$hours}h {$minutes}m

# The faction choices live in a popup rather than on the lobby panel itself.
cmu-lobby-join-round = Join the Round
cmu-lobby-join-round-window-title = Join the Round
cmu-lobby-join-round-prompt = Pick a side to join as.

cmu-lobby-join-colonists-desc = Whether by signing a contract or choosing to start over, you now live on one of hundreds of colonies on the fringes of human-controlled space. Some are glad of the flag flying over it. Others are counting the days until it comes down.
cmu-lobby-join-govfor-desc = Money, healthcare, free college. For one reason or another, you now serve in one of the many military or paramilitary organizations that keep order on the frontier. Law is on your side, enforce it.
cmu-lobby-join-opfor-desc = Sovereignty, profit, or a score worth settling. You now serve one of the powers the frontier's authorities call hostile, a title that depends entirely on who is doing the calling. You have your own orders and your own flag. Neither answers to theirs.
cmu-lobby-join-other-desc = Humanity has a knack for destroying itself, and out here it finally has competition. Hives that empty a colony in a night, hunters who come for the sport, things the survivors never agree on a name for. Whatever you are, you did not come to negotiate.

# The ready toggle names the state it is IN. The slashes are doing the same job as the inverted
# fill behind them - the state has to be obvious without reading the word. Only the ready side is
# marked, deliberately: an empty bracket on the other one added a second thing to read without
# saying anything the dark fill and the dim text did not already say.
cmu-lobby-ready-yes = /// READY ///
cmu-lobby-ready-no = Not ready

# The round clock: a caption saying what is being counted, and a face holding only the value. The
# split is what lets the face be sized to be read across the screen - a face carrying a whole
# sentence could not be.
cmu-lobby-clock-caption-countdown = Round starts in
cmu-lobby-clock-caption-start = Round start
cmu-lobby-clock-caption-elapsed = Round in progress
cmu-lobby-clock-face-soon = Soon
cmu-lobby-clock-face-paused = Paused
cmu-lobby-clock-face-now = Now

# Folding the clock away into the action column, and getting it back. The docked button's label is
# the countdown itself, so it needs a format rather than a fixed string.
cmu-lobby-clock-minimize-tooltip = Fold the clock into the panel on the left
cmu-lobby-clock-restore-tooltip = Put the clock back where it was
cmu-lobby-clock-docked = Round in {$time}
cmu-lobby-clock-docked-paused = Round start paused
cmu-lobby-clock-docked-soon = Round starts soon
cmu-lobby-clock-docked-now = Round starting now
