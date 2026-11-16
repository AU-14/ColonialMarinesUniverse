using System.Linq;
using Content.Shared._RMC14.Overwatch;
using Content.Shared.CMU14.ShipWeapons;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;

namespace Content.Client.CMU14.ShipWeapons;

public sealed class CMUShipWeaponPicker : BoxContainer
{
    public event Action<ProtoId<CMUShipWeaponPrototype>?, CMUShipWeaponBearing>? OnSelected;
    private readonly OptionButton _weapon = new();
    private readonly OptionButton _bearing = new();
    private readonly Label _bearingLabel = new();
    private readonly Label _remaining = new();
    private readonly List<ProtoId<CMUShipWeaponPrototype>?> _ids = new();

    public CMUShipWeaponPicker()
    {
        Orientation = LayoutOrientation.Vertical;
        AddChild(new Label { Text = Loc.GetString("cmu-ship-weapons-select") });
        AddChild(_weapon);
        AddChild(_remaining);
        _bearingLabel.Text = Loc.GetString("cmu-ship-weapons-bearing");
        AddChild(_bearingLabel);
        AddChild(_bearing);
        foreach (var bearing in Enum.GetValues<CMUShipWeaponBearing>())
            _bearing.AddItem(Loc.GetString($"cmu-ship-weapons-bearing-{bearing.ToString().ToLowerInvariant()}"), (int) bearing);
        _weapon.OnItemSelected += args =>
        {
            _weapon.SelectId(args.Id);
            Selected();
        };
        _bearing.OnItemSelected += args =>
        {
            _bearing.SelectId(args.Id);
            Selected();
        };
    }

    private void Selected()
    {
        if (_weapon.SelectedId < 0 || _weapon.SelectedId >= _ids.Count)
            return;

        OnSelected?.Invoke(_ids[_weapon.SelectedId], (CMUShipWeaponBearing) _bearing.SelectedId);
    }

    public void Update(OverwatchConsoleComponent console)
    {
        Visible = console.CMUShipWeapons.Length > 0;
        var prototypes = IoCManager.Resolve<IPrototypeManager>();

        // CMU14 BadBlood: when the server keeps the standard cannon picked (no armed missions, only rule extras like
        // the Vessel Strike), the picker lists it first and shows it selected, so the display matches what fires
        var standard = console.CMUSelectedShipWeapon == null ||
                       !console.CMUShipWeapons.Any(w => prototypes.Index(w.Weapon).Armable);
        var offset = standard ? 1 : 0;

        var rebuild = _ids.Count != console.CMUShipWeapons.Length + offset ||
                      standard && _ids[0] != null;
        for (var i = 0; !rebuild && i < console.CMUShipWeapons.Length; i++)
            rebuild = _ids[i + offset] != console.CMUShipWeapons[i].Weapon;
        if (rebuild)
        {
            _weapon.Clear();
            _ids.Clear();
            if (standard)
            {
                _weapon.AddItem(Loc.GetString("cmu-ship-weapons-standard-cannon"), _ids.Count);
                _ids.Add(null);
            }

            foreach (var status in console.CMUShipWeapons)
            {
                _weapon.AddItem(string.Empty, _ids.Count);
                _ids.Add(status.Weapon);
            }
        }
        var selectedId = 0;
        for (var i = 0; i < console.CMUShipWeapons.Length; i++)
        {
            var status = console.CMUShipWeapons[i];
            var id = i + offset;
            _weapon.SetItemText(id, Loc.GetString("cmu-ship-weapons-remaining",
                ("weapon", Loc.GetString(prototypes.Index(status.Weapon).Name)), ("uses", status.Remaining),
                ("max", status.Max)));
            if (status.Weapon == console.CMUSelectedShipWeapon)
                selectedId = id;
        }
        if (_ids.Count > 0)
            _weapon.SelectId(selectedId);
        _bearing.SelectId((int) console.CMUShipWeaponBearing);

        // Only walking barrages have a direction, so the picker only shows while one is selected.
        var walking = console.CMUSelectedShipWeapon is { } selected && prototypes.Index(selected).WalkingDistance > 0;
        _bearing.Visible = walking;
        _bearingLabel.Visible = walking;
        _remaining.Text = Loc.GetString("cmu-ship-weapons-armed-hint");
    }
}
