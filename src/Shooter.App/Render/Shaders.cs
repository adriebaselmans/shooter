namespace Shooter.Render;

/// <summary>GLSL source for the small set of shaders the game uses.</summary>
internal static class Shaders
{
    public const string WorldVert = """
#version 330 core
layout(location=0) in vec3 aPos;
layout(location=1) in vec3 aNormal;
layout(location=2) in vec2 aUv;
uniform mat4 uModel;
uniform mat4 uViewProj;
uniform mat4 uNormalMat;
out vec3 vWorldPos;
out vec3 vNormal;
out vec2 vUv;
void main(){
    vec4 wp = uModel * vec4(aPos,1.0);
    vWorldPos = wp.xyz;
    vNormal = normalize((uNormalMat * vec4(aNormal,0.0)).xyz);
    vUv = aUv;
    gl_Position = uViewProj * wp;
}
""";

    public const string WorldFrag = """
#version 330 core
in vec3 vWorldPos;
in vec3 vNormal;
in vec2 vUv;
out vec4 FragColor;
uniform vec3 uTint;
uniform vec3 uAmbient;
uniform vec3 uSunDir;     // direction the sun light travels
uniform vec3 uSunColor;
void main(){
    vec3 n = normalize(vNormal);
    float ndl = max(dot(n, -normalize(uSunDir)), 0.0);
    // Soft checker derived from world position to give scale cues without textures.
    vec3 cell = floor(vWorldPos * 0.5);
    float chk = mod(cell.x + cell.y + cell.z, 2.0);
    vec3 base = uTint * mix(0.85, 1.0, chk);
    vec3 lit = base * (uAmbient + uSunColor * ndl);
    FragColor = vec4(lit, 1.0);
}
""";

    public const string PickupVert = WorldVert;
    public const string PickupFrag = """
#version 330 core
in vec3 vWorldPos;
in vec3 vNormal;
in vec2 vUv;
out vec4 FragColor;
uniform vec3 uTint;
uniform vec3 uAmbient;
uniform vec3 uSunDir;
uniform vec3 uSunColor;
void main(){
    vec3 n = normalize(vNormal);
    float ndl = max(dot(n, -normalize(uSunDir)), 0.0);
    vec3 lit = uTint * (uAmbient + uSunColor * ndl) + uTint * 0.25; // self-illum
    FragColor = vec4(lit, 1.0);
}
""";

    /// <summary>Textured Phong fragment shader used for GLB-based models (weapon viewmodel, rockets).</summary>
    public const string TexturedModelFrag = """
#version 330 core
in vec3 vWorldPos;
in vec3 vNormal;
in vec2 vUv;
out vec4 FragColor;
uniform sampler2D uBaseColor;
uniform vec4 uBaseColorFactor;
uniform int  uHasTexture;
uniform vec3 uAmbient;
uniform vec3 uSunDir;
uniform vec3 uSunColor;
void main(){
    vec4 base = (uHasTexture == 1) ? texture(uBaseColor, vUv) * uBaseColorFactor : uBaseColorFactor;
    if (base.a < 0.05) discard;
    vec3 n = normalize(vNormal);
    float ndl = max(dot(n, -normalize(uSunDir)), 0.0);
    vec3 lit = base.rgb * (uAmbient + uSunColor * ndl);
    FragColor = vec4(lit, base.a);
}
""";

    public const string DecalVert = """
#version 330 core
layout(location=0) in vec3 aPos;
uniform mat4 uViewProj;
void main(){
    gl_Position = uViewProj * vec4(aPos,1.0);
}
""";

    public const string DecalFrag = """
#version 330 core
out vec4 FragColor;
void main(){
    FragColor = vec4(0.06, 0.04, 0.04, 1.0);
}
""";

    public const string TracerVert = """
#version 330 core
layout(location=0) in vec3 aPos;
uniform mat4 uViewProj;
void main(){
    gl_Position = uViewProj * vec4(aPos,1.0);
}
""";

    public const string TracerFrag = """
#version 330 core
out vec4 FragColor;
uniform vec4 uColor;
void main(){
    FragColor = uColor;
}
""";

    public const string HudVert = """
#version 330 core
layout(location=0) in vec2 aPos;
void main(){
    gl_Position = vec4(aPos, 0.0, 1.0);
}
""";

