using System.Linq;
using Content.Client.CMU14.Administration;
using Content.Shared._RMC14.Admin;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Marines.Squads;
using Content.Shared._RMC14.Vendors;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.Strain;
using Content.Shared.CMU14.Administration;
using Content.Shared.CMU14.Xenomorphs.Pathogen;
using Content.Shared.Eui;
using Content.Shared.Prototypes;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.Client._RMC14.Admin;

public sealed partial class RMCAdminEui
{
    [Dependency] private IComponentFactory _compFactory = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    private CMUAdminActionsWindow _cmuWindow = default!;
    private RMCAdminEuiTargetState? _cmuState;
    private readonly List<CMUTransformChoice> _cmuTransforms = new();
    private readonly List<EntProtoId> _cmuHiveTypes = new();
    private readonly Dictionary<EntProtoId<SkillDefinitionComponent>, SpinBox> _cmuSkills = new();
    private readonly List<(Control Row, string Search)> _cmuSkillRows = new();
    private readonly HashSet<EntProtoId<SkillDefinitionComponent>> _cmuEditedSkills = new();
    private bool _cmuEditedPoints;
    private bool _cmuEditedSpecialistPoints;

    private void OpenCMUActions()
    {
        _cmuWindow = new CMUAdminActionsWindow();
        _cmuWindow.OnClose += () => SendMessage(new CloseEuiMessage());
        _cmuWindow.Refresh.OnPressed += _ =>
        {
            _cmuEditedSkills.Clear();
            _cmuEditedPoints = false;
            _cmuEditedSpecialistPoints = false;
            SendMessage(new RMCAdminRefresh());
        };
        _cmuWindow.TransformCategoryChanged += RefreshCMUTransforms;
        _cmuWindow.TransformSearch.OnTextChanged += _ => RefreshCMUTransforms();

        foreach (var proto in _prototypes.EnumeratePrototypes<EntityPrototype>())
        {
            if (proto.Abstract)
                continue;

            if (proto.TryComp(out XenoComponent? xeno, _compFactory) &&
                !proto.HasComp<XenoHiddenComponent>(_compFactory) && !proto.HasComp<XenoStrainComponent>(_compFactory))
            {
                var category = proto.HasComp<CMUPathogenHiveMemberComponent>(_compFactory) ? 1 : 0;
                _cmuTransforms.Add(new CMUTransformChoice(
                    proto.ID, proto.Name, category, xeno.Tier, proto.Description));
            }

            if (proto.HasComp<HiveComponent>(_compFactory))
                _cmuHiveTypes.Add(proto.ID);
        }

        foreach (var hiveType in _cmuHiveTypes.OrderBy(id => id.Id != "CMUCorruptedHive")
                     .ThenBy(id => _prototypes.Index(id).Name))
        {
            var proto = _prototypes.Index(hiveType);
            var create = new Button
            {
                Text = proto.Name,
                ClipText = true,
                HorizontalExpand = true,
                Margin = new Thickness(2),
                ToolTip = proto.Name,
            };
            create.OnPressed += _ => SendMessage(new CMUAdminCreateHiveMsg(hiveType,
                _cmuWindow.HiveName.Text, _cmuWindow.AssignCreatedHive.Pressed));
            CMUAdminActionsTheme.Apply(create, hiveType.Id == "CMUCorruptedHive"
                ? CMUAdminActionsTheme.Amber : CMUAdminActionsTheme.Violet);
            _cmuWindow.HiveTypes.AddChild(create);
        }
        _cmuWindow.HiveName.IsValid = name => name.Length <= 64;
        _cmuWindow.CreateCorruptedHive.OnPressed += _ => SendMessage(new CMUAdminCreateHiveMsg(
            "CMUCorruptedHive", string.Empty, _cmuState?.CMUDetails?.IsXeno == true));
        _cmuWindow.LeaveHive.OnPressed += _ => SendMessage(new CMUAdminSetHiveMsg(null));
        _cmuWindow.RemoveSquad.OnPressed += _ => SendMessage(new CMUAdminRemoveSquadMsg());
        _cmuWindow.HiveSearch.OnTextChanged += _ => RefreshCMUHives();
        _cmuWindow.SquadSearch.OnTextChanged += _ => RefreshCMUSquads();
        _cmuWindow.SkillSearch.OnTextChanged += _ => FilterCMUSkills();
        _cmuWindow.Points.ValueChanged += _ => _cmuEditedPoints = true;
        _cmuWindow.SpecialistPoints.ValueChanged += _ => _cmuEditedSpecialistPoints = true;
        _cmuWindow.ApplyPoints.OnPressed += _ =>
        {
            _cmuEditedPoints = false;
            SendMessage(new RMCAdminSetVendorPointsMsg(_cmuWindow.Points.Value));
        };
        _cmuWindow.ApplySpecialistPoints.OnPressed += _ =>
        {
            _cmuEditedSpecialistPoints = false;
            SendMessage(new RMCAdminSetSpecialistVendorPointsMsg(_cmuWindow.SpecialistPoints.Value));
        };

        RefreshCMUTransforms();
        _cmuWindow.OpenCentered();
    }

