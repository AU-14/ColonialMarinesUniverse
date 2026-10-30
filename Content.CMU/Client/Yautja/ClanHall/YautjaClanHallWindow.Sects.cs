using System.Linq;
using Content.Shared.CMU14.Yautja;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.CMU14.Yautja.ClanHall;

// sects: subdivisions of a clan, households or a group with its own purpose. the leader makes them
// and puts members in them, everyone who can see the clan page sees who's in which
public sealed partial class YautjaClanHallWindow
{
    private const int NewSect = -1;
    private const int NoSect = -1;

    private LineEdit _sectName = default!;
    private LineEdit _sectDescription = default!;
    private int? _editingSect;

    private void BuildSectEditors()
    {
        _sectName = Hud.LineEdit(Loc.GetString("cmu-yautja-hall-sect-name"));
        _sectName.HorizontalExpand = true;
        _sectDescription = Hud.LineEdit(Loc.GetString("cmu-yautja-hall-sect-purpose"));
        _sectDescription.HorizontalExpand = true;
    }

    private IEnumerable<Control> SectEditors()
    {
        yield return _sectName;
        yield return _sectDescription;
    }

    private Control? SectsBlock(YautjaClanHallClanPage clan)
    {
        if (clan.Sects.Count == 0 && !clan.CanManageSects)
            return null;

        var frame = Frame("cmu-yautja-hall-sects");
        if (clan.CanManageSects && _editingSect == null && clan.Sects.Count < YautjaClanHallPolicy.MaxSects)
        {
            frame.Header.AddChild(Gap());
            frame.Header.AddChild(Button("cmu-yautja-hall-sect-new", () => StartSectEdit(null)));
        }

        if (_editingSect == NewSect)
            frame.Body.AddChild(SectEditor(clan, null));

        foreach (var sect in clan.Sects)
        {
            if (_editingSect == sect.Id)
            {
                frame.Body.AddChild(SectEditor(clan, sect.Id));
                continue;
            }

            var entry = VBox(2);
            entry.Margin = new Thickness(0, 2, 0, 4);
            var title = HBox(6);
            var name = Hud.Write(sect.Name, _bodyBold, Hud.Text, clip: true);
            name.HorizontalExpand = true;
            name.VerticalAlignment = VAlignment.Center;
            title.AddChild(name);
            var count = clan.Members.Count(member => member.SectId == sect.Id);
            title.AddChild(new HudTag(Loc.GetString("cmu-yautja-hall-members-count", ("members", count)), _small) { Fill = Hud.Label, Outline = true });
            if (clan.CanManageSects && _editingSect == null)
            {
                var id = sect.Id;
                title.AddChild(Button("cmu-yautja-hall-edit", () => StartSectEdit(sect)));
                title.AddChild(Button("cmu-yautja-hall-sect-delete", () => Request(new YautjaClanHallDeleteSectMessage(clan.Id, id))));
            }

            entry.AddChild(title);
            if (!string.IsNullOrWhiteSpace(sect.Description))
                entry.AddChild(Hud.Paragraph(sect.Description, Hud.Muted));
            frame.Body.AddChild(entry);
        }

        if (clan.Sects.Count == 0 && _editingSect == null)
            frame.Body.AddChild(Empty("cmu-yautja-hall-sects-empty"));

        return frame;
    }

    private Control SectEditor(YautjaClanHallClanPage clan, int? sectId)
    {
        var editor = VBox(4);
        foreach (var field in SectEditors())
        {
            var slot = new PanelContainer { PanelOverride = Hud.Field() };
            slot.AddChild(field);
            editor.AddChild(slot);
        }

        var buttons = HBox(4);
        buttons.AddChild(Gap());
        buttons.AddChild(Button("cmu-yautja-hall-cancel", StopSectEdit));
        var clanId = clan.Id;
        buttons.AddChild(Button("cmu-yautja-hall-save", () =>
        {
            if (string.IsNullOrWhiteSpace(_sectName.Text))
                return;

            Request(new YautjaClanHallEditSectMessage(clanId, sectId, _sectName.Text, _sectDescription.Text));
            StopSectEdit();
        }));
        editor.AddChild(buttons);
        return editor;
    }

    private void StartSectEdit(YautjaClanHallSectRow? sect)
    {
        _editingSect = sect?.Id ?? NewSect;
        _sectName.Text = sect?.Name ?? string.Empty;
        _sectDescription.Text = sect?.Description ?? string.Empty;
        if (_state != null)
            UpdateClanPage(_state);
    }

    private void StopSectEdit()
    {
        _editingSect = null;
        if (_state != null)
            UpdateClanPage(_state);
    }

    // members board row: the hunter, plus their sect. the leader gets a picker instead of the tag
    private Control MemberRow(YautjaClanHallClanPage clan, YautjaClanHallHunterRow member)
    {
        var sect = clan.Sects.FirstOrDefault(entry => entry.Id == member.SectId);
        if (!clan.CanManageSects || clan.Sects.Count == 0)
            return HunterRow(member, sectTag: sect?.Name);

        var row = HBox(4);
        var line = HunterRow(member);
        line.HorizontalExpand = true;
        row.AddChild(line);

        var picker = new OptionButton { StyleBoxOverride = Hud.Field(), MinWidth = 140 };
        picker.AddItem(Loc.GetString("cmu-yautja-hall-sect-none"), 0);
        var ids = new List<int> { NoSect };
        foreach (var entry in clan.Sects)
        {
            picker.AddItem(entry.Name, ids.Count);
            ids.Add(entry.Id);
        }

        picker.SelectId(Math.Max(0, ids.IndexOf(member.SectId ?? NoSect)));
        var clanId = clan.Id;
        var target = member.PlayerId;
        picker.OnItemSelected += args =>
        {
            picker.SelectId(args.Id);
            var chosen = ids[args.Id];
            Request(new YautjaClanHallSetSectMessage(clanId, target, chosen == NoSect ? null : chosen));
        };
        row.AddChild(picker);
        return row;
    }
}
