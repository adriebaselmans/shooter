namespace Shooter.Render;

internal static partial class Shaders
{
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
