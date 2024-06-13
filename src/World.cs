using System.Collections.Generic;
using Godot;
using MechGrinder.Util;

namespace MechGrinder;

public partial class World : Node2D
{
	/// <summary>
	/// This list is the authority on the available BlockTypes. The index of each BlockType is its ID.
	/// </summary>
	public readonly List<BlockType> BlockTypes = new();
	
	[Export]
	public RenderMode RenderMode { get; set; }

	public World()
	{
		RenderMode = RenderMode.MultiMesh;
	}

	public override void _Ready()
	{
		base._Ready();
		GD.Print("creating world");
		RectangleShape2D rectangleShape = new RectangleShape2D();
		rectangleShape.Size = new Vector2(10, 10);
		BlockType.BlockTypeBuilder commandBlockTypeBuilder = BlockType.Builder("Command", rectangleShape)
			.Density(5)
			.Durability(1);
		AddBlockType(commandBlockTypeBuilder.Build());
		AddBlockType(commandBlockTypeBuilder.Scale(2).Build());
		AddBlockType(commandBlockTypeBuilder.Scale(3).Build());
		Vector2[] trianglePolygon = { Vector2.Zero, new(10, 0), new(10, 10) };
		BlockType.BlockTypeBuilder triHullBlockTypeBuilder = BlockType.Builder("TriHull", new ConvexPolygonShape2D { Points = trianglePolygon })
			.Density(1)
			.Durability(1);
		AddBlockType(triHullBlockTypeBuilder.Build());
		AddBlockType(triHullBlockTypeBuilder.Scale(2).Build());
		AddBlockType(triHullBlockTypeBuilder.Scale(3).Build());
		AddBlockType(BlockType.Builder("Diamond", new ConvexPolygonShape2D { Points = PolygonUtil.RegularConvexPolygon(4, 2.5f) }).Density(1).Durability(1).Build());

		Cluster cluster = new Cluster(this, new Block(0, this));
		AddCluster(cluster);
		cluster.ControlMode = ControlMode.Player;
		cluster.AddBlock(new Block(4, this), 2, 0, 2);
		cluster.AddBlock(new Block(5, this), 3, 1, 1);

		Camera2D camera = new Camera2D();
		camera.Zoom = new Vector2(2, 2);
		cluster.AddChild(camera);
	}

	public void AddBlockType(BlockType blockType)
	{
		BlockTypes.Add(blockType);
	}

	private void AddCluster(Cluster cluster)
	{
		cluster.World = this;
		AddChild(cluster);
	}
}

public enum RenderMode
{
	MultiMesh,
	Canvas,
}
