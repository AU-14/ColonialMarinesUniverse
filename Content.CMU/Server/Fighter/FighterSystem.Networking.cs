using System.Numerics;
using Content.Shared.CMU14.Fighter;

namespace Content.Server.CMU14.Fighter;

public sealed partial class FighterSystem
{
    private readonly Dictionary<EntityUid, AircraftNetworkState> _aircraftNetworkState = [];

    // Compare at the existing network cadence. Action handlers may still request an immediate full update.
    private void DirtyAircraftChanges(Entity<FighterAircraftComponent> entity)
    {
        var current = new AircraftNetworkState(entity.Comp);
        if (!_aircraftNetworkState.TryGetValue(entity, out var previous))
            Dirty(entity);
        else
        {
            if (current.FlightPlan != previous.FlightPlan) DirtyField(entity, entity.Comp, nameof(FighterAircraftComponent.FlightPlan));
            if (current.FlightLimits != previous.FlightLimits) DirtyField(entity, entity.Comp, nameof(FighterAircraftComponent.FlightLimits));
            if (current.Sensors != previous.Sensors) DirtyField(entity, entity.Comp, nameof(FighterAircraftComponent.Sensors));
            if (current.GroundEntity != previous.GroundEntity) DirtyField(entity, entity.Comp, nameof(FighterAircraftComponent.GroundEntity));
            if (current.GroundState != previous.GroundState) DirtyField(entity, entity.Comp, nameof(FighterAircraftComponent.GroundState));
            if (current.RecoveryHandoff != previous.RecoveryHandoff) DirtyField(entity, entity.Comp, nameof(FighterAircraftComponent.RecoveryHandoff));
            if (current.GroundStateStartedAt != previous.GroundStateStartedAt) DirtyField(entity, entity.Comp, nameof(FighterAircraftComponent.GroundStateStartedAt));
            if (current.GroundStateEndsAt != previous.GroundStateEndsAt) DirtyField(entity, entity.Comp, nameof(FighterAircraftComponent.GroundStateEndsAt));
            if (current.TerrainMap != previous.TerrainMap) DirtyField(entity, entity.Comp, nameof(FighterAircraftComponent.TerrainMap));
            if (current.ViewMap != previous.ViewMap) DirtyField(entity, entity.Comp, nameof(FighterAircraftComponent.ViewMap));
            if (current.Position != previous.Position) DirtyField(entity, entity.Comp, nameof(FighterAircraftComponent.Position));
            if (current.Heading != previous.Heading) DirtyField(entity, entity.Comp, nameof(FighterAircraftComponent.Heading));
            if (current.Speed != previous.Speed) DirtyField(entity, entity.Comp, nameof(FighterAircraftComponent.Speed));
            if (current.TargetSpeed != previous.TargetSpeed) DirtyField(entity, entity.Comp, nameof(FighterAircraftComponent.TargetSpeed));
            if (current.Bank != previous.Bank) DirtyField(entity, entity.Comp, nameof(FighterAircraftComponent.Bank));
            if (current.Altitude != previous.Altitude) DirtyField(entity, entity.Comp, nameof(FighterAircraftComponent.Altitude));
            if (current.Flying != previous.Flying) DirtyField(entity, entity.Comp, nameof(FighterAircraftComponent.Flying));
            if (current.ForcedRetreat != previous.ForcedRetreat) DirtyField(entity, entity.Comp, nameof(FighterAircraftComponent.ForcedRetreat));
            if (current.Phase != previous.Phase) DirtyField(entity, entity.Comp, nameof(FighterAircraftComponent.Phase));
            if (current.Progress != previous.Progress) DirtyField(entity, entity.Comp, nameof(FighterAircraftComponent.Progress));
            if (current.Height != previous.Height) DirtyField(entity, entity.Comp, nameof(FighterAircraftComponent.Height));
            if (current.TargetHeight != previous.TargetHeight) DirtyField(entity, entity.Comp, nameof(FighterAircraftComponent.TargetHeight));
            if (current.Mark != previous.Mark) DirtyField(entity, entity.Comp, nameof(FighterAircraftComponent.Mark));
            if (current.MarkAge != previous.MarkAge) DirtyField(entity, entity.Comp, nameof(FighterAircraftComponent.MarkAge));
            if (current.TrainingImpact != previous.TrainingImpact) DirtyField(entity, entity.Comp, nameof(FighterAircraftComponent.TrainingImpact));
            if (current.PassNumber != previous.PassNumber) DirtyField(entity, entity.Comp, nameof(FighterAircraftComponent.PassNumber));
            if (current.MarkPass != previous.MarkPass) DirtyField(entity, entity.Comp, nameof(FighterAircraftComponent.MarkPass));
            if (current.FrontSeat != previous.FrontSeat) DirtyField(entity, entity.Comp, nameof(FighterAircraftComponent.FrontSeat));
            if (current.RearSeat != previous.RearSeat) DirtyField(entity, entity.Comp, nameof(FighterAircraftComponent.RearSeat));
            if (current.Hull != previous.Hull) DirtyField(entity, entity.Comp, nameof(FighterAircraftComponent.Hull));
            if (current.Canopy != previous.Canopy) DirtyField(entity, entity.Comp, nameof(FighterAircraftComponent.Canopy));
        }
        _aircraftNetworkState[entity] = current;
    }

