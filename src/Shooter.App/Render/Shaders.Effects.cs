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

    public const string FullscreenVert = """
#version 330 core
out vec2 vUv;
void main(){
    vec2 p = vec2((gl_VertexID == 1) ? 3.0 : -1.0,
                  (gl_VertexID == 2) ? 3.0 : -1.0);
    vUv = p * 0.5 + 0.5;
    gl_Position = vec4(p, 0.0, 1.0);
}
""";

    public const string BloomThresholdFrag = """
#version 330 core
in vec2 vUv;
out vec4 FragColor;
uniform sampler2D uSrc;
void main(){
    vec3 c = texture(uSrc, vUv).rgb;
    float bright = max(max(c.r, c.g), c.b);
    float soft = clamp(bright - 0.9, 0.0, 0.5);
    soft = soft * soft / 0.5;
    float contrib = max(soft, max(bright - 1.0, 0.0));
    contrib = (bright > 0.0) ? contrib / max(bright, 1e-4) : 0.0;
    FragColor = vec4(c * contrib, 1.0);
}
""";

    public const string BloomDownFrag = """
#version 330 core
in vec2 vUv;
out vec4 FragColor;
uniform sampler2D uSrc;
uniform vec2 uTexel;
void main(){
    vec2 t = uTexel;
    vec3 a = texture(uSrc, vUv + t * vec2(-1.0, -1.0)).rgb;
    vec3 b = texture(uSrc, vUv + t * vec2( 0.0, -1.0)).rgb;
    vec3 c = texture(uSrc, vUv + t * vec2( 1.0, -1.0)).rgb;
    vec3 d = texture(uSrc, vUv + t * vec2(-0.5, -0.5)).rgb;
    vec3 e = texture(uSrc, vUv + t * vec2( 0.5, -0.5)).rgb;
    vec3 f = texture(uSrc, vUv + t * vec2(-1.0,  0.0)).rgb;
    vec3 g = texture(uSrc, vUv).rgb;
    vec3 h = texture(uSrc, vUv + t * vec2( 1.0,  0.0)).rgb;
    vec3 i = texture(uSrc, vUv + t * vec2(-0.5,  0.5)).rgb;
    vec3 j = texture(uSrc, vUv + t * vec2( 0.5,  0.5)).rgb;
    vec3 k = texture(uSrc, vUv + t * vec2(-1.0,  1.0)).rgb;
    vec3 l = texture(uSrc, vUv + t * vec2( 0.0,  1.0)).rgb;
    vec3 m = texture(uSrc, vUv + t * vec2( 1.0,  1.0)).rgb;
    vec3 res =
        (d + e + i + j) * (0.5 / 4.0) +
        (a + b + g + f) * (0.125 / 4.0) +
        (b + c + h + g) * (0.125 / 4.0) +
        (f + g + l + k) * (0.125 / 4.0) +
        (g + h + m + l) * (0.125 / 4.0);
    FragColor = vec4(res, 1.0);
}
""";

    public const string BloomUpFrag = """
#version 330 core
in vec2 vUv;
out vec4 FragColor;
uniform sampler2D uSrc;
uniform vec2 uTexel;
uniform float uRadius;
void main(){
    vec2 t = uTexel * uRadius;
    vec3 a = texture(uSrc, vUv + vec2(-t.x, -t.y)).rgb;
    vec3 b = texture(uSrc, vUv + vec2( 0.0, -t.y)).rgb * 2.0;
    vec3 c = texture(uSrc, vUv + vec2( t.x, -t.y)).rgb;
    vec3 d = texture(uSrc, vUv + vec2(-t.x,  0.0)).rgb * 2.0;
    vec3 e = texture(uSrc, vUv).rgb * 4.0;
    vec3 f = texture(uSrc, vUv + vec2( t.x,  0.0)).rgb * 2.0;
    vec3 g = texture(uSrc, vUv + vec2(-t.x,  t.y)).rgb;
    vec3 h = texture(uSrc, vUv + vec2( 0.0,  t.y)).rgb * 2.0;
    vec3 i = texture(uSrc, vUv + vec2( t.x,  t.y)).rgb;
    vec3 res = (a + c + g + i) + (b + d + f + h) + e;
    FragColor = vec4(res * (1.0 / 16.0), 1.0);
}
""";

    public const string PostFxFrag = """
#version 330 core
in vec2 vUv;
out vec4 FragColor;
uniform sampler2D uHdr;
uniform sampler2D uBloom;
uniform sampler2D uAo;
uniform float uExposure;
uniform float uBloomStrength;
uniform float uAoStrength;
uniform float uContrast;
uniform float uSaturation;
uniform float uShadowCool;
uniform float uHighlightWarm;
uniform float uVignetteStrength;

vec3 aces(vec3 x){
    const float a = 2.51;
    const float b = 0.03;
    const float c = 2.43;
    const float d = 0.59;
    const float e = 0.14;
    return clamp((x * (a * x + b)) / (x * (c * x + d) + e), 0.0, 1.0);
}
void main(){
    vec3 hdr = texture(uHdr, vUv).rgb;
    vec3 bloom = texture(uBloom, vUv).rgb;
    float ao = texture(uAo, vUv).r;
    float aoMul = mix(1.0, ao, clamp(uAoStrength, 0.0, 1.0));
    vec3 c = (hdr * aoMul + bloom * uBloomStrength) * uExposure;
    c = aces(c);
    float lum = dot(c, vec3(0.2126, 0.7152, 0.0722));
    vec3 cool = vec3(1.0 - uShadowCool * 0.35, 1.0, 1.0 + uShadowCool);
    vec3 warm = vec3(1.0 + uHighlightWarm, 1.0 + uHighlightWarm * 0.35, 1.0 - uHighlightWarm * 0.55);
    c *= mix(cool, warm, smoothstep(0.18, 0.90, lum));
    c = mix(vec3(lum), c, uSaturation);
    c = (c - 0.5) * uContrast + 0.5;
    c = clamp(c, 0.0, 1.0);
    c = pow(c, vec3(1.0 / 2.2));
    vec2 q = vUv * 2.0 - 1.0;
    float vignette = 1.0 - dot(q, q) * 0.22 * uVignetteStrength;
    c *= clamp(vignette, 0.0, 1.0);
    FragColor = vec4(c, 1.0);
}
""";

    public const string SsaoFrag = """
#version 330 core
in vec2 vUv;
out float oAO;
uniform sampler2D uDepth;
uniform sampler2D uNormal;
uniform sampler2D uNoise;
uniform vec2 uNoiseScale;
uniform vec3 uSamples[16];
uniform mat4 uProj;
uniform mat4 uInvProj;
uniform float uRadius;
uniform float uBias;

vec3 viewPos(vec2 uv){
    float d = texture(uDepth, uv).r;
    vec4 clip = vec4(uv * 2.0 - 1.0, d * 2.0 - 1.0, 1.0);
    vec4 v = uInvProj * clip;
    return v.xyz / v.w;
}

void main(){
    float d = texture(uDepth, vUv).r;
    if (d >= 0.99999) { oAO = 1.0; return; }
    vec3 P = viewPos(vUv);
    vec3 N = texture(uNormal, vUv).xyz;
    if (dot(N, N) < 0.001) { oAO = 1.0; return; }
    N = normalize(N);
    vec3 rnd = texture(uNoise, vUv * uNoiseScale).xyz;
    rnd = vec3(rnd.xy * 2.0 - 1.0, 0.0);
    vec3 T = normalize(rnd - N * dot(rnd, N));
    vec3 B = cross(N, T);
    mat3 TBN = mat3(T, B, N);
    float occ = 0.0;
    for (int i = 0; i < 16; ++i){
        vec3 s = TBN * uSamples[i];
        vec3 sp = P + s * uRadius;
        vec4 ofs = uProj * vec4(sp, 1.0);
        ofs.xyz /= ofs.w;
        vec2 sUv = ofs.xy * 0.5 + 0.5;
        if (sUv.x < 0.0 || sUv.x > 1.0 || sUv.y < 0.0 || sUv.y > 1.0) continue;
        float sd = texture(uDepth, sUv).r;
        if (sd >= 0.99999) continue;
        vec4 sc = vec4(sUv * 2.0 - 1.0, sd * 2.0 - 1.0, 1.0);
        vec4 sv = uInvProj * sc; sv.xyz /= sv.w;
        float rangeCheck = smoothstep(0.0, 1.0, uRadius / max(abs(P.z - sv.z), 0.0001));
        occ += ((sv.z >= sp.z + uBias) ? 1.0 : 0.0) * rangeCheck;
    }
    occ = 1.0 - occ / 16.0;
    oAO = pow(clamp(occ, 0.0, 1.0), 1.5);
}
""";

    public const string SsaoBlurFrag = """
#version 330 core
in vec2 vUv;
out float oAO;
uniform sampler2D uAo;
uniform sampler2D uDepth;
uniform vec2 uTexel;
void main(){
    float centerD = texture(uDepth, vUv).r;
    float sum = 0.0;
    float weight = 0.0;
    for (int y = -2; y < 2; ++y){
        for (int x = -2; x < 2; ++x){
            vec2 o = vec2(x, y) * uTexel;
            float d = texture(uDepth, vUv + o).r;
            float w = exp(-abs(d - centerD) * 4000.0);
            sum += texture(uAo, vUv + o).r * w;
            weight += w;
        }
    }
    oAO = (weight > 0.0) ? sum / weight : 1.0;
}
""";

    public const string LogLuminanceFrag = """
#version 330 core
in vec2 vUv;
out float oLum;
uniform sampler2D uSrc;
void main(){
    vec3 c = texture(uSrc, vUv).rgb;
    float lum = dot(c, vec3(0.2126, 0.7152, 0.0722));
    oLum = log(max(lum, 0.0001));
}
""";
}
