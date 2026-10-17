using System.Numerics;
using Content.Shared._RMC14.Flag;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Map;

namespace Content.Client.CMU14.Lobby;

public sealed partial class LobbyPartyShowControl
{
    private readonly List<(EntityUid Entity, SpriteView View)> _flags = new();

    private void InitializeParade()
    {
        // Use the same entities and planted appearance as the flags players carry in game.
        // These three scenery previews are owned here, unlike the borrowed player portraits.
        foreach (var prototype in new[] { "AU14FlagCarriableUA", "AU14FlagCarriableUPP", "AU14FlagCarriableCLF" })
        {
            var entity = _entities.SpawnEntity(prototype, MapCoordinates.Nullspace);
            _entities.System<SharedAppearanceSystem>().SetData(entity, PlantableFlagVisuals.Planted, true);
            if (_reduced)
                _entities.System<SpriteSystem>().LayerSetAutoAnimated(entity, "base", false);
            var view = new SpriteView
            {
                Stretch = SpriteView.StretchMode.None,
                MouseFilter = MouseFilterMode.Ignore,
                RectClipContent = false,
                OverrideDirection = Direction.South,
            };
            view.SetEntity(entity);
            _flags.Add((entity, view));
            AddChild(view);
        }
    }

    private void PositionFlags()
    {
        for (var i = 0; i < _flags.Count; i++)
        {
            var (_, view) = _flags[i];
            var vehicle = LobbyPartyChoreography.Vehicle(_elapsed, i, Size, _reduced);
            var at = vehicle.Position + new Vector2(-30, -43) * Unit;
            view.Scale = new Vector2(1.6f * Unit);
            var half = new Vector2(70 * Unit);
            view.Measure(half * 2);
            view.Arrange(new UIBox2(at - half, at + half));
        }
    }

    private void AdvanceParade(float previous)
    {
        if (Crossed(previous, 0.5f))
            Sound("jet-idle.ogg", -27);
        if (Crossed(previous, 9.3f))
            Sound("pressure-vent.ogg", -24);
        if (Crossed(previous, 15))
            Sound("gau-impact.ogg", -27);
        if (Crossed(previous, 20))
            Sound("countermeasures.ogg", -25);
    }

    private void DrawParade(DrawingHandleScreen handle)
    {
        var top = Size.Y * 0.23f;
        var bottom = Size.Y * 0.95f;
        handle.DrawRect(new UIBox2(0, top, Size.X, bottom), Color.FromHex("#0B1929").WithAlpha(0.68f));
        handle.DrawLine(new Vector2(0, top), new Vector2(Size.X, top), Color.FromHex("#6A9BAA").WithAlpha(0.55f));
        handle.DrawLine(new Vector2(0, bottom), new Vector2(Size.X, bottom), Color.FromHex("#6A9BAA").WithAlpha(0.55f));
        for (var stripe = 0f; stripe < Size.X; stripe += 120 * Unit)
        {
            handle.DrawRect(new UIBox2(stripe, bottom - 10 * Unit, stripe + 50 * Unit, bottom - 7 * Unit),
                Color.FromHex("#D3BC72").WithAlpha(0.65f));
            handle.DrawRect(new UIBox2(stripe, top + 7 * Unit, stripe + 50 * Unit, top + 10 * Unit),
                Color.FromHex("#D3BC72").WithAlpha(0.35f));
        }

        // Grounded shadows make flips and hops read as height, rather than people sliding around.
        for (var i = 0; i < _actors.Count; i++)
        {
            if (_actors[i].Card.Parent == null)
                continue;
            var pose = LobbyPartyChoreography.March(_elapsed, i, _actors.Count, Size, _reduced, _seed);
            Ellipse(handle, pose.Position + new Vector2(0, pose.Height + 20 * Unit),
                new Vector2(17, 6) * Unit, Color.Black.WithAlpha(0.32f));
        }

        for (var vehicle = 0; vehicle < 3; vehicle++)
        {
            var pose = LobbyPartyChoreography.Vehicle(_elapsed, vehicle, Size, _reduced);
            var at = pose.Position;
            if (at.X < -240 * Unit || at.X > Size.X + 240 * Unit)
                continue;
            var rotation = pose.Rotation;
            Ellipse(handle, at + new Vector2(0, 24) * Unit, new Vector2(78, 23) * Unit, Color.Black.WithAlpha(0.4f));
            if (!_reduced)
            {
                for (var puff = 0; puff < 9; puff++)
                {
                    var dust = (_elapsed * 0.7f + puff / 9f) % 1;
                    var p = at + new Vector2(-80 - dust * 95, 24 - dust * 18) * Unit;
                    handle.DrawCircle(p, (4 + dust * 14) * Unit, Color.FromHex("#BCA783").WithAlpha((1 - dust) * 0.2f));
                }
            }
            switch (vehicle)
            {
                case 0:
                    Sprite(handle, TankArt, "tank_base", at, 192 * Unit, rotation);
                    Sprite(handle, TankArt, _reduced ? "wheels_1" : "wheels_0", at, 192 * Unit, rotation);
                    var salute = _reduced ? 0 : MathF.Sin(_elapsed * 2.5f) *
                        LobbyPartyChoreography.Window(_elapsed, 7, 13, 1) * 0.55f;
                    Sprite(handle, TankArt, "tank_turret_0", at, 192 * Unit, rotation + salute);
                    Sprite(handle, TankArt, "ltb_cannon_0", at, 192 * Unit, rotation + salute);
                    break;
                case 1:
                    Sprite(handle, ApcArt, "apc_base", at, 184 * Unit, rotation);
                    Sprite(handle, ApcArt, _reduced ? "wheels_1" : "wheels_0", at, 184 * Unit, rotation);
                    Sprite(handle, ApcArt, "dualcannon_0", at, 184 * Unit, rotation);
                    break;
                case 2:
                    Sprite(handle, TruckArt, "miltruck_1", at, 160 * Unit, rotation);
                    Sprite(handle, TruckArt, "miltruckwheels_0", at, 160 * Unit, rotation);
                    break;
            }
        }
        DrawParadeMishaps(handle);
    }

