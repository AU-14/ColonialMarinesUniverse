using System.Linq;
using Content.Client._RMC14.Xenonids.UI;
using Content.Client.Message;
using Content.Shared._RMC14.Xenonids.Evolution;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.Strain;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Systems;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Client._RMC14.Xenonids.Evolution;

[UsedImplicitly]
public sealed partial class XenoEvolutionBui : BoundUserInterface
{
    [Dependency] private IComponentFactory _compFactory = default!;
    [Dependency] private IPrototypeManager _prototype = default!;

    private readonly SpriteSystem _sprite;
    private readonly MobStateSystem _mobState;

    [ViewVariables]
    private XenoEvolutionWindow? _window;

    private readonly Dictionary<EntProtoId, XenoChoiceControl> _evolutionControls = new();
    private readonly Dictionary<EntProtoId, XenoChoiceControl> _strainControls = new();

    public XenoEvolutionBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        _sprite = EntMan.System<SpriteSystem>();
        _mobState = EntMan.System<MobStateSystem>();
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<XenoEvolutionWindow>();
        _window.OvipositorNeededLabel.Visible = false;
        _window.OvermindNeededLabel.Visible = false;

        if (EntMan.TryGetComponent(Owner, out XenoEvolutionComponent? xeno))
        {
            foreach (var strain in xeno.Strains)
            {
                AddStrain(strain);
            }
        }

        _window.StrainsLabel.Visible = _window.StrainsContainer.ChildCount > 0;
        Refresh();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        Refresh();
    }

    private void AddEvolution(EntProtoId evolutionId)
    {
        if (!_prototype.TryIndex(evolutionId, out var evolution))
            return;

        if (!_evolutionControls.TryGetValue(evolutionId, out var control))
        {
            control = new XenoChoiceControl();
            control.Set(evolution.Name, _sprite.Frame0(evolution));
            control.Button.Disabled = false;

            control.Button.OnPressed += _ =>
            {
                SendPredictedMessage(new XenoEvolveBuiMsg(evolutionId));
                Close();
            };

            _evolutionControls[evolutionId] = control;
            _window?.EvolutionsContainer.AddChild(control);
        }

        control.Visible = true;
        control.Button.Disabled = false;
    }

    private void AddStrain(EntProtoId strainId)
    {
        if (_window is not { IsOpen: true })
            return;

        if (!_prototype.TryIndex(strainId, out var strain))
            return;

        if (!_strainControls.TryGetValue(strainId, out var control))
        {
            control = new XenoChoiceControl();

            var name = strain.Name;
            string? description = null;

            if (strain.TryComp(out XenoStrainComponent? strainComp, _compFactory))
            {
                name = $"{Loc.GetString(strainComp.Name)} {name}";
                description = strainComp.Description;
            }

            control.Set(name, _sprite.Frame0(strain));
            control.Button.Disabled = false;

            control.Button.OnPressed += _ =>
            {
                var confirmWindow = new XenoStrainConfirmWindow();
                confirmWindow.SetInfo(name, _sprite.Frame0(strain), description);

                confirmWindow.OnConfirm += () =>
                {
                    SendPredictedMessage(new XenoStrainBuiMsg(strainId));
                    confirmWindow.Close();
                    Close();
                };

                confirmWindow.OpenCentered();
            };

            _strainControls[strainId] = control;
            _window.StrainsContainer.AddChild(control);
        }

        control.Visible = true;
        control.Button.Disabled = false;
    }

    public void Refresh()
    {
        if (_window == null)
            return;

        if (!EntMan.TryGetComponent(Owner, out XenoEvolutionComponent? xeno))
            return;

        _window.PointsLabel.Visible = xeno.Max > FixedPoint2.Zero;
        _window.PointsLabel.Text = Loc.GetString("rmc-xeno-ui-evolution-points",
            ("points", (int)Math.Floor(((FixedPoint2)xeno.Points).Double())),
            ("maxPoints", xeno.Max));

        foreach (var control in _evolutionControls.Values)
            control.Visible = false;

        var hasQueenAlive = HiveHasLivingQueen();
        foreach (var evolutionId in xeno.EvolvesToWithoutPoints)
        {
            if (hasQueenAlive &&
                _prototype.TryIndex(evolutionId, out var proto) &&
                proto.TryComp(out XenoEvolutionGranterComponent? _, _compFactory))
            {
                continue;
            }

            AddEvolution(evolutionId);
        }

        if (xeno.Points >= xeno.Max)
        {
            foreach (var evolutionId in xeno.EvolvesTo)
                AddEvolution(evolutionId);

            if (!xeno.MarinesLanded)
            {
                foreach (var evolutionId in xeno.EarlyEvolvesTo)
                    AddEvolution(evolutionId);
            }
        }

        _window.Separator.Visible = _window.EvolutionsContainer.Children.Any(child => child.Visible) &&
                                    _window.StrainsContainer.Children.Any(child => child.Visible);

        var lackingOvipositor = State is XenoEvolveBuiState { LackingOvipositor: true };

        // Determine which "lacking" label to show
        bool showOviLabel = false;
        bool showOvermindLabel = false;

        if (lackingOvipositor)
        {
            if (IsPathogenHive())
                showOvermindLabel = true;
            else
                showOviLabel = true;
        }

        // OvipositorNeededLabel
        if (showOviLabel)
        {
            if (!_window.OvipositorNeededLabel.Visible)
            {
                _window.OvipositorNeededLabel.SetMarkupPermissive(Loc.GetString("rmc-xeno-ui-ovi-needed-label"));
                _window.OvipositorNeededLabel.Visible = true;
            }
        }
        else
        {
            _window.OvipositorNeededLabel.Visible = false;
        }

        // OvermindNeededLabel - add this RichTextLabel to XenoEvolutionWindow.xaml alongside OvipositorNeededLabel
        if (showOvermindLabel)
        {
            if (!_window.OvermindNeededLabel.Visible)
            {
                _window.OvermindNeededLabel.SetMarkupPermissive(Loc.GetString("cmu-pathogen-ui-overmind-needed-label"));
                _window.OvermindNeededLabel.Visible = true;
            }
        }
        else
        {
            _window.OvermindNeededLabel.Visible = false;
        }
    }

    private bool HiveHasLivingQueen()
    {
        if (!EntMan.TryGetComponent(Owner, out HiveMemberComponent? member) ||
            member.Hive is not { } hive ||
            !EntMan.TryGetComponent(hive, out HiveComponent? hiveComp) ||
            hiveComp.CurrentQueen is not { } queen)
        {
            return false;
        }

        return !_mobState.IsDead(queen);
    }

    private bool IsPathogenHive()
    {
        if (!EntMan.TryGetComponent(Owner, out HiveMemberComponent? member) ||
            member.Hive is not { } hive ||
            !EntMan.TryGetComponent(hive, out HiveComponent? hiveComp))
        {
            return false;
        }

        // Check hive prototype ID
        return EntMan.GetComponent<MetaDataComponent>(hive).EntityPrototype?.ID == "CMUPathogenHive";
    }

    private bool PathogenHiveHasLivingOvermind()
    {
        if (!EntMan.TryGetComponent(Owner, out HiveMemberComponent? member) ||
            member.Hive is not { } hive)
        {
            return false;
        }

        var query = EntMan.AllEntityQueryEnumerator<HiveMemberComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out var hiveMember, out var meta))
        {
            if (hiveMember.Hive != hive)
                continue;

            if (meta.EntityPrototype?.ID == "CMU14XenoOvermind" && !_mobState.IsDead(uid))
                return true;
        }

        return false;
    }
}
