using System.Globalization;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;

namespace Content.Client.Administration.UI.CustomControls;

public sealed class AdminLogLabel : PanelContainer
{
    private const string TimestampFormat = "yyyy-MM-dd HH:mm:ss";

    private static readonly Color BackgroundColor = Color.FromHex("#1B1B1E");
    private static readonly Color BorderColor = Color.FromHex("#303645");
    private static readonly Color ChipBackgroundColor = Color.FromHex("#23272F");
    private static readonly Color ChipBorderColor = Color.FromHex("#343A45");
    private static readonly Color ChipLabelColor = Color.FromHex("#8E98A8");
    private static readonly Color ChipValueColor = Color.FromHex("#DCE5F2");
    private static readonly Color HeaderColor = Color.FromHex("#C8D0DB");
    private static readonly Color SeparatorColor = Color.FromHex("#252A33");
    private static readonly Color TypeColor = Color.FromHex("#9DBCE6");

    public AdminLogLabel(ref SharedAdminLog log, HSeparator separator)
    {
        Log = log;
        Separator = separator;
        Details = AdminLogMessageFormatter.GetDetails(log.Type, log.Message);
        TimeText = log.Date.ToLocalTime().ToString(TimestampFormat, CultureInfo.InvariantCulture);
        ImpactText = log.Impact.ToString();
        TypeText = log.Type.ToString();

        HorizontalExpand = true;
        PanelOverride = new StyleBoxFlat
        {
            BackgroundColor = BackgroundColor,
            BorderColor = GetImpactColor(log.Impact),
            BorderThickness = new Thickness(4, 1, 1, 1),
            ContentMarginLeftOverride = 8,
            ContentMarginRightOverride = 8,
            ContentMarginTopOverride = 6,
            ContentMarginBottomOverride = 6,
        };

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            SeparationOverride = 4,
        };

        var header = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            SeparationOverride = 8,
        };

        header.AddChild(new Label
        {
            Text = TimeText,
            ClipText = true,
            MinWidth = 145,
            FontColorOverride = HeaderColor,
        });

        header.AddChild(new Label
        {
            Text = ImpactText,
            ClipText = true,
            FontColorOverride = GetImpactColor(log.Impact),
        });

        header.AddChild(new Label
        {
            Text = TypeText,
            ClipText = true,
            HorizontalExpand = true,
            FontColorOverride = TypeColor,
        });

        var message = new RichTextLabel
        {
            HorizontalExpand = true,
        };
        message.SetMessage(AdminLogMessageFormatter.Format(log.Type, log.Message));

        root.AddChild(header);
        root.AddChild(message);

        if (Details.Count > 0)
        {
            root.AddChild(CreateDetailsRow(Details));
        }

        AddChild(root);

        Separator.Color = SeparatorColor;
        OnVisibilityChanged += VisibilityChanged;
    }

    public new SharedAdminLog Log { get; }

    public HSeparator Separator { get; }

    public IReadOnlyList<AdminLogMessageDetail> Details { get; }

    public string TimeText { get; }

    public string ImpactText { get; }

    public string TypeText { get; }

    private static Color GetImpactColor(LogImpact impact)
    {
        return impact switch
        {
            LogImpact.Extreme => Color.FromHex("#FF6B5F"),
            LogImpact.High => Color.FromHex("#FF9C5A"),
            LogImpact.Medium => Color.FromHex("#D7B95E"),
            LogImpact.Low => Color.FromHex("#8DBA75"),
            _ => BorderColor,
        };
    }

    private static Control CreateDetailsRow(IReadOnlyList<AdminLogMessageDetail> details)
    {
        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            SeparationOverride = 4,
        };

        foreach (var detail in details)
        {
            row.AddChild(CreateDetailChip(detail));
        }

        return row;
    }

    private static Control CreateDetailChip(AdminLogMessageDetail detail)
    {
        var chip = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = ChipBackgroundColor,
                BorderColor = ChipBorderColor,
                BorderThickness = new Thickness(1),
                ContentMarginLeftOverride = 6,
                ContentMarginRightOverride = 6,
                ContentMarginTopOverride = 2,
                ContentMarginBottomOverride = 2,
            },
        };

        var text = new RichTextLabel
        {
            MaxWidth = 220,
        };

        var message = new Robust.Shared.Utility.FormattedMessage();
        message.PushColor(ChipLabelColor);
        message.AddText($"{detail.Label} ");
        message.Pop();
        message.PushColor(GetDetailValueColor(detail.Label));
        message.AddText(detail.Value);
        message.Pop();
        text.SetMessage(message);

        chip.AddChild(text);
        return chip;
    }

    private static Color GetDetailValueColor(string label)
    {
        return label switch
        {
            "damage" => AdminLogMessageFormatter.DamageColor,
            "proto" => AdminLogMessageFormatter.PrototypeColor,
            "coord" => AdminLogMessageFormatter.CoordinateColor,
            "command" => AdminLogMessageFormatter.MessageColor,
            _ => ChipValueColor,
        };
    }

    private void VisibilityChanged(Control control)
    {
        Separator.Visible = Visible;
    }

    [System.Obsolete]
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        OnVisibilityChanged -= VisibilityChanged;
    }
}
