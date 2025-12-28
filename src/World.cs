using System;
using System.Collections.Generic;
using System.Diagnostics;
using Godot;
using MechGrinder.Editor;
using MechGrinder.Util;

namespace MechGrinder;

public partial class World : Node2D
{
    /// <summary>
    ///     This list is the authority on the available BlockTypes. The index of each BlockType is its ID.
    /// </summary>
    public readonly List<BlockType> BlockTypes = new();

    private MechEditor _mechEditor;

    // Input actions
    private StringName _openEditorAction = "open_editor";

    public World()
    {
        RenderMode = RenderMode.MultiMesh;
    }

    [Export] public RenderMode RenderMode { get; set; }

    public override void _Ready()
    {
        base._Ready();

        Cluster.DebugVisiblePorts = true;
        Cluster.DebugCenterOfMass = true;

        // Get references to nodes
        _mechEditor = GetNode<MechEditor>("%MechEditor");

        GD.Print("creating world");
        var rectangleShape = new RectangleShape2D();
        rectangleShape.Size = new Vector2(1, 1);
        RotatableBlockTypeBuilder walkerCoreBlockTypeBuilder =
            new RotatableBlockTypeBuilder("WalkerCore", rectangleShape)
                .Core()
                .Density(5)
                .Durability(1);
        var walkerCoreBlockS1 = AddBlockType(walkerCoreBlockTypeBuilder.Build());
        var walkerCoreBlockS2 = AddBlockType(walkerCoreBlockTypeBuilder.Scale(2).Build());
        var walkerCoreBlockS3 = AddBlockType(walkerCoreBlockTypeBuilder.Scale(3).Build());
        BlockTypeBuilder squareHullBlockTypeBuilder = new BlockTypeBuilder("SquareHull", rectangleShape)
            .Density(1)
            .Durability(1);
        var squareHullBlockS1 = AddBlockType(squareHullBlockTypeBuilder.Build());
        var squareHullBlockS2 = AddBlockType(squareHullBlockTypeBuilder.Scale(2).Build());
        var squareHullBlockS3 = AddBlockType(squareHullBlockTypeBuilder.Scale(3).Build());
        Vector2[] trianglePolygon = { Vector2.Zero, new(1, 0), new(1, 1) };
        BlockTypeBuilder triHullBlockTypeBuilder =
            new BlockTypeBuilder("TriHull", new ConvexPolygonShape2D { Points = trianglePolygon })
                .Weak()
                .Density(1)
                .Durability(1);
        var triHullBlockS1 = AddBlockType(triHullBlockTypeBuilder.Build());
        var triHullBlockS2 = AddBlockType(triHullBlockTypeBuilder.Scale(2).Build());
        var triHullBlockS3 = AddBlockType(triHullBlockTypeBuilder.Scale(3).Build());
        var diamondBlock = AddBlockType(new BlockTypeBuilder("Diamond",
                new ConvexPolygonShape2D { Points = PolygonUtil.RegularConvexPolygon(4, 2.5f) })
            .Density(1).Durability(1).Build());

        // Mech mech = new Walker(this, new Block(0, this));
        // AddCluster(mech);
        // mech.ControlMode = ControlMode.Player;
        // mech.AddBlock(new Block(triHullBlockS1, this), 2, 0, 3);
        // mech.AddBlock(new Block(triHullBlockS2, this), 2, 1, 5);

        Mech mech = new Walker(this, new Block(walkerCoreBlockS1, this));
        AddCluster(mech);
        mech.ControlMode = ControlMode.Player;
        for (var i = 0; i < 10; i++) mech.AddBlock(new Block(squareHullBlockS1, this), 2, i, 0);

        var camera = new Camera2D();
        camera.Zoom = new Vector2(16, 16);
        mech.AddChild(camera);
    }

    public override void _Input(InputEvent @event)
    {
        base._Input(@event);
        if (@event.IsAction(_openEditorAction))
            _mechEditor.Visible = true;
    }

    public BlockType GetBlockType(Block block)
    {
        Debug.Assert(block.BlockTypeId < BlockTypes.Count);

        return BlockTypes[block.BlockTypeId];
    }

    public T GetBlockTypeAsType<T>(Block block) where T : BlockType
    {
        try
        {
            return (T)GetBlockType(block);
        }
        catch (InvalidCastException e)
        {
            throw new Exception($"Block's BlockType is not of type {typeof(T)}.", e);
        }
    }

    public int AddBlockType(BlockType blockType)
    {
        BlockTypes.Add(blockType);
        return BlockTypes.Count - 1;
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
    Canvas
}
