namespace Shooter.Render;

internal static partial class Shaders
{
    public const string WorldGBufferFrag = """
#version 330 core
in vec3 vWorldPos;
in vec3 vNormal;
in vec3 vViewNormal;
in vec2 vUv;
in mat3 vTbn;
layout(location=0) out vec4 oAlbedoAo;
layout(location=1) out vec4 oNormal;
layout(location=2) out vec4 oMaterial;
uniform sampler2D uBaseColor;
uniform sampler2D uNormalMap;
uniform sampler2D uRoughnessMap;
uniform sampler2D uMetallicMap;
uniform sampler2D uAoMap;
uniform sampler2D uHeightMap;
uniform int uHasTexture;
uniform int uHasNormalMap;
uniform int uHasRoughnessMap;
uniform int uHasMetallicMap;
uniform int uHasAoMap;
uniform int uHasHeightMap;
uniform int uEnableParallax;
uniform vec3 uTint;
uniform vec2 uTexelSize;
uniform float uParallaxScale;
uniform vec4 uMaterialParams;
uniform vec4 uMaterialFx0;
uniform mat4 uView;
""" + "\n" + LightingHeader + "\n" + """
float reliefShadowTerm(vec2 uv, float strength){
    if (uEnableParallax == 0 || uHasHeightMap == 0 || strength <= 0.0001)
        return 1.0;
    vec2 texel = max(uTexelSize, vec2(0.0005));
    float center = texture(uHeightMap, uv).r;
    float left   = texture(uHeightMap, uv - vec2(texel.x, 0.0)).r;
    float right  = texture(uHeightMap, uv + vec2(texel.x, 0.0)).r;
    float down   = texture(uHeightMap, uv - vec2(0.0, texel.y)).r;
    float up     = texture(uHeightMap, uv + vec2(0.0, texel.y)).r;
    float crevice = clamp(1.0 - center, 0.0, 1.0);
    float slope = clamp((abs(left - right) + abs(up - down)) * 1.6, 0.0, 1.0);
    float shadow = 1.0 - (crevice * 0.22 + slope * 0.10) * clamp(strength * 6.0, 0.0, 1.0);
    return clamp(shadow, 0.72, 1.0);
}

void main(){
    vec3 albedo = ((uHasTexture == 1) ? texture(uBaseColor, vUv).rgb : vec3(1.0)) * uTint;
    float ao = (uHasAoMap == 1) ? texture(uAoMap, vUv).r : 1.0;
    float roughness = (uHasRoughnessMap == 1) ? texture(uRoughnessMap, vUv).r : uMaterialParams.x;
    float metallic = (uHasMetallicMap == 1) ? texture(uMetallicMap, vUv).r : uMaterialParams.y;
    float emissive = uMaterialFx0.y;
    float wetness = 0.0;

    vec3 baseN = normalize(vNormal);
    float reliefAmount = (uEnableParallax == 1 && uHasHeightMap == 1)
        ? clamp(uParallaxScale / 0.12, 0.0, 1.0)
        : 0.0;
    bool useRelief = reliefAmount > 0.001;
    float reliefStrength = max(0.02, max(uMaterialParams.z, 0.35) * mix(0.12, 1.45, reliefAmount));
    vec3 detailN = useRelief
        ? detailNormalFromHeight(uHeightMap, vUv, max(uTexelSize, vec2(0.0005)), baseN, reliefStrength, uHasHeightMap)
        : detailNormalFromAlbedo(uBaseColor, vUv, uTexelSize, baseN, uMaterialParams.z, uHasTexture);
    vec3 mapN = texture(uNormalMap, vUv).xyz * 2.0 - 1.0;
    float reliefBlend = useRelief ? mix(0.03, 0.36, reliefAmount) : 0.08;
    vec3 n = (uHasNormalMap == 1)
        ? normalize(mix(normalize(vTbn * mapN), detailN, reliefBlend))
        : detailN;

    ao *= useRelief ? mix(1.0, reliefShadowTerm(vUv, reliefStrength), reliefAmount) : 1.0;

    oAlbedoAo = vec4(albedo, ao);
    oNormal = vec4(normalize((uView * vec4(n, 0.0)).xyz), 1.0);
    oMaterial = vec4(clamp(roughness, 0.0, 1.0), clamp(metallic, 0.0, 1.0), clamp(emissive, 0.0, 1.0), wetness);
}
""";

