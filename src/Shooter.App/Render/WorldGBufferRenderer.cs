using System.Numerics;
using MapEditor.Core.Entities;
using Shooter.Game;
using Silk.NET.OpenGL;

namespace Shooter.Render;

/// <summary>Writes opaque world-brush material data into the deferred GBuffer.
/// Water and other special surfaces remain on specialized forward paths for now.</summary>
public sealed class WorldGBufferRenderer : IDisposable
{
    private readonly GL _gl;
    private readonly ShaderProgram _shader;
    private readonly TextureCache _textures;
    private readonly IReadOnlyDictionary<Guid, GlMesh> _brushMeshes;

    public WorldGBufferRenderer(GL gl, GameWorld world, IReadOnlyDictionary<Guid, GlMesh> brushMeshes)
    {
        _gl = gl;
        _brushMeshes = brushMeshes;
        _shader = new ShaderProgram(gl, Shaders.WorldVert, Shaders.WorldGBufferFrag);
        _textures = new TextureCache(gl);
    }

    public unsafe void Draw(Matrix4x4 view, Matrix4x4 viewProj, GameWorld world)
    {
        _gl.Enable(EnableCap.DepthTest);
        _gl.Enable(EnableCap.CullFace);
        _gl.CullFace(TriangleFace.Back);
        _gl.FrontFace(FrontFaceDirection.Ccw);

        _shader.Use();
        UploadMatrix(_shader.U("uViewProj"), viewProj);
        UploadMatrix(_shader.U("uView"), view);
        _gl.Uniform1(_shader.U("uBaseColor"), 0);
        _gl.Uniform1(_shader.U("uRoughnessMap"), 2);
        _gl.Uniform1(_shader.U("uMetallicMap"), 3);
        _gl.Uniform1(_shader.U("uAoMap"), 6);

        foreach (var wb in world.Brushes)
        {
            if (wb.MaterialKind != BrushMaterialKind.Standard || wb.Opacity < 0.99f)
                continue;
            if (!_brushMeshes.TryGetValue(wb.BrushId, out var glMesh))
                continue;

            UploadMatrix(_shader.U("uModel"), wb.Model);
            UploadMatrix(_shader.U("uNormalMat"), wb.NormalMatrix);
            _gl.Uniform3(_shader.U("uTint"), wb.TintColor.X, wb.TintColor.Y, wb.TintColor.Z);
            _gl.Uniform4(_shader.U("uMaterialParams"), wb.Roughness, wb.Metallic, wb.DetailNormalStrength, 1f);
            _gl.Uniform4(_shader.U("uMaterialFx0"), (float)wb.MaterialKind, wb.EmissiveStrength, wb.Opacity, wb.FresnelStrength);

            var material = _textures.GetMaterialSet(wb.TexturePath);
            _gl.ActiveTexture(TextureUnit.Texture0);
            _gl.BindTexture(TextureTarget.Texture2D, material.BaseColorHandle);
            _gl.ActiveTexture(TextureUnit.Texture2);
            _gl.BindTexture(TextureTarget.Texture2D, material.RoughnessHandle);
            _gl.ActiveTexture(TextureUnit.Texture3);
            _gl.BindTexture(TextureTarget.Texture2D, material.MetallicHandle);
            _gl.ActiveTexture(TextureUnit.Texture6);
            _gl.BindTexture(TextureTarget.Texture2D, material.AoHandle);
            _gl.Uniform1(_shader.U("uHasTexture"), _textures.HasTexture(wb.TexturePath) ? 1 : 0);
            _gl.Uniform1(_shader.U("uHasRoughnessMap"), material.HasRoughnessMap ? 1 : 0);
            _gl.Uniform1(_shader.U("uHasMetallicMap"), material.HasMetallicMap ? 1 : 0);
            _gl.Uniform1(_shader.U("uHasAoMap"), material.HasAoMap ? 1 : 0);

            _gl.ActiveTexture(TextureUnit.Texture0);
            glMesh.Bind();
            _gl.DrawElements(PrimitiveType.Triangles, (uint)glMesh.IndexCount, DrawElementsType.UnsignedInt, (void*)0);
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

    public void Dispose()
    {
        _textures.Dispose();
        _shader.Dispose();
    }
}
