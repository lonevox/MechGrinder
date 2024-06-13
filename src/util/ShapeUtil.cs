using System;
using Godot;

namespace MechGrinder.Util;

public static class ShapeUtil
{
	/// <summary>
	/// Creates an appropriate mesh depending on the type of Shape2D.
	/// Only works with RectangleShape2D, ConvexPolygonShape2D, or ConcavePolygonShape2D.
	/// </summary>
	public static Mesh Shape2DToMesh(Shape2D shape)
	{
		switch (shape.GetClass())
		{
			case "RectangleShape2D":
				return RectangleShape2DToMesh((shape as RectangleShape2D)!);
			case "ConvexPolygonShape2D":
				ConvexPolygonShape2D convexShape = (shape as ConvexPolygonShape2D)!;
				return PolygonUtil.ConvexPolygonToMesh(convexShape.Points);
			case "ConcavePolygonShape2D":
				ConcavePolygonShape2D concaveShape = (shape as ConcavePolygonShape2D)!;
				return PolygonUtil.PolygonToMesh(concaveShape.Segments);
			default:
				throw new ArgumentException("Invalid Shape2D type: The given shape is of type '" + shape.GetClass() +
					"'. The only valid types are 'RectangleShape2D', 'ConvexPolygonShape2D', and 'ConcavePolygonShape2D'.");
		}
	}
	
	/// <summary>
	/// Only works with RectangleShape2D, ConvexPolygonShape2D, or ConcavePolygonShape2D.
	/// </summary>
	public static Vector2[] Shape2DToPolygon(Shape2D shape)
	{
		switch (shape.GetClass())
		{
			case "RectangleShape2D":
				RectangleShape2D rectangleShape = (shape as RectangleShape2D)!;
				return PolygonUtil.RectanglePolygon(rectangleShape.Size);
			case "ConvexPolygonShape2D":
				ConvexPolygonShape2D convexShape = (shape as ConvexPolygonShape2D)!;
				return convexShape.Points;
			case "ConcavePolygonShape2D":
				ConcavePolygonShape2D concaveShape = (shape as ConcavePolygonShape2D)!;
				return PolygonUtil.PolygonSegmentsToPoints(concaveShape.Segments);
			default:
				throw new ArgumentException("Invalid Shape2D type: The given shape is of type '" + shape.GetClass() +
				                            "'. The only valid types are 'RectangleShape2D', 'ConvexPolygonShape2D', and 'ConcavePolygonShape2D'.");
		}
	}
	
	/// <summary>
	/// Only works with RectangleShape2D, ConvexPolygonShape2D, or ConcavePolygonShape2D.
	/// </summary>
	public static Shape2D ScaleShape(Shape2D shape, Vector2 scale)
	{
		switch (shape.GetClass())
		{
			case "RectangleShape2D":
				return ScaleRectangleShape2D((shape as RectangleShape2D)!, scale);
			case "ConvexPolygonShape2D":
				return ScaleConvexPolygonShape2D((shape as ConvexPolygonShape2D)!, scale);
			case "ConcavePolygonShape2D":
				return ScaleConcavePolygonShape2D((shape as ConcavePolygonShape2D)!, scale);
			default:
				throw new ArgumentException("Invalid Shape2D type: The given shape is of type '" + shape.GetClass() +
				                            "'. The only valid types are 'RectangleShape2D', 'ConvexPolygonShape2D', and 'ConcavePolygonShape2D'.");
		}
	}
	
	/// <summary>
	/// Scales the given RectangleShape2D.
	/// </summary>
	public static RectangleShape2D ScaleRectangleShape2D(RectangleShape2D shape, Vector2 scale)
	{
		shape.Size *= scale;
		return shape;
	}
	
	/// <summary>
	/// Scales the given ConvexPolygonShape2D.
	/// </summary>
	public static ConvexPolygonShape2D ScaleConvexPolygonShape2D(ConvexPolygonShape2D shape, Vector2 scale)
	{
		shape.Points = PolygonUtil.TransformPolygon(shape.Points, Transform2D.Identity.Scaled(scale));
		return shape;
	}
	
	/// <summary>
	/// Scales the given ConcavePolygonShape2D.
	/// </summary>
	public static ConcavePolygonShape2D ScaleConcavePolygonShape2D(ConcavePolygonShape2D shape, Vector2 scale)
	{
		Vector2[] polygon = PolygonUtil.PolygonSegmentsToPoints(shape.Segments);
		Vector2[] scaledPolygon = PolygonUtil.TransformPolygon(polygon, Transform2D.Identity.Scaled(scale));
		shape.Segments = PolygonUtil.PolygonPointsToSegments(scaledPolygon);
		return shape;
	}
	
	/// <summary>
	/// Transforms the given Shape2D to its center. If you know your shape is a ConvexPolygonShape2D or ConcavePolygonShape2D,
	/// prefer calling CenterPolygonShape2D.
	/// Doesn't work with shapes with no center, i.e. SegmentShape2D, SeparationRayShape2D, or WorldBoundaryShape2D.
	/// Note that RectangleShape2D, CircleShape2D, and CapsuleShape2D are already centered, so this function simply
	/// returns those shapes unchanged.
	/// </summary>
	public static Shape2D CenterShape2D(Shape2D shape)
	{
		switch (shape.GetClass())
		{
			// These shapes are already centered
			case "RectangleShape2D":
			case "CircleShape2D":
			case "CapsuleShape2D":
				return shape;
			// Polygon shapes need to be centered
			case "ConvexPolygonShape2D":
			case "ConcavePolygonShape2D":
				return CenterPolygonShape2D(shape);
			default:
				throw new ArgumentException("Invalid Shape2D type: The given shape is of type '" + shape.GetClass() + "'.");
		}
	}

