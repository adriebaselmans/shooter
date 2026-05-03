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
    private readonly uint _whiteTex;
    private readonly uint _flatNormalTex;
    public ShaderProgram Shader { get; }

    public TexturedModelRenderer(GL gl)
    {
        _gl = gl;
        Shader = new ShaderProgram(gl, Shaders.WorldVert, Shaders.TexturedModelFrag);
        _whiteTex = CreateSolidTexture([255, 255, 255, 255]);
        _flatNormalTex = CreateSolidTexture([128, 128, 255, 255]);
    }

    /// <summary>Begins a draw pass with the textured shader bound, lighting/shadow/IBL uniforms
    /// configured. <paramref name="receiveShadows"/> = false disables shadow lookup entirely
    /// for this pass (used by the view-space weapon viewmodel).</summary>
    public void BeginPass(Matrix4x4 view, Matrix4x4 viewProj, bool clearDepthFirst, LightingEnvironment env,
        ShadowMap shadow, IblProbe ibl, WorldRenderer worldRen, bool receiveShadows, bool writeNormal,
        bool viewSpaceLighting, bool applyFog, float roughness, float specularStrength)
    {
        Matrix4x4.Invert(view, out var invView);
        var cameraPos = new Vector3(invView.M41, invView.M42, invView.M43);
        if (clearDepthFirst) _gl.Clear(ClearBufferMask.DepthBufferBit);
        _gl.Disable(EnableCap.CullFace);
        _gl.Enable(EnableCap.DepthTest);
        Shader.Use();
        worldRen.BindLighting(Shader, env, shadow, ibl, cameraPos, view);
        UploadMatrix(Shader.U("uViewProj"), viewProj);
        UploadMatrix(Shader.U("uView"), view);
        _gl.Uniform1(Shader.U("uReceiveShadows"), receiveShadows ? 1 : 0);
        _gl.Uniform1(Shader.U("uWriteNormal"), writeNormal ? 1 : 0);
        _gl.Uniform1(Shader.U("uViewSpaceLighting"), viewSpaceLighting ? 1 : 0);
        _gl.Uniform1(Shader.U("uApplyFog"), applyFog ? 1 : 0);
        _gl.Uniform1(Shader.U("uBaseColor"), 0); // texture unit 0
        _gl.Uniform1(Shader.U("uNormalMap"), 1);
        _gl.Uniform1(Shader.U("uRoughnessMap"), 2);
        _gl.Uniform1(Shader.U("uAoMap"), 3);
        _gl.Uniform1(Shader.U("uHasNormalMap"), 0);
        _gl.Uniform1(Shader.U("uHasRoughnessMap"), 0);
        _gl.Uniform1(Shader.U("uHasAoMap"), 0);
        _gl.Uniform4(Shader.U("uMaterialParams"), roughness, specularStrength, 0.0f, applyFog ? 1f : 0f);
        _gl.Uniform4(Shader.U("uMaterialFx0"), 0f, 0f, 1f, 0f);
        _gl.Uniform4(Shader.U("uMaterialFx1"), 0f, 0f, 0f, 0f);
        _gl.ActiveTexture(TextureUnit.Texture1);
        _gl.BindTexture(TextureTarget.Texture2D, _flatNormalTex);
        _gl.ActiveTexture(TextureUnit.Texture2);
        _gl.BindTexture(TextureTarget.Texture2D, _whiteTex);
        _gl.ActiveTexture(TextureUnit.Texture3);
        _gl.BindTexture(TextureTarget.Texture2D, _whiteTex);
        _gl.ActiveTexture(TextureUnit.Texture0);
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
                _gl.Uniform2(Shader.U("uTexelSize"), 1f / Math.Max(1, tex.Width), 1f / Math.Max(1, tex.Height));
            }
            else
            {
                _gl.Uniform1(Shader.U("uHasTexture"), 0);
                _gl.Uniform2(Shader.U("uTexelSize"), 0f, 0f);
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

    private uint CreateSolidTexture(byte[] pixel)
    {
        uint handle = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, handle);
        unsafe
        {
            fixed (byte* p = pixel)
            {
                _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8, 1, 1, 0, PixelFormat.Rgba, PixelType.UnsignedByte, p);
            }
        }
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.Repeat);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.Repeat);
        return handle;
    }

    public void Dispose()
    {
        _gl.DeleteTexture(_whiteTex);
        _gl.DeleteTexture(_flatNormalTex);
        Shader.Dispose();
    }
}
