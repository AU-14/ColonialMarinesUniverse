using System;
using System.Collections.Generic;
using Content.Client.CMU14.Interface;
using Content.Client.Stylesheets;
using Content.Shared.GameTicking;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Localization;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.Client.Info
{
    /// <summary>
    // CMU14
    ///     The lobby's round-info panel. Three bands on one screen: the planet and gamemode, then the
    ///     two forces under their own headings, then the player count and round timer.
    /// </summary>
    public sealed class ServerInfo : BoxContainer
    {
        // CMU14
        private readonly Label _planet;
        private readonly Label _gamemode;
        private readonly BoxContainer _groups;
        private readonly BoxContainer _extraLines;

        public ServerInfo()
        {
            Orientation = LayoutOrientation.Vertical;
            // CMU14
            HorizontalExpand = true;

            // CMU14
            // Natural width, like the row labels below it - the leader beside it is what expands.
            _planet = new Label
            {
                // CMU14
                StyleClasses = { StyleNano.StyleClassCrtHeading },
            };

            // CMU14
            // No ClipText without HorizontalExpand: a clipping Label measures at zero width and draws nothing.
            _gamemode = new Label
            {
                // CMU14
                StyleClasses = { StyleNano.StyleClassCrtFieldValue },
            };

            // CMU14
            AddChild(new BoxContainer
            {
                Orientation = LayoutOrientation.Horizontal,
                // CMU14
                HorizontalExpand = true,
                SeparationOverride = 8,
                Children = { _planet, Leader(), _gamemode },
            });

            AddChild(new PanelContainer
            {
                StyleClasses = { StyleNano.StyleClassCrtSectionRule },
                HorizontalExpand = true,
                MinHeight = 1,
                Margin = new Thickness(0, 7, 0, 7),
            });

            _groups = new BoxContainer
            {
                Orientation = LayoutOrientation.Vertical,
                HorizontalExpand = true,
            };
            AddChild(_groups);

            RoundTimeLabel = new Label { StyleClasses = { StyleNano.StyleClassCrtStatValue } };
            PlayersLabel = new Label { StyleClasses = { StyleNano.StyleClassCrtStatValue } };

            // Takes the slack, so the counts sit on the bottom edge of the screen zone rather than trailing.
            AddChild(new Control { VerticalExpand = true });

            AddChild(new PanelContainer
            {
                StyleClasses = { StyleNano.StyleClassCrtSectionRule },
                HorizontalExpand = true,
                MinHeight = 1,
                Margin = new Thickness(0, 7, 0, 6),
            });

            AddChild(new BoxContainer
            {
                Orientation = LayoutOrientation.Horizontal,
                HorizontalExpand = true,
                SeparationOverride = 6,
                Children =
                {
                    // CMU14
                    FieldLabel(Loc.GetString("lobby-info-players")),
                    PlayersLabel,
                    // CMU14
                    Leader(),
                    FieldLabel(Loc.GetString("lobby-info-round-time")),
                    RoundTimeLabel,
                },
            // CMU14
            });

            _extraLines = new BoxContainer
            {
                Orientation = LayoutOrientation.Vertical,
                HorizontalExpand = true,
                // CMU14
                Margin = new Thickness(0, 4, 0, 0),
            };
            AddChild(_extraLines);
        }

        // CMU14
        /// <summary>The round timer, driven per-frame by LobbyState.</summary>
        public Label RoundTimeLabel { get; }

        // CMU14
        /// <summary>The player count, drawn beside the timer at the foot of the panel.</summary>
        public Label PlayersLabel { get; }

        /// <summary>
        // CMU14
        ///     Sets the server's intro text. The first line is the server's own name and is dropped -
        ///     the housing's nameplate already says which server this is. Any further lines render
        ///     under the panel.
        /// </summary>
        public void SetInfoBlob(string markup)
        {
            _extraLines.ReleaseChildren();

            var first = true;
            foreach (var line in markup.Split('\n'))
            {
                var trimmed = line.Trim();
                if (trimmed.Length == 0)
                    continue;

                if (first)
                {
                    first = false;
                    continue;
                }

                // CMU14
                // The class opts out of the generic CRT body-text style; CrtLobbyTheme leaves it alone.
                var label = new RichTextLabel
                {
                    HorizontalAlignment = HAlignment.Center,
                    StyleClasses = { StyleNano.StyleClassCrtServerInfoText },
                };
                label.SetMessage(FormattedMessage.FromMarkupOrThrow(trimmed), tagsAllowed: null);
                _extraLines.AddChild(label);
            }
        }

        // CMU14
        /// <summary>Rebuilds the panel from the server's fields.</summary>
        public void SetRoundInfo(IReadOnlyList<LobbyRoundInfoField> fields)
        {
            // CMU14
            _groups.DisposeAllChildren();

            // CMU14
            if (fields.Count > 0)
                _planet.Text = fields[0].Value;

            // CMU14
            if (fields.Count > 1)
                _gamemode.Text = fields[1].Value;

            if (fields.Count > 0)
                PlayersLabel.Text = fields[^1].Value;

            // CMU14
            string? openGroup = null;
            for (var i = 2; i < fields.Count - 1; i++)
            {
                var field = fields[i];
                var colour = ParseColour(field.Color);

                if (field.Group != null && field.Group != openGroup)
                {
                    var heading = FieldLabel(field.Group);
                    heading.FontColorOverride = colour;
                    // Air above each heading but the first, so the two forces read as separate blocks.
                    heading.Margin = new Thickness(0, openGroup == null ? 0 : 5, 0, 1);
                    _groups.AddChild(heading);
                    openGroup = field.Group;
                }

                _groups.AddChild(Row(field.Label, field.Value, colour, field.Group != null));
            }
        }

        // CMU14
        private static BoxContainer Row(string label, string value, Color? colour, bool indent)
        {
            var valueLabel = new Label
            {
                Text = value,
                StyleClasses = { StyleNano.StyleClassCrtFieldValue },
            };

            if (colour != null)
                valueLabel.FontColorOverride = colour;

            return new BoxContainer
            {
                Orientation = LayoutOrientation.Horizontal,
                HorizontalExpand = true,
                SeparationOverride = 6,
                Margin = new Thickness(indent ? 10 : 0, 0, 0, 1),
                Children = { FieldLabel(label), Leader(), valueLabel },
            };
        }

        private static PanelContainer Leader()
        {
            return new PanelContainer
            {
                HorizontalExpand = true,
                VerticalAlignment = VAlignment.Center,
                MinHeight = 1,
                PanelOverride = new CmuDashedRuleStyleBox
                {
                    Color = CrtTerminalPalette.Line,
                    DashLength = 1,
                    GapLength = 3,
                    Thickness = 1,
                },
            };
        }

        private static Label FieldLabel(string text)
        {
            return new Label
            {
                Text = text,
                VerticalAlignment = VAlignment.Center,
                // CMU14
                StyleClasses = { StyleNano.StyleClassCrtFieldLabel },
            };
        }

        // CMU14
        private static Color? ParseColour(string? hex)
        {
            // CMU14
            return hex != null && Color.TryFromHex(hex, out var colour) ? colour : null;
        }
    }
}
