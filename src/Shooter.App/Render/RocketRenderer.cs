using System.Numerics;
using Shooter.Game;
using Silk.NET.OpenGL;

namespace Shooter.Render;

/// <summary>Renders all in-flight rockets as oriented, textured GLB models.</summary>
public sealed class RocketRenderer : IDisposable
{
    private readonly TexturedModelRenderer _modelRen;
    private readonly GpuModel? _model;
    /// <summary>World-space length the rocket is scaled to.</summary>
    public const float RocketLength = 0.7f;

    public RocketRenderer(GL gl)
    {
        _modelRen = new TexturedModelRenderer(gl);
        var path = Path.Combine(AssetLocator.Root, "StylooGunsAssetPack", "GLB", "quadrocket.glb");
        var data = ModelData.TryLoad(path);
        if (data is null) { _model = null; return; }
        // Same alignment as weapons: longest axis -> -Z, front face at origin, scaled.
        var aligned = data.AlignBarrelToForward(targetForwardLength: RocketLength);
        _model = GpuModel.Upload(gl, aligned);
        Console.WriteLine($"[Rocket] Loaded quadrocket.glb ({_model.Primitives.Count} prims).");
    }

    public void Draw(Matrix4x4 viewProj, RocketSystem rockets)
    {
        if (_model is null || rockets.Active.Count == 0) return;

        _modelRen.BeginPass(viewProj, clearDepthFirst: false);
        foreach (var r in rockets.Active)
        {
            // Build a basis aligned with the rocket's forward direction. Aligned mesh treats
            // -Z as forward, so we map model-(+X,+Y,-Z) to world-(right, up, fwd).
            Vector3 fwd = r.Forward;
            if (fwd.LengthSquared() < 1e-6f) fwd = Vector3.UnitZ;
            fwd = Vector3.Normalize(fwd);
            Vector3 worldUp = MathF.Abs(fwd.Y) < 0.95f ? Vector3.UnitY : Vector3.UnitX;
            Vector3 right = Vector3.Normalize(Vector3.Cross(worldUp, fwd));
            Vector3 up = Vector3.Cross(fwd, right);
            // Rotation matrix (column-major in Matrix4x4): basis columns are right, up, -fwd.
            var rot = new Matrix4x4(
                right.X, right.Y, right.Z, 0f,
                up.X, up.Y, up.Z, 0f,
                -fwd.X, -fwd.Y, -fwd.Z, 0f,
                0f, 0f, 0f, 1f);
            var model = rot * Matrix4x4.CreateTranslation(r.Position);
            _modelRen.DrawModel(_model, model);
        }
    }

    public void Dispose() { _model?.Dispose(); _modelRen.Dispose(); }
}
