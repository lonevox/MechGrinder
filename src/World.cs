using System.Collections.Generic;
using Godot;
using MechGrinder.Editor;
using MechGrinder.Util;

namespace MechGrinder;

public partial class World : Node2D
{
	/// <summary>
	/// This list is the authority on the available BlockTypes. The index of each BlockType is its ID.
	/// </summary>
	public readonly List<BlockType> BlockTypes = new();
	
	// Input actions
	private StringName _openEditorAction = "open_editor";
	
	[Export]
	public RenderMode RenderMode { get; set; }

	private MechEditor _mechEditor;

	public World()
	{
		RenderMode = RenderMode.MultiMesh;
	}

	public override void _Ready()
	{
		base._Ready();

		Cluster.DebugVisiblePorts = true;
		Cluster.DebugCenterOfMass = true;
		
		// Get references to nodes
		_mechEditor = GetNode<MechEditor>("%MechEditor");
		
		GD.Print("creating world");
		RectangleShape2D rectangleShape = new RectangleShape2D();
		rectangleShape.Size = new Vector2(10, 10);
		BlockType.BlockTypeBuilder coreBlockTypeBuilder = BlockType.Builder("Core", rectangleShape)
			.Core()
			.Density(5)
			.Durability(1);
		AddBlockType(coreBlockTypeBuilder.Build());
		AddBlockType(coreBlockTypeBuilder.Scale(2).Build());
		AddBlockType(coreBlockTypeBuilder.Scale(3).Build());
		Vector2[] trianglePolygon = { Vector2.Zero, new(10, 0), new(10, 10) };
		BlockType.BlockTypeBuilder triHullBlockTypeBuilder = BlockType.Builder("TriHull", new ConvexPolygonShape2D { Points = trianglePolygon })
			.Weak()
			.Density(1)
			.Durability(1);
		AddBlockType(triHullBlockTypeBuilder.Build());
		AddBlockType(triHullBlockTypeBuilder.Scale(2).Build());
		AddBlockType(triHullBlockTypeBuilder.Scale(3).Build());
		AddBlockType(BlockType.Builder("Diamond", new ConvexPolygonShape2D { Points = PolygonUtil.RegularConvexPolygon(4, 2.5f) }).Density(1).Durability(1).Build());

		Mech mech = new Walker(this, new Block(0, this));
		AddCluster(mech);
		mech.ControlMode = ControlMode.Player;
		// mech.AddBlock(new Block(4, this), 2, 0, 3);
		// mech.AddBlock(new Block(5, this), 2, 1, 5);

		Camera2D camera = new Camera2D();
		camera.Zoom = new Vector2(2, 2);
		mech.AddChild(camera);
	}

	public override void _Input(InputEvent @event)
	{
		base._Input(@event);
		if (@event.IsAction(_openEditorAction))
		{
			_mechEditor.Visible = true;
		}
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

	public void SetPlayerMech(Cluster cluster)
	{
		
	}
}

public enum RenderMode
{
	MultiMesh,
	Canvas,
}
