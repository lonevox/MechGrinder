using System;
using System.Diagnostics;
using Godot;

namespace MechGrinder;

public partial class Mech : Cluster
{
    /// <summary>
    /// This block ID is reserved for the core block. With this, you can access the core block in <see cref="Cluster.Blocks"/>.
    /// </summary>
    private const int CoreBlockId = 0;

    public ControlMode ControlMode;

    protected Vector2 InputDirection;
    protected Vector2 TargetDirection;
    
    public Mech(World world, Block initialBlock) : base(world, initialBlock)
    {
        Debug.Assert(World != null, nameof(World) + " != null");
        
        BlockType initialBlockType = World.BlockTypes[initialBlock.BlockTypeId];
        if (!initialBlockType.Features.HasFlag(BlockFeatures.Core))
            throw new Exception("Can't make Mech: Initial block must have Core feature.");
    }
    
    public override void _IntegrateForces(PhysicsDirectBodyState2D state)
    {
        base._IntegrateForces(state);
        for (int i = 0; i < state.GetContactCount(); i++)
        {
            int blockId = state.GetContactLocalShape(i);
            Block block = Blocks[blockId];
            block.Health -= 1;
            if (block.Health <= 0)
                DestroyBlock(blockId);
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        base._UnhandledInput(@event);
        if (ControlMode == ControlMode.Player)
        {
            InputDirection = Input.GetVector("ui_left", "ui_right", "ui_up", "ui_down");
            if (@event is InputEventMouseMotion)
                TargetDirection = Position.DirectionTo(GetGlobalMousePosition());
        }
    }

    public void DestroyBlock(int blockId)
    {
        Debug.Assert(World != null, nameof(World) + " != null");
		
        DisableBlock(blockId);
		
        // Disable all neighboring weak blocks
        Block block = Blocks[blockId];
        for (int i = 0; i < block.Links.Length; i++)
        {
            BlockPortPair? portPair = block.Links[i];
            if (portPair == null)
                continue;
            Block connectedBlock = Blocks[portPair.BlockId];
            BlockType connectedBlockType = World.BlockTypes[connectedBlock.BlockTypeId];
            if (connectedBlockType.Features.HasFlag(BlockFeatures.Weak))
                DisableBlock(portPair.BlockId);
        }
    }

    public override void AddBlock(Block block, int port, int toBlockId, int toPort)
    {
        Debug.Assert(World != null, nameof(World) + " != null");
        
        BlockType blockType = World.BlockTypes[block.BlockTypeId];
        if (blockType.Features.HasFlag(BlockFeatures.Core))
            throw new Exception("Can't add block: Core block already exists on this mech.");
        
        base.AddBlock(block, port, toBlockId, toPort);
    }

    public override void RemoveBlock(int blockId)
    {
        Debug.Assert(World != null, nameof(World) + " != null");

        if (blockId == CoreBlockId)
            throw new ArgumentException("Can't remove Core block from Mech.");
        
        base.RemoveBlock(blockId);
    }
}

public enum ControlMode
{
    None,
    Player,
    Ai,
}
