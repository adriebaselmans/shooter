using System.Numerics;
using Shooter.Game;
using Silk.NET.OpenGL;

namespace Shooter.Render;

/// <summary>Renders an aligned, textured GLB model with HDR-linear lit shading.
/// Used by the held weapon viewmodel (no shadows — view-space lookups are wrong) and rocket
/// projectiles (shadowed). Lighting uniforms are bound via <see cref="WorldRenderer.BindLighting"/>.</summary>
public sealed class TexturedModelRenderer : IDisposable
{
    private readonly GL _gl;
    public ShaderProgram Shader { get; }

    public TexturedModelRenderer(GL gl)
    {
        _gl = gl;
        Shader = new ShaderProgram(gl, Shaders.WorldVert, Shaders.TexturedModelFrag);
    }

    /// <summary>Begins a draw pass with the textured shader bound, lighting/shadow/IBL uniforms
    /// configured. <paramref name="receiveShadows"/> = false disables shadow lookup entirely
    /// for this pass (used by the view-space weapon viewmodel).</summary>
    public void BeginPass(Matrix4x4 view, Matrix4x4 viewProj, bool clearDepthFirst, LightingEnvironment env,
        ShadowMap shadow, IblProbe ibl, WorldRenderer worldRen, bool receiveShadows, bool writeNormal)
    {
        if (clearDepthFirst) _gl.Clear(ClearBufferMask.DepthBufferBit);
        _gl.Disable(EnableCap.CullFace);
        _gl.Enable(EnableCap.DepthTest);
        Shader.Use();
        worldRen.BindLighting(Shader, env, shadow, ibl);
        UploadMatrix(Shader.U("uViewProj"), viewProj);
        UploadMatrix(Shader.U("uView"), view);
        _gl.Uniform1(Shader.U("uReceiveShadows"), receiveShadows ? 1 : 0);
        _gl.Uniform1(Shader.U("uWriteNormal"), writeNormal ? 1 : 0);
        _gl.Uniform1(Shader.U("uBaseColor"), 0); // texture unit 0
    }

    public unsafe void DrawModel(GpuModel model, Matrix4x4 modelMatrix)
    {
        Matrix4x4.Invert(modelMatrix, out var inv);
        var normalMat = Matrix4x4.Transpose(inv);
        UploadMatrix(Shader.U("uModel"), modelMatrix);
        UploadMatrix(Shader.U("uNormalMat"), normalMat);

        foreach (var prim in model.Primitives)
        {
            var f = prim.BaseColorFactor;
            _gl.Uniform4(Shader.U("uBaseColorFactor"), f.X, f.Y, f.Z, f.W);
            if (prim.BaseColor is { } tex)
            {
                tex.Bind(0);
                _gl.Uniform1(Shader.U("uHasTexture"), 1);
            }
            else
            {
                _gl.Uniform1(Shader.U("uHasTexture"), 0);
            }
            prim.Mesh.Bind();
            _gl.DrawElements(PrimitiveType.Triangles, (uint)prim.Mesh.IndexCount, DrawElementsType.UnsignedInt, (void*)0);
        }
        _gl.BindVertexArray(0);
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

    public void Dispose() => Shader.Dispose();
}
