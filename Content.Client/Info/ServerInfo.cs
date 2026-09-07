using System;
using System.Collections.Generic;
using Content.Client.Changelog;
using Content.Client.Credits;
using Content.Client.Stylesheets;
using Content.Client.UserInterface.Controls;
using Content.Shared.GameTicking;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.Client.Info
{
    /// <summary>
    ///     The lobby's server-info block: a heading row, then a table of round info. The round timer
    ///     is a cell of that table, kept alive across rebuilds so LobbyState can drive it per-frame.
    /// </summary>
    public sealed class ServerInfo : BoxContainer
    {
        private readonly Label _title;
        private readonly GridContainer _roundInfoGrid;
        private readonly BoxContainer _extraLines;

        public ServerInfo()
        {
            Orientation = LayoutOrientation.Vertical;

            // A plain label, not a NanoHeading. NanoHeading draws its own bordered panel, which is
            // the boxed treatment the section headings used to carry - a frame around a word, on a
            // screen where nothing else has a frame any more.
            _title = new Label
            {
                VerticalAlignment = VAlignment.Center,
                StyleClasses = { StyleNano.StyleClassCrtSectionTitle },
            };

            RoundTimeLabel = new Label
            {
                StyleClasses = { StyleNano.StyleClassCrtStatValue },
            };
            PlayersLabel = new Label
            {
                StyleClasses = { StyleNano.StyleClassCrtStatValue },
            };

            // Inline in the heading row rather than cells of their own. As tall narrow cells beside
            // the character block these two wasted most of their box - they are one short line each,
            // so they want to be wide and short. The heading row was already there and mostly empty,
            // so putting them in it costs no vertical height at all and removes the leftover column
            // the character block could never fill.
            StatsColumn = new BoxContainer
            {
                Orientation = LayoutOrientation.Horizontal,
                SeparationOverride = 6,
                VerticalAlignment = VAlignment.Center,
                Children =
                {
                    MakeInlineLabel(Loc.GetString("lobby-info-players")),
                    PlayersLabel,
                    MakeInlineLabel("|"),
                    MakeInlineLabel(Loc.GetString("lobby-info-round-time")),
                    RoundTimeLabel,
                },
            };

            // The rule under this row is a CRT-genre convention - the rich-text markup used
            // elsewhere has no underline tag, so CRT draws the separator as a border instead. NanoUI
            // has no such convention and no matching visual language for it, so the row stays a
            // plain, unstyled grouping box in base mode rather than carrying a CRT-only decoration.
            var titleRow = new PanelContainer
            {
                HorizontalExpand = true,
                // Air above and below. The heading and the two stats had none of their own, so they
                // sat hard against the command band above and the round table below and read as one
                // dense block rather than as a section with a title. Small on purpose: this row is
                // three short items and does not want a margin wide enough to become a gap.
                Margin = new Thickness(0, 7, 0, 6),
                Children =
                {
                    new BoxContainer
                    {
                        Orientation = LayoutOrientation.Horizontal,
                        HorizontalExpand = true,
                        SeparationOverride = 8,
                        // A hairline in place of the plain spacer that used to hold this row open.
                        // The gap between the title and the stats was already there and doing
                        // nothing; a rule across it is what makes the heading read as a section
                        // break rather than as one more line of text, and it costs no height.
                        Children =
                        {
                            _title,
                            new PanelContainer
                            {
                                StyleClasses = { StyleNano.StyleClassCrtSectionRule },
                                HorizontalExpand = true,
                                VerticalAlignment = VAlignment.Center,
                                MinHeight = 1,
                            },
                            StatsColumn,
                        },
                    },
                },
            };

            AddChild(titleRow);

            // Round info is a real grid rather than pre-formatted text so the columns stay aligned
            // at any panel width and however long a ship or platoon name gets.
            _roundInfoGrid = new GridContainer
            {
                Columns = 2,
                HorizontalExpand = true,
                HSeparationOverride = 2,
                VSeparationOverride = 2,
                Margin = new Thickness(0, 4, 0, 0),
            };
            AddChild(_roundInfoGrid);

            _extraLines = new BoxContainer
            {
                Orientation = LayoutOrientation.Vertical,
                HorizontalExpand = true,
                Margin = new Thickness(0, 2, 0, 0),
            };
            AddChild(_extraLines);
        }

        /// <summary>
        ///     Heading shown to the left of the server title.
        /// </summary>
        public string? Title
        {
            get => _title.Text;
            set => _title.Text = value;
        }

        /// <summary>
        ///     The round timer, driven per-frame by LobbyState.
        /// </summary>
        public Label RoundTimeLabel { get; }

        /// <summary>
        ///     The player count, shown in <see cref="StatsColumn"/> rather than in the round-info grid.
        /// </summary>
        public Label PlayersLabel { get; }

        /// <summary>
        ///     Players and round time, as a standalone column for the lobby to place beside the
        ///     character block. Never parented into the round-info grid.
        /// </summary>
        public Control StatsColumn { get; }

        /// <summary>
        ///     Sets the server's intro text. The first line becomes the title heading and is drawn as
        ///     plain text, so it must not contain markup. Any further lines render underneath.
        /// </summary>
        public void SetInfoBlob(string markup)
        {
            _extraLines.DisposeAllChildren();

            var first = true;
            foreach (var line in markup.Split('\n'))
            {
                var trimmed = line.Trim();
                if (trimmed.Length == 0)
                    continue;

                // The first line is the server's own name, which used to be drawn as a heading beside
                // "SERVER INFO". It is dropped: the lobby's own header already says which server this
                // is, so repeating it inside the panel was duplication taking up a whole row.
                if (first)
                {
                    first = false;
                    continue;
                }

                var label = new RichTextLabel
                {
                    HorizontalAlignment = HAlignment.Center,
                    // Opts out of the generic CRT body-text style so it can be sized independently.
                    // CrtLobbyTheme checks for this class and leaves it alone.
                    StyleClasses = { StyleNano.StyleClassCrtServerInfoText },
                };
                label.SetMessage(FormattedMessage.FromMarkupOrThrow(trimmed), tagsAllowed: null);
                _extraLines.AddChild(label);
            }
        }

        /// <summary>
        ///     Rebuilds the round-info table. Each field becomes a boxed cell with its heading above
        ///     its value, laid out two columns per row, with the round timer last.
        /// </summary>
        public void SetRoundInfo(IReadOnlyList<LobbyRoundInfoField> fields)
        {
            _roundInfoGrid.DisposeAllChildren();

            // The lead pair, and the trailing player count, are both positional - see
            // GameTicker.GetRoundInfoFields, which owns the order. Planet and gamemode lead; the
            // player count is last and is consumed by StatsColumn instead of being drawn in the grid,
            // so the grid is left with exactly the paired GOVFOR/OPFOR fields and never a ragged row.
            const int leadCells = 2;
            var gridCount = Math.Max(0, fields.Count - 1);

            for (var i = 0; i < gridCount; i++)
            {
                var field = fields[i];
                var lead = i < leadCells;

                var value = new Label
                {
                    Text = field.Value,
                    HorizontalExpand = true,
                    Align = Label.AlignMode.Left,
                    ClipText = true,
                    StyleClasses =
                    {
                        lead
                            ? StyleNano.StyleClassCrtFieldValueLead
                            : StyleNano.StyleClassCrtFieldValue,
                    },
                };

                if (field.Color != null && Color.TryFromHex(field.Color, out var color))
                    value.FontColorOverride = color;

                var cellClass = lead
                    ? StyleNano.StyleClassCrtTableCellLead
                    : (i - leadCells) / _roundInfoGrid.Columns % 2 == 0
                        ? StyleNano.StyleClassCrtTableCell
                        : StyleNano.StyleClassCrtTableCellAlt;

                _roundInfoGrid.AddChild(MakeCell(field.Label, value, cellClass));
            }

            if (fields.Count > 0)
                PlayersLabel.Text = fields[^1].Value;
        }

        private static Label MakeInlineLabel(string text)
        {
            return new Label
            {
                Text = text,
                StyleClasses = { StyleNano.StyleClassCrtFieldLabel },
                VerticalAlignment = VAlignment.Center,
            };
        }

        private static PanelContainer MakeCell(
            string headingText,
            Label value,
            string cellClass = StyleNano.StyleClassCrtTableCell)
        {
            var heading = new Label
            {
                Text = headingText,
                HorizontalExpand = true,
                Align = Label.AlignMode.Left,
                ClipText = true,
                StyleClasses = { StyleNano.StyleClassCrtFieldLabel },
            };

            return new PanelContainer
            {
                HorizontalExpand = true,
                StyleClasses = { cellClass },
                Children =
                {
                    new BoxContainer
                    {
                        Orientation = LayoutOrientation.Vertical,
                        HorizontalExpand = true,
                        SeparationOverride = 2,
                        Children = { heading, value },
                    },
                },
            };
        }
    }
}
