using Content.Client._RMC14.Xenonids.UI;
using Content.Shared._RMC14.Xenonids.Evolution;
using Content.Shared._RMC14.Xenonids.Strain;

namespace Content.Client._RMC14.Xenonids.Evolution;

public sealed partial class XenoEvolutionBui
{
    private void AddDestrain()
    {
        if (_window == null ||
            !EntMan.TryGetComponent(Owner, out XenoStrainComponent? strain) ||
            strain.BaseCaste is not { } baseCaste ||
            !_prototype.TryIndex(baseCaste, out var caste))
        {
            return;
        }

        var name = Loc.GetString("cmu-xeno-destrain-choice", ("caste", caste.Name));
        var control = new XenoChoiceControl();
        control.Set(name, _sprite.Frame0(caste));
        control.Button.OnPressed += _ =>
        {
            var confirmWindow = new XenoStrainConfirmWindow();
            confirmWindow.SetInfo(name, _sprite.Frame0(caste), "cmu-xeno-destrain-description");
            confirmWindow.Title = Loc.GetString("cmu-xeno-destrain-title");
            confirmWindow.ConfirmButton.Text = Loc.GetString("cmu-xeno-destrain-confirm");
            confirmWindow.OnConfirm += () =>
            {
                SendPredictedMessage(new XenoStrainBuiMsg(baseCaste));
                confirmWindow.Close();
                Close();
            };
            confirmWindow.OpenCentered();
        };
        _window.StrainsContainer.AddChild(control);
    }
}