    public const string DeferredLightingFrag = """
#version 330 core
in vec2 vUv;
out vec4 FragColor;
uniform sampler2D uG0;
uniform sampler2D uG1;
uniform sampler2D uG2;
uniform sampler2D uDepth;
uniform sampler2D uSsao;
uniform sampler2D uContactShadow;
uniform mat4 uInvViewProj;
uniform mat4 uInvView;
""" + "\n" + LightingHeader + "\n" + """

vec3 reconstructWorldPos(vec2 uv, float depth){
    vec4 clip = vec4(uv * 2.0 - 1.0, depth * 2.0 - 1.0, 1.0);
    vec4 world = uInvViewProj * clip;
    return world.xyz / max(world.w, 0.0001);
}

void main(){
    float depth = texture(uDepth, vUv).r;
    if (depth >= 0.99999) discard;

    vec4 g0 = texture(uG0, vUv);
    vec3 viewN = normalize(texture(uG1, vUv).xyz);
    vec3 n = normalize((uInvView * vec4(viewN, 0.0)).xyz);
    vec4 g2 = texture(uG2, vUv);

    vec3 worldPos = reconstructWorldPos(vUv, depth);
    vec3 viewDir = normalize(uCameraPos - worldPos);
    float ao = texture(uSsao, vUv).r * g0.a;
    float contact = texture(uContactShadow, vUv).r;
    vec3 albedo = g0.rgb;
    float roughness = clamp(g2.r, 0.04, 1.0);
    float metallic = clamp(g2.g, 0.0, 1.0);
    float emissive = g2.b;
    float wetness = g2.a;

    roughness = mix(roughness, max(0.04, roughness * 0.45), wetness);
    float vis = pcfShadow(worldPos, n) * contact;
    vec3 f0 = mix(vec3(0.04), albedo, metallic);
    vec3 diffuseColor = albedo * (1.0 - metallic);
    vec3 reflectDir = reflect(-viewDir, n);
    float envMip = roughness * 6.0;
    vec3 envSpec = textureLod(uSpecularEnv, reflectDir, envMip).rgb;
    vec3 envF = fresnelSchlick(max(dot(n, viewDir), 0.0), f0);
    vec3 lit = diffuseColor * iblAmbient(n) * ao
             + envSpec * envF * mix(0.20, 0.85, 1.0 - roughness) * mix(1.0, 1.35, wetness)
             + directSun(n, diffuseColor, vis)
             + sunSpecular(n, viewDir, -uSunDir, roughness, f0, vis)
             + albedo * emissive;
    lit = applyFog(lit, worldPos, 1.0);
    FragColor = vec4(lit, 1.0);
}
""";

    public const string ContactShadowFrag = """
#version 330 core
in vec2 vUv;
out float oVis;
uniform sampler2D uDepth;
uniform sampler2D uNormal;
uniform mat4 uProj;
uniform mat4 uInvProj;
uniform vec3 uLightDirView;
uniform float uMaxDistance;
uniform float uThickness;
uniform float uBias;

vec3 viewPos(vec2 uv, float d){
    vec4 clip = vec4(uv * 2.0 - 1.0, d * 2.0 - 1.0, 1.0);
    vec4 v = uInvProj * clip;
    return v.xyz / max(v.w, 0.0001);
}

void main(){
    float d = texture(uDepth, vUv).r;
    if (d >= 0.99999) { oVis = 1.0; return; }

    vec3 P = viewPos(vUv, d);
    vec3 N = normalize(texture(uNormal, vUv).xyz);
    if (dot(N, N) < 0.001) { oVis = 1.0; return; }

    float ndl = max(dot(N, uLightDirView), 0.0);
    if (ndl <= 0.05) { oVis = 1.0; return; }

    const int STEPS = 8;
    float vis = 1.0;
    for (int i = 1; i <= STEPS; ++i){
        float t = (float(i) / float(STEPS)) * uMaxDistance;
        vec3 sp = P + uLightDirView * (t + uBias);
        vec4 clip = uProj * vec4(sp, 1.0);
        if (clip.w <= 0.0) continue;
        vec3 ndc = clip.xyz / clip.w;
        vec2 uv = ndc.xy * 0.5 + 0.5;
        if (uv.x <= 0.001 || uv.x >= 0.999 || uv.y <= 0.001 || uv.y >= 0.999) continue;
        float sd = texture(uDepth, uv).r;
        if (sd >= 0.99999) continue;
        vec3 sceneP = viewPos(uv, sd);
        float diff = sceneP.z - sp.z;
        float localThickness = uThickness + t * 0.05;
        if (diff > 0.0 && diff < localThickness){
            vis = 0.0;
            break;
        }
    }
    oVis = vis;
}
""";
}
