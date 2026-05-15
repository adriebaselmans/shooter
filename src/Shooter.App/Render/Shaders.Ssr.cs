namespace Shooter.Render;

internal static partial class Shaders
{
    public const string SsrTraceFrag = """
#version 330 core
in vec2 vUv;
out vec4 FragColor;
uniform sampler2D uSceneColor;
uniform sampler2D uDepth;
uniform sampler2D uNormal;
uniform sampler2D uMaterial;
uniform samplerCube uSpecularEnv;
uniform mat4 uProj;
uniform mat4 uInvProj;
uniform mat4 uInvView;

vec3 viewPos(vec2 uv, float depth){
    vec4 clip = vec4(uv * 2.0 - 1.0, depth * 2.0 - 1.0, 1.0);
    vec4 v = uInvProj * clip;
    return v.xyz / max(v.w, 0.0001);
}

vec2 projectUv(vec3 pos){
    vec4 clip = uProj * vec4(pos, 1.0);
    vec2 ndc = clip.xy / max(clip.w, 0.0001);
    return ndc * 0.5 + 0.5;
}

void main(){
    float rawDepth = texture(uDepth, vUv).r;
    if (rawDepth >= 0.99999) { FragColor = vec4(0.0); return; }

    vec3 P = viewPos(vUv, rawDepth);
    vec3 N = normalize(texture(uNormal, vUv).xyz);
    vec4 mat = texture(uMaterial, vUv);
    float roughness = clamp(mat.r, 0.04, 1.0);
    float wetness = clamp(mat.a, 0.0, 1.0);

    float reflectivity = clamp((1.0 - roughness) * 0.75 + wetness * 0.55, 0.0, 1.0);
    if (reflectivity < 0.05) { FragColor = vec4(0.0); return; }

    vec3 V = normalize(-P);
    vec3 R = normalize(reflect(-V, N));
    vec3 worldR = normalize((uInvView * vec4(R, 0.0)).xyz);
    vec3 fallbackEnv = textureLod(uSpecularEnv, worldR, roughness * 6.0).rgb;

    float stepLen = mix(0.22, 0.50, roughness);
    float maxDist = mix(18.0, 8.0, roughness);
    float thickness = mix(0.10, 0.22, roughness);
    float t = 0.10;
    float prevT = t;
    bool hit = false;
    vec2 hitUv = vUv;

    for (int i = 0; i < 28; ++i){
        vec3 samplePos = P + R * t;
        vec2 uv = projectUv(samplePos);
        if (uv.x <= 0.001 || uv.x >= 0.999 || uv.y <= 0.001 || uv.y >= 0.999)
            break;
        float sd = texture(uDepth, uv).r;
        if (sd < 0.99999) {
            vec3 scenePos = viewPos(uv, sd);
            float dz = scenePos.z - samplePos.z;
            if (dz > 0.0 && dz < thickness) {
                float lo = prevT;
                float hi = t;
                for (int j = 0; j < 4; ++j) {
                    float mid = 0.5 * (lo + hi);
                    vec3 midPos = P + R * mid;
                    vec2 midUv = projectUv(midPos);
                    float md = texture(uDepth, midUv).r;
                    vec3 mp = viewPos(midUv, md);
                    float mdz = mp.z - midPos.z;
                    if (mdz > 0.0 && mdz < thickness) {
                        hi = mid;
                        hitUv = midUv;
                    } else {
                        lo = mid;
                    }
                }
                hit = true;
                break;
            }
        }
        prevT = t;
        t += stepLen;
        if (t > maxDist) break;
    }

    float ndv = max(dot(N, V), 0.0);
    float fresnel = pow(clamp(1.0 - ndv, 0.0, 1.0), 5.0);
    float amount = reflectivity * mix(0.08, 0.70, fresnel);

    if (!hit) {
        FragColor = vec4(fallbackEnv, amount * 0.35);
        return;
    }

    vec3 hitColor = texture(uSceneColor, hitUv).rgb;
    float edgeFade = 1.0 - smoothstep(0.80, 0.98, max(abs(hitUv.x * 2.0 - 1.0), abs(hitUv.y * 2.0 - 1.0)));
    vec3 reflection = mix(fallbackEnv, hitColor, edgeFade);
    FragColor = vec4(reflection, amount * edgeFade);
}
""";

    public const string SsrTemporalFrag = """
#version 330 core
in vec2 vUv;
out vec4 FragColor;
uniform sampler2D uCurrentSsr;
uniform sampler2D uHistorySsr;
uniform sampler2D uDepth;
uniform mat4 uInvViewProj;
uniform mat4 uPrevViewProj;
uniform int uUseHistory;

vec3 worldPos(vec2 uv, float depth){
    vec4 clip = vec4(uv * 2.0 - 1.0, depth * 2.0 - 1.0, 1.0);
    vec4 w = uInvViewProj * clip;
    return w.xyz / max(w.w, 0.0001);
}

void main(){
    vec4 cur = texture(uCurrentSsr, vUv);
    if (uUseHistory == 0 || cur.a <= 0.001) {
        FragColor = cur;
        return;
    }

    float depth = texture(uDepth, vUv).r;
    if (depth >= 0.99999) {
        FragColor = cur;
        return;
    }

    vec3 wp = worldPos(vUv, depth);
    vec4 prevClip = uPrevViewProj * vec4(wp, 1.0);
    vec2 prevUv = prevClip.xy / max(prevClip.w, 0.0001) * 0.5 + 0.5;
    if (prevUv.x <= 0.001 || prevUv.x >= 0.999 || prevUv.y <= 0.001 || prevUv.y >= 0.999) {
        FragColor = cur;
        return;
    }

    vec4 hist = texture(uHistorySsr, prevUv);
    float valid = step(0.001, hist.a);
    float colorDelta = length(cur.rgb - hist.rgb);
    float reject = smoothstep(0.45, 0.85, colorDelta);
    float historyWeight = valid * (1.0 - reject) * clamp(hist.a, 0.0, 1.0) * 0.82;
    FragColor = vec4(mix(cur.rgb, hist.rgb, historyWeight), max(cur.a, hist.a * 0.95 * valid));
}
""";

    public const string SsrCompositeFrag = """
#version 330 core
in vec2 vUv;
out vec4 FragColor;
uniform sampler2D uScene;
uniform sampler2D uSsr;
void main(){
    vec3 scene = texture(uScene, vUv).rgb;
    vec4 ssr = texture(uSsr, vUv);
    FragColor = vec4(mix(scene, ssr.rgb, clamp(ssr.a, 0.0, 1.0)), 1.0);
}
""";
}
