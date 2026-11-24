using System.Numerics;

namespace Content.Shared.CMU14.Pool;

public enum CMUPoolContact
{
    Ball,
    Rail,
    Pocket,
}

public sealed class CMUPoolSimulation
{
    public const int BallCount = 16;
    public const int Width = 640;
    public const int Height = 320;
    public const float Radius = 8;
    public const float PocketRadius = 15;
    public const float MinimumShotPower = 0.035f;
    private const float StepSeconds = 1f / 240;
    private float _accumulator;

    private static readonly int[] RackNumbers = [1, 10, 2, 3, 8, 11, 12, 4, 14, 5, 15, 6, 9, 7, 13];

    private static readonly Vector2[] Pockets =
    [
        new(0, 0), new(Width / 2f, 0), new(Width, 0),
        new(0, Height), new(Width / 2f, Height), new(Width, Height),
    ];

    public sealed class Ball
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public bool Pocketed;
    }

    public Ball[] Balls { get; } = new Ball[BallCount];
    public Ball CueBall => Balls[0];
    public int Remaining { get; private set; }
    public bool Rolling { get; private set; }
    public event Action<CMUPoolContact, float>? Contact;

    public CMUPoolSimulation()
    {
        for (var i = 0; i < Balls.Length; i++)
            Balls[i] = new Ball();

        Rack();
    }

    public void Rack()
    {
        _accumulator = 0;
        Remaining = 15;
        Rolling = false;
        foreach (var ball in Balls)
        {
            ball.Velocity = Vector2.Zero;
            ball.Pocketed = false;
        }

        CueBall.Position = new Vector2(Width * 0.25f, Height * 0.5f);
        var index = 0;
        const float spacing = Radius * 2 + 0.45f;
        for (var row = 0; row < 5; row++)
        {
            for (var column = 0; column <= row; column++)
            {
                Balls[RackNumbers[index++]].Position = new Vector2(
                    Width * 0.67f + row * spacing * 0.8660254f,
                    Height * 0.5f + (column - row * 0.5f) * spacing);
            }
        }
    }

    public void ApplyPresentation(Vector2[] positions, int pocketedMask, bool rolling)
    {
        Remaining = 0;
        Rolling = rolling;
        for (var i = 0; i < BallCount; i++)
        {
            var ball = Balls[i];
            ball.Position = positions[i];
            ball.Pocketed = (pocketedMask & (1 << i)) != 0;
            if (i != 0 && !ball.Pocketed)
                Remaining++;
        }
    }

    public bool CanPlaceCueBall(Vector2 position)
    {
        if (Rolling || !CueBall.Pocketed || position.X < Radius || position.X > Width - Radius
            || position.Y < Radius || position.Y > Height - Radius)
            return false;

        foreach (var pocket in Pockets)
        {
            if (Vector2.DistanceSquared(position, pocket) < (PocketRadius + Radius) * (PocketRadius + Radius))
                return false;
        }

        foreach (var ball in Balls)
        {
            if (!ball.Pocketed && Vector2.DistanceSquared(position, ball.Position) < 4 * Radius * Radius)
                return false;
        }
        return true;
    }

    public bool TryPlaceCueBall(Vector2 position)
    {
        if (!CanPlaceCueBall(position))
            return false;

        CueBall.Position = position;
        CueBall.Pocketed = false;
        return true;
    }

    public void Shoot(Vector2 direction, float power)
    {
        if (Rolling || CueBall.Pocketed || Remaining == 0 || power < MinimumShotPower)
            return;

        CueBall.Velocity = direction * (1100 * Math.Clamp(power, 0, 1));
        Rolling = true;
    }

    public void Update(float elapsed)
    {
        if (!Rolling)
            return;

        _accumulator += Math.Min(elapsed, 0.05f);
        while (_accumulator >= StepSeconds && Rolling)
        {
            _accumulator -= StepSeconds;
            Step();
        }
        if (!Rolling)
            _accumulator = 0;
    }

    private void Step()
    {
        foreach (var ball in Balls)
        {
            if (ball.Pocketed)
                continue;

            ball.Position += ball.Velocity * StepSeconds;
            if (TryPocket(ball))
                continue;

            var clamped = Vector2.Clamp(ball.Position, new Vector2(Radius), new(Width - Radius, Height - Radius));
            var railSpeed = 0f;
            if (clamped.X != ball.Position.X && ball.Velocity.X * (ball.Position.X - clamped.X) > 0)
            {
                railSpeed = MathF.Abs(ball.Velocity.X);
                ball.Velocity.X *= -0.79f;
            }
            if (clamped.Y != ball.Position.Y && ball.Velocity.Y * (ball.Position.Y - clamped.Y) > 0)
            {
                railSpeed = MathF.Max(railSpeed, MathF.Abs(ball.Velocity.Y));
                ball.Velocity.Y *= -0.79f;
            }
            if (railSpeed > 0)
                Contact?.Invoke(CMUPoolContact.Rail, railSpeed);
            ball.Position = clamped;
        }

        for (var i = 0; i < Balls.Length; i++)
        {
            var first = Balls[i];
            if (first.Pocketed)
                continue;

            for (var j = i + 1; j < Balls.Length; j++)
            {
                if (!Balls[j].Pocketed)
                    Collide(first, Balls[j]);
            }
        }

        Rolling = false;
        foreach (var ball in Balls)
        {
            if (ball.Pocketed)
                continue;

            var speed = ball.Velocity.Length();
            ball.Velocity = speed <= 5 ? Vector2.Zero : ball.Velocity * (1 - 82 * StepSeconds / speed);
            Rolling |= ball.Velocity != Vector2.Zero;
        }
    }

    private bool TryPocket(Ball ball)
    {
        foreach (var pocket in Pockets)
        {
            if (Vector2.DistanceSquared(ball.Position, pocket) > PocketRadius * PocketRadius)
                continue;

            var entrySpeed = ball.Velocity.Length();
            ball.Pocketed = true;
            ball.Velocity = Vector2.Zero;
            if (ball != CueBall)
                Remaining--;
            Contact?.Invoke(CMUPoolContact.Pocket, entrySpeed);
            return true;
        }
        return false;
    }

    private void Collide(Ball first, Ball second)
    {
        var delta = second.Position - first.Position;
        var distanceSquared = delta.LengthSquared();
        if (distanceSquared >= 4 * Radius * Radius)
            return;

        var distance = MathF.Sqrt(distanceSquared);
        var normal = distance > 0.0001f ? delta / distance : Vector2.UnitX;
        var correction = normal * ((Radius * 2 - distance) * 0.5f);
        first.Position -= correction;
        second.Position += correction;

        var approach = Vector2.Dot(first.Velocity - second.Velocity, normal);
        if (approach <= 0)
            return;

        var impulse = normal * (approach * 0.96f);
        first.Velocity -= impulse;
        second.Velocity += impulse;
        Contact?.Invoke(CMUPoolContact.Ball, approach);
    }
}
