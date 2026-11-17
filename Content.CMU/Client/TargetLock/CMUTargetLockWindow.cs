using System.Numerics;
using Content.Client.CMU14.Interface;
using Content.Shared.CMU14.TargetLock;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.CMU14.TargetLock;

/// <summary>
///     What the scanner read off someone, in the CRT theme: a health estimate over a small picture of them, their name
///     and position, a warning when they carry a weapon, and their criminal and medical records one click away.
/// </summary>
public sealed class CMUTargetLockWindow : DefaultWindow
{
    [Dependency] private readonly IEntityManager _entities = default!;

    private readonly CrtConsoleKit _kit;
    private readonly Color _textColor;
    private readonly BoxContainer _recordBox = CrtConsoleKit.Column(4);

    public CMUTargetLockWindow(CMUTargetLockReportEvent report)
    {
        IoCManager.InjectDependencies(this);

        // Scanlines and the CRT look, but in plain whites and greys instead of the theme's green, and always on.
        CrtTerminalPalette.Neutral = true;
        try
        {
            _kit = new CrtConsoleKit(neutral: true);
            _textColor = CrtTerminalPalette.Text;
            Build(report);
        }
        finally
        {
            CrtTerminalPalette.Neutral = false;
        }
    }

    private void Build(CMUTargetLockReportEvent report)
    {
        Title = Loc.GetString("cmu-target-lock-title");
        MinSize = new Vector2(320, 360);
        SetSize = new Vector2(340, 420);

        var healthColor = report.Health switch
        {
            CMUTargetLockHealth.Healthy => CrtTerminalPalette.Accent,
            CMUTargetLockHealth.Light => CrtTerminalPalette.Text,
            CMUTargetLockHealth.Wounded => CrtTerminalPalette.Caution,
            _ => CrtTerminalPalette.Alert,
        };

        var tag = _kit.Tag(out var tagText);
        CrtConsoleKit.SetTag(tag, tagText, Loc.GetString("cmu-target-lock-locked"), CrtTerminalPalette.Accent);
        _kit.Root.AddChild(_kit.Header(Loc.GetString("cmu-target-lock-heading"), out var sub, tag));
        sub.Text = Loc.GetString("cmu-target-lock-subheading");

        var scroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false };
        var body = CrtConsoleKit.Column(8);
        body.HorizontalExpand = true;
        scroll.AddChild(body);
        _kit.Root.AddChild(scroll);

        // The estimate sits right above the picture.
        var health = CrtConsoleKit.Text(
            Loc.GetString("cmu-target-lock-health", ("percent", report.HealthPercent),
                ("state", Loc.GetString($"cmu-target-lock-health-{report.Health.ToString().ToLowerInvariant()}"))),
            _kit.Small, healthColor);
        health.HorizontalAlignment = HAlignment.Center;
        body.AddChild(health);

        var entity = _entities.TryGetEntity(report.Target, out var target) ? target : null;
        body.AddChild(new CMUTargetLockSnapshotView(entity, new Vector2i(96, 96)));

        body.AddChild(_kit.Field(Loc.GetString("cmu-target-lock-name"), report.Name, CrtTerminalPalette.TextBright, 80));
        body.AddChild(_kit.Field(Loc.GetString("cmu-target-lock-position"), report.Position, null, 80));

        if (report.Weapons.Count > 0)
        {
            body.AddChild(_kit.Section(Loc.GetString("cmu-target-lock-armed"), out var armed, CrtTerminalPalette.Alert));
            armed.AddChild(CrtConsoleKit.Wrapping(string.Join(", ", report.Weapons), CrtTerminalPalette.Alert));
        }

        var buttons = CrtConsoleKit.Row(8);
        var criminal = _kit.Button(Loc.GetString("cmu-target-lock-criminal"), CmuButtonStyles.Variant.Caution);
        var medical = _kit.Button(Loc.GetString("cmu-target-lock-medical"), CmuButtonStyles.Variant.Affirm);
        criminal.HorizontalExpand = true;
        medical.HorizontalExpand = true;
        criminal.OnPressed += _ => ShowRecord(report.CriminalRecord);
        medical.OnPressed += _ => ShowRecord(report.MedicalRecord);
        buttons.AddChild(criminal);
        buttons.AddChild(medical);
        body.AddChild(buttons);
        body.AddChild(_recordBox);

        _kit.Root.AddChild(_kit.Footer(out var footer));
        footer.Text = Loc.GetString("cmu-target-lock-footer");

        Contents.AddChild(_kit.Tube);
    }

    private void ShowRecord(string text)
    {
        _recordBox.RemoveAllChildren();
        _recordBox.AddChild(CrtConsoleKit.Wrapping(text, _textColor));
    }
}
