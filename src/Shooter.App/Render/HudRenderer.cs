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

        // Crosshair (4 small rectangles around screen center, fixed pixel size)
        const int armLen = 8, armThick = 2, gap = 4;

        // Crosshair white
        SetColor(1f, 1f, 1f, 0.9f);
        DrawRect(-armLen * pxX, armThick * pxY * 0.5f, -gap * pxX, -armThick * pxY * 0.5f); // left
        DrawRect(gap * pxX, armThick * pxY * 0.5f, armLen * pxX, -armThick * pxY * 0.5f); // right
        DrawRect(-armThick * pxX * 0.5f, -gap * pxY, armThick * pxX * 0.5f, -armLen * pxY); // bottom
        DrawRect(-armThick * pxX * 0.5f, armLen * pxY, armThick * pxX * 0.5f, gap * pxY);  // top

        // Bottom-left health bar
        float barW = 280 * pxX, barH = 22 * pxY;
        float barX = -1f + 16 * pxX;
        float barY = -1f + 16 * pxY;
        SetColor(0.05f, 0.05f, 0.05f, 0.7f);
        DrawRect(barX - 2 * pxX, barY + barH + 2 * pxY, barX + barW + 2 * pxX, barY - 2 * pxY);
        float frac = player.MaxHealth > 0 ? (float)player.Health / player.MaxHealth : 0f;
        SetColor(0.85f, 0.15f, 0.15f, 0.95f);
        DrawRect(barX, barY + barH, barX + barW * Math.Clamp(frac, 0, 1), barY);

        // Bottom-right ammo bar
        var w = weapons.Current;
        float aBarW = 220 * pxX, aBarH = 18 * pxY;
        float aX = 1f - 16 * pxX - aBarW;
        float aY = -1f + 16 * pxY;
        SetColor(0.05f, 0.05f, 0.05f, 0.7f);
        DrawRect(aX - 2 * pxX, aY + aBarH + 2 * pxY, aX + aBarW + 2 * pxX, aY - 2 * pxY);
        float aFrac = w.Def.InfiniteAmmo ? 1f : (w.Def.AmmoMax > 0 ? (float)w.Ammo / w.Def.AmmoMax : 0f);
        SetColor(0.95f, 0.85f, 0.20f, 0.95f);
        DrawRect(aX, aY + aBarH, aX + aBarW * Math.Clamp(aFrac, 0, 1), aY);

        // Weapon slots top-left (3 squares; current one bright)
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