    private void DrawParadeMishaps(DrawingHandleScreen handle)
    {
        if (_reduced)
            return;
        // The truck loses its cargo over a bump, then a xeno chases the bouncing crate.
        var t = Math.Clamp((_elapsed - 15) / 6, 0, 1);
        if (t is > 0 and < 1)
        {
            var truck = LobbyPartyChoreography.Vehicle(15, 2, Size, false).Position;
            var at = truck + new Vector2(t * 380 - 40, -MathF.Abs(MathF.Sin(t * MathF.PI * 3)) * (1 - t) * 130 + 12) * Unit;
            var previous = handle.GetTransform();
            handle.SetTransform(Matrix3x2.CreateRotation(t * 13) * Matrix3x2.CreateTranslation(at) * previous);
            var half = new Vector2(14, 11) * Unit;
            handle.DrawRect(new UIBox2(-half, half), Color.FromHex("#887252"));
            handle.DrawRect(new UIBox2(-half, half), Color.FromHex("#D6C29A"), false);
            handle.DrawLine(-half, half, Color.FromHex("#CEB580"));
            handle.DrawLine(new Vector2(-half.X, half.Y), new Vector2(half.X, -half.Y), Color.FromHex("#CEB580"));
            handle.SetTransform(previous);
            Sprite(handle, XenoArt, "alive", truck + new Vector2(t * 480 - 170,
                12 - MathF.Abs(MathF.Sin(t * 35)) * 8) * Unit, 70 * Unit);
        }
        // One very late escort tries to catch up, trips, and cartwheels through the review.
        var late = (_elapsed - 19) / 7;
        if (late is > 0 and < 1)
        {
            var roll = LobbyPartyChoreography.Window(late, 0.3f, 0.85f, 0.1f);
            Sprite(handle, XenoArt, "alive", new Vector2(-90 * Unit + late * (Size.X + 180 * Unit),
                Size.Y * 0.40f - MathF.Abs(MathF.Sin(late * 20)) * 24 * Unit),
                76 * Unit, MathF.Sin(late * 24) * roll * 3);
        }
    }

    private void DrawConfetti(DrawingHandleScreen handle)
    {
        if (_reduced || _elapsed < 5 || _elapsed > 29)
            return;
        for (var i = 0; i < 100; i++)
        {
            var cycle = (_elapsed * 0.22f + i * 0.618f) % 1;
            var at = new Vector2(((i * 0.618f + (uint) _seed % 97 * 0.01f) % 1) * Size.X +
                MathF.Sin(cycle * 8 + i) * 25 * Unit, Size.Y * (0.22f + cycle * 0.73f));
            var half = new Vector2(MathF.Abs(2.5f * MathF.Cos(_elapsed * 8 + i)) + 0.4f, 4) * Unit;
            var color = i % 3 == 0 ? Color.FromHex("#E4BB75") : i % 3 == 1 ? Color.FromHex("#81CCBE") : Color.FromHex("#B78CCB");
            handle.DrawRect(new UIBox2(at - half, at + half), color.WithAlpha((1 - cycle) * 0.85f));
        }
        // Two salutes blow paper streamers across the road instead of firing live ammunition.
        for (var salvo = 0; salvo < 2; salvo++)
        {
            var age = _elapsed - (9.3f + salvo * 10.7f);
            if (age is < 0 or > 2.5f)
                continue;
            var origin = new Vector2(Size.X * (salvo == 0 ? 0.22f : 0.78f), Size.Y * 0.86f);
            for (var i = 0; i < 28; i++)
            {
                var velocity = new Vector2(MathF.Sin(i * 2.4f) * 145, -180 - i * 8 % 150) * Unit;
                var p = origin + velocity * age + new Vector2(0, age * age * 110) * Unit;
                handle.DrawLine(p, p - velocity * 0.04f, (i % 2 == 0 ? Color.Gold : Color.LightBlue).WithAlpha(1 - age / 2.5f));
            }
        }
    }

    private static void Ellipse(DrawingHandleScreen handle, Vector2 at, Vector2 radius, Color color)
    {
        var previous = handle.GetTransform();
        handle.SetTransform(Matrix3x2.CreateScale(radius) * Matrix3x2.CreateTranslation(at) * previous);
        handle.DrawCircle(Vector2.Zero, 1, color);
        handle.SetTransform(previous);
    }
}
