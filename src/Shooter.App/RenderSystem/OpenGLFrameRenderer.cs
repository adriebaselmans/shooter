using System.Numerics;
using Shooter.Game;
using Shooter.Render;

namespace Shooter.RenderSystem;

/// <summary>Owns the OpenGL frame render flow and pass ordering.</summary>
internal sealed class OpenGLFrameRenderer
{
    private Matrix4x4 _prevViewProj = Matrix4x4.Identity;

    public void Render(OpenGLRenderResources resources, double dt, RenderFrameData frame)
    {
        resources.EnsureFramebufferSized();

        var fb = resources.Window.FramebufferSize;
        float aspect = fb.Y > 0 ? (float)fb.X / fb.Y : 16f / 9f;
        var view = Matrix4x4.CreateLookAt(frame.Player.EyePosition, frame.Player.EyePosition + frame.Player.Forward(), Vector3.UnitY);
        var proj = Matrix4x4.CreatePerspectiveFieldOfView(WeaponViewmodelRenderer.FovYRadians, aspect, 0.05f, 1000f);

        // Apply TAA Jitter
        if (frame.Lighting.TaaEnabled)
        {
            var jitter = resources.Post.TaaPass.GetJitter();
            float jx = jitter.jx * 2f / fb.X;
            float jy = jitter.jy * 2f / fb.Y;
            proj.M31 += jx;
            proj.M32 += jy;
        }
        else
        {
            resources.Post.TaaPass.Initialized = false;
        }

        var viewProj = view * proj;

        if (frame.Lighting.ShadowsEnabled)
            RunShadowPass(resources, frame);
        RunScenePass(resources, frame, view, proj, viewProj, fb.X, fb.Y);
        RunPostPass(resources, frame, proj, dt, fb.X, fb.Y);
        RunHudPass(resources, frame, fb.X, fb.Y);
        UpdateDebugTitle(frame);

        _prevViewProj = viewProj;
        resources.Post.TaaPass.FrameIndex++;
    }

    private static void RunShadowPass(OpenGLRenderResources resources, RenderFrameData frame)
    {
        var lightSpace = resources.Lighting.ShadowMap.BuildLightSpace(frame.Player.Position, frame.Lighting);
        resources.Lighting.ShadowMap.RenderPass(frame.World.Brushes, (Dictionary<Guid, GlMesh>)resources.Scene.WorldRenderer.BrushMeshes, lightSpace);
    }

    private void RunScenePass(OpenGLRenderResources resources, RenderFrameData frame, Matrix4x4 view, Matrix4x4 proj, Matrix4x4 viewProj, int fbWidth, int fbHeight)
    {
        resources.Post.HdrTarget.Bind();
        resources.Gl.ClearColor(0f, 0f, 0f, 1f);
        resources.Gl.Clear(Silk.NET.OpenGL.ClearBufferMask.ColorBufferBit | Silk.NET.OpenGL.ClearBufferMask.DepthBufferBit);

        var viewNoTrans = view;
        viewNoTrans.M41 = 0f;
        viewNoTrans.M42 = 0f;
        viewNoTrans.M43 = 0f;

        resources.Scene.SkyRenderer.Draw(viewNoTrans, proj, frame.Lighting);
        resources.Scene.WorldRenderer.Draw(view, viewProj, _prevViewProj, frame.World, frame.Pickups, frame.Lighting, resources.Lighting.ShadowMap, resources.Lighting.IblProbe);
        resources.Scene.DecalRenderer.Draw(viewProj, frame.Holes);
        resources.Scene.ScorchRenderer.Draw(viewProj, frame.Scorches);
        resources.Scene.TracerRenderer.Draw(viewProj, frame.Tracers);
        resources.Scene.RocketRenderer.Draw(view, viewProj, _prevViewProj, frame.Rockets, frame.Lighting, resources.Lighting.ShadowMap, resources.Lighting.IblProbe, resources.Scene.WorldRenderer);
        resources.Scene.ParticleRenderer.Draw(viewProj, frame.Player.Right(), Vector3.Cross(frame.Player.Right(), frame.Player.Forward()), frame.Particles);
        resources.Scene.WeaponViewmodelRenderer.Draw(fbWidth, fbHeight, proj, frame.Weapons, frame.Lighting, resources.Lighting.ShadowMap, resources.Lighting.IblProbe, resources.Scene.WorldRenderer);
        if (frame.MuzzleFlash is not null)
            resources.Scene.MuzzleFlashRenderer.Draw(fbWidth, fbHeight, frame.MuzzleFlash);
    }

    private static void RunPostPass(OpenGLRenderResources resources, RenderFrameData frame, Matrix4x4 proj, double dt, int fbWidth, int fbHeight)
    {
        if (frame.Lighting.SsaoEnabled)
            resources.Post.SsaoPass.Run(resources.Post.HdrTarget.DepthTex, resources.Post.HdrTarget.NormalTex, proj, frame.Lighting.SsaoRadius, frame.Lighting.SsaoBias);
        if (frame.Lighting.BloomEnabled)
            resources.Post.Bloom.Run(resources.Post.HdrTarget.ColorTex);
            
        // TAA Resolve
        uint finalHdrTex = resources.Post.HdrTarget.ColorTex;
        if (frame.Lighting.TaaEnabled)
        {
            resources.Post.TaaPass.Resolve(resources.Post.HdrTarget.ColorTex, resources.Post.HdrTarget.VelocityTex);
            finalHdrTex = resources.Post.TaaPass.OutputTex;
        }

        resources.Post.AutoExposure.Run(finalHdrTex, frame.Lighting, (float)dt);
        resources.Post.PostFx.Draw(
            finalHdrTex,
            resources.Post.Bloom.OutputTex,
            resources.Post.SsaoPass.AoTex,
            frame.Lighting,
            frame.Lighting.BloomEnabled ? frame.Lighting.BloomStrength : 0f,
            frame.Lighting.SsaoEnabled ? frame.Lighting.SsaoStrength : 0f,
            resources.Post.AutoExposure.CurrentExposure,
            frame.Lighting.FxaaEnabled,
            fbWidth,
            fbHeight);
    }

    private static void RunHudPass(OpenGLRenderResources resources, RenderFrameData frame, int fbWidth, int fbHeight)
    {
        resources.Scene.HudRenderer.Draw(fbWidth, fbHeight, frame.Player, frame.Weapons, frame.Lighting, frame.MenuOpen, frame.MenuSelection);
    }

    private static void UpdateDebugTitle(RenderFrameData frame)
    {
        if (!frame.ShowDebug) return;
        Console.Title = $"Shooter [OpenGL] | pos={frame.Player.Position:F1} fps={frame.FpsValue:F0} tris={frame.World.AllTriangles.Count} holes={frame.Holes.Count} tracers={frame.Tracers.Active.Count}";
    }
}
