using System.Linq;
using Content.Shared.CMU14.Yautja;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.CMU14.Yautja.ClanHall;

// hunter tab: one hunter's record, and only the actions you're allowed to take on them
public sealed partial class YautjaClanHallWindow
{
    private BoxContainer _hunterPage = default!;

    private Control BuildHunterPage()
    {
        _hunterPage = VBox(12);
        return _hunterPage;
    }

    private void UpdateHunterPage(YautjaClanHallEuiState state)
    {
        _hunterPage.RemoveAllChildren();
        if (state.Hunter is not { } hunter)
        {
            var frame = Frame(null);
            frame.Body.AddChild(Empty("cmu-yautja-hall-hunter-hidden"));
            _hunterPage.AddChild(frame);
            return;
        }

        // no individual ranking, so the ring isn't measured against anyone else
        var header = HBox(18);
        var gauge = new HudGauge(170, _number, _small);
        gauge.Set(hunter.Honor > 0 ? 1f : 0f, hunter.Honor.ToString(),
            Loc.GetString("cmu-yautja-hall-honor").ToUpperInvariant(),
            string.Empty);
        header.AddChild(gauge);

        var id = VBox(8);
        id.HorizontalExpand = true;
        id.VerticalAlignment = VAlignment.Center;
        var nameRow = HBox(10);
        nameRow.AddChild(RankIcon(hunter.Rank, 40));
        var name = Hud.Write(hunter.Name.ToUpperInvariant(), _big, Hud.Bright, clip: true);
        name.HorizontalExpand = true;
        name.VerticalAlignment = VAlignment.Center;
        nameRow.AddChild(name);
        id.AddChild(nameRow);

        var tags = HBox(6);
        tags.AddChild(new HudTag(RankName(hunter.Rank), _small));
        tags.AddChild(new HudTag(Loc.GetString(hunter.Online ? "cmu-yautja-hall-online" : "cmu-yautja-hall-offline"), _small)
        {
            Fill = hunter.Online ? Hud.Hot : Hud.Muted,
            Outline = !hunter.Online,
        });
        id.AddChild(tags);

        var clanRow = HBox(6);
        clanRow.AddChild(Caption(Loc.GetString("cmu-yautja-hall-clan")));
        if (hunter.ClanId is { } clanId && state.Clans.FirstOrDefault(c => c.Id == clanId) is { } clan)
        {
            var open = Button("cmu-yautja-hall-open", () => OpenClan(clanId));
            clanRow.AddChild(Hud.Write(clan.Name, _bodyBold, Hud.Text));
            open.Disabled = !clan.CanView;
            clanRow.AddChild(open);
        }
        else
        {
            clanRow.AddChild(Hud.Write(Loc.GetString("cmu-yautja-hall-clanless"), _body, Hud.Muted));
        }

        id.AddChild(clanRow);
        header.AddChild(id);
        _hunterPage.AddChild(header);

        var target = hunter.PlayerId;
        var actions = Frame("cmu-yautja-hall-actions");
        var any = false;

        if (hunter.AssignableRanks.Count > 0)
        {
            any = true;
            var row = HBox(6);
            row.AddChild(Caption(Loc.GetString("cmu-yautja-hall-rank")));
            foreach (var rank in YautjaClanPolicy.GetNormalAssignableRanks())
            {
                if (rank != hunter.Rank && !hunter.AssignableRanks.Contains(rank))
                    continue;

                var picked = rank;
                var button = new HudButton(RankName(rank), _small) { Selected = rank == hunter.Rank };
                button.Activated += () =>
                {
                    if (picked != hunter.Rank)
                        Request(new YautjaClanHallSetRankMessage(target, picked));
                };
                row.AddChild(button);
            }

            actions.Body.AddChild(row);
        }

        if (hunter.CanMove)
        {
            any = true;
            var row = HBox(6);
            row.AddChild(Caption(Loc.GetString("cmu-yautja-hall-move")));
            var clans = new OptionButton { MinWidth = 200, StyleBoxOverride = Hud.Field() };
            var ids = new List<int?> { null };
            clans.AddItem(Loc.GetString("cmu-yautja-hall-clanless"), 0);
            foreach (var other in state.Clans.Where(c => c.Id != hunter.ClanId))
            {
                clans.AddItem(other.Name, ids.Count);
                ids.Add(other.Id);
            }

            clans.OnItemSelected += args => clans.SelectId(args.Id);
            row.AddChild(clans);
            row.AddChild(Button("cmu-yautja-hall-move-button", () =>
                Request(new YautjaClanHallMoveMessage(target, ids[Math.Clamp(clans.SelectedId, 0, ids.Count - 1)]))));
            actions.Body.AddChild(row);
        }

        var standing = HBox(6);
        if (hunter.CanExile)
            standing.AddChild(Button("cmu-yautja-hall-exile", () => Request(new YautjaClanHallExileMessage(target)), confirm: true));
        if (hunter.CanSetAncient)
        {
            var enable = !hunter.IsAncient;
            standing.AddChild(Button(enable ? "cmu-yautja-hall-make-ancient" : "cmu-yautja-hall-remove-ancient",
                () => Request(new YautjaClanHallSetAncientMessage(target, enable)), confirm: true));
        }

        if (hunter.CanPurge)
            standing.AddChild(Button("cmu-yautja-hall-purge", () => Request(new YautjaClanHallPurgeMessage(target)), confirm: true));

        if (standing.ChildCount > 0)
        {
            any = true;
            actions.Body.AddChild(standing);
        }

        if (!any)
            actions.Body.AddChild(Empty(hunter.IsSelf ? "cmu-yautja-hall-self" : "cmu-yautja-hall-no-actions"));

        _hunterPage.AddChild(actions);

        if (hunter.Whitelist is { } flags)
        {
            var whitelist = Frame("cmu-yautja-hall-whitelist");
            whitelist.Body.AddChild(WhitelistToggles(hunter.PlayerId.ToString(), flags));
            _hunterPage.AddChild(whitelist);
        }
    }
}
