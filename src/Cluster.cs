using System;
using System.Collections.Generic;
using System.Diagnostics;
using Godot;
using MechGrinder.Util;

namespace MechGrinder;

public partial class Cluster : RigidBody2D
{
	/// <summary>
	/// This is the number of mesh instances each MultiMesh will be created with.
	/// </summary>
	private const int InitialMultiMeshInstanceCapacity = 8;
	/// <summary>
	/// This is the number of floats of each instance within the cluster's MultiMeshes.
	/// See <see cref="RenderingServer.MultimeshSetBuffer"/> for an explanation of MultiMesh instance data.
	/// </summary>
	private const int MultiMeshInstanceFloatCount = 8;
	
	private static readonly float[] InitialMultiMeshBuffer = new float[InitialMultiMeshInstanceCapacity * MultiMeshInstanceFloatCount];
	/// <summary>
	/// Empty transform used for hiding mesh instances.
	/// </summary>
	private static readonly Transform2D ZeroTransform = new();
	
	private bool _debugVisiblePorts = true;
	private bool _debugCenterOfMass = true;

	public ControlMode ControlMode;
	// public readonly List<int> BlockIds = new List<int>();
	// public readonly List<Transform2D> BlockTransforms = new List<Transform2D>();
	private readonly List<Block> _blocks = new();
	/// <summary>
	/// The World that this Cluster exists in.
	/// </summary>
	[Export]
	public World? World;

	/// <summary>
	/// The RID of this body. Retrieved from <c>GetRid()</c> on construction.
	/// </summary>
	private readonly Rid _rid;
	/// <summary>
	/// Only <c>ConvexPolygonShape2D</c> shapes should be added to this shape owner.
	/// </summary>
	private readonly uint _shapeOwner;
	/// <summary>
	/// All unique block types of blocks that have been added to the cluster.
	/// </summary>
	private readonly HashSet<int> _usedBlockTypes = new();
	/// <summary>
	/// The cluster's multi meshes, keyed by BlockTypeID.
	/// </summary>
	private readonly Dictionary<int, ExpandableMultiMesh> _expandableMultiMeshes = new();
	/// <summary>
	/// Each _expandableMultiMeshes mesh instances, keyed by block ID.
	/// </summary>
	private readonly Dictionary<ExpandableMultiMesh, Dictionary<int, int>> _expandableMultiMeshInstances = new();

	private bool _freezeGraph;

	public Cluster()
	{
		ContactMonitor = true;
		MaxContactsReported = 8;
		CenterOfMassMode = CenterOfMassModeEnum.Custom;
		_rid = GetRid();
		_shapeOwner = CreateShapeOwner(this);
	}

	public Cluster(World world, Block initialBlock) : this()
	{
		World = world;
		AddBlock(initialBlock);
	}

	public override void _PhysicsProcess(double delta)
	{
		base._PhysicsProcess(delta);
		if (ControlMode == ControlMode.Player)
		{
			Vector2 inputDirection = Input.GetVector("ui_left", "ui_right", "ui_up", "ui_down");
			ApplyCentralForce(inputDirection * 500);
		}
	}

	public override void _IntegrateForces(PhysicsDirectBodyState2D state)
	{
		base._IntegrateForces(state);
		for (int i = 0; i < state.GetContactCount(); i++)
		{
			int blockId = state.GetContactLocalShape(i);
			Block block = _blocks[blockId];
			block.Health -= 1;
			if (block.Health <= 0)
				DisableBlock(blockId);
		}
	}

