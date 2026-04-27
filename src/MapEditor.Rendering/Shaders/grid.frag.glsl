#version 410 core

in vec2 vUV;

uniform vec3  uGridColor;
uniform float uGridSpacing;
uniform mat4  uInvViewProj;
uniform vec2  uViewportSize;
uniform float uGridAlpha;

out vec4 FragColor;

// Analytical grid lines based on world-space position
float gridLine(vec2 worldPos, float spacing)
{
    vec2 grid = abs(fract(worldPos / spacing - 0.5) - 0.5) / fwidth(worldPos / spacing);
    return min(grid.x, grid.y);
}

void main()
{
    // Reconstruct world-space position from NDC
    vec4 ndcPos = vec4(vUV, 0.0, 1.0);
    vec4 worldPos4 = uInvViewProj * ndcPos;
    vec2 worldPos = worldPos4.xz / worldPos4.w;

    float line = gridLine(worldPos, uGridSpacing);
    float alpha = 1.0 - min(line, 1.0);
    alpha *= uGridAlpha;

    if (alpha < 0.01) discard;

    FragColor = vec4(uGridColor, alpha);
}
