using System;
using System.Diagnostics;
using Godot;

namespace MechGrinder;

// TODO: This is currently unused and unfinished. Test if this is faster than ExpandableMultiMesh. I think that
// this will be slower until Engine/C# marshalling is made faster, due to this implementation accessing
// MultiMesh.Buffer frequently, which copies from the huge Godot array to a new C# array.
public class ExpandableMultiMeshWrapper
{
	public readonly MultiMesh MultiMesh;
	public int BufferSize
	{
		get => MultiMesh.InstanceCount * CalculateInstanceByteSize();
		private set
		{
			int instanceByteSize = CalculateInstanceByteSize();
			if (value < _usedInstanceCount * instanceByteSize)
			{
				throw new ArgumentOutOfRangeException(nameof(value), "BufferSize must be greater than UsedInstanceCount times the per-instance byte size.");
			}
			if (value != MultiMesh.InstanceCount * instanceByteSize)
			{
				if (value > 0)
				{
					float[] oldBuffer = MultiMesh.Buffer;
					MultiMesh.InstanceCount = value;
					float[] newBuffer = new float[MultiMesh.InstanceCount * instanceByteSize];
					Array.Copy(oldBuffer, newBuffer, MultiMesh.InstanceCount * instanceByteSize);
					MultiMesh.Buffer = newBuffer;
				}
				else
				{
					MultiMesh.Buffer = Array.Empty<float>();
				}
			}
		}
	}
	private int _usedInstanceCount;
	public int UsedInstanceCount
	{
		get => _usedInstanceCount;
		set
		{
			_usedInstanceCount = value;
			if (_usedInstanceCount * CalculateInstanceByteSize() > MultiMesh.Buffer.Length)
			{
				Expand(_usedInstanceCount);
			}
		}
	}

	public ExpandableMultiMeshWrapper(MultiMesh multiMesh)
	{
		MultiMesh = multiMesh;
	}

	private int CalculateInstanceByteSize()
	{
		int floatCount = 0;
		if (MultiMesh.TransformFormat == MultiMesh.TransformFormatEnum.Transform2D)
			floatCount += 8;
		else
			floatCount += 12;
		if (MultiMesh.UseColors)
			floatCount += 4;
		if (MultiMesh.UseCustomData)
			floatCount += 4;
		return floatCount * sizeof(float);
	}

	/// <summary>
	/// Expand the MultiMesh buffer to at least hold instanceCapacity number of instances.
	/// </summary>
	private void Expand(int instanceCapacity)
	{
		Debug.Assert(MultiMesh.InstanceCount < instanceCapacity);

		int instanceByteSize = CalculateInstanceByteSize();
		int newBufferSize = MultiMesh.InstanceCount * instanceByteSize * 2;

		// Allow the buffer to grow to maximum possible capacity (~2G elements) before encountering overflow.
		// Note that this check works even when newBufferSize overflowed thanks to the (uint) cast.
		if ((uint)newBufferSize > Array.MaxLength)
			newBufferSize = Array.MaxLength;
		
		// If the computed capacity is still less than specified, set to the original argument.
		// Capacities exceeding Array.MaxLength will be surfaced as OutOfMemoryException by Array.Resize.
		int instanceCapacityBytes = instanceCapacity * instanceByteSize;
		if (newBufferSize < instanceCapacityBytes)
			newBufferSize = instanceCapacityBytes;

		BufferSize = newBufferSize;
	}
}