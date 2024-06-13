using System;
using Godot;

namespace MechGrinder.Util;

public static class PolygonUtil
{
	public static Vector2[] PolygonSegmentsToPoints(Vector2[] segments)
	{
		Vector2[] points = new Vector2[segments.Length / 2];
		for (int i = 0; i < points.Length; i++)
			points[i] = segments[i * 2];
		return points;
	}

	public static Vector2[] PolygonPointsToSegments(Vector2[] points)
	{
		Vector2[] segments = new Vector2[points.Length * 2];
		for (int i = 0; i < segments.Length; i++)
		{
			if (i % 2 == 0) // Is even
				segments[i] = points[i / 2];
			else
				segments[i] = points[i - 1];
		}
		return segments;
	}

	public static Vector2[] PolygonToTriangleVertices(Vector2[] polygon)
	{
		int[] triangleIndices = Geometry2D.TriangulatePolygon(polygon);
		return PolygonVerticesAtIndices(polygon, triangleIndices);
	}

	/// <summary>
	/// Creates a mesh from a polygon. If you are sure that the polygon is convex, prefer <see cref="ConvexPolygonToMesh"/>
	/// as it creates a smaller mesh.
	/// </summary>
	public static Mesh PolygonToMesh(Vector2[] polygon)
	{
		Vector2[] triangleVertices = PolygonToTriangleVertices(polygon);
		return BasicMesh(Mesh.PrimitiveType.Triangles, triangleVertices);
	}

	public static Mesh ConvexPolygonToMesh(Vector2[] polygon)
	{
		Vector2[] triangleStripVertices = ConvexPolygonTriangleStripVertices(polygon);
		return BasicMesh(Mesh.PrimitiveType.TriangleStrip, triangleStripVertices);
	}

	/// <summary>
	/// Creates a basic mesh. It has a single surface which is rendered with the given primitive type and vertex array.
	/// </summary>
	private static Mesh BasicMesh(Mesh.PrimitiveType primitive, Vector2[] vertices)
	{
		Godot.Collections.Array meshArray = new Godot.Collections.Array();
		meshArray.Resize((int)Mesh.ArrayType.Max);
		meshArray[(int)Mesh.ArrayType.Vertex] = vertices;

		ArrayMesh mesh = new ArrayMesh();
		mesh.AddSurfaceFromArrays(primitive, meshArray);
		return mesh;
	}

	public static Vector2[] RegularConvexPolygon(int sides, float radius)
	{
		if (sides < 3)
			throw new ArgumentException("Regular convex polygon must have 3 or more sides.");
		
		float angleDelta = MathF.PI * 2 / sides;
		Vector2 vector = new Vector2(radius, 0);
		Vector2[] polygon = new Vector2[sides];
		for (int i = 0; i < sides; i++)
		{
			polygon[i] = vector;
			vector = vector.Rotated(angleDelta);
		}
		return polygon;
	}

	public static Vector2[] RectanglePolygon(Vector2 size)
	{
		float halfX = size.X / 2;
		float halfY = size.Y / 2;
		Vector2[] polygon = { new(-halfX, -halfY), new(halfX, -halfY), new(halfX, halfY), new(-halfX, halfY) };
		return polygon;
	}

	public static int[] ConvexPolygonTriangleStripIndices(Vector2[] polygon)
	{
		int[] triangleStripIndices = new int[polygon.Length];
		for (int i = 0; i < triangleStripIndices.Length; i++)
		{
			if (i % 2 == 0) // If even
				triangleStripIndices[i] = i / 2;
			else
				triangleStripIndices[i] = triangleStripIndices.Length - (i + 1) / 2;
		}
		return triangleStripIndices;
	}

	public static Vector2[] ConvexPolygonTriangleStripVertices(Vector2[] polygon)
	{
		int[] triangleStripIndices = ConvexPolygonTriangleStripIndices(polygon);
		return PolygonVerticesAtIndices(polygon, triangleStripIndices);
	}

	public static Vector2[] PolygonVerticesAtIndices(Vector2[] polygon, int[] indices)
	{
		Vector2[] vertices = new Vector2[indices.Length];
		for (int i = 0; i < vertices.Length; i++)
			vertices[i] = polygon[indices[i]];
		return vertices;
	}
	
