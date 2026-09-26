using System.Numerics;
using Robust.Client.Graphics;

namespace Content.Client.CMU14.Lobby;

public sealed partial class LobbyPartyShowControl
{
    private void AdvanceSupply(float previous)
    {
        if (Crossed(previous, 1))
            Sound("jet-idle.ogg", -29);
        if (Crossed(previous, 5) || Crossed(previous, 10) || Crossed(previous, 15))
            Sound("pressure-vent.ogg", -25);
        if (Crossed(previous, 19))
            Sound("countermeasures.ogg", -26);
        if (Crossed(previous, 4) || Crossed(previous, 18))
            _caption.Text = Loc.GetString(_elapsed < 18 ? "cmu-lobby-party-supply-warning" : "cmu-lobby-party-supply-finale");
    }

    private void DrawSupply(DrawingHandleScreen handle)
    {
        handle.DrawRect(new UIBox2(0, Size.Y * 0.22f, Size.X, Size.Y * 0.94f), Color.FromHex("#152329EB"));
        for (var lane = 0; lane < 3; lane++)
        {
            var y = Size.Y * (0.30f + lane * 0.25f);
            for (var x = 0f; x < Size.X; x += 90 * Unit)
                handle.DrawRect(new UIBox2(x, y + 30 * Unit, x + 35 * Unit, y + 33 * Unit), Color.FromHex("#C0AA67"));
            var cart = LobbyPartyChoreography.SupplyCart(_elapsed, lane, Size, _reduced);
            Ellipse(handle, cart + new Vector2(0, 23) * Unit, new Vector2(52, 10) * Unit, Color.Black.WithAlpha(0.4f));
            // Requisition trolleys have apparently had their brakes signed out by somebody else.
            handle.DrawRect(new UIBox2(cart + new Vector2(-44, -5) * Unit, cart + new Vector2(44, 15) * Unit), Color.FromHex("#778C87"));
            handle.DrawLine(cart + new Vector2(-43, 10) * Unit, cart + new Vector2(-52, -35) * Unit, Color.FromHex("#B9C9C6"));
            for (var wheel = -1; wheel <= 1; wheel += 2)
            {
                var at = cart + new Vector2(wheel * 30, 20) * Unit;
                handle.DrawCircle(at, 9 * Unit, Color.FromHex("#0B1319"));
                var angle = _reduced ? 0 : _elapsed * 10;
                handle.DrawLine(at, at + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 7 * Unit, Color.LightGray);
            }
            for (var box = 0; box < 3; box++)
            {
                var wobble = _reduced ? 0 : MathF.Sin(_elapsed * 7 + box + lane) * 0.18f;
                DrawSupplyCrate(handle, cart + new Vector2(-24 + box * 24, -18 - box % 2 * 24) * Unit, wobble);
            }
            // The official courier is a xeno chasing the trolley, with little regard for queue etiquette.
            var bounce = _reduced ? 0 : MathF.Abs(MathF.Sin(_elapsed * 10 + lane)) * 9;
            Sprite(handle, XenoArt, "alive", cart + new Vector2(lane == 1 ? 95 : -100, -bounce) * Unit,
                65 * Unit, direction: lane == 1 ? Robust.Shared.Graphics.RSI.RsiDirection.West : Robust.Shared.Graphics.RSI.RsiDirection.East);
        }
        for (var i = 0; i < _actors.Count; i++)
        {
            if (_actors[i].Card.Parent == null)
                continue;
            var pose = LobbyPartyChoreography.SupplyCrew(_elapsed, i, _actors.Count, Size, _reduced, _seed);
            Ellipse(handle, pose.Position + new Vector2(0, pose.Height + 18 * Unit),
                new Vector2(14, 5) * Unit, Color.Black.WithAlpha(0.3f));
        }
    }

    private void DrawSupplyCargo(DrawingHandleScreen handle)
    {
        if (_reduced || _actors.Count == 0)
            return;
        for (var crate = 0; crate < LobbyPartyChoreography.SupplyCrates; crate++)
        {
            var target = LobbyPartyChoreography.SupplyTarget(crate, _actors.Count, _seed);
            if (_actors[target].Card.Parent == null)
                continue;
            var impact = LobbyPartyChoreography.SupplyImpact(crate);
            var age = _elapsed - impact;
            if (age < -LobbyPartyChoreography.SupplyFlight || age > 2.2f)
                continue;
            var end = LobbyPartyChoreography.BattlePosition(target, _actors.Count, Size, _seed);
            if (age < 0)
            {
                var from = LobbyPartyChoreography.SupplyCart(impact - LobbyPartyChoreography.SupplyFlight, crate % 3, Size, false);
                var t = 1 + age / LobbyPartyChoreography.SupplyFlight;
                var at = Vector2.Lerp(from, end, t) - new Vector2(0, MathF.Sin(t * MathF.PI) * 100 * Unit);
                DrawSupplyCrate(handle, at, t * MathF.Tau);
            }
            else
            {
                var life = age / 2.2f;
                for (var snack = 0; snack < 8; snack++)
                {
                    var ray = new Vector2(MathF.Sin(snack * 2.4f), MathF.Cos(snack * 2.4f));
                    var at = end + ray * life * 80 * Unit + new Vector2(0, life * life * 70 * Unit);
                    handle.DrawCircle(at, 4 * Unit, (snack % 2 == 0 ? Color.Gold : Color.FromHex("#FF9EA7")).WithAlpha(1 - life));
                }
                DrawSupplyCrate(handle, end + new Vector2(age * 40, -MathF.Abs(MathF.Sin(age * 6)) * (1 - life) * 35) * Unit, age * 6);
            }
        }
        if (_elapsed > 18)
            DrawConfetti(handle);
    }

    private void DrawSupplyCrate(DrawingHandleScreen handle, Vector2 at, float angle)
    {
        var previous = handle.GetTransform();
        handle.SetTransform(Matrix3x2.CreateRotation(angle) * Matrix3x2.CreateTranslation(at) * previous);
        var half = new Vector2(14, 12) * Unit;
        handle.DrawRect(new UIBox2(-half, half), Color.FromHex("#9C815A"));
        handle.DrawRect(new UIBox2(-half, half), Color.FromHex("#E4CE9C"), false);
        handle.DrawLine(-half, half, Color.FromHex("#E4CE9C"));
        handle.DrawRect(new UIBox2(new Vector2(-4, -5) * Unit, new Vector2(4, 5) * Unit), Color.FromHex("#E8E4CC"));
        handle.SetTransform(previous);
    }
}
