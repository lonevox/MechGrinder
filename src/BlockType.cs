using System;
using Godot;
using MechGrinder.Util;

namespace MechGrinder;

/// <summary>
/// </summary>
public partial class BlockType : Resource
{
    private static readonly Shape2D DefaultShape = new RectangleShape2D();

    public string Name = "";
    public Shape2D Shape = (Shape2D)DefaultShape.Duplicate();
    public Mesh Mesh = ShapeUtil.Shape2DToMesh(DefaultShape);
    public Vector2[] PortPositions = Array.Empty<Vector2>();
    public Vector2[] PortNormals = Array.Empty<Vector2>();
    public BlockFeatures Features;
    public int Scale;
    public float Durability;
    public float Density;
    public float Mass;
    public float Area;
    public Vector2 CenterOfMass;
    public float Health;

    public BlockType()
    {
    }

    public BlockType(
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
        BlockFeatures features)
    {
        Name = name;
        Shape = shape;
        Mesh = mesh;
        PortPositions = portPositions;
        PortNormals = portNormals;
        Features = features;
        Scale = scale;
        Durability = durability;
        Density = density;
        Mass = mass;
        Area = area;
        CenterOfMass = centerOfMass;
        Health = health;
    }
}

public abstract class BlockTypeBuilder<TBuilder, TBlockType>
    where TBuilder : BlockTypeBuilder<TBuilder, TBlockType>
    where TBlockType : BlockType
{
    protected readonly string _name;
    protected float _area;
    protected Vector2 _centerOfMass;
    protected float _density;
    protected float _durability;
    protected BlockFeatures _features;
    protected float _health;
    protected float _mass;
    protected Mesh _mesh;
    protected Vector2[] _portNormals = [];
    protected Vector2[] _portPositions = [];
    protected int _scale = 1;
    protected Shape2D _shape;

    protected BlockTypeBuilder(string name, Shape2D shape)
    {
        _name = name;
        _shape = shape;
    }

    /// <summary>
    ///     Returns the concrete builder type for use in method chaining.
    /// </summary>
    protected abstract TBuilder Self { get; }

    public TBuilder PortPositions(Vector2[] portPositions)
    {
        _portPositions = portPositions;
        return Self;
    }

    public TBuilder Scale(int scale)
    {
        if (scale < 1)
            throw new ArgumentOutOfRangeException(nameof(scale), "BlockType scale must be 1 or more.");
        _scale = scale;
        return Self;
    }

    public TBuilder Durability(float durability)
    {
        if (durability <= 0)
            throw new ArgumentOutOfRangeException(nameof(durability), "BlockType durability must be greater than 0.");
        _durability = durability;
        return Self;
    }

    public TBuilder Density(float density)
    {
        if (density <= 0)
            throw new ArgumentOutOfRangeException(nameof(density), "BlockType density must be greater than 0.");
        _density = density;
        return Self;
    }

    public TBuilder Mass(float mass)
    {
        if (mass <= 0)
            throw new ArgumentOutOfRangeException(nameof(mass), "BlockType mass must be greater than 0.");
        _mass = mass;
        return Self;
    }

    public TBuilder Health(float health)
    {
        if (health <= 0)
            throw new ArgumentOutOfRangeException(nameof(health), "BlockType health must be greater than 0.");
        _health = health;
        return Self;
    }

    /// <summary>
    ///     Sets the <see cref="BlockFeatures" /> for this block. Overwrites any features added with <see cref="AddFeatures" />
    ///     or any other builder methods that add features such as <see cref="Weak" />.
    /// </summary>
    public TBuilder Features(BlockFeatures features)
    {
        _features = features;
        return Self;
    }

    public TBuilder AddFeatures(BlockFeatures features)
    {
        _features |= features;
        return Self;
    }

    private void SetFeatures(BlockFeatures features, bool enable)
    {
        if (enable)
            _features |= features;
        else
            _features &= ~features;
    }

    public TBuilder Core(bool enable = true)
    {
        SetFeatures(BlockFeatures.Core, enable);
        return Self;
    }

    public TBuilder Weak(bool enable = true)
    {
        SetFeatures(BlockFeatures.Weak, enable);
        return Self;
    }

    public virtual BlockType Build()
    {
        // Scale shape
        var shape = (Shape2D)_shape.Duplicate();
        if (_scale != 1)
            shape = ShapeUtil.ScaleShape(shape, new Vector2(_scale, _scale));

        // Make sure shape is centered
        _shape = ShapeUtil.CenterShape2D(shape);

        // Create mesh from shape
        _mesh = ShapeUtil.Shape2DToMesh(_shape);

        // Port positions
        var shapePolygon = ShapeUtil.Shape2DToPolygon(_shape);
        var portCount = shapePolygon.Length * _scale;
        if (_portPositions.IsEmpty())
        {
            // Calculate port positions based on shape and scale. Ports are evenly spaced. The number of ports on
            // each side of a block is equal to the scale.
            _portPositions = new Vector2[portCount];
            for (var i = 0; i < shapePolygon.Length; i++)
            for (var j = 0; j < _scale; j++)
            {
                var ratio = 1f / _scale * (j + 1) - 1f / _scale / 2;
                var portPosition = PolygonUtil.PolygonPointAlongSide(shapePolygon, i, ratio);
                _portPositions[i * _scale + j] = portPosition;
            }
        }
        else
        {
            // Port positions must be cloned because the builder could be used again, in which case we don't want
            // two block types to share the same _portPositions array reference.
            _portPositions = (Vector2[])_portPositions.Clone();
        }

        // Port normals
        _portNormals = new Vector2[portCount];
        for (var i = 0; i < shapePolygon.Length; i++)
        for (var j = 0; j < _scale; j++)
        {
            var portNormal = PolygonUtil.PolygonSideNormal(shapePolygon, i);
            _portNormals[i * _scale + j] = portNormal;
        }

        _area = ShapeUtil.Shape2DArea(_shape);
        _centerOfMass = PolygonUtil.PolygonCentroid(shapePolygon);

        // If density or mass is missing, then one is used to specify the other. If both are missing, throw.
        if (_density != 0 && _mass == 0)
            _mass = _density * _area;
        else if (_density == 0 && _mass != 0)
            _density = _mass / _area;
        else
            throw new Exception("Must specify either density or mass in order to build BlockType.");

        // If durability is specified, overwrite health. Otherwise, health sets durability. If health isn't set either, throw.
        if (_durability != 0)
            _health = _durability * _area;
        else if (_health != 0)
            _durability = _health / _area;
        else
            throw new Exception("Must specify either durability or health in order to build BlockType.");

        return (TBlockType)new BlockType(_name, _shape, _mesh, _scale, _durability, _density, _mass, _area,
            _centerOfMass, _health, _portPositions, _portNormals, _features);
    }
}

public class BlockTypeBuilder : BlockTypeBuilder<BlockTypeBuilder, BlockType>
{
    public BlockTypeBuilder(string name, Shape2D shape) : base(name, shape)
    {
    }

    protected override BlockTypeBuilder Self => this;
}

[Flags]
public enum BlockFeatures
{
    /// <summary>
    ///     This block can be used to control a cluster.
    /// </summary>
    Core = 1,

    /// <summary>
    ///     Weak blocks are destroyed when one of their neighbours is destroyed.
    /// </summary>
    Weak = 2
}