    public const string HudFrag = """
#version 330 core
out vec4 FragColor;
uniform vec4 uColor;
void main(){
    FragColor = uColor;
}
""";

    /// <summary>View-space billboard for the muzzle flash. Quad vertices are in [-1,1] view-space
    /// quad coordinates; the renderer scales/rotates/translates them into the projection in the
    /// vertex shader so the flash inherits the viewmodel's recoil offset trivially.</summary>
    public const string MuzzleFlashVert = """
#version 330 core
layout(location=0) in vec2 aQuad; // [-1,1] quad corner
uniform mat4 uProj;
uniform vec3 uViewCenter;   // view-space center (e.g. MuzzleViewOffset)
uniform vec2 uHalfSize;     // half width/height in view-space units
uniform float uRotation;    // radians, around the view-space Z axis
out vec2 vUv;
void main(){
    float c = cos(uRotation), s = sin(uRotation);
    vec2 p = vec2(aQuad.x * c - aQuad.y * s, aQuad.x * s + aQuad.y * c);
    vec3 viewPos = uViewCenter + vec3(p * uHalfSize, 0.0);
    vUv = aQuad;
    gl_Position = uProj * vec4(viewPos, 1.0);
}
""";

    public const string MuzzleFlashFrag = """
#version 330 core
in vec2 vUv;
out vec4 FragColor;
uniform float uIntensity;   // 0..1
void main(){
    float r = length(vUv);
    if (r > 1.0) discard;
    // Bright tight core + warmer halo, additive composited.
    float core = exp(-r * 7.0) * 1.6;
    float halo = exp(-r * 2.2) * 0.55;
    // Soft cross-shaped streaks along the quad axes.
    float streak = exp(-abs(vUv.y) * 16.0) * exp(-abs(vUv.x) * 1.8) * 0.6;
    float a = clamp((core + halo + streak) * uIntensity, 0.0, 1.5);
    vec3 hot   = vec3(1.0, 0.96, 0.78);
    vec3 warm  = vec3(1.0, 0.65, 0.20);
    vec3 color = mix(warm, hot, clamp(core, 0.0, 1.0));
    FragColor = vec4(color * a, a);
}
""";

    /// <summary>Scorch decal: a quad attached to a surface. Vertex shader passes a per-vertex
    /// uv in [-1,1] so the fragment shader can do radial falloff with hash-based noise to make
    /// each smudge look organic instead of a perfect circle.</summary>
    public const string ScorchVert = """
#version 330 core
layout(location=0) in vec3 aPos;
layout(location=1) in vec2 aUv;
layout(location=2) in float aSeed;
uniform mat4 uViewProj;
out vec2 vUv;
out float vSeed;
void main(){
    vUv = aUv;
    vSeed = aSeed;
    gl_Position = uViewProj * vec4(aPos, 1.0);
}
""";

    public const string ScorchFrag = """
#version 330 core
in vec2 vUv;
in float vSeed;
out vec4 FragColor;
float hash21(vec2 p){ return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float noise(vec2 p){
    vec2 i = floor(p), f = fract(p);
    float a = hash21(i);
    float b = hash21(i + vec2(1, 0));
    float c = hash21(i + vec2(0, 1));
    float d = hash21(i + vec2(1, 1));
    vec2 u = f * f * (3.0 - 2.0 * f);
    return mix(a, b, u.x) + (c - a) * u.y * (1.0 - u.x) + (d - b) * u.x * u.y;
}
void main(){
    // Per-decal seed jitters the noise lookup so two scorches don't look identical.
    vec2 q = vUv * 3.0 + vec2(vSeed * 17.0, vSeed * 31.0);
    float n = noise(q) * 0.5 + noise(q * 2.0) * 0.25 + noise(q * 4.0) * 0.125;
    float r = length(vUv) * (0.85 + n * 0.30);  // radius perturbed by fbm
    if (r > 1.0) discard;
    // Two-zone falloff: dark sooty core, soft brown halo.
    float core = 1.0 - smoothstep(0.0, 0.55, r);
    float halo = 1.0 - smoothstep(0.55, 1.00, r);
    float alpha = clamp(core * 0.92 + halo * 0.45, 0.0, 0.95);
    vec3 sooty = vec3(0.03, 0.025, 0.02);
    vec3 ash   = vec3(0.22, 0.18, 0.14);
    vec3 col   = mix(ash, sooty, core);
    FragColor = vec4(col, alpha);
}
""";
}
