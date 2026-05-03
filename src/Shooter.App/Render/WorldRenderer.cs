using System.Numerics;
using MapEditor.Core.Entities;
using MapEditor.Core.Geometry;
using Shooter.Game;
using Silk.NET.OpenGL;

namespace Shooter.Render;

/// <summary>Renders the static world brushes plus pickup markers using HDR-linear lit shaders
/// with PCF-shadowed Lambert direct lighting and IBL ambient.</summary>
public sealed class WorldRenderer : IDisposable
{
    private readonly GL _gl;
    private readonly ShaderProgram _shader;
    private readonly TextureCache _textures;
    private readonly GlMesh _pickupCube;
    private readonly Dictionary<Guid, GlMesh> _brushMeshes = new();
    private readonly GameWorld _world;

    /// <summary>Exposed so the shadow pass can re-bind the same VBO/VAO as the lit pass.</summary>
    public IReadOnlyDictionary<Guid, GlMesh> BrushMeshes => _brushMeshes;

    public WorldRenderer(GL gl, GameWorld world)
    {
        _gl = gl;
        _world = world;
        // World brushes and pickups use the same lit shader. Pickup pass sets uSelfIllum > 0.
        _shader = new ShaderProgram(gl, Shaders.WorldVert, Shaders.WorldFrag);
        _textures = new TextureCache(gl);

        foreach (var wb in world.Brushes)
            _brushMeshes[wb.BrushId] = new GlMesh(gl, wb.Mesh);

        var cubeBrush = new Brush { Primitive = BrushPrimitive.Box, Transform = Transform.Identity };
        var cubeMesh = MeshGenerator.GenerateMesh(cubeBrush);
        _pickupCube = new GlMesh(gl, cubeMesh);
    }

    public unsafe void Draw(Matrix4x4 view, Matrix4x4 viewProj, GameWorld world, PickupSystem pickups,
        LightingEnvironment env, ShadowMap shadow, IblProbe ibl)
    {
        Matrix4x4.Invert(view, out var invView);
        var cameraPos = new Vector3(invView.M41, invView.M42, invView.M43);
        _gl.Enable(EnableCap.DepthTest);
        _gl.Enable(EnableCap.CullFace);
        _gl.CullFace(TriangleFace.Back);
        _gl.FrontFace(FrontFaceDirection.Ccw);

        _shader.Use();
        BindLighting(_shader, env, shadow, ibl, cameraPos, view);
        UploadMatrix(_shader.U("uViewProj"), viewProj);
        UploadMatrix(_shader.U("uView"), view);
        _gl.Uniform1(_shader.U("uReceiveShadows"), 1);
        _gl.Uniform1(_shader.U("uBaseColor"), 0);
        _gl.Uniform1(_shader.U("uNormalMap"), 1);
        _gl.Uniform1(_shader.U("uRoughnessMap"), 2);
        _gl.Uniform1(_shader.U("uAoMap"), 3);
        _gl.Uniform1(_shader.U("uSelfIllum"), 0f);

        foreach (var wb in world.Brushes)
        {
            var glMesh = _brushMeshes[wb.BrushId];
            UploadMatrix(_shader.U("uModel"), wb.Model);
            UploadMatrix(_shader.U("uNormalMat"), wb.NormalMatrix);
            _gl.Uniform3(_shader.U("uTint"), wb.TintColor.X, wb.TintColor.Y, wb.TintColor.Z);
            var material = _textures.GetMaterialSet(wb.TexturePath);
            _gl.ActiveTexture(TextureUnit.Texture0);
            _gl.BindTexture(TextureTarget.Texture2D, material.BaseColorHandle);
            _gl.ActiveTexture(TextureUnit.Texture1);
            _gl.BindTexture(TextureTarget.Texture2D, material.NormalHandle);
            _gl.ActiveTexture(TextureUnit.Texture2);
            _gl.BindTexture(TextureTarget.Texture2D, material.RoughnessHandle);
            _gl.ActiveTexture(TextureUnit.Texture3);
            _gl.BindTexture(TextureTarget.Texture2D, material.AoHandle);
            _gl.Uniform1(_shader.U("uHasTexture"), _textures.HasTexture(wb.TexturePath) ? 1 : 0);
            _gl.Uniform1(_shader.U("uHasNormalMap"), material.HasNormalMap ? 1 : 0);
            _gl.Uniform1(_shader.U("uHasRoughnessMap"), material.HasRoughnessMap ? 1 : 0);
            _gl.Uniform1(_shader.U("uHasAoMap"), material.HasAoMap ? 1 : 0);
            _gl.Uniform2(_shader.U("uTexelSize"), material.TexelSizeX, material.TexelSizeY);
            _gl.Uniform4(_shader.U("uMaterialParams"), wb.Roughness, wb.SpecularStrength, wb.DetailNormalStrength, 1f);
            _gl.Uniform4(_shader.U("uMaterialFx0"), (float)wb.MaterialKind, wb.EmissiveStrength, wb.Opacity, wb.FresnelStrength);
            _gl.Uniform4(_shader.U("uMaterialFx1"), wb.FlowSpeed.X, wb.FlowSpeed.Y, wb.DistortionStrength, wb.PulseStrength);
            _gl.ActiveTexture(TextureUnit.Texture0);
            glMesh.Bind();
            _gl.DrawElements(PrimitiveType.Triangles, (uint)glMesh.IndexCount, DrawElementsType.UnsignedInt, (void*)0);
        }

        // Pickups: same shader, bumped self-illum so they read as "objects of interest".
        _gl.Uniform1(_shader.U("uSelfIllum"), 0.45f);
        _gl.Uniform1(_shader.U("uHasTexture"), 0);
        _gl.Uniform1(_shader.U("uHasNormalMap"), 0);
        _gl.Uniform1(_shader.U("uHasRoughnessMap"), 0);
        _gl.Uniform1(_shader.U("uHasAoMap"), 0);
        _gl.Uniform2(_shader.U("uTexelSize"), 0f, 0f);
        _gl.Uniform4(_shader.U("uMaterialParams"), 0.68f, 0.18f, 0.0f, 1f);
        _gl.Uniform4(_shader.U("uMaterialFx0"), 0f, 0f, 1f, 0f);
        _gl.Uniform4(_shader.U("uMaterialFx1"), 0f, 0f, 0f, 0f);
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, _textures.GetOrWhite(null));
        _pickupCube.Bind();
        float baseY = MathF.Sin((float)Environment.TickCount / 600f) * 0.08f;
        foreach (var p in pickups.Active)
        {
            if (!p.Active) continue;
            var color = PickupSystem.ColorFor(p.Kind);
            var pos = p.Position + new Vector3(0, baseY + 0.5f, 0);
            var model = Matrix4x4.CreateScale(0.4f) * Matrix4x4.CreateRotationY(pickups.SpinAngle) * Matrix4x4.CreateTranslation(pos);
            UploadMatrix(_shader.U("uModel"), model);
            Matrix4x4.Invert(model, out var inv);
            UploadMatrix(_shader.U("uNormalMat"), Matrix4x4.Transpose(inv));
            _gl.Uniform3(_shader.U("uTint"), color.X, color.Y, color.Z);
            _gl.DrawElements(PrimitiveType.Triangles, (uint)_pickupCube.IndexCount, DrawElementsType.UnsignedInt, (void*)0);
        }

