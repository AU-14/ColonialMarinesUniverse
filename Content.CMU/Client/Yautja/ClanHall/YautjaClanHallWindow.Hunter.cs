using System.Linq;
using Content.Shared.CMU14.Yautja;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.CMU14.Yautja.ClanHall;

// one hunter's record, plus every lever the viewer is allowed to pull on them
public sealed partial class YautjaClanHallWindow
{
    private BoxContainer _hunterContent = default!;
    private readonly List<int> _moveIds = new();

    private Control BuildHunterPage()
    {
        var page = YautjaClanHallStyle.VBox(10);
        page.HorizontalExpand = page.VerticalExpand = true;
        var top = YautjaClanHallStyle.HBox(6);
        var back = new HallButton("◀ " + Loc.GetString("cmu-yautja-hall-back"), _small, YautjaClanHallStyle.HotRed, 8, 2);
        back.OnPressed += _ => ShowPage(_backPage);
        top.AddChild(back);
        page.AddChild(top);
        _hunterContent = YautjaClanHallStyle.VBox(10);
        page.AddChild(Scroll(_hunterContent));
        return page;
    }

    private void UpdateHunterPage(YautjaClanHallEuiState state)
    {
        _hunterContent.RemoveAllChildren();
        if (state.Hunter is not { } hunter)
        {
            _hunterContent.AddChild(Empty(Loc.GetString("cmu-yautja-hall-hunter-hidden")));
            return;
        }

        // record card
        var card = YautjaClanHallStyle.Panel(YautjaClanHallStyle.Card, YautjaClanHallStyle.Border, 14, 10);
        var row = YautjaClanHallStyle.HBox(16);
        row.AddChild(YautjaClanHallStyle.Icon(YautjaClanHallStyle.RankIcon(hunter.Rank), 72));
        var who = YautjaClanHallStyle.VBox(3);
        who.HorizontalExpand = true;
        var nameRow = YautjaClanHallStyle.HBox(8);
        nameRow.AddChild(YautjaClanHallStyle.Label(hunter.Name.ToUpperInvariant(), _big, YautjaClanHallStyle.Text));
        nameRow.AddChild(new Pip(hunter.Online) { VerticalAlignment = VAlignment.Center });
        who.AddChild(nameRow);
        who.AddChild(YautjaClanHallStyle.Label(YautjaClanHallStyle.RankName(hunter.Rank).ToUpperInvariant(), _heading, YautjaClanHallStyle.HotRed));
        if (hunter.ClanId is { } clanId && state.Clans.FirstOrDefault(c => c.Id == clanId) is { } clan)
        {
            var clanButton = new HallButton(clan.Name, _bodyBold, YautjaClanHallStyle.ClanColor(clan.Color), 8, 2)
            {
                HorizontalAlignment = HAlignment.Left,
                Disabled = !clan.CanView,
            };
            clanButton.Label.FontColorOverride = YautjaClanHallStyle.Readable(YautjaClanHallStyle.ClanColor(clan.Color));
            clanButton.OnPressed += _ => OpenClan(clanId);
            who.AddChild(clanButton);
        }
        else
        {
            who.AddChild(YautjaClanHallStyle.Label(Loc.GetString("cmu-yautja-hall-clanless"), _body, YautjaClanHallStyle.Dim));
        }

        row.AddChild(who);
        var numbers = YautjaClanHallStyle.VBox(2);
        numbers.AddChild(Readout(Loc.GetString("cmu-yautja-hall-honor"), YautjaClanHallStyle.Number(hunter.Honor), YautjaClanHallStyle.Honor));
        numbers.AddChild(Readout(Loc.GetString("cmu-yautja-hall-standing"),
            hunter.Standing > 0 ? Loc.GetString("cmu-yautja-hall-standing-value", ("standing", hunter.Standing), ("count", state.Hunters.Count)) : "—",
            YautjaClanHallStyle.Text));
        row.AddChild(numbers);
        card.AddChild(row);
        _hunterContent.AddChild(card);

        var anyAction = false;

        if (hunter.AssignableRanks.Count > 0)
        {
            anyAction = true;
            _hunterContent.AddChild(SectionTitle(Loc.GetString("cmu-yautja-hall-set-rank")));
            var ranks = YautjaClanHallStyle.HBox(4);
            foreach (var rank in YautjaClanPolicy.GetNormalAssignableRanks())
            {
                var button = new HallButton(YautjaClanHallStyle.RankName(rank).ToUpperInvariant(), _small, YautjaClanHallStyle.HotRed, 8, 3)
                {
                    Selected = rank == hunter.Rank,
                    Disabled = rank != hunter.Rank && !hunter.AssignableRanks.Contains(rank),
                };
                var target = hunter.PlayerId;
                var picked = rank;
                button.OnPressed += _ =>
                {
                    if (picked != hunter.Rank)
                        Request(new YautjaClanHallSetRankMessage(target, picked));
                };
                ranks.AddChild(button);
            }

            _hunterContent.AddChild(ranks);
        }

        var standing = YautjaClanHallStyle.HBox(6);
        if (hunter.CanExile)
        {
            var exile = new ConfirmButton(Loc.GetString("cmu-yautja-hall-exile"), Loc.GetString("cmu-yautja-hall-exile-confirm"), _readout);
            var target = hunter.PlayerId;
            exile.Confirmed += () => Request(new YautjaClanHallExileMessage(target));
            standing.AddChild(exile);
        }

        if (hunter.CanSetAncient)
        {
            var enable = !hunter.IsAncient;
            var ancient = new ConfirmButton(
                Loc.GetString(enable ? "cmu-yautja-hall-make-ancient" : "cmu-yautja-hall-strip-ancient"),
                Loc.GetString("cmu-yautja-hall-are-you-sure"),
                _readout) { Accent = YautjaClanHallStyle.Staff };
            var target = hunter.PlayerId;
            ancient.Confirmed += () => Request(new YautjaClanHallSetAncientMessage(target, enable));
            standing.AddChild(ancient);
        }

        if (hunter.CanPurge)
        {
            var purge = new ConfirmButton(Loc.GetString("cmu-yautja-hall-purge"), Loc.GetString("cmu-yautja-hall-purge-confirm"), _readout);
            var target = hunter.PlayerId;
            purge.Confirmed += () => Request(new YautjaClanHallPurgeMessage(target));
            standing.AddChild(purge);
        }

        if (standing.ChildCount > 0)
        {
            anyAction = true;
            _hunterContent.AddChild(SectionTitle(Loc.GetString("cmu-yautja-hall-standing-actions")));
            _hunterContent.AddChild(standing);
        }

        if (hunter.CanMove)
        {
            anyAction = true;
            _hunterContent.AddChild(SectionTitle(Loc.GetString("cmu-yautja-hall-move")));
            var moveRow = YautjaClanHallStyle.HBox(6);
            var picker = new OptionButton { MinWidth = 220 };
            _moveIds.Clear();
            picker.AddItem(Loc.GetString("cmu-yautja-hall-clanless"), 0);
            _moveIds.Add(-1);
            foreach (var other in state.Clans.Where(c => c.Id != hunter.ClanId))
            {
                picker.AddItem(other.Name, _moveIds.Count);
                _moveIds.Add(other.Id);
            }

            picker.OnItemSelected += args => picker.SelectId(args.Id);
            moveRow.AddChild(picker);
            var move = new HallButton(Loc.GetString("cmu-yautja-hall-move-button"), _readout, YautjaClanHallStyle.HotRed);
            var target = hunter.PlayerId;
            move.OnPressed += _ =>
            {
                var id = _moveIds[Math.Clamp(picker.SelectedId, 0, _moveIds.Count - 1)];
                Request(new YautjaClanHallMoveMessage(target, id < 0 ? null : id));
            };
            moveRow.AddChild(move);
            _hunterContent.AddChild(moveRow);
        }

        if (hunter.Whitelist is { } flags)
        {
            anyAction = true;
            _hunterContent.AddChild(SectionTitle(Loc.GetString("cmu-yautja-hall-whitelist"), YautjaClanHallStyle.Staff));
            _hunterContent.AddChild(WhitelistToggles(hunter.PlayerId.ToString(), flags));
        }

        if (!anyAction)
        {
            _hunterContent.AddChild(Empty(Loc.GetString(hunter.IsSelf
                ? "cmu-yautja-hall-self"
                : "cmu-yautja-hall-no-authority")));
        }
    }

    private Control Readout(string title, string value, Color color)
    {
        var box = YautjaClanHallStyle.VBox(0);
        var t = YautjaClanHallStyle.Label(title.ToUpperInvariant(), _small, YautjaClanHallStyle.Dim);
        t.HorizontalAlignment = HAlignment.Right;
        var v = YautjaClanHallStyle.Label(value, _title, color);
        v.HorizontalAlignment = HAlignment.Right;
        box.AddChild(t);
        box.AddChild(v);
        return box;
    }
}
