// ReSharper disable CheckNamespace

using System.Linq;
using Content.Shared.CMU14.Religion;

namespace Content.Client.Lobby.UI;

// CMU14: the character's religion, in the background section.
public sealed partial class HumanoidProfileEditor
{
    private readonly List<CMUReligionPrototype> _religions = new();

    private void InitializeReligion()
    {
        ReligionButton.Clear();
        _religions.Clear();
        _religions.AddRange(_prototypeManager.EnumeratePrototypes<CMUReligionPrototype>()
            .OrderBy(religion => religion.Order)
            .ThenBy(religion => Loc.GetString(religion.Name)));

        for (var i = 0; i < _religions.Count; i++)
        {
            ReligionButton.AddItem(Loc.GetString(_religions[i].Name), i);
        }

        ReligionButton.OnItemSelected += args =>
        {
            ReligionButton.SelectId(args.Id);
            Profile = Profile?.WithReligion(_religions[args.Id].ID);
            SetDirty();
        };

        UpdateReligionControls();
    }

    private void UpdateReligionControls()
    {
        var selected = Profile?.Religion ?? CMUReligionPrototype.Atheist;
        var index = _religions.FindIndex(religion => religion.ID == selected.Id);
        if (index >= 0)
            ReligionButton.SelectId(index);
    }
}
