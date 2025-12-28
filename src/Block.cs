using Godot;

namespace MechGrinder;

// TODO: Turn this class into a struct.
public class Block
{
    public readonly BlockPortPair?[] Links;
    public int BlockTypeId;
    public bool Disabled = true;
    public float Health;
    public Transform2D Transform = Transform2D.Identity;

    public Block(int blockTypeId, World world)
    {
        BlockTypeId = blockTypeId;
        var blockType = world.BlockTypes[blockTypeId];
        Health = blockType.Health;
        Links = new BlockPortPair[blockType.PortPositions.Length];
    }
}
