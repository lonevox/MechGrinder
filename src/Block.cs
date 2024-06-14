using System;
using Godot;
using MechGrinder.Util;

namespace MechGrinder;

/// <summary>
/// 
/// </summary>
/// <param name="Name"></param>
/// <param name="Shape"></param>
/// <param name="Mesh"></param>
/// <param name="Center"></param>
/// <param name="PortPositions"></param>
/// <param name="PortNormals"></param>
/// <param name="Features"></param>
/// <param name="Scale"></param>
/// <param name="Durability"></param>
/// <param name="Density"></param>
/// <param name="Mass"></param>
/// <param name="Area"></param>
/// <param name="Health"></param>
public sealed record BlockType(
	string Name,
	Shape2D Shape,
	Mesh Mesh,
	Vector2[] PortPositions,
	Vector2[] PortNormals,
	BlockFeatures Features,
	int Scale,
	float Durability,
	float Density,
	float Mass,
	float Area,
	Vector2 CenterOfMass,
	float Health)
{
	public static BlockTypeBuilder Builder(string name, Shape2D shape)
	{
		return new BlockTypeBuilder(name, shape);
	}
	
	public class BlockTypeBuilder
	{
		private readonly string _name;
		private readonly Shape2D _shape;
		private int _scale = 1;
		private float _durability;
		private float _density;
		private float _mass;
		private float _health;
		private Vector2[]? _portPositions;
		private BlockFeatures _features;
		
		public BlockTypeBuilder(string name, Shape2D shape)
		{
			_name = name;
			_shape = shape;
		}
		
		public BlockTypeBuilder PortPositions(Vector2[] portPositions)
		{
			_portPositions = portPositions;
			return this;
		}

		public BlockTypeBuilder Scale(int scale)
		{
			if (scale < 1)
				throw new ArgumentOutOfRangeException(nameof(scale), "BlockType scale must be 1 or more.");
			_scale = scale;
			return this;
		}
		
		public BlockTypeBuilder Durability(float durability)
		{
			if (durability <= 0)
				throw new ArgumentOutOfRangeException(nameof(durability), "BlockType durability must be greater than 0.");
			_durability = durability;
			return this;
		}
		
		public BlockTypeBuilder Density(float density)
		{
			if (density <= 0)
				throw new ArgumentOutOfRangeException(nameof(density), "BlockType density must be greater than 0.");
			_density = density;
			return this;
		}
		
		public BlockTypeBuilder Mass(float mass)
		{
			if (mass <= 0)
				throw new ArgumentOutOfRangeException(nameof(mass), "BlockType mass must be greater than 0.");
			_mass = mass;
			return this;
		}
		
		public BlockTypeBuilder Health(float health)
		{
			if (health <= 0)
				throw new ArgumentOutOfRangeException(nameof(health), "BlockType health must be greater than 0.");
			_health = health;
			return this;
		}
		
		/// <summary>
		/// Sets the <see cref="BlockFeatures"/> for this block. Overwrites any features added with <see cref="AddFeatures"/>
		/// or any other builder methods that add features such as <see cref="Weak"/>.
		/// </summary>
		public BlockTypeBuilder Features(BlockFeatures features)
		{
			_features = features;
			return this;
		}
		
		public BlockTypeBuilder AddFeatures(BlockFeatures features)
		{
			_features |= features;
			return this;
		}

		private void SetFeatures(BlockFeatures features, bool enable)
		{
			if (enable)
				_features |= features;
			else
				_features &= ~features;
		}
		
		public BlockTypeBuilder Core(bool enable = true)
		{
			SetFeatures(BlockFeatures.Core, enable);
			return this;
		}
		
		public BlockTypeBuilder Weak(bool enable = true)
		{
			SetFeatures(BlockFeatures.Weak, enable);
			return this;
		}

		public BlockType Build()
		{
			// Scale shape
			Shape2D shape = (Shape2D) _shape.Duplicate();
			if (_scale != 1)
				shape = ShapeUtil.ScaleShape(shape, new Vector2(_scale, _scale));
			
			// Make sure shape is centered
			shape = ShapeUtil.CenterShape2D(shape);
			
			// Create mesh from shape
			Mesh mesh = ShapeUtil.Shape2DToMesh(shape);

			// Port positions
			Vector2[] shapePolygon = ShapeUtil.Shape2DToPolygon(shape);
			int portCount = shapePolygon.Length * _scale;
			Vector2[] portPositions;
			if (_portPositions == null)
			{
				// Calculate port positions based on shape and scale. Ports are evenly spaced. The number of ports on
				// each side of a block is equal to the scale.
				portPositions = new Vector2[portCount];
				for (int i = 0; i < shapePolygon.Length; i++)
				{
					for (int j = 0; j < _scale; j++)
					{
						float ratio = 1f / _scale * (j + 1) - 1f / _scale / 2;
						Vector2 portPosition = PolygonUtil.PolygonPointAlongSide(shapePolygon, i, ratio);
						portPositions[i * _scale + j] = portPosition;
					}
				}
			}
			else
			{
				// Port positions must be cloned because the builder could be used again, in which case we don't want
				// two block types to share the same _portPositions array reference.
				portPositions = (Vector2[]) _portPositions.Clone();
			}
			
			// Port normals
			Vector2[] portNormals = new Vector2[portCount];
			for (int i = 0; i < shapePolygon.Length; i++)
			{
				for (int j = 0; j < _scale; j++)
				{
					Vector2 portNormal = PolygonUtil.PolygonSideNormal(shapePolygon, i);
					portNormals[i * _scale + j] = portNormal;
				}
			}

			float area = ShapeUtil.Shape2DArea(shape);
			Vector2 centerOfMass = PolygonUtil.PolygonCentroid(shapePolygon);
			
			// If density or mass is missing, then one is used to specify the other. If both are missing, throw.
			float mass = _mass;
			float density = _density;
			if (density != 0 && mass == 0)
				mass = density * area;
			else if (density == 0 && mass != 0)
				density = mass / area;
			else
				throw new Exception("Must specify either density or mass in order to build BlockType.");
			
			// If durability is specified, overwrite health. Otherwise, health sets durability. If health isn't set either, throw.
			float health = _health;
			float durability = _durability;
			if (durability != 0)
				health = durability * area;
			else if (health != 0)
				durability = health / area;
			else
				throw new Exception("Must specify either durability or health in order to build BlockType.");
			
			return new BlockType(_name, shape, mesh, portPositions, portNormals, _features, _scale, durability, density, mass, area, centerOfMass, health);
		}
	}
}

// TODO: Turn this class into a struct.
public class Block
{
	public int BlockTypeId;
	public Transform2D Transform = Transform2D.Identity;
	public float Health;
	public bool Disabled = true;
	public readonly BlockPortPair?[] Links;

	public Block(int blockTypeId, World world)
	{
		BlockTypeId = blockTypeId;
		BlockType blockType = world.BlockTypes[blockTypeId];
		Health = blockType.Health;
		Links = new BlockPortPair[blockType.PortPositions.Length];
	}
}

[Flags] public enum BlockFeatures
{
	/// <summary>
	/// This block can be used to control a cluster.
	/// </summary>
	Core = 1,
	/// <summary>
	/// Weak blocks are destroyed when one of their neighbours is destroyed.
	/// </summary>
	Weak = 2,
}