	public override void _Draw()
	{
		base._Draw();

		if (World == null) return;
		Rid canvasItem = GetCanvasItem();
		switch (World.RenderMode)
		{
			case RenderMode.MultiMesh:

				foreach (ExpandableMultiMesh multiMesh in _expandableMultiMeshes.Values)
				{
					RenderingServer.CanvasItemAddMultimesh(canvasItem, multiMesh.MultiMeshRid);
				}
				break;
			case RenderMode.Canvas:
				for (int i = 0; i < _blocks.Count; i++)
				{
					Shape2D shape = ShapeOwnerGetShape(_shapeOwner, i);
					Transform2D blockTransform = _blocks[i].Transform;
					ShapeUtil.DrawShape(canvasItem, shape, new Color(1, 0, 0), blockTransform.Origin);
				}
				break;
			default:
				throw new ArgumentException("Enum has an invalid value.", nameof(World.RenderMode));
		}
		
		if (_debugVisiblePorts)
		{
			for (int i = 0; i < _blocks.Count; i++)
			{
				Block block = _blocks[i];
				if (block.Disabled)
					continue;
				BlockType blockType = World.BlockTypes[block.BlockTypeId];
				for (int j = 0; j < block.Links.Length; j++)
				{
					Transform2D portTransform = block.Transform.TranslatedLocal(blockType.PortPositions[j]);
					Vector2 portPosition = portTransform.Origin;
					
					// Draw normal
					Vector2 portNormal = blockType.PortNormals[j];
					Vector2 portNormalEndPosition = portTransform.TranslatedLocal(portNormal * 5).Origin;
					DrawLine(portPosition, portNormalEndPosition, Colors.Aqua);
					
					// Draw port position
					if (block.Links[j] == null)
						DrawCircle(portPosition, 1.5f, Colors.Green);
					else
						DrawCircle(portPosition, 1.5f, Colors.Red);
				}
			}
		}

		if (_debugCenterOfMass)
			DrawCircle(CenterOfMass, 2, Colors.Black);
	}

	public override void _Notification(int what)
	{
		base._Notification(what);
		// Dispose of multi meshes when cluster object destroyed
		if (what == NotificationPredelete)
		{
			foreach (ExpandableMultiMesh multiMesh in _expandableMultiMeshes.Values)
				multiMesh.Dispose();
		}
	}

	/// <summary>
	/// NOTE: The given Block must already be considered valid before calling this.
	/// </summary>
	/// <param name="block"></param>
	private void AddBlock(Block block)
	{
		Debug.Assert(World != null, nameof(World) + " != null");
		
		int blockTypeId = block.BlockTypeId;
		Debug.Assert(blockTypeId < World.BlockTypes.Count, "Can't add block: The given block's BlockTypeID must be an ID that exists in the cluster's World.");
		
		int blockId = _blocks.Count;
		_blocks.Add(block);
		
		// Add a MultiMesh to the cluster if the BlockType hasn't been seen before
		BlockType blockType = World.BlockTypes[blockTypeId];
		if (_usedBlockTypes.Add(blockTypeId))
			AddMultiMesh(blockTypeId, blockType.Mesh);
		// Add the block's mesh
		ExpandableMultiMesh multiMesh = _expandableMultiMeshes[block.BlockTypeId];
		multiMesh.InstanceCount += 1;
		_expandableMultiMeshInstances[multiMesh][blockId] = multiMesh.InstanceCount - 1;
		
		// Add the block's shape to the cluster
		ShapeOwnerAddShape(_shapeOwner, blockType.Shape);
		PhysicsServer2D.BodySetShapeTransform(_rid, blockId, block.Transform);
		
		EnableBlock(blockId);
	}

