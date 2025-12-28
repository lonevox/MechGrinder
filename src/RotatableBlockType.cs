using Godot;

namespace MechGrinder;

public partial class RotatableBlockType : BlockType
{
    public float MaxAngularVelocity = 2f;
    public float RotationForce = 100f;

    public RotatableBlockType()
    {
    }

    public RotatableBlockType(
        string name,
        Shape2D shape,
        Mesh mesh,
        int scale,
        float durability,
        float density,
        float mass,
        float area,
        Vector2 centerOfMass,
        float health,
        Vector2[] portPositions,
        Vector2[] portNormals,
        BlockFeatures features,
        float maxAngularVelocity,
        float rotationForce)
        : base(name, shape, mesh, scale, durability, density, mass, area,
            centerOfMass, health, portPositions, portNormals, features)
    {
        MaxAngularVelocity = maxAngularVelocity;
        RotationForce = rotationForce;
    }
}

public class RotatableBlockTypeBuilder : BlockTypeBuilder<RotatableBlockTypeBuilder, RotatableBlockType>
{
    private float _maxAngularVelocity;
    private float _rotationForce;

    public RotatableBlockTypeBuilder(string name, Shape2D shape) : base(name, shape)
    {
    }

    protected override RotatableBlockTypeBuilder Self => this;

    public RotatableBlockTypeBuilder MaxAngularVelocity(float maxAngularVelocity)
    {
        _maxAngularVelocity = maxAngularVelocity;
        return this;
    }

    public RotatableBlockTypeBuilder RotationForce(float rotationForce)
    {
        _rotationForce = rotationForce;
        return this;
    }

    public override RotatableBlockType Build()
    {
        base.Build();
        return new RotatableBlockType(_name, _shape, _mesh, _scale, _durability, _density, _mass, _area, _centerOfMass,
            _health, _portPositions, _portNormals, _features, _maxAngularVelocity, _rotationForce);
    }
}
