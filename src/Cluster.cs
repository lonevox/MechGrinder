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

	/// <summary>
	/// The Block ID of the core block on this cluster. Only valid if <see cref="HasCoreBlock"/> is true.
	/// </summary>
	private int _coreBlock;
	public bool HasCoreBlock;
	public ControlMode ControlMode;
	
	private readonly List<Block> _blocks = new();
	/// <summary>
	/// These are IDs that were in use by blocks, but those blocks have since been removed with <see cref="RemoveBlock"/>.
	/// When adding a new block with <see cref="AddBlock"/>, these IDs are preferred to adding a new block ID.
	/// </summary>
	private readonly Stack<int> _freeBlockIds = new();
	
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
	/// Counts for all unique block types of blocks that have been added to the cluster.
	/// </summary>
	private readonly Dictionary<int, int> _usedBlockTypeCounts = new();
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
		BlockSetup(initialBlock, 0);
	}

	public override void _PhysicsProcess(double delta)
	{
		base._PhysicsProcess(delta);
		if (HasCoreBlock && ControlMode == ControlMode.Player)
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
				DestroyBlock(blockId);
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
	/// Add block with the given block ID.
	/// NOTE: The given Block must already be considered valid before calling this. The ID must also be available.
	/// </summary>
	/// <param name="block"></param>
	/// <param name="blockId"></param>
	private void BlockSetup(Block block, int blockId)
	{
		Debug.Assert(World != null, nameof(World) + " != null");
		
		int blockTypeId = block.BlockTypeId;
		BlockType blockType = World.BlockTypes[blockTypeId];

		if (blockId == _blocks.Count)
		{
			_blocks.Add(block);
			ShapeOwnerAddShape(_shapeOwner, blockType.Shape);
		}
		else
		{
			_blocks[blockId] = block;
			PhysicsServer2D.BodySetShape(_rid, blockId, blockType.Shape.GetRid());
		}
		PhysicsServer2D.BodySetShapeTransform(_rid, blockId, block.Transform);
		
		// Add a MultiMesh to the cluster if the BlockType hasn't been seen before
		if (_usedBlockTypeCounts.TryAdd(blockTypeId, 0))
			AddMultiMesh(blockTypeId, blockType.Mesh);
		_usedBlockTypeCounts[blockTypeId] += 1;
		// Add the block's mesh
		ExpandableMultiMesh multiMesh = _expandableMultiMeshes[block.BlockTypeId];
		multiMesh.InstanceCount += 1;
		_expandableMultiMeshInstances[multiMesh][blockId] = multiMesh.InstanceCount - 1;
		
		if (blockType.Features.HasFlag(BlockFeatures.Core))
		{
			_coreBlock = blockId;
			HasCoreBlock = true;
		}
		
		EnableBlock(blockId);
	}

	/// <summary>
	/// Adds a new Block to the cluster.
	/// The block's BlockTypeID must be an ID that exists in this Cluster's World.
	/// </summary>
	public void AddBlock(Block block, int port, int toBlockId, int toPort)
	{
		Debug.Assert(World != null, nameof(World) + " != null");
		
		if (block.BlockTypeId >= World.BlockTypes.Count)
			throw new ArgumentException("Can't add block: The given block's BlockTypeID must be an ID that exists in the cluster's World.");
		
		if (_blocks.Count <= toBlockId)
			throw new ArgumentOutOfRangeException(nameof(toBlockId), "Can't add block: There is no block in the cluster with a block ID of " + toBlockId + ".");
		Block toBlock = _blocks[toBlockId];
		
		// Throw if block can't connect to given port
		if (toBlock.Links.Length <= toPort)
			throw new ArgumentOutOfRangeException(nameof(toPort), "Can't add block: Block with ID '" + toBlockId + "' doesn't have port '" + toPort + "'.");
		if (toBlock.Links[toPort] != null)
			throw new Exception("Can't add block: Port '" + toPort + "' of Block with ID '" + toBlockId + "' is in use.");
		
		BlockType blockType = World.BlockTypes[block.BlockTypeId];
		if (blockType.Features.HasFlag(BlockFeatures.Core) && HasCoreBlock)
			throw new Exception("Can't add block: Core block already exists on this cluster.");
		
		// Figure out the block's ID
		int blockId = _freeBlockIds.Count > 0 ? _freeBlockIds.Pop() : _blocks.Count;
		
		// Link blocks
		block.Links[port] = new BlockPortPair(toBlockId, toPort);
		toBlock.Links[toPort] = new BlockPortPair(blockId, port);

		// Transform block based on ports
		BlockType toBlockType = World.BlockTypes[toBlock.BlockTypeId];
		Vector2 blockPortPosition = blockType.PortPositions[port];
		Vector2 toBlockPortPosition = toBlockType.PortPositions[toPort];
		Vector2 blockPortNormal = blockType.PortNormals[port];
		Vector2 toBlockPortNormal = toBlockType.PortNormals[toPort];
		float blockRotation = -toBlockPortNormal.AngleTo(blockPortNormal) + MathF.PI;
		block.Transform = toBlock.Transform
			.TranslatedLocal(toBlockPortPosition)
			.RotatedLocal(blockRotation)
			.TranslatedLocal(-blockPortPosition);
		
		BlockSetup(block, blockId);
	}

	public void RemoveBlock(int blockId)
	{
		Debug.Assert(World != null, nameof(World) + " != null");
		
		Block block = _blocks[blockId];
		int blockTypeId = block.BlockTypeId;
		BlockType blockType = World.BlockTypes[blockTypeId];

		BlockCleanup(blockId);
		
		// Remove MultiMesh if it is no longer in use
		_usedBlockTypeCounts[blockTypeId] -= 1;
		if (_usedBlockTypeCounts[blockTypeId] == 0)
			RemoveMultiMesh(blockTypeId);
		
		// Unlink blocks
		for (int i = 0; i < block.Links.Length; i++)
		{
			BlockPortPair? portPair = block.Links[i];
			if (portPair != null)
			{
				Block toBlock = _blocks[portPair.BlockId];
				toBlock.Links[portPair.Port] = null;
			}
			block.Links[i] = null;
		}
		
		if (blockType.Features.HasFlag(BlockFeatures.Core))
			HasCoreBlock = false;
		
		_freeBlockIds.Push(blockId);
	}

	private void AddMultiMesh(int blockTypeId, Mesh mesh)
	{
		ExpandableMultiMesh multiMesh = new ExpandableMultiMesh(InitialMultiMeshBuffer);
		multiMesh.SetMesh(mesh);
		_expandableMultiMeshes.Add(blockTypeId, multiMesh);
		_expandableMultiMeshInstances[multiMesh] = new Dictionary<int, int>();
	}

	private void RemoveMultiMesh(int blockTypeId)
	{
		ExpandableMultiMesh multiMesh = _expandableMultiMeshes[blockTypeId];
		_expandableMultiMeshInstances.Remove(multiMesh);
		_expandableMultiMeshes.Remove(blockTypeId);
	}
	
	public void EnableBlock(int blockId)
	{
		Block block = _blocks[blockId];
		if (!block.Disabled)
			return;
		block.Disabled = false;
		
		// Enable collision shape
		CallDeferred(MethodName.SetShapeDisabled, blockId, false);
		
		UpdateCenterOfMass();

		SetBlockVisibility(blockId, true);
	}

	public void DisableBlock(int blockId)
	{
		Block block = _blocks[blockId];
		if (block.Disabled)
			return;
		
		BlockCleanup(blockId);
	}

	private void BlockCleanup(int blockId)
	{
		Block block = _blocks[blockId];
		block.Disabled = true;
		UpdateCenterOfMass();
		CallDeferred(MethodName.SetShapeDisabled, blockId, true);
		SetBlockVisibility(blockId, false);
	}

	public void DestroyBlock(int blockId)
	{
		Debug.Assert(World != null, nameof(World) + " != null");
		
		DisableBlock(blockId);
		
		// Disable all neighboring weak blocks
		Block block = _blocks[blockId];
		for (int i = 0; i < block.Links.Length; i++)
		{
			BlockPortPair? portPair = block.Links[i];
			if (portPair == null)
				continue;
			Block connectedBlock = _blocks[portPair.BlockId];
			BlockType connectedBlockType = World.BlockTypes[connectedBlock.BlockTypeId];
			if (connectedBlockType.Features.HasFlag(BlockFeatures.Weak))
				DisableBlock(portPair.BlockId);
		}
	}

	private void SetBlockVisibility(int blockId, bool visible)
	{
		
		Block block = _blocks[blockId];
		ExpandableMultiMesh multiMesh = _expandableMultiMeshes[block.BlockTypeId];
		int multiMeshInstance = _expandableMultiMeshInstances[multiMesh][blockId];
		RenderingServer.MultimeshInstanceSetTransform2D(multiMesh.MultiMeshRid, multiMeshInstance,
			visible ? block.Transform : ZeroTransform);
		QueueRedraw();
	}

	private void SetShapeDisabled(int shapeIdx, bool disabled)
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