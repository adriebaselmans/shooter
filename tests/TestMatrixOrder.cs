using System;
using System.Numerics;

var point = new Vector4(1f, 0f, 0f, 1f);
var scale = Matrix4x4.CreateScale(2f);
var trans = Matrix4x4.CreateTranslation(10f, 0f, 0f);

Console.WriteLine("=== Matrix Multiplication Order Test ===\n");

Console.WriteLine("Test: scale * trans on point (1,0,0)");
var m1 = scale * trans;
var result1 = Vector4.Transform(point, m1);
Console.WriteLine($"Result: {result1}\n");

Console.WriteLine("Test: trans * scale on point (1,0,0)");
var m2 = trans * scale;
var result2 = Vector4.Transform(point, m2);
Console.WriteLine($"Result: {result2}\n");

Console.WriteLine("=== Selection Outline Problem ===\n");
Console.WriteLine("Brush at position (100,0,0) with scale 2.0");
Console.WriteLine("Local vertex at (5,0,0)\n");

var brushModel = Matrix4x4.CreateScale(2f) * Matrix4x4.CreateTranslation(100f, 0f, 0f);
var localVertex = new Vector4(5f, 0f, 0f, 1f);
var worldPos = Vector4.Transform(localVertex, brushModel);
Console.WriteLine($"Normal rendering: vertex at {worldPos}");

var outlineModel = Matrix4x4.CreateScale(1.02f) * brushModel;
var outlinePos = Vector4.Transform(localVertex, outlineModel);
Console.WriteLine($"Outline rendering: vertex at {outlinePos}");

var diff = new Vector3(
    outlinePos.X - worldPos.X,
    outlinePos.Y - worldPos.Y,
    outlinePos.Z - worldPos.Z);
Console.WriteLine($"Difference: {diff}");
Console.WriteLine($"Distance: {diff.Length()}");
