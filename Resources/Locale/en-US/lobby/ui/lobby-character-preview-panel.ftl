lobby-character-preview-panel-header = Character
lobby-character-preview-panel-character-setup-button = Customize
lobby-character-preview-panel-unloaded-preferences-label = Your character preferences have not yet loaded, please stand by.
lobby-character-preview-prev-char-tooltip = Previous character
lobby-character-preview-next-char-tooltip = Next character
lobby-character-preview-ignore-allegiance = Ignore Allegiance
lobby-character-preview-ignore-allegiance-tooltip = When enabled, spawns your currently selected character regardless of allegiance matching.
# The toggle states below spell out on/off in the label itself rather than relying on the button's
# colour alone - color-only state indicators are hard to read at a glance and unreliable for anyone
# with a colour vision deficiency. The On state additionally carries hazard striping, so the enabled
# state is marked by shape as well as by word and fill: this toggle overrides allegiance matching and
# is the one setting here that changes who you can spawn as, so it should be obvious at a glance that
# it is armed. Plain slashes rather than an icon glyph - the OSD font has no icon coverage and a
# missing glyph renders as a blank box.
lobby-character-preview-ignore-allegiance-off = Ignore Allegiance: Off
lobby-character-preview-ignore-allegiance-on = /// Ignore Allegiance: On ///

# Two-line character summary shown beside the preview sprite. The pronoun and its verb have to stay
# inside one selector ("He is" vs "They are"), so the colour wraps the whole phrase.
# Both hues sit well under the terminal text's own brightness so they read as secondary rather than
# as two alarm colours on a green screen: saturation 0.22, luminance 0.62. See docs/cmu/09-theming.md.
lobby-character-summary-name = This is [color=#FFFFFF]{$name}[/color]
lobby-character-summary-age = [color=#88A3AF]{$gender ->
    [male] He is
    [female] She is
    [epicene] They are
    *[other] It is
}[/color] [color=#BF9595]{$age}[/color] years old
