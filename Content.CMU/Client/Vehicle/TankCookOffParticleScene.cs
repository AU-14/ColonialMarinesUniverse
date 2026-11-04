using System.Numerics;

namespace Content.Client.CMU14.Vehicle;

/// <summary>
/// Deterministic choreography for tank fire, smoke and ballistic ejecta.
/// This exact source also drives the WebGL preview. It never changes authoritative damage.
/// </summary>
internal sealed class TankCookOffParticleScene
{
    internal enum Material : byte { Smoke, Blast, Flame, Ash, Spark, Fragment, Trail = 8, Dust, Flash = 11, WreckFire }
    internal struct Particle
    {
        public Material Material;
        public Vector3 Position, Velocity, Acceleration;
        public Vector2 Size, EndSize;
        public Vector4 Color;
        public float Birth, Life, Drag, Rotation, Spin, Repeat;
        public int Variant, Layer;
    }
    internal readonly record struct Sample(Material Material, Vector2 Position, Vector2 Size,
        Vector4 Color, float Rotation, float Frame, int Variant, int Layer, float Depth);

    private readonly List<Particle> _particles = new(256);
    private uint _random;
    private readonly int _variant;
    public int ParticleCount => _particles.Count;
    public IReadOnlyList<Particle> Particles => _particles;
    public TankCookOffParticleScene(uint seed)
    {
        _variant = (int)(seed % 3);
        _random = seed == 0 ? 1u : seed;
    }

    private float Random(float min = 0, float max = 1)
    {
        _random ^= _random << 13; _random ^= _random >> 17; _random ^= _random << 5;
        return min + (_random & 0xFFFFFF) / 16777216f * (max - min);
    }
    private Vector2 Direction()
    {
        var a = Random(0, MathF.Tau);
        return new(MathF.Cos(a), MathF.Sin(a));
    }
    private void Add(Material material, Vector3 position, Vector3 velocity, float birth, float life,
        Vector2 size, Vector2 endSize, float opacity = 1, float drag = 0,
        Vector3 acceleration = default, int layer = 1, float rotation = 0, float spin = 0, float repeat = 0, int variant = -1)
    {
        // Emitter recipes, rather than running time, determine storage. No entity per particle.
        if (_particles.Count >= 640) return;
        _particles.Add(new Particle { Material = material, Position = position, Velocity = velocity,
            Birth = birth, Life = life, Size = size, EndSize = endSize,
            Color = new(1, 1, 1, opacity), Drag = drag,
            Acceleration = acceleration, Layer = layer, Rotation = rotation, Spin = spin,
            Repeat = repeat, Variant = variant < 0 ? (int)Random(0, 256) : (int)Random(0, 84) * 3 + variant });
    }

