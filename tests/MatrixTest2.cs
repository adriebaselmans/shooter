using System;
using System.Numerics;

Console.WriteLine("Testing matrix composition for model transform:");
Console.WriteLine("We want: Scale first, then Rotate, then Translate (in object space)");
Console.WriteLine();

var testPoint = new Vector4(1f, 0f, 0f, 1f);
Console.WriteLine($"Original point: {testPoint}");

var scale = Matrix4x4.CreateScale(2f);
var rot = Matrix4x4.CreateRotationY(float.DegreesToRadians(90f));
var trans = Matrix4x4.CreateTranslation(10f, 0f, 0f);

// In object space: first scale, then rotate, then translate
// With row-major multiplication (like .NET), we compose RIGHT-TO-LEFT
// So: Transform = Translation * Rotation * Scale
// Then apply: result = point * Transform (or Transform * point for column vectors)

Console.WriteLine();
Console.WriteLine("Order 1: S * R * T (scale * rot * trans)");
var order1 = scale * rot * trans;
var result1 = Vector4.Transform(testPoint, order1);
Console.WriteLine($"  Result: {result1}");

Console.WriteLine();
Console.WriteLine("Order 2: T * R * S (trans * rot * scale)");
var order2 = trans * rot * scale;
var result2 = Vector4.Transform(testPoint, order2);
Console.WriteLine($"  Result: {result2}");

Console.WriteLine();
Console.WriteLine("Manual calculation for T*R*S:");
Console.WriteLine("  1. (1,0,0) * scale(2) = (2,0,0)");
Console.WriteLine("  2. (2,0,0) rotated 90deg around Y = (0,0,-2)");
Console.WriteLine("  3. (0,0,-2) + translate(10,0,0) = (10,0,-2)");
