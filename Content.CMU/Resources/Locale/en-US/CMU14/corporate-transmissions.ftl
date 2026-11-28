## Secure Transmissions terminal

cmu-transmissions-title = Secure Transmissions
cmu-transmissions-heading = Signal Intercept
cmu-transmissions-instructions = Break into the intercepted signal by picking codes off the grid. Your first pick comes from the top row. After that, picks alternate between the column and the row of your last pick, and no code can be used twice. Every pick goes into the buffer. Each target sequence that ends up in the buffer, in order and unbroken, pays out. Upload early if the buffer has what you need.
cmu-transmissions-tiers-section = Intercept a signal
cmu-transmissions-tier =
    {$name}, up to ${$reward}
    {$size}x{$size} grid, {$sequences} {$sequences ->
        [one] target
       *[other] targets
    }, buffer {$buffer}
cmu-transmissions-tier-timed =
    {$name}, up to ${$reward}
    {$size}x{$size}, {$sequences} targets, buffer {$buffer}, {$seconds}s
cmu-transmissions-tier-commercial = Commercial
cmu-transmissions-tier-military = Military
cmu-transmissions-tier-blacksite = Black site
cmu-transmissions-targets = Target sequences
cmu-transmissions-target = {$codes}
cmu-transmissions-target-done = {$codes}   [color=#7fd47f]UPLOADED[/color]
cmu-transmissions-buffer = Buffer
cmu-transmissions-signal = Signal
cmu-transmissions-upload = Upload
cmu-transmissions-timer = {$seconds}s left
cmu-transmissions-timer-waiting = {$seconds}s once you start
cmu-transmissions-status-active = {$name} signal. {$left} {$left ->
    [one] pick
   *[other] picks
} left in the buffer.
cmu-transmissions-status-done = Uploaded {$completed} of {$total} {$total ->
    [one] sequence
   *[other] sequences
}. Paid ${$payout} to the corporate budget.
cmu-transmissions-status-timed-out = The signal closed before the upload. Nothing was recovered.
cmu-transmissions-status-cooldown = Next signal in {$seconds}s.
cmu-transmissions-status-idle = Pick a signal to start.
cmu-transmissions-cooldown = The terminal is still re-tuning to the next signal.