	/// <summary>
	/// Adds a new Block to the cluster.
	/// The block's BlockTypeID must be an ID that exists in this Cluster's World.
	/// </summary>
	public void AddBlock(Block block, int port, int toBlockId, int toPort)
	{
		Debug.Assert(World != null, nameof(World) + " != null");
		
		int blockTypeId = block.BlockTypeId;
		if (blockTypeId >= World.BlockTypes.Count)
			throw new ArgumentException("Can't add block: The given block's BlockTypeID must be an ID that exists in the cluster's World.");
		
		if (_blocks.Count <= toBlockId)
			throw new ArgumentOutOfRangeException(nameof(toBlockId), "Can't add block: There is no block in the cluster with a block ID of " + toBlockId + ".");
		Block toBlock = _blocks[toBlockId];
		
		// Throw if block can't connect to given port
		if (toBlock.Links.Length <= toPort)
			throw new ArgumentOutOfRangeException(nameof(toPort), "Can't add block: Block with ID '" + toBlockId + "' doesn't have port '" + toPort + "'.");
		if (toBlock.Links[toPort] != null)
			throw new Exception("Can't add block: Port '" + toPort + "' of Block with ID '" + toBlockId + "' is in use.");
		
		// Link blocks
		block.Links[port] = new BlockPortPair(toBlockId, toPort);
		toBlock.Links[toPort] = new BlockPortPair(_blocks.Count, port);

		// Transform block based on ports
		BlockType blockType = World.BlockTypes[blockTypeId];
		BlockType toBlockType = World.BlockTypes[toBlock.BlockTypeId];
		Vector2 blockPortPosition = blockType.PortPositions[port];
		Vector2 toBlockPortPosition = toBlockType.PortPositions[toPort];
		Vector2 blockPosition = toBlockPortPosition - blockPortPosition;
		Vector2 blockPortNormal = blockType.PortNormals[port];
		Vector2 toBlockPortNormal = toBlockType.PortNormals[toPort];
		float blockRotation = toBlockPortNormal.AngleTo(blockPortNormal) % Mathf.Pi;
		// TODO: This could almost certainly be done with less transforms. This is just the first thing that worked.
		block.Transform = Transform2D.Identity
			.Translated(blockPosition)
			.Translated(-toBlockPortPosition)
			.Rotated(blockRotation)
			.Translated(toBlockPortPosition)
			.TranslatedLocal(toBlock.Transform.Origin)
			.Rotated(toBlock.Transform.Rotation);
		
		AddBlock(block);
	}

	private void AddMultiMesh(int blockTypeId, Mesh mesh)
	{
		ExpandableMultiMesh multiMesh = new ExpandableMultiMesh(InitialMultiMeshBuffer);
		multiMesh.SetMesh(mesh);
		_expandableMultiMeshes.Add(blockTypeId, multiMesh);
		_expandableMultiMeshInstances[multiMesh] = new Dictionary<int, int>();
	}
	
	public void EnableBlock(int blockId)
	{
		Block block = _blocks[blockId];
		if (!block.Disabled)
			return;
		block.Disabled = false;
		
		// Enable collision shape
		PhysicsServer2D.BodySetShapeDisabled(_rid, blockId, false);
		
		UpdateCenterOfMass();
		
		// Show multi mesh instance
		ExpandableMultiMesh multiMesh = _expandableMultiMeshes[block.BlockTypeId];
		int multiMeshInstance = _expandableMultiMeshInstances[multiMesh][blockId];
		RenderingServer.MultimeshInstanceSetTransform2D(multiMesh.MultiMeshRid, multiMeshInstance, block.Transform);
		QueueRedraw();
	}

	public void DisableBlock(int blockId)
	{
		Block block = _blocks[blockId];
		if (block.Disabled)
			return;
		block.Disabled = true;
		
		// Disable collision shape
		CallDeferred(MethodName.ShapeSetDisabled, blockId, true);
		
		UpdateCenterOfMass();
		
		// Hide multi mesh instance
		ExpandableMultiMesh multiMesh = _expandableMultiMeshes[block.BlockTypeId];
		int multiMeshInstance = _expandableMultiMeshInstances[multiMesh][blockId];
		RenderingServer.MultimeshInstanceSetTransform2D(multiMesh.MultiMeshRid, multiMeshInstance, ZeroTransform);
		QueueRedraw();
	}

	private void ShapeSetDisabled(int shapeIdx, bool disabled)
	{
		PhysicsServer2D.BodySetShapeDisabled(_rid, shapeIdx, disabled);
	}

	private void UpdateCenterOfMass()
	{
		Debug.Assert(World != null, nameof(World) + " != null");
		
		Vector2 centerOfMass = Vector2.Zero;
		float totalMass = 0;
		for (int i = 0; i < _blocks.Count; i++)
		{
			Block block = _blocks[i];
			if (block.Disabled)
				continue;
			
			BlockType blockType = World.BlockTypes[block.BlockTypeId];
			Vector2 blockCenterOfMass = block.Transform.Origin + blockType.CenterOfMass;
			centerOfMass += blockCenterOfMass * blockType.Mass;
			totalMass += blockType.Mass;
		}
		if (centerOfMass != Vector2.Zero)
			centerOfMass /= totalMass;
		
		CenterOfMass = centerOfMass;
	}
}

public enum ControlMode
{
	None,
	Player,
	Ai,
}