        _gl.BindVertexArray(0);
    }

    /// <summary>Binds shadow map and irradiance cube + uploads lighting uniforms onto the
    /// supplied shader. Reused by other lit renderers (textured model) so the call site stays
    /// in one place.</summary>
    public void BindLighting(ShaderProgram s, LightingEnvironment env, ShadowMap shadow, IblProbe ibl, Vector3 cameraPos, Matrix4x4 view)
    {
        // Texture unit 4 = shadow map, unit 5 = irradiance cube.
        _gl.ActiveTexture(TextureUnit.Texture4);
        _gl.BindTexture(TextureTarget.Texture2D, shadow.DepthTex);
        _gl.Uniform1(s.U("uShadowMap"), 4);

        _gl.ActiveTexture(TextureUnit.Texture5);
        _gl.BindTexture(TextureTarget.TextureCubeMap, ibl.IrradianceCube);
        _gl.Uniform1(s.U("uIrradiance"), 5);

        UploadMatrix(s.U("uLightSpace"), shadow.LightSpace);
        var d = Vector3.Normalize(env.SunDirection);
        _gl.Uniform3(s.U("uSunDir"), d.X, d.Y, d.Z);
        _gl.Uniform3(s.U("uSunColor"), env.SunColor.X, env.SunColor.Y, env.SunColor.Z);
        _gl.Uniform1(s.U("uSunIntensity"), env.SunIntensity);
        _gl.Uniform1(s.U("uIrradianceIntensity"), env.IrradianceIntensity);
        _gl.Uniform1(s.U("uShadowSoftness"), env.ShadowSoftness);
        _gl.Uniform3(s.U("uCameraPos"), cameraPos.X, cameraPos.Y, cameraPos.Z);
        var toSunView = Vector3.Normalize(Vector3.TransformNormal(env.ToSun, view));
        _gl.Uniform3(s.U("uToSunView"), toSunView.X, toSunView.Y, toSunView.Z);
        _gl.Uniform3(s.U("uFogColor"), env.FogColor.X, env.FogColor.Y, env.FogColor.Z);
        _gl.Uniform1(s.U("uFogDensity"), env.FogDensity);
        _gl.Uniform1(s.U("uFogStart"), env.FogStart);
        _gl.Uniform1(s.U("uFogHeightFalloff"), env.FogHeightFalloff);
        _gl.Uniform1(s.U("uFogBaseHeight"), env.FogBaseHeight);
        _gl.Uniform1(s.U("uTime"), Environment.TickCount / 1000f);

        _gl.ActiveTexture(TextureUnit.Texture0); // leave unit 0 active for downstream textures
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
        _textures.Dispose();
        _shader.Dispose();
    }
}