	public static Vector2[] TransformPolygon(Vector2[] polygon, Transform2D transform)
	{
		Vector2[] transformedPolygon = new Vector2[polygon.Length];
		for (int i = 0; i < polygon.Length; i++)
		{
			Vector2 transformedPoint = transform.BasisXform(polygon[i]);
			transformedPoint += transform.Origin;
			transformedPolygon[i] = transformedPoint;
		}
		return transformedPolygon;
	}

	public static Vector2[] TranslatePolygon(Vector2[] polygon, Vector2 translation)
	{
		Vector2[] polygonTranslated = new Vector2[polygon.Length];
		for (int i = 0; i < polygon.Length; i++)
		{
			Vector2 point = polygon[i];
			Vector2 pointTranslated = point + translation;
			polygonTranslated[i] = pointTranslated;
		}
		return polygonTranslated;
	}

	public static void DrawPolygonAtPosition(Rid canvasItem, Vector2[] polygon, Color color, Vector2 position)
	{
		Vector2[] polygonTranslated = TranslatePolygon(polygon, position);
		RenderingServer.CanvasItemAddPolygon(canvasItem, polygonTranslated, new[] { color });
	}
	
	public static float TriangleToArea(Vector2 a, Vector2 b, Vector2 c)
	{
		return 0.5f * (a.X * (b.Y - c.Y) + b.X * (c.Y - a.Y) + c.X * (a.Y - b.Y));
	}

	/// <summary>
	/// Uses the <a href="https://en.wikipedia.org/wiki/Shoelace_formula">shoelace formula</a> to calculate the area of a polygon.
	/// </summary>
	public static float PolygonToArea(Vector2[] polygon)
	{
		int n = polygon.Length;
		float area = 0;
		for (int i = 0; i < n - 1; i++) {
			area += polygon[i].X * polygon[i + 1].Y - polygon[i + 1].X * polygon[i].Y;
		}
		return Math.Abs(area + polygon[n - 1].X * polygon[0].Y - polygon[0].X * polygon[n - 1].Y) / 2;
	}

	public static Vector2 PolygonPointAlongSide(Vector2[] polygon, int side, float ratio)
	{
		Vector2 sideStartPosition = polygon[side];
		Vector2 sideEndPosition = polygon[(side + 1) % polygon.Length];
		Vector2 point = sideStartPosition.Lerp(sideEndPosition, ratio);
		return point;
	}
	
	public static float PolygonSideLength(Vector2[] polygon, int side)
	{
		Vector2 sideStartPosition = polygon[side];
		Vector2 sideEndPosition = polygon[(side + 1) % polygon.Length];
		float sideLength = sideStartPosition.DistanceTo(sideEndPosition);
		return sideLength;
	}
	
	public static Vector2 PolygonSideNormal(Vector2[] polygon, int side)
	{
		Vector2 sideStartPosition = polygon[side];
		Vector2 sideEndPosition = polygon[(side + 1) % polygon.Length];
		Vector2 sideVector = sideStartPosition - sideEndPosition;
		Vector2 normal = sideVector.Rotated(Mathf.Pi / 2).Normalized();
		return normal;
	}

	/// <summary>
	/// The average location of all vertices in the polygon. For convex polygons this will always be within the polygon,
	/// but for concave polygons the center may be outside.
	/// </summary>
	public static Vector2 PolygonCenter(Vector2[] polygon)
	{
		Vector2 summedPoints = Vector2.Zero;
		for (int i = 0; i < polygon.Length; i++)
			summedPoints += polygon[i];
		Vector2 center = summedPoints / polygon.Length;
		return center;
	}
	
	public static Vector2 PolygonCentroid(Vector2[] polygon)
	{
		Vector2 centroid = Vector2.Zero;
		float signedArea = 0;

		int lastIndex = polygon.Length - 1;
		Vector2 prev = polygon[lastIndex];
		
		for (int i = 0; i < polygon.Length; ++i)
		{
			Vector2 next = polygon[i];
			float x0 = prev.X; // Current vertex X
			float y0 = prev.Y; // Current vertex Y
			float x1 = next.X; // Next vertex X
			float y1 = next.Y; // Next vertex Y
			float a = x0 * y1 - x1 * y0;  // Partial signed area
			signedArea += a;
			centroid.X += (x0 + x1) * a;
			centroid.Y += (y0 + y1) * a;
			prev = next;
		}

		signedArea *= 0.5f;
		centroid.X /= (6 * signedArea);
		centroid.Y /= (6 * signedArea);

		return centroid;
	}
}
