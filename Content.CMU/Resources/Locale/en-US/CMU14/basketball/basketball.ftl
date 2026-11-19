ent-CMUBasketballHoopLeft = left basketball hoop
    .desc = Scores for the left team when a ball goes through it. Link it to a scoreboard with a multitool.
ent-CMUBasketballHoopRight = right basketball hoop
    .desc = Scores for the right team when a ball goes through it. Link it to a scoreboard with a multitool.
ent-CMUBasketballScoreboard = basketball scoreboard
    .desc = Displays the score of the hoops linked to it. Right-click it to name the teams or pick the winning score.
ent-CMUBasketballReset = basketball scoreboard reset
    .desc = Resets the scoreboards linked to it and starts a new game.

# Default team names. Used as {$team} below, so keep them usable as the subject of a sentence.
cmu-basketball-side-left = Left
cmu-basketball-side-right = Right

cmu-basketball-shot = {$team} scores {$points} { $points ->
    [one] point
   *[other] points
}!
cmu-basketball-dunk = {$team} slams in a {$points}-point dunk!
cmu-basketball-win = {$team} wins, {$score} to {$otherScore}!
cmu-basketball-miss = The shot bounces off the rim!

cmu-basketball-reset = The basketball score has been reset.
cmu-basketball-reset-cooldown = The reset button is recharging.
cmu-basketball-reset-unlinked = The reset button isn't linked to a scoreboard.

# {$side} is "left" or "right".
cmu-basketball-verb-name = Set { $side ->
    [left] left
   *[right] right
} team name
cmu-basketball-verb-target = Set winning score
cmu-basketball-locked = A game is in progress. Press reset to change the teams or the winning score.

cmu-basketball-name-title = Team name
cmu-basketball-name-prompt = Enter a name for the { $side ->
    [left] left
   *[right] right
} team. Leave it blank to reset it.
cmu-basketball-name-set = The { $side ->
    [left] left
   *[right] right
} team is now called {$name}.

cmu-basketball-target-title = Winning score
cmu-basketball-target-score = First to {$score}
cmu-basketball-target-none = No limit
cmu-basketball-target-set = Winning score set: {$target}.

cmu-basketball-scoreboard-examine = [color=yellow]{$left}[/color] {$leftScore} - {$rightScore} [color=yellow]{$right}[/color]
cmu-basketball-scoreboard-examine-target = Winning score: {$target}.
cmu-basketball-scoreboard-examine-winner = [color=green]{$team} won![/color] Press reset to play again.

cmu-signal-port-name-basketball-score = Score
cmu-signal-port-description-basketball-score-source = Sends this hoop's baskets to the scoreboard.
cmu-signal-port-description-basketball-score-sink = Adds the linked hoop's baskets to its side of the score.
cmu-signal-port-name-basketball-reset = Reset
cmu-signal-port-description-basketball-reset-source = Resets the linked scoreboards when pressed.
cmu-signal-port-description-basketball-reset-sink = Sets both sides of the score back to zero.
