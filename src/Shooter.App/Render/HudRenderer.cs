using System.Numerics;
using Shooter.Game;
using Silk.NET.OpenGL;

namespace Shooter.Render;

/// <summary>2D HUD: crosshair, health bar, ammo bar, weapon slots. Coordinates are clip-space (-1..1).</summary>
public sealed class HudRenderer : IDisposable
{
    private readonly GL _gl;
    private readonly ShaderProgram _shader;
    private readonly DynamicPosBuffer _buffer;

    public HudRenderer(GL gl)
    {
        _gl = gl;
        _shader = new ShaderProgram(gl, Shaders.HudVert, Shaders.HudFrag);
        _buffer = new DynamicPosBuffer(gl, componentsPerVertex: 2);
    }

    public unsafe void Draw(int viewportWidth, int viewportHeight, Player player, WeaponSystem weapons)
    {
        _gl.Disable(EnableCap.DepthTest);
        _gl.Disable(EnableCap.CullFace);
        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

        _shader.Use();

        float pxX = 2f / viewportWidth;
        float pxY = 2f / viewportHeight;

        var w = weapons.Current;
        float time = Environment.TickCount / 1000f;
        float cooldownFrac = w.Def.FireRateHz > 0f ? Math.Clamp(w.Cooldown * w.Def.FireRateHz, 0f, 1f) : 0f;
        float ammoFrac = w.Def.InfiniteAmmo ? 1f : (w.Def.AmmoMax > 0 ? (float)w.Ammo / w.Def.AmmoMax : 0f);

        // Crosshair: reacts to current weapon and active recoil/cooldown.
        float armLenPx = w.Def.Kind switch
        {
            WeaponKind.Ak47 => 8f,
            WeaponKind.Shotgun => 10f,
            WeaponKind.RocketLauncher => 12f,
            _ => 8f,
        };
        float armThickPx = w.Def.Kind == WeaponKind.RocketLauncher ? 3f : 2f;
        float gapPx = 4f + w.Def.SpreadDegrees * 1.1f + cooldownFrac * (w.Def.Kind == WeaponKind.Ak47 ? 8f : 12f);
        Vector3 crossColor = w.Def.Kind switch
        {
            WeaponKind.Ak47 => new Vector3(0.95f, 0.94f, 0.88f),
            WeaponKind.Shotgun => new Vector3(1.00f, 0.86f, 0.52f),
            WeaponKind.RocketLauncher => new Vector3(1.00f, 0.70f, 0.36f),
            _ => Vector3.One,
        };
        if (!w.Def.InfiniteAmmo && ammoFrac < 0.20f)
        {
            float pulse = 0.55f + 0.45f * (0.5f + 0.5f * MathF.Sin(time * 10f));
            crossColor = Vector3.Lerp(crossColor, new Vector3(1.0f, 0.25f, 0.18f), pulse);
        }
        SetColor(crossColor.X, crossColor.Y, crossColor.Z, 0.92f);
        DrawRect(-armLenPx * pxX, armThickPx * pxY * 0.5f, -gapPx * pxX, -armThickPx * pxY * 0.5f);
        DrawRect(gapPx * pxX, armThickPx * pxY * 0.5f, armLenPx * pxX, -armThickPx * pxY * 0.5f);
        DrawRect(-armThickPx * pxX * 0.5f, -gapPx * pxY, armThickPx * pxX * 0.5f, -armLenPx * pxY);
        DrawRect(-armThickPx * pxX * 0.5f, armLenPx * pxY, armThickPx * pxX * 0.5f, gapPx * pxY);
        DrawRect(-1.2f * pxX, 1.2f * pxY, 1.2f * pxX, -1.2f * pxY);

        // Bottom-left health bar
        float barW = 280 * pxX, barH = 22 * pxY;
        float barX = -1f + 16 * pxX;
        float barY = -1f + 16 * pxY;
        SetColor(0.05f, 0.05f, 0.05f, 0.7f);
        DrawRect(barX - 2 * pxX, barY + barH + 2 * pxY, barX + barW + 2 * pxX, barY - 2 * pxY);
        float healthFrac = player.MaxHealth > 0 ? Math.Clamp((float)player.Health / player.MaxHealth, 0f, 1f) : 0f;
        Vector3 hpLow = new(0.88f, 0.18f, 0.16f);
        Vector3 hpHigh = new(0.22f, 0.84f, 0.28f);
        Vector3 healthColor = Vector3.Lerp(hpLow, hpHigh, MathF.Sqrt(healthFrac));
        if (healthFrac < 0.35f)
        {
            float pulse = 0.55f + 0.45f * (0.5f + 0.5f * MathF.Sin(time * 8f));
            healthColor = Vector3.Lerp(healthColor, new Vector3(1.0f, 0.28f, 0.16f), pulse);
        }
        SetColor(healthColor.X, healthColor.Y, healthColor.Z, 0.96f);
        DrawRect(barX, barY + barH, barX + barW * healthFrac, barY);

        // Bottom-right ammo bar
        float aBarW = 220 * pxX, aBarH = 18 * pxY;
        float aX = 1f - 16 * pxX - aBarW;
        float aY = -1f + 16 * pxY;
        SetColor(0.05f, 0.05f, 0.05f, 0.7f);
        DrawRect(aX - 2 * pxX, aY + aBarH + 2 * pxY, aX + aBarW + 2 * pxX, aY - 2 * pxY);
        Vector3 ammoColor = w.Def.Kind switch
        {
            WeaponKind.Ak47 => new Vector3(0.95f, 0.85f, 0.20f),
            WeaponKind.Shotgun => new Vector3(0.95f, 0.55f, 0.15f),
            WeaponKind.RocketLauncher => new Vector3(1.00f, 0.38f, 0.18f),
            _ => new Vector3(0.95f, 0.85f, 0.20f),
        };
        if (!w.Def.InfiniteAmmo && ammoFrac < 0.25f)
        {
            float pulse = 0.55f + 0.45f * (0.5f + 0.5f * MathF.Sin(time * 9f));
            ammoColor = Vector3.Lerp(ammoColor, new Vector3(1.0f, 0.22f, 0.16f), pulse);
        }
        SetColor(ammoColor.X, ammoColor.Y, ammoColor.Z, 0.96f);
        DrawRect(aX, aY + aBarH, aX + aBarW * Math.Clamp(ammoFrac, 0f, 1f), aY);

        // Weapon slots top-left with small ammo/owned state strips.
        float slot = 28 * pxX, slotY = 28 * pxY;
        float sX = -1f + 16 * pxX;
        float sY = 1f - 16 * pxY - slotY;
        for (int i = 0; i < weapons.Weapons.Count; i++)
        {
            float x = sX + i * (slot + 6 * pxX);
            var ws = weapons.Weapons[i];
            if (i == weapons.CurrentIndex) SetColor(0.9f, 0.9f, 0.3f, 0.95f);
            else if (ws.Owned) SetColor(0.7f, 0.7f, 0.7f, 0.85f);
            else SetColor(0.25f, 0.25f, 0.25f, 0.6f);
            DrawRect(x, sY + slotY, x + slot, sY);

            float fillFrac = ws.Def.InfiniteAmmo ? 1f : (ws.Def.AmmoMax > 0 ? Math.Clamp((float)ws.Ammo / ws.Def.AmmoMax, 0f, 1f) : 0f);
            SetColor(ws.Owned ? 0.15f : 0.10f, ws.Owned ? 0.15f : 0.10f, ws.Owned ? 0.15f : 0.10f, 0.85f);
            DrawRect(x + 2 * pxX, sY + 6 * pxY, x + slot - 2 * pxX, sY + 2 * pxY);
            if (ws.Owned)
            {
                SetColor(i == weapons.CurrentIndex ? 0.95f : 0.75f, i == weapons.CurrentIndex ? 0.85f : 0.75f, 0.25f, 0.95f);
                DrawRect(x + 2 * pxX, sY + 6 * pxY, x + 2 * pxX + (slot - 4 * pxX) * fillFrac, sY + 2 * pxY);
            }
        }

        _gl.Disable(EnableCap.Blend);
        _gl.Enable(EnableCap.DepthTest);
    }

    private void SetColor(float r, float g, float b, float a) => _gl.Uniform4(_shader.U("uColor"), r, g, b, a);

    private void DrawRect(float x0, float y0, float x1, float y1)
    {
        Span<float> verts =
        [
            x0, y0, x1, y0, x1, y1,
            x0, y0, x1, y1, x0, y1,
        ];
        _buffer.Upload(verts);
        _buffer.Bind();
        _gl.DrawArrays(PrimitiveType.Triangles, 0, 6);
    }

    public void Dispose()
    {
        _shader.Dispose();
        _buffer.Dispose();
    }
}
