using Content.Client.Stylesheets;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client.Actions.UI
{
    /// <summary>
    /// Tooltip for actions or alerts because they are very similar.
    /// </summary>
    public sealed partial class ActionAlertTooltip : PanelContainer // CMU14: cached text updates.
    {
        private const float TooltipTextMaxWidth = 350;

        private readonly RichTextLabel _cooldownLabel;
        private readonly RichTextLabel _dynamicMessageLabel;
        private readonly IGameTiming _gameTiming;

        /// <summary>
        /// Current cooldown displayed in this tooltip. Set to null to show no cooldown.
        /// </summary>
        public (TimeSpan Start, TimeSpan End)? Cooldown { get; set; }
        public string? DynamicMessage { get; set; }

        public ActionAlertTooltip(FormattedMessage name, FormattedMessage? desc, string? requires = null)
        {
            Stylesheet = IoCManager.Resolve<IStylesheetManager>().SheetSystem;
            _gameTiming = IoCManager.Resolve<IGameTiming>();

            SetOnlyStyleClass(StyleClass.TooltipPanel);

            BoxContainer vbox;
            AddChild(vbox = new BoxContainer
            {
                Orientation = LayoutOrientation.Vertical,
                RectClipContent = true
            });
            var nameLabel = new RichTextLabel
            {
                MaxWidth = TooltipTextMaxWidth,
                StyleClasses = { StyleClass.TooltipTitle }
            };
            nameLabel.SetMessage(name);
            vbox.AddChild(nameLabel);

            if (desc != null && !string.IsNullOrWhiteSpace(desc.ToString()))
            {
                var description = new RichTextLabel
                {
                    MaxWidth = TooltipTextMaxWidth,
                    StyleClasses = { StyleClass.TooltipDesc }
                };
                description.SetMessage(desc);
                vbox.AddChild(description);
            }

            vbox.AddChild(_cooldownLabel = new RichTextLabel
            {
                MaxWidth = TooltipTextMaxWidth,
                StyleClasses = { StyleClass.TooltipDesc },
                Visible = false
            });

            vbox.AddChild(_dynamicMessageLabel = new RichTextLabel
            {
                MaxWidth = TooltipTextMaxWidth,
                StyleClasses = {StyleNano.StyleClassTooltipActionDynamicMessage},
                Visible = false
            });

            if (!string.IsNullOrWhiteSpace(requires))
            {
                var requiresLabel = new RichTextLabel
                {
                    MaxWidth = TooltipTextMaxWidth,
                    StyleClasses = { StyleClass.TooltipDesc }
                };

                if (!FormattedMessage.TryFromMarkup("[color=#635c5c]" + requires + "[/color]", out var markup))
                    return;

                requiresLabel.SetMessage(markup);

                vbox.AddChild(requiresLabel);
            }
        }

        // CMU14 method: only rebuild markup when the displayed inputs change.
        protected override void FrameUpdate(FrameEventArgs args)
        {
            base.FrameUpdate(args);
            CMUUpdateText();
        }
    }
}