    private void RefreshCMUTransforms()
    {
        _cmuWindow.Transforms.RemoveAllChildren();
        var choices = _cmuTransforms.Where(choice => choice.Category == _cmuWindow.TransformCategory &&
                CMUMatches(choice.Name + " " + choice.Id, _cmuWindow.TransformSearch.Text))
            .DistinctBy(choice => (choice.Tier, choice.Name))
            .OrderBy(choice => choice.Tier).ThenBy(choice => choice.Name).ToList();
        foreach (var tier in choices.GroupBy(choice => choice.Tier))
        {
            _cmuWindow.Transforms.AddChild(new Label
            {
                Text = Loc.GetString("cmu-admin-tier", ("tier", tier.Key)),
                StyleClasses = { CMUAdminActionsTheme.Heading },
            });
            var grid = new GridContainer { Columns = 3, HorizontalExpand = true };
            foreach (var choice in tier)
            {
                var button = new Button
                {
                    Text = choice.Name,
                    ClipText = true,
                    HorizontalExpand = true,
                    Margin = new Thickness(2),
                    ToolTip = choice.Name + "\n" + choice.Description,
                };
                button.OnPressed += _ => SendMessage(new RMCAdminTransformXenoMsg(choice.Id));
                grid.AddChild(button);
            }
            _cmuWindow.Transforms.AddChild(grid);
        }
        CMUAdminActionsTheme.Apply(_cmuWindow.Transforms, _cmuWindow.TransformCategory == 0
            ? CMUAdminActionsTheme.Violet : CMUAdminActionsTheme.Amber);
        _cmuWindow.TransformEmpty.Visible = choices.Count == 0;
        _cmuWindow.TransformCount.Text = Loc.GetString("cmu-admin-body-count", ("count", choices.Count));
    }

    private void UpdateCMUActions(EuiStateBase state)
    {
        if (state is not RMCAdminEuiTargetState target)
            return;

        var changedTarget = _cmuState?.CMUDetails?.Target != target.CMUDetails?.Target;
        _cmuState = target;
        var details = target.CMUDetails;
        _cmuWindow.TargetName.Text = details == null
            ? Loc.GetString("cmu-admin-target-missing")
            : details.Name;
        _cmuWindow.TargetId.Text = details == null ? string.Empty : details.Target.ToString();
        _cmuWindow.Tabs.Visible = details != null;
        if (details == null)
            return;

        // Saving one field must not discard other unapplied edits. Refresh explicitly reloads them.
        if (changedTarget)
        {
            _cmuEditedSkills.Clear();
            _cmuEditedPoints = false;
            _cmuEditedSpecialistPoints = false;
        }
        if (!_cmuEditedPoints)
            _cmuWindow.Points.OverrideValue(target.Points);
        if (!_cmuEditedSpecialistPoints)
            _cmuWindow.SpecialistPoints.OverrideValue(
                target.ExtraPoints.GetValueOrDefault(SharedCMAutomatedVendorSystem.SpecialistPoints));

        foreach (var (skill, level) in details.Skills.OrderBy(pair => _prototypes.Index(pair.Key).Name))
        {
            if (!_cmuSkills.TryGetValue(skill, out var spin))
            {
                var proto = _prototypes.Index(skill);
                var row = new BoxContainer { SeparationOverride = 8 };
                row.AddChild(new Label
                {
                    Text = proto.Name, HorizontalExpand = true, ClipText = true, ToolTip = proto.ID,
                });
                spin = new SpinBox { MinWidth = 110, IsValid = value => value >= 0 };
                spin.InitDefaultButtons();
                row.AddChild(spin);
                var input = spin;
                var apply = new Button { Text = Loc.GetString("cmu-admin-apply") };
                spin.ValueChanged += _ => _cmuEditedSkills.Add(skill);
                apply.OnPressed += _ =>
                {
                    _cmuEditedSkills.Remove(skill);
                    SendMessage(new CMUAdminSetSkillMsg(skill, input.Value));
                };
                row.AddChild(apply);
                CMUAdminActionsTheme.Apply(row);
                _cmuWindow.Skills.AddChild(row);
                _cmuSkills.Add(skill, spin);
                _cmuSkillRows.Add((row, proto.Name + " " + proto.ID));
            }

            if (!_cmuEditedSkills.Contains(skill))
                spin.OverrideValue(level);
        }
        FilterCMUSkills();

        _cmuWindow.SpecialistSkills.RemoveAllChildren();
        foreach (var skill in target.SpecialistSkills.OrderBy(skill => skill.Name))
        {
            var button = new CheckBox { Text = skill.Name, Pressed = skill.Present };
            button.OnPressed += _ => SendMessage(button.Pressed
                ? new RMCAdminAddSpecSkillMsg(skill.Name)
                : new RMCAdminRemoveSpecSkillMsg(skill.Name));
            _cmuWindow.SpecialistSkills.AddChild(button);
        }
        CMUAdminActionsTheme.Apply(_cmuWindow.SpecialistSkills);
        RefreshCMUSquads();
        RefreshCMUHives();
    }

