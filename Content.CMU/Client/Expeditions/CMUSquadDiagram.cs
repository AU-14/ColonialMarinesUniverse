using System.Numerics;
using Content.Shared.CMU14.Expeditions;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;

namespace Content.Client.CMU14.Expeditions;

public sealed class CMUSquadDiagram : Control
{
    public CMUSquadDiagram() => RectClipContent = true;

    private List<CMUSquadMemberView> _members = new();
    private CMUSquadMemberView? _selected;
    public void SetMembers(List<CMUSquadMemberView> members, CMUSquadMemberView? selected)
    {
        _members = members;
        _selected = selected;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        handle.DrawRect(new UIBox2(Vector2.Zero, PixelSize), Color.FromHex("#111A22"));
        if (_selected is not { } selected)
            return;
        var center = PixelSize / 2;
        var scale = MathF.Min(PixelSize.X, PixelSize.Y) / 48;
        Vector2 Project(Vector2 point) => center + new Vector2(point.X - selected.Position.X, selected.Position.Y - point.Y) * scale;
        for (var offset = -24; offset <= 24; offset += 4)
        {
            handle.DrawLine(center + new Vector2(offset, -24) * scale, center + new Vector2(offset, 24) * scale, Color.FromHex("#243441"));
            handle.DrawLine(center + new Vector2(-24, offset) * scale, center + new Vector2(24, offset) * scale, Color.FromHex("#243441"));
        }
        var previous = selected.Position;
        foreach (var point in selected.Route)
        {
            handle.DrawLine(Project(previous), Project(point), Color.Cyan);
            previous = point;
        }
        if (selected.Target is { } target)
        {
            handle.DrawLine(center, Project(target), Color.Red);
            handle.DrawCircle(Project(target), 5, Color.Red);
        }
        if (selected.Destination is { } destination)
            handle.DrawCircle(Project(destination), 6, Color.Yellow, false);
        if (selected.Cover is { } cover)
            handle.DrawCircle(Project(cover), 7, Color.Green, false);
        foreach (var rejected in selected.RejectedCover)
            handle.DrawCircle(Project(rejected), 7, Color.Orange, false);
        foreach (var member in _members)
            if (member.Map == selected.Map)
            {
                var point = Project(member.Position);
                var color = !member.Active ? Color.Gray : member.Injured ? Color.Orange : Color.White;
                handle.DrawCircle(point, 4, color);
                if (member.Entity == selected.Entity)
                    handle.DrawCircle(point, 8, Color.Cyan, false);
                if (!member.Active)
                {
                    handle.DrawLine(point - new Vector2(5), point + new Vector2(5), color);
                    handle.DrawLine(point + new Vector2(-5, 5), point + new Vector2(5, -5), color);
                }
            }
    }
}
