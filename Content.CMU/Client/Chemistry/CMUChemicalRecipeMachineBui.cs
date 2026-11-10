using Content.Shared.CMU14.Chemistry;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.CMU14.Chemistry;

public sealed class CMUChemicalRecipeMachineBui(EntityUid owner, Enum key) : BoundUserInterface(owner, key)
{
    private DefaultWindow? _window;
    private BoxContainer _body = default!;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<DefaultWindow>();
        _window.Title = Loc.GetString("cmu-chem-recipe-machine-title");
        _window.MinWidth = 440;
        _body = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        var scroll = new ScrollContainer { HScrollEnabled = false, MaxHeight = 600 };
        scroll.AddChild(_body);
        _window.Contents.AddChild(scroll);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (_window == null || state is not CMUChemicalRecipeMachineUiState recipe)
            return;

        _body.RemoveAllChildren();
        _body.AddChild(new Label { Text = recipe.Status });
        if (recipe.RecipeName != null)
            _body.AddChild(new Label { Text = recipe.RecipeName, Margin = new Thickness(0, 8, 0, 0) });

        if (recipe.Inputs.Count > 0)
        {
            _body.AddChild(new Label
            {
                Text = Loc.GetString("cmu-chem-recipe-machine-required-inputs"),
                Margin = new Thickness(0, 8, 0, 0),
            });
            foreach (var input in recipe.Inputs)
                _body.AddChild(new Label { Text = input });
        }

        if (recipe.Outputs.Count > 0)
        {
            _body.AddChild(new Label
            {
                Text = Loc.GetString("cmu-chem-recipe-machine-products"),
                Margin = new Thickness(0, 8, 0, 0),
            });
            foreach (var output in recipe.Outputs)
                _body.AddChild(new Label { Text = output });
        }

        var actions = new BoxContainer();
        var load = new Button
        {
            Text = Loc.GetString("cmu-chem-recipe-machine-load"),
            Disabled = !recipe.CanLoad,
        };
        load.OnPressed += _ => SendMessage(new CMUChemicalRecipeMachineLoadMessage());
        actions.AddChild(load);

        var synthesize = new Button
        {
            Text = Loc.GetString("cmu-chem-recipe-machine-synthesize"),
            Disabled = !recipe.CanSynthesize,
        };
        synthesize.OnPressed += _ => SendMessage(new CMUChemicalRecipeMachineSynthesizeMessage());
        actions.AddChild(synthesize);
        _body.AddChild(actions);

        var eject = new BoxContainer();
        var ejectRecipe = new Button
        {
            Text = Loc.GetString("cmu-chem-recipe-machine-eject-paper"),
            Disabled = !recipe.HasRecipe,
        };
        ejectRecipe.OnPressed += _ => SendMessage(new CMUChemicalRecipeMachineEjectMessage(true));
        eject.AddChild(ejectRecipe);

        var ejectVessel = new Button
        {
            Text = Loc.GetString("cmu-chem-recipe-machine-eject-vessel"),
            Disabled = !recipe.HasVessel,
        };
        ejectVessel.OnPressed += _ => SendMessage(new CMUChemicalRecipeMachineEjectMessage(false));
        eject.AddChild(ejectVessel);
        _body.AddChild(eject);
    }
}