    private void FilterCMUSkills()
    {
        foreach (var (row, search) in _cmuSkillRows)
            row.Visible = CMUMatches(search, _cmuWindow.SkillSearch.Text);
    }

    private void RefreshCMUSquads()
    {
        if (_cmuState?.CMUDetails is not { } details)
            return;

        var current = _prototypes.TryIndex(details.CurrentSquad, out var currentProto)
            ? currentProto.Name : Loc.GetString("cmu-admin-none");
        _cmuWindow.CurrentSquad.Text = Loc.GetString("cmu-admin-current-squad", ("squad", current));
        _cmuWindow.RemoveSquad.Disabled = details.CurrentSquad == null;
        _cmuWindow.Squads.RemoveAllChildren();
        foreach (var squad in _cmuState.Squads)
        {
            if (!_prototypes.TryIndex(squad.Id, out var proto) || proto.Abstract ||
                !CMUMatches(proto.Name + " " + proto.ID, _cmuWindow.SquadSearch.Text))
                continue;

            var row = new BoxContainer { SeparationOverride = 8 };
            var color = proto.TryComp(out SquadTeamComponent? team, _compFactory) ? team.Color : Color.White;
            row.AddChild(new Label
            {
                Text = Loc.GetString("cmu-admin-squad-row", ("name", proto.Name), ("members", squad.Members)),
                FontColorOverride = color, HorizontalExpand = true, ClipText = true, ToolTip = proto.ID,
            });
            var assign = new Button
            {
                Text = Loc.GetString(details.CurrentSquad == squad.Id ? "cmu-admin-current" :
                    squad.Exists ? "cmu-admin-assign" : "cmu-admin-create-assign"),
                Disabled = details.CurrentSquad == squad.Id,
                MinWidth = 110,
            };
            assign.OnPressed += _ => SendMessage(new RMCAdminAddToSquadMsg(squad.Id));
            row.AddChild(assign);
            CMUAdminActionsTheme.Apply(row);
            _cmuWindow.Squads.AddChild(row);
        }
    }

    private void RefreshCMUHives()
    {
        if (_cmuState?.CMUDetails is not { } details)
            return;

        var current = details.Hives.FirstOrDefault(hive => hive.Id == details.CurrentHive);
        _cmuWindow.CurrentHive.Text = Loc.GetString("cmu-admin-current-hive",
            ("hive", current.Name ?? Loc.GetString("cmu-admin-none")));
        _cmuWindow.LeaveHive.Disabled = !details.IsXeno || details.CurrentHive == null;
        _cmuWindow.AssignCreatedHive.Disabled = !details.IsXeno;
        _cmuWindow.HiveHint.SetMessage(Loc.GetString(details.IsXeno ? "cmu-admin-hive-hint" : "cmu-admin-hive-needs-xeno"));
        _cmuWindow.Hives.RemoveAllChildren();
        foreach (var hive in details.Hives)
        {
            if (!CMUMatches(hive.Name, _cmuWindow.HiveSearch.Text))
                continue;

            var row = new BoxContainer { SeparationOverride = 8 };
            var info = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Vertical,
                HorizontalExpand = true,
            };
            info.AddChild(new Label
            {
                Text = hive.Corrupted ? Loc.GetString("cmu-admin-corrupted-hive", ("name", hive.Name)) : hive.Name,
                StyleClasses = { CMUAdminActionsTheme.Heading },
                ClipText = true,
            });
            info.AddChild(new Label
            {
                Text = Loc.GetString("cmu-admin-hive-details", ("members", hive.Members), ("larva", hive.Larva),
                    ("queen", hive.Queen ?? Loc.GetString("cmu-admin-none"))),
                StyleClasses = { CMUAdminActionsTheme.Dim },
                ToolTip = Loc.GetString("cmu-admin-hive-queen", ("queen", hive.Queen ?? Loc.GetString("cmu-admin-none"))),
                ClipText = true,
            });
            row.AddChild(info);
            var assign = new Button
            {
                Text = Loc.GetString(details.CurrentHive == hive.Id ? "cmu-admin-current" : "cmu-admin-assign"),
                Disabled = !details.IsXeno || details.CurrentHive == hive.Id,
                MinWidth = 100,
                VerticalAlignment = Control.VAlignment.Center,
            };
            assign.OnPressed += _ => SendMessage(new CMUAdminSetHiveMsg(hive.Id));
            row.AddChild(assign);
            CMUAdminActionsTheme.Apply(row, hive.Corrupted ? CMUAdminActionsTheme.Amber : CMUAdminActionsTheme.Violet);
            _cmuWindow.Hives.AddChild(row);
        }
    }

    private static bool CMUMatches(string text, string search) =>
        text.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase);

    private sealed record CMUTransformChoice(string Id, string Name, int Category, int Tier, string Description);
}
