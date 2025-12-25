using Godot;

namespace MechGrinder;

public partial class Walker : Mech
{
    public float RotationSpeed => 100f / Mass;
    
    public Walker(World world, Block initialBlock) : base(world, initialBlock)
    {
    }

    public override void _IntegrateForces(PhysicsDirectBodyState2D state)
    {
        base._IntegrateForces(state);
        
        // Rotate towards target
        float currentAngle = state.Transform.Rotation;
        float targetAngle = TargetDirection.Angle();
        float angleDiff = Mathf.AngleDifference(currentAngle, targetAngle);
        state.AngularVelocity = angleDiff * RotationSpeed;
        
        ApplyCentralForce(InputDirection * 500);
    }
    
    private float GetInertiaa()
    {
        return 1.0f / PhysicsServer2D.BodyGetDirectState(GetRid()).InverseInertia;
    }
}
