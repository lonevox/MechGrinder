using Godot;

namespace MechGrinder;

public partial class Walker : Mech
{
    public Walker(World world, Block initialBlock) : base(world, initialBlock)
    {
    }

    public override void _IntegrateForces(PhysicsDirectBodyState2D state)
    {
        base._IntegrateForces(state);
        // // GD.Print(Rotation - TargetRotation);
        // float direction = Transform.Y.Dot(TargetDirection);
        // // ConstantTorque = direction * 5000;
        // float targetRotation = Vector2.Right.AngleTo(TargetDirection);
        // // ApplyTorque(-(Rotation - targetRotation) * 5000);
        //
        // Transform2D xform = state.GetTransform();
        // bool rotatingLeft = Rotation - targetRotation > 0;
        // GD.Print(rotatingLeft);
        // if (rotatingLeft)
        // {
        //     if (Rotation - targetRotation > 0.1f)
        //         xform = xform.RotatedLocal(-0.05f);
        // }
        // else
        // {
        //     if (Rotation - targetRotation < -0.1f)
        //         xform = xform.RotatedLocal(0.05f);
        // }
        // // state.SetTransform(xform);
        // // GD.Print(GetInertiaa());
        // GD.Print(direction);
        // if (rotatingLeft)
        // {
        //     // ApplyTorque(-direction * 1000);
        // }
        // else
        // {
        //     // ApplyTorque(direction * 1000);
        // }
        // // ApplyTorque(100f);
        
        // Calculate angle difference
        float currentAngle = state.Transform.Rotation;
        float targetAngle = TargetDirection.Angle();
        float angleDiff = Mathf.AngleDifference(currentAngle, targetAngle);
        
        // Set angular velocity to rotate towards target
        state.AngularVelocity = angleDiff * targetAngle;
        
        ApplyCentralForce(InputDirection * 500);
    }
    
    private float GetInertiaa()
    {
        return 1.0f / PhysicsServer2D.BodyGetDirectState(GetRid()).InverseInertia;
    }
}
