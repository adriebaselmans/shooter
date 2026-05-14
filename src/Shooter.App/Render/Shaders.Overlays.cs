namespace Shooter.Render;

internal static partial class Shaders
{
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
layout(location=0) out vec4 oColor;
layout(location=1) out vec4 oViewNormal;
void main(){
    oColor = vec4(0.025, 0.018, 0.015, 1.0);
    oViewNormal = vec4(0.0);
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
layout(location=0) out vec4 oColor;
layout(location=1) out vec4 oViewNormal;
uniform vec4 uColor;
void main(){
    oColor = uColor;
    oViewNormal = vec4(0.0);
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

    public const string HudTextVert = """
#version 330 core
layout(location=0) in vec2 aPos;
layout(location=1) in vec2 aUv;
layout(location=2) in vec4 aColor;
out vec2 vUv;
out vec4 vColor;
void main(){
    vUv = aUv;
    vColor = aColor;
    gl_Position = vec4(aPos, 0.0, 1.0);
}
""";

    public const string HudTextFrag = """
#version 330 core
in vec2 vUv;
in vec4 vColor;
out vec4 FragColor;
uniform sampler2D uAtlas;
void main(){
    float a = texture(uAtlas, vUv).a;
    if (a <= 0.01) discard;
    FragColor = vec4(vColor.rgb, vColor.a * a);
}
""";

    public const string ParticleVert = """
#version 330 core
layout(location=0) in vec3 aPos;
layout(location=1) in vec2 aUv;
layout(location=2) in vec4 aColor;
uniform mat4 uViewProj;
out vec2 vUv;
out vec4 vColor;
void main(){
    vUv = aUv;
    vColor = aColor;
    gl_Position = uViewProj * vec4(aPos, 1.0);
}
""";

    public const string ParticleFrag = """
#version 330 core
in vec2 vUv;
in vec4 vColor;
layout(location=0) out vec4 oColor;
layout(location=1) out vec4 oViewNormal;
void main(){
    float r = length(vUv);
    if (r > 1.0) discard;
    float alpha = smoothstep(1.0, 0.15, r) * vColor.a;
    oColor = vec4(vColor.rgb, alpha);
    oViewNormal = vec4(0.0);
}
""";

    public const string MuzzleFlashVert = """
#version 330 core
layout(location=0) in vec2 aQuad;
uniform mat4 uProj;
uniform vec3 uViewCenter;
uniform vec2 uHalfSize;
uniform float uRotation;
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
layout(location=0) out vec4 oColor;
layout(location=1) out vec4 oViewNormal;
uniform float uIntensity;
const float HDR_BOOST = 6.0;
void main(){
    float r = length(vUv);
    if (r > 1.0) discard;
    float core = exp(-r * 7.0) * 1.6;
    float halo = exp(-r * 2.2) * 0.55;
    float streak = exp(-abs(vUv.y) * 16.0) * exp(-abs(vUv.x) * 1.8) * 0.6;
    float a = clamp((core + halo + streak) * uIntensity, 0.0, 1.5);
    vec3 hot   = vec3(1.0, 0.96, 0.78);
    vec3 warm  = vec3(1.0, 0.65, 0.20);
    vec3 color = mix(warm, hot, clamp(core, 0.0, 1.0));
    oColor = vec4(color * a * HDR_BOOST, a);
    oViewNormal = vec4(0.0);
}
""";

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
layout(location=0) out vec4 oColor;
layout(location=1) out vec4 oViewNormal;
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
    vec2 q = vUv * 3.0 + vec2(vSeed * 17.0, vSeed * 31.0);
    float n = noise(q) * 0.5 + noise(q * 2.0) * 0.25 + noise(q * 4.0) * 0.125;
    float r = length(vUv) * (0.85 + n * 0.30);
    if (r > 1.0) discard;
    float core = 1.0 - smoothstep(0.0, 0.55, r);
    float halo = 1.0 - smoothstep(0.55, 1.00, r);
    float alpha = clamp(core * 0.92 + halo * 0.45, 0.0, 0.95);
    vec3 sooty = vec3(0.012, 0.010, 0.008);
    vec3 ash   = vec3(0.060, 0.045, 0.035);
    vec3 col   = mix(ash, sooty, core);
    oColor = vec4(col, alpha);
    oViewNormal = vec4(0.0);
}
""";
}
