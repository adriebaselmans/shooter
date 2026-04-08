using System;
using System.Numerics;

Console.WriteLine("Standard model matrix composition should be T*R*S");
Console.WriteLine("This applies: Scale in object space, then Rotate, then Translate");
Console.WriteLine();

var testPoint = new Vector4(1f, 0f, 0f, 1f);
var scale = Matrix4x4.CreateScale(2f);
var rot = Matrix4x4.CreateRotationY(float.DegreesToRadians(90f));
var trans = Matrix4x4.CreateTranslation(10f, 0f, 0f);

Console.WriteLine("Code uses: S * R * T");
var currentImpl = scale * rot * trans;
var currentResult = Vector4.Transform(testPoint, currentImpl);
Console.WriteLine($"  Result: {currentResult}");

Console.WriteLine();
Console.WriteLine("Standard is: T * R * S");  
var standard = trans * rot * scale;
var standardResult = Vector4.Transform(testPoint, standard);
Console.WriteLine($"  Result: {standardResult}");

Console.WriteLine();
Console.WriteLine("Step-by-step for T*R*S:");
var step1 = Vector4.Transform(testPoint, scale);
Console.WriteLine($"  After scale: {step1}");
var step2 = Vector4.Transform(step1, rot);
Console.WriteLine($"  After rotate: {step2}");
var step3 = Vector4.Transform(step2, trans);
Console.WriteLine($"  After translate: {step3}");
