using System.Numerics;
using MapEditor.Core.Entities;
using MapEditor.Core.Geometry;
using Shooter.Game;
using Silk.NET.OpenGL;

namespace Shooter.Render;

/// <summary>Renders the static world brushes plus pickup markers.</summary>
public sealed class WorldRenderer : IDisposable
{
    private readonly GL _gl;
    private readonly ShaderProgram _shader;
    private readonly ShaderProgram _pickupShader;
    private readonly GlMesh _pickupCube;
    private readonly Dictionary<Guid, GlMesh> _brushMeshes = new();
    private readonly GameWorld _world;

    public WorldRenderer(GL gl, GameWorld world)
    {
        _gl = gl;
        _world = world;
        _shader = new ShaderProgram(gl, Shaders.WorldVert, Shaders.WorldFrag);
        _pickupShader = new ShaderProgram(gl, Shaders.PickupVert, Shaders.PickupFrag);

        foreach (var wb in world.Brushes)
            _brushMeshes[wb.BrushId] = new GlMesh(gl, wb.Mesh);

        // Unit cube mesh for pickups (scaled in shader via uModel).
        var cubeBrush = new Brush { Primitive = BrushPrimitive.Box, Transform = Transform.Identity };
        var cubeMesh = MeshGenerator.GenerateMesh(cubeBrush);
        _pickupCube = new GlMesh(gl, cubeMesh);
    }

    public unsafe void Draw(Matrix4x4 viewProj, GameWorld world, PickupSystem pickups)
    {
        _gl.Enable(EnableCap.DepthTest);
        _gl.Enable(EnableCap.CullFace);
        _gl.CullFace(TriangleFace.Back);
        _gl.FrontFace(FrontFaceDirection.Ccw);

        _shader.Use();
        SetSceneUniforms(_shader, viewProj, world);
        foreach (var wb in world.Brushes)
        {
            var glMesh = _brushMeshes[wb.BrushId];
            UploadMatrix(_shader.U("uModel"), wb.Model);
            UploadMatrix(_shader.U("uNormalMat"), wb.NormalMatrix);
            _gl.Uniform3(_shader.U("uTint"), wb.TintColor.X, wb.TintColor.Y, wb.TintColor.Z);
            glMesh.Bind();
            _gl.DrawElements(PrimitiveType.Triangles, (uint)glMesh.IndexCount, DrawElementsType.UnsignedInt, (void*)0);
        }

        // Pickups
        _pickupShader.Use();
        SetSceneUniforms(_pickupShader, viewProj, world);
        _pickupCube.Bind();
        float baseY = MathF.Sin((float)Environment.TickCount / 600f) * 0.08f;
        foreach (var p in pickups.Active)
        {
            if (!p.Active) continue;
            var color = PickupSystem.ColorFor(p.Kind);
            var pos = p.Position + new Vector3(0, baseY + 0.5f, 0);
            var model = Matrix4x4.CreateScale(0.4f) * Matrix4x4.CreateRotationY(pickups.SpinAngle) * Matrix4x4.CreateTranslation(pos);
            UploadMatrix(_pickupShader.U("uModel"), model);
            Matrix4x4.Invert(model, out var inv);
            UploadMatrix(_pickupShader.U("uNormalMat"), Matrix4x4.Transpose(inv));
            _gl.Uniform3(_pickupShader.U("uTint"), color.X, color.Y, color.Z);
            _gl.DrawElements(PrimitiveType.Triangles, (uint)_pickupCube.IndexCount, DrawElementsType.UnsignedInt, (void*)0);
        }

        _gl.BindVertexArray(0);
    }

    private void SetSceneUniforms(ShaderProgram s, Matrix4x4 vp, GameWorld w)
    {
        UploadMatrix(s.U("uViewProj"), vp);
        _gl.Uniform3(s.U("uAmbient"), w.AmbientColor.X, w.AmbientColor.Y, w.AmbientColor.Z);
        _gl.Uniform3(s.U("uSunDir"), w.SunDirection.X, w.SunDirection.Y, w.SunDirection.Z);
        _gl.Uniform3(s.U("uSunColor"), w.SunColor.X, w.SunColor.Y, w.SunColor.Z);
    }

    private unsafe void UploadMatrix(int loc, Matrix4x4 m)
    {
        Span<float> data =
        [
            m.M11, m.M12, m.M13, m.M14,
            m.M21, m.M22, m.M23, m.M24,
            m.M31, m.M32, m.M33, m.M34,
            m.M41, m.M42, m.M43, m.M44,
        ];
        fixed (float* p = data)
            _gl.UniformMatrix4(loc, 1, false, p);
    }

    public void Dispose()
    {
        foreach (var m in _brushMeshes.Values) m.Dispose();
        _pickupCube.Dispose();
        _shader.Dispose();
        _pickupShader.Dispose();
    }
}