    public static TankCookOffParticleScene Tank(float ruptureDelay, uint seed)
    {
        var s = new TankCookOffParticleScene(seed);
        // Pressure escapes through alternating hatch seams, accelerating towards rupture.
        for (var i = 0; i < 16; i++)
        {
            var progress = i / 16f;
            var birth = MathF.Sqrt(progress) * ruptureDelay;
            var height = .6f + progress * 2.1f;
            var side = i % 2 == 0 ? -1f : 1f;
            s.Add(Material.Flame, new(side * .58f, .1f + height * .39f, 0), default,
                birth, s.Random(.22f, .45f), new(.26f, height), new(.5f, height * 1.1f),
                rotation: side * -.15f);
            s.Add(Material.Smoke, new(side * .6f, .7f, 0), new(side * .15f, .35f, .6f),
                birth, 1.5f, new(.5f, 1.1f), new(1.3f, 2), .55f);
        }

        // One sharp rupture, followed by a split ammunition plume rather than repeated ground explosions.
        // Hull pressure kicks up separate low dust fans, not an orbital-sized circular wave.
        for (var i = 0; i < 10; i++)
        {
            var direction = s.Direction() * new Vector2(1, .55f);
            s.Add(Material.Dust, new(direction * 1.2f, .05f), new(direction * s.Random(5, 8), .15f),
                ruptureDelay + s.Random(.02f, .12f), s.Random(.75f, 1.4f), new(.8f, .35f), new(2.7f, .9f),
                .6f, drag: 2.8f, layer: 0);
        }
        s.Add(Material.Flash, new(0, .5f, 0), default, ruptureDelay, .24f, new(7, 4), new(10, 5), layer: 2);
        var size = s._variant switch { 0 => new Vector2(8.7f, 13), 1 => new Vector2(6.6f, 16), _ => new Vector2(8, 14.5f) };
        s.Add(Material.Blast, new(0, size.Y * .39f, 0), default, ruptureDelay, 6.2f,
            size, size, variant: s._variant);
        s.Add(Material.Ash, Vector3.Zero, default, ruptureDelay, 30, new(5, 2.7f), new(5.8f, 3), layer: 0);
        s.TankEjecta(Vector2.Zero, ruptureDelay, 46, 1);

        for (var i = 0; i < 7; i++)
        {
            var point = new Vector2(s.Random(-.8f, .8f), s.Random(-.45f, .45f));
            var birth = ruptureDelay + .5f + i * .62f + s.Random(0, .25f);
            var height = s.Random(2.3f, 4.6f);
            s.Add(Material.Flame, new(point + new Vector2(0, height * .39f), 0), default,
                birth, s.Random(.45f, .8f), new(.65f, height), new(1.2f, height * 1.15f),
                rotation: s.Random(-.35f, .35f));
            s.Add(Material.Flash, new(point, .3f), default, birth, .12f, new(1.5f), new(2.2f), .65f, layer: 2);
            s.TankEjecta(point, birth, 7, .42f);
        }

        // Dust blooms where the turret actually lands; the small bounce ends in a fixed wreck.
        var landing = TankTurret(1.65f, seed).Ground;
        for (var i = 0; i < 8; i++)
        {
            var direction = s.Direction();
            s.Add(Material.Dust, new(landing, .05f), new(direction * s.Random(1.4f, 3), .15f),
                ruptureDelay + 1.65f, s.Random(.8f, 1.5f), new(.8f, .4f), new(2, .85f), .7f, drag: 2, layer: 0);
        }

        // One continuous fire/soot field rooted in the open hull, without repeating torch emitters.
        // Its phase encodes elapsed seconds, so it keeps evolving rather than restarting every few seconds.
        s.Add(Material.WreckFire, new(0, 3.25f, 0), default, ruptureDelay + .65f, 4096,
            new(5.6f, 7.2f), new(5.6f, 7.2f), repeat: 4096);
        for (var i = 0; i < 24; i++)
        {
            var life = s.Random(2.8f, 4.5f);
            s.Add(Material.Spark, new(s.Random(-.65f, .65f), .15f, .55f),
                new(s.Random(-.2f, .55f), .1f, s.Random(.8f, 1.8f)),
                ruptureDelay + 1.3f + i * .23f, life, new(s.Random(.045f, .075f)), new(.012f),
                s.Random(.4f, .9f), repeat: life);
        }
        return s;
    }

    private void TankEjecta(Vector2 point, float birth, int count, float force)
    {
        for (var i = 0; i < count; i++)
        {
            var direction = Direction();
            var velocity = new Vector3(direction * Random(2.8f, 7) * force, Random(7, 14) * force);
            var fragment = i % 5 == 0;
            var width = fragment ? Random(.14f, .28f) : Random(.035f, .065f);
            Add(fragment ? Material.Fragment : Material.Trail, new(point, .4f), velocity,
                birth + Random(0, .09f), fragment ? (velocity.Z + MathF.Sqrt(velocity.Z * velocity.Z + 9.6f)) / 12 + .9f : Random(.55f, 1.5f),
                new(width, width * 1.8f), new(width), drag: .35f, acceleration: new(0, 0, -12),
                layer: 2, rotation: Random(0, MathF.Tau), spin: Random(-7, 7));
        }
    }

