using System.Numerics;
using Content.Shared.CMU14.Fob;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Graphics.RSI;

namespace Content.Client.CMU14.Fob;

/// <summary>Client-local animation clock. None of this state controls power, targeting or damage.</summary>
[RegisterComponent]
public sealed partial class CMUFobAnimationComponent : Component
{
    public CMUFobPresentationStatus Status;
    public CMUFobPresentationCue Cue;
    public TimeSpan? CueAt;
    public TimeSpan? ShotAt;
    public TimeSpan StatusSince;
    public int ShotSequence;
    public Vector2 Aim;
    public bool Open;
    public float LockDuration = 3;
    public float Yaw;
    public Angle? TrackingAngle;
    public RSI.State? Source;
    public RsiDirection Direction;
    public readonly List<SpriteComponent.Layer> Layers = new();
    public readonly List<CMUFobSpritePart> Parts = new();
}

public readonly record struct CMUFobSpritePart(UIBox2 Bounds, int MovingPart, Texture Texture);
