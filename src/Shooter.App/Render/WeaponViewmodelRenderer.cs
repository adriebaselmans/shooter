using System.Numerics;
using Shooter.Game;
using Silk.NET.OpenGL;

namespace Shooter.Render;

/// <summary>Loads textured GLB weapon models and renders the currently-held one as a viewmodel.
/// Shares <see cref="MuzzleViewOffset"/> with gameplay code so tracer/rocket spawn points line
/// up with the rendered barrel tip.</summary>
public sealed class WeaponViewmodelRenderer : IDisposable
{
    /// <summary>View-space position of the held weapon's barrel tip (anchor).
    /// The aligned mesh has its barrel tip at model-local origin so translating by this places
    /// the tip exactly here.</summary>
    public static readonly Vector3 MuzzleViewOffset = new(0.20f, -0.16f, -0.55f);

    /// <summary>Vertical FOV (radians) shared by world and viewmodel projections so view-space
    /// points map to the same screen pixels in both passes.</summary>
    public const float FovYRadians = 75f * MathF.PI / 180f;

    private readonly GL _gl;
    private readonly TexturedModelRenderer _modelRen;
    private readonly Dictionary<WeaponKind, GpuModel> _models = new();

    public WeaponViewmodelRenderer(GL gl)
    {
        _gl = gl;
        _modelRen = new TexturedModelRenderer(gl);

        TryAdd(WeaponKind.Ak47, "ak47.glb");
        TryAdd(WeaponKind.Shotgun, "shotgun.glb");
        TryAdd(WeaponKind.RocketLauncher, "rocketlaucher.glb"); // sic in asset pack
    }

    private void TryAdd(WeaponKind kind, string glbFile)
    {
        var path = Path.Combine(AssetLocator.Root, "StylooGunsAssetPack", "GLB", glbFile);
        var data = ModelData.TryLoad(path);
        if (data is null) return;
        // Align longest axis with -Z and place the barrel tip at the model origin so anchoring
        // at MuzzleViewOffset puts the tip there.
        var aligned = data.AlignBarrelToForward(targetForwardLength: 0.55f);
        _models[kind] = GpuModel.Upload(_gl, aligned);
        int triCount = 0;
        foreach (var p in _models[kind].Primitives) triCount += p.Mesh.IndexCount / 3;
        Console.WriteLine($"[Viewmodel] Loaded {glbFile} for {kind} ({_models[kind].Primitives.Count} prims, {triCount} tris).");
    }

    public bool HasModel(WeaponKind kind) => _models.ContainsKey(kind);

    public void Draw(int fbWidth, int fbHeight, WeaponSystem weapons,
        LightingEnvironment env, ShadowMap shadow, IblProbe ibl, WorldRenderer worldRen)
    {
        var w = weapons.Current;
        if (!_models.TryGetValue(w.Def.Kind, out var model)) return;

        // Recoil kick: fraction of remaining cooldown, scaled by the weapon's RecoilStrength.
        float kick = 0f;
        if (w.Cooldown > 0 && w.Def.FireRateHz > 0)
        {
            float frac = w.Cooldown * w.Def.FireRateHz; // 0..1
            kick = MathF.Max(0f, frac) * w.Def.RecoilStrength;
        }

        float aspect = fbHeight > 0 ? (float)fbWidth / fbHeight : 16f / 9f;
        var proj = Matrix4x4.CreatePerspectiveFieldOfView(FovYRadians, aspect, 0.01f, 10f);

        var pos = MuzzleViewOffset + new Vector3(0f, -kick * 0.04f, 0f);
        var rot = Matrix4x4.CreateFromYawPitchRoll(0f, kick * 0.25f, 0f);
        var modelMat = rot * Matrix4x4.CreateTranslation(pos);

        // View-space rendering: shadow map lookups are nonsense here, so disable receive.
        // The view-space normal is also wrong for SSAO (the viewmodel lives in a different
        // projection), so writeNormal=false makes the shader emit 0 to leave the normal buffer
        // intact.
        _modelRen.BeginPass(Matrix4x4.Identity, proj, clearDepthFirst: true, env, shadow, ibl, worldRen,
            receiveShadows: false, writeNormal: false, viewSpaceLighting: true, applyFog: false,
            roughness: 0.28f, specularStrength: 0.24f);
        _modelRen.DrawModel(model, modelMat);
    }

    public void Dispose()
    {
        foreach (var m in _models.Values) m.Dispose();
        _modelRen.Dispose();
    }
}