    private readonly record struct AircraftNetworkState
    {
        public readonly FighterFlightPlan FlightPlan;
        public readonly FighterFlightLimits FlightLimits;
        public readonly FighterSensors Sensors;
        public readonly EntityUid? GroundEntity;
        public readonly FighterGroundState GroundState;
        public readonly bool RecoveryHandoff;
        public readonly TimeSpan GroundStateStartedAt;
        public readonly TimeSpan GroundStateEndsAt;
        public readonly EntityUid TerrainMap;
        public readonly EntityUid ViewMap;
        public readonly Vector2 Position;
        public readonly float Heading;
        public readonly float Speed;
        public readonly float TargetSpeed;
        public readonly float Bank;
        public readonly FighterAltitude Altitude;
        public readonly bool Flying;
        public readonly bool ForcedRetreat;
        public readonly FighterPhase Phase;
        public readonly float Progress;
        public readonly float Height;
        public readonly float TargetHeight;
        public readonly Vector2? Mark;
        public readonly float MarkAge;
        public readonly Vector2? TrainingImpact;
        public readonly int PassNumber;
        public readonly int MarkPass;
        public readonly EntityUid? FrontSeat;
        public readonly EntityUid? RearSeat;
        public readonly EntityUid? Hull;
        public readonly EntityUid? Canopy;

        public AircraftNetworkState(FighterAircraftComponent component)
        {
            FlightPlan = component.FlightPlan;
            FlightLimits = component.FlightLimits;
            Sensors = component.Sensors;
            GroundEntity = component.GroundEntity;
            GroundState = component.GroundState;
            RecoveryHandoff = component.RecoveryHandoff;
            GroundStateStartedAt = component.GroundStateStartedAt;
            GroundStateEndsAt = component.GroundStateEndsAt;
            TerrainMap = component.TerrainMap;
            ViewMap = component.ViewMap;
            Position = component.Position;
            Heading = component.Heading;
            Speed = component.Speed;
            TargetSpeed = component.TargetSpeed;
            Bank = component.Bank;
            Altitude = component.Altitude;
            Flying = component.Flying;
            ForcedRetreat = component.ForcedRetreat;
            Phase = component.Phase;
            Progress = component.Progress;
            Height = component.Height;
            TargetHeight = component.TargetHeight;
            Mark = component.Mark;
            MarkAge = component.MarkAge;
            TrainingImpact = component.TrainingImpact;
            PassNumber = component.PassNumber;
            MarkPass = component.MarkPass;
            FrontSeat = component.FrontSeat;
            RearSeat = component.RearSeat;
            Hull = component.Hull;
            Canopy = component.Canopy;
        }
    }

    private readonly Dictionary<EntityUid, SeatNetworkState> _seatNetworkState = [];

