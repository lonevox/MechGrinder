using System;
using Godot;

namespace MechGrinder;

public partial class Walker : Mech
{
    private readonly float _maxAngularVelocity;
    private readonly float _rotationForce;

    public Walker(World world, Block coreBlock) : base(world, coreBlock)
    {
        var coreBlockType = world.GetBlockTypeAsType<RotatableBlockType>(coreBlock);
        _maxAngularVelocity = coreBlockType.MaxAngularVelocity;
        _rotationForce = coreBlockType.RotationForce;
    }

    public override void _IntegrateForces(PhysicsDirectBodyState2D state)
    {
        base._IntegrateForces(state);

        // Rotate towards target
        var currentAngle = state.Transform.Rotation;
        var targetAngle = TargetDirection.Angle();
        var angleDiff = Mathf.AngleDifference(currentAngle, targetAngle);
        GD.Print(CapVelocity(angleDiff * _rotationForce / Mass, _maxAngularVelocity));
        state.AngularVelocity = CapVelocity(angleDiff * _rotationForce / Mass, _maxAngularVelocity);

        ApplyCentralForce(InputDirection * 500);
    }

    private float GetInertiaa()
    {
        return 1.0f / PhysicsServer2D.BodyGetDirectState(GetRid()).InverseInertia;
    }

    /// <summary>
    ///     This function caps a velocity to a given max velocity. It can cap both positive and negative velocities.
    /// </summary>
    private static float CapVelocity(float velocity, float maxVelocity)
    {
        var capped = Math.Min(Math.Abs(velocity), maxVelocity);
        return velocity > 0 ? capped : -capped;
    }
}