    internal readonly record struct TurretSample(Vector2 Ground, Vector2 Position, float Rotation);

    public static TurretSample TankTurret(float age, uint seed)
    {
        var q = Math.Clamp(age / 1.65f, 0, 1);
        var side = seed % 3 == 1 ? -1f : 1f;
        var distance = 3.2f + seed % 7 * .12f;
        var ground = new Vector2(side * distance, -.65f - seed % 5 * .12f) * q;
        var height = (5.1f + seed % 3 * .4f) * 4 * q * (1 - q);
        var settle = Math.Clamp((age - 1.65f) / .38f, 0, 1);
        height += MathF.Sin(settle * MathF.PI) * .32f;
        ground += new Vector2(side * .35f, -.08f) * settle;
        return new(ground, ground + Vector2.UnitY * height, side * (q * 4.7f + settle * .25f));
    }

    public bool TrySample(int index, float clock, out Sample sample)
    {
        var p = _particles[index];
        var age = clock - p.Birth;
        sample = default;
        if (age < 0) return false;
        if (p.Repeat > 0) age %= p.Repeat;
        if (age >= p.Life) return false;
        var phase = age / p.Life;
        var travel = p.Drag > 0 ? (1 - MathF.Exp(-p.Drag * age)) / p.Drag : age;
        var pos = p.Position + p.Velocity * travel + p.Acceleration * (age * age * .5f);
        var rotationAge = age;
        if (p.Material is Material.Fragment or Material.Trail or Material.Spark)
        {
            // Vertical flight is ballistic; drag affects ground travel. Heavy pieces bounce and settle.
            pos.Z = p.Position.Z + p.Velocity.Z * age + p.Acceleration.Z * age * age * .5f;
            if (pos.Z < 0)
            {
                if (p.Material != Material.Fragment || p.Acceleration.Z >= 0) return false;
                var landing = (-p.Velocity.Z - MathF.Sqrt(p.Velocity.Z * p.Velocity.Z - 2 * p.Acceleration.Z * p.Position.Z)) / p.Acceleration.Z;
                var landedAge = age - landing;
                var groundTravel = p.Drag > 0 ? (1 - MathF.Exp(-p.Drag * landing)) / p.Drag : landing;
                var settle = Math.Min(landedAge, .3f);
                rotationAge = Math.Min(age, landing + .3f);
                pos = p.Position + p.Velocity * (groundTravel + settle * .13f);
                pos.Z = landedAge < .3f ? MathF.Sin(landedAge / .3f * MathF.PI) * .14f : 0;
            }
        }
        var projected = new Vector2(pos.X, pos.Y + Math.Max(0, pos.Z) * .8f);
        var size = Vector2.Lerp(p.Size, p.EndSize, phase);
        var rotation = p.Rotation + p.Spin * rotationAge;
        if (p.Material == Material.Trail)
        {
            var velocity = p.Velocity * MathF.Exp(-p.Drag * age) + p.Acceleration * age;
            velocity.Z = p.Velocity.Z + p.Acceleration.Z * age;
            var tangent = new Vector2(velocity.X, velocity.Y + velocity.Z * .8f);
            size.Y = Math.Min(tangent.Length() * .14f, 2.8f);
            rotation = MathF.Atan2(tangent.Y, tangent.X) - MathF.PI * .5f;
        }
        var alpha = p.Material == Material.WreckFire
            ? p.Color.W * Smooth(0, .7f, clock - p.Birth)
            : p.Color.W * (1 - Smooth(.68f, 1, phase));
        if (p.Material is Material.Smoke or Material.Dust) alpha *= Smooth(0, .12f, phase);
        sample = new(p.Material, projected, size, new(1, 1, 1, alpha), rotation, phase,
            p.Variant, p.Layer, pos.Y - pos.Z * .08f);
        return alpha > .005f;
    }
    private static float Smooth(float a, float b, float t)
    {
        t = Math.Clamp((t - a) / (b - a), 0, 1);
        return t * t * (3 - 2 * t);
    }
}