	public static Shape2D CenterPolygonShape2D(Shape2D polygonShape)
	{
		switch (polygonShape.GetClass())
		{
			case "ConvexPolygonShape2D":
				ConvexPolygonShape2D convexShape = (polygonShape as ConvexPolygonShape2D)!;
				convexShape.Points = CenterAndTranslatePolygonShape(polygonShape);
				return convexShape;
			case "ConcavePolygonShape2D":
				ConcavePolygonShape2D concaveShape = (polygonShape as ConcavePolygonShape2D)!;
				Vector2[] concaveShapeVertices = CenterAndTranslatePolygonShape(polygonShape);
				concaveShape.Segments = PolygonUtil.PolygonPointsToSegments(concaveShapeVertices);
				return concaveShape;
			default:
				throw new ArgumentException("Invalid Shape2D type: The given shape is of type '" + polygonShape.GetClass() +
				                            "'. The only valid types are 'ConvexPolygonShape2D', and 'ConcavePolygonShape2D'.");
		}

		Vector2[] CenterAndTranslatePolygonShape(Shape2D shape)
		{
			Vector2[] polygon = Shape2DToPolygon(shape);
			Vector2 polygonCenter = PolygonUtil.PolygonCenter(polygon);
			Vector2[] polygonTranslated = PolygonUtil.TranslatePolygon(polygon, -polygonCenter);
			return polygonTranslated;
		}
	}

	public static Mesh CircleShape2DToMesh(CircleShape2D shape, int radialSegments)
	{
		Vector2[] polygon = PolygonUtil.RegularConvexPolygon(radialSegments, shape.Radius);
		return PolygonUtil.PolygonToMesh(polygon);
	}

	public static Mesh RectangleShape2DToMesh(RectangleShape2D shape)
	{
		return new QuadMesh
		{
			Size = shape.Size
		};
	}

	public static Mesh ConvexPolygonShape2DToMesh(ConvexPolygonShape2D shape)
	{
		return PolygonUtil.PolygonToMesh(shape.Points);
	}

	public static Mesh ConcavePolygonShape2DToMesh(ConcavePolygonShape2D shape)
	{
		Vector2[] polygon = PolygonUtil.PolygonSegmentsToPoints(shape.Segments);
		return PolygonUtil.PolygonToMesh(polygon);
	}

	public static void DrawShape(Rid canvasItem, Shape2D shape, Color color, Vector2 position)
	{
		switch (shape.GetClass())
		{
			case "RectangleShape2D":
				DrawRectangleShape(canvasItem, (shape as RectangleShape2D)!, color, position);
				break;
			case "ConvexPolygonShape2D":
				DrawConvexPolygonShape(canvasItem, (shape as ConvexPolygonShape2D)!, color, position);
				break;
			case "ConcavePolygonShape2D":
				DrawConcavePolygonShape(canvasItem, (shape as ConcavePolygonShape2D)!, color, position);
				break;
			default:
				throw new ArgumentException("Invalid Shape2D type: The given shape is of type '" + shape.GetClass() +
					"'. The only valid types are 'RectangleShape2D', 'ConvexPolygonShape2D', and 'ConcavePolygonShape2D'.");
		}
	}

	public static void DrawRectangleShape(Rid canvasItem, RectangleShape2D shape, Color color, Vector2 position)
	{
		Rect2 rect = new Rect2(position, shape.Size);
		RenderingServer.CanvasItemAddRect(canvasItem, rect, color);
	}

	public static void DrawConvexPolygonShape(Rid canvasItem, ConvexPolygonShape2D shape, Color color, Vector2 position)
	{
		PolygonUtil.DrawPolygonAtPosition(canvasItem, shape.Points, color, position);
	}

	public static void DrawConcavePolygonShape(Rid canvasItem, ConcavePolygonShape2D shape, Color color, Vector2 position)
	{
		Vector2[] polygon = PolygonUtil.PolygonSegmentsToPoints(shape.Segments);
		PolygonUtil.DrawPolygonAtPosition(canvasItem, polygon, color, position);
	}

	public static float Shape2DArea(Shape2D shape)
	{
		switch (shape.GetClass())
		{
			case "RectangleShape2D":
				return RectangleShape2DArea((shape as RectangleShape2D)!);
			case "ConvexPolygonShape2D":
				return ConvexPolygonShape2DArea((shape as ConvexPolygonShape2D)!);
			case "ConcavePolygonShape2D":
				return ConcavePolygonShape2DArea((shape as ConcavePolygonShape2D)!);
			default:
				throw new ArgumentException("Invalid Shape2D type: The given shape is of type '" + shape.GetClass() +
				                            "'. The only valid types are 'RectangleShape2D', 'ConvexPolygonShape2D', and 'ConcavePolygonShape2D'.");
		}
	}

	public static float RectangleShape2DArea(RectangleShape2D shape)
	{
		return shape.Size.X * shape.Size.Y;
	}
	
	public static float ConvexPolygonShape2DArea(ConvexPolygonShape2D shape)
	{
		return PolygonUtil.PolygonToArea(shape.Points);
	}
	
	public static float ConcavePolygonShape2DArea(ConcavePolygonShape2D shape)
	{
		return PolygonUtil.PolygonToArea(PolygonUtil.PolygonSegmentsToPoints(shape.Segments));
	}
}
