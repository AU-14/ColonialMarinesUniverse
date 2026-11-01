using Content.Server._RMC14.Language.Systems;
using Content.Shared._RMC14.Language.Components;
using Content.Shared.CMU14.DroneOperator;

namespace Content.Server.CMU14.DroneOperator;

public sealed partial class CMUDroneOperatorSystem
{
    [Dependency] private LanguageSystem _language = default!;

    // the drone is just a mouthpiece, so it speaks whatever its operator speaks while someone's on the stick
    private void LendOperatorLanguages(Entity<CMUDroneControlSessionComponent> drone)
    {
        if (!TryComp<LanguageComponent>(drone.Comp.Operator, out var operatorLanguages))
            return;

        drone.Comp.AddedLanguageComponent = !HasComp<LanguageComponent>(drone.Owner);
        var droneLanguages = EnsureComp<LanguageComponent>(drone.Owner);
        drone.Comp.DroneLanguageBefore = droneLanguages.CurrentLanguage;
        drone.Comp.LentSpoken.Clear();
        drone.Comp.LentUnderstood.Clear();

        foreach (var spoken in operatorLanguages.SpokenLanguages)
        {
            if (!_language.CanSpeak((drone.Owner, droneLanguages), spoken))
                drone.Comp.LentSpoken.Add(spoken);
        }

        foreach (var understood in operatorLanguages.UnderstoodLanguages)
        {
            if (!_language.CanUnderstand((drone.Owner, droneLanguages), understood))
                drone.Comp.LentUnderstood.Add(understood);
        }

        foreach (var spoken in drone.Comp.LentSpoken)
            _language.AddLanguage(drone.Owner, spoken, addSpoken: true, addUnderstood: false);

        foreach (var understood in drone.Comp.LentUnderstood)
            _language.AddLanguage(drone.Owner, understood, addSpoken: false, addUnderstood: true);

        if (operatorLanguages.CurrentLanguage is { } current)
            _language.SetLanguage(drone.Owner, current);
    }

    private void ReturnOperatorLanguages(Entity<CMUDroneControlSessionComponent> drone)
    {
        if (drone.Comp.AddedLanguageComponent)
        {
            RemComp<LanguageComponent>(drone.Owner);
        }
        else
        {
            foreach (var spoken in drone.Comp.LentSpoken)
                _language.RemoveLanguage(drone.Owner, spoken, removeSpoken: true, removeUnderstood: false);

            foreach (var understood in drone.Comp.LentUnderstood)
                _language.RemoveLanguage(drone.Owner, understood, removeSpoken: false, removeUnderstood: true);

            if (drone.Comp.DroneLanguageBefore is { } before)
                _language.SetLanguage(drone.Owner, before);
        }

        drone.Comp.AddedLanguageComponent = false;
        drone.Comp.LentSpoken.Clear();
        drone.Comp.LentUnderstood.Clear();
        drone.Comp.DroneLanguageBefore = null;
    }
}