    // Compare at the existing network cadence. Action handlers may still request an immediate full update.
    private void DirtySeatChanges(Entity<FighterSeatComponent> entity)
    {
        var current = new SeatNetworkState(entity.Comp);
        if (!_seatNetworkState.TryGetValue(entity, out var previous))
            Dirty(entity);
        else
        {
            if (current.Pilot != previous.Pilot) DirtyField(entity, entity.Comp, nameof(FighterSeatComponent.Pilot));
            if (current.Aircraft != previous.Aircraft) DirtyField(entity, entity.Comp, nameof(FighterSeatComponent.Aircraft));
            if (current.Occupant != previous.Occupant) DirtyField(entity, entity.Comp, nameof(FighterSeatComponent.Occupant));
            if (current.Camera != previous.Camera) DirtyField(entity, entity.Comp, nameof(FighterSeatComponent.Camera));
            if (current.ExteriorCamera != previous.ExteriorCamera) DirtyField(entity, entity.Comp, nameof(FighterSeatComponent.ExteriorCamera));
            if (current.Aim != previous.Aim) DirtyField(entity, entity.Comp, nameof(FighterSeatComponent.Aim));
            if (current.SensorFocus != previous.SensorFocus) DirtyField(entity, entity.Comp, nameof(FighterSeatComponent.SensorFocus));
            if (current.SensorLock != previous.SensorLock) DirtyField(entity, entity.Comp, nameof(FighterSeatComponent.SensorLock));
            if (current.Zoomed != previous.Zoomed) DirtyField(entity, entity.Comp, nameof(FighterSeatComponent.Zoomed));
            if (current.Target != previous.Target) DirtyField(entity, entity.Comp, nameof(FighterSeatComponent.Target));
            if (current.TargetPosition != previous.TargetPosition) DirtyField(entity, entity.Comp, nameof(FighterSeatComponent.TargetPosition));
            if (current.WeaponSlot != previous.WeaponSlot) DirtyField(entity, entity.Comp, nameof(FighterSeatComponent.WeaponSlot));
            if (current.Laser != previous.Laser) DirtyField(entity, entity.Comp, nameof(FighterSeatComponent.Laser));
            if (current.LaserLockTarget != previous.LaserLockTarget) DirtyField(entity, entity.Comp, nameof(FighterSeatComponent.LaserLockTarget));
            if (current.LaserLockSlot != previous.LaserLockSlot) DirtyField(entity, entity.Comp, nameof(FighterSeatComponent.LaserLockSlot));
            if (current.LaserLockReadyAt != previous.LaserLockReadyAt) DirtyField(entity, entity.Comp, nameof(FighterSeatComponent.LaserLockReadyAt));
            if (current.FireQueued != previous.FireQueued) DirtyField(entity, entity.Comp, nameof(FighterSeatComponent.FireQueued));
            if (current.CameraControl != previous.CameraControl) DirtyField(entity, entity.Comp, nameof(FighterSeatComponent.CameraControl));
            if (current.EjectConfirmUntil != previous.EjectConfirmUntil) DirtyField(entity, entity.Comp, nameof(FighterSeatComponent.EjectConfirmUntil));
        }
        _seatNetworkState[entity] = current;
    }

    private readonly record struct SeatNetworkState
    {
        public readonly bool Pilot;
        public readonly EntityUid? Aircraft;
        public readonly EntityUid? Occupant;
        public readonly EntityUid? Camera;
        public readonly EntityUid? ExteriorCamera;
        public readonly Vector2 Aim;
        public readonly Vector2? SensorFocus;
        public readonly Vector2? SensorLock;
        public readonly bool Zoomed;
        public readonly NetEntity? Target;
        public readonly Vector2? TargetPosition;
        public readonly int WeaponSlot;
        public readonly EntityUid? Laser;
        public readonly NetEntity? LaserLockTarget;
        public readonly int LaserLockSlot;
        public readonly TimeSpan LaserLockReadyAt;
        public readonly bool FireQueued;
        public readonly bool CameraControl;
        public readonly TimeSpan EjectConfirmUntil;

        public SeatNetworkState(FighterSeatComponent component)
        {
            Pilot = component.Pilot;
            Aircraft = component.Aircraft;
            Occupant = component.Occupant;
            Camera = component.Camera;
            ExteriorCamera = component.ExteriorCamera;
            Aim = component.Aim;
            SensorFocus = component.SensorFocus;
            SensorLock = component.SensorLock;
            Zoomed = component.Zoomed;
            Target = component.Target;
            TargetPosition = component.TargetPosition;
            WeaponSlot = component.WeaponSlot;
            Laser = component.Laser;
            LaserLockTarget = component.LaserLockTarget;
            LaserLockSlot = component.LaserLockSlot;
            LaserLockReadyAt = component.LaserLockReadyAt;
            FireQueued = component.FireQueued;
            CameraControl = component.CameraControl;
            EjectConfirmUntil = component.EjectConfirmUntil;
        }
    }

}
