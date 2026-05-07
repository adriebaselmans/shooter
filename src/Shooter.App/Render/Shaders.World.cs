namespace Shooter.Render;

internal static partial class Shaders
{
    public const string WorldVert = """
#version 330 core
layout(location=0) in vec3 aPos;
layout(location=1) in vec3 aNormal;
layout(location=2) in vec2 aUv;
uniform mat4 uModel;
uniform mat4 uView;
uniform mat4 uViewProj;
uniform mat4 uNormalMat;
out vec3 vWorldPos;
out vec3 vNormal;
out vec3 vViewNormal;
out vec2 vUv;
void main(){
    vec4 wp = uModel * vec4(aPos,1.0);
    vWorldPos = wp.xyz;
    vec3 wn = normalize((uNormalMat * vec4(aNormal,0.0)).xyz);
    vNormal = wn;
    vViewNormal = mat3(uView) * wn;
    vUv = aUv;
    gl_Position = uViewProj * wp;
}
""";

    public static readonly string WorldFrag = """
#version 330 core
in vec3 vWorldPos;
in vec3 vNormal;
in vec3 vViewNormal;
in vec2 vUv;
layout(location=0) out vec4 oColor;
layout(location=1) out vec4 oViewNormal;
uniform sampler2D uBaseColor;
uniform sampler2D uNormalMap;
uniform sampler2D uRoughnessMap;
uniform sampler2D uAoMap;
uniform int uHasTexture;
uniform int uHasNormalMap;
uniform int uHasRoughnessMap;
uniform int uHasAoMap;
uniform vec3 uTint;
uniform float uSelfIllum;
uniform vec2 uTexelSize;
uniform vec4 uMaterialParams;
uniform vec4 uMaterialFx0;
uniform vec4 uMaterialFx1;
""" + "\n" + LightingHeader + "\n" + """
void main(){
    vec2 flowUvA = vUv + uMaterialFx1.xy * uTime;
    vec2 flowUvB = vUv - uMaterialFx1.xy * (uTime * 0.73 + 0.17);
    vec2 rippleA = vec2(
        sin((vWorldPos.x + uTime * 0.9) * 1.7),
        cos((vWorldPos.z - uTime * 0.7) * 1.4)) * (uMaterialFx1.z * 0.06);
    vec2 rippleB = vec2(
        cos((vWorldPos.z + uTime * 0.6) * 1.2),
        sin((vWorldPos.x - uTime * 0.5) * 1.5)) * (uMaterialFx1.z * 0.04);
    vec2 sampleUv = flowUvA + rippleA;
    vec2 sampleUv2 = flowUvB - rippleB;
    vec3 baseN = normalize(vNormal);
    vec3 texA = (uHasTexture == 1) ? texture(uBaseColor, sampleUv).rgb : vec3(1.0);
    vec3 texB = (uHasTexture == 1) ? texture(uBaseColor, sampleUv2).rgb : vec3(1.0);
    vec3 tex = mix(texA, texB, 0.35);
    vec3 albedo = tex * uTint;
    vec3 n = (uHasNormalMap == 1)
        ? normalFromMap(uNormalMap, vWorldPos, sampleUv, baseN)
        : detailNormalFromAlbedo(uBaseColor, sampleUv, uTexelSize, baseN, uMaterialParams.z, uHasTexture);
    float roughness = (uHasRoughnessMap == 1) ? texture(uRoughnessMap, sampleUv).r : uMaterialParams.x;
    roughness = clamp(max(roughness, uMaterialParams.x * 0.45), 0.02, 1.0);
    float ao = (uHasAoMap == 1) ? texture(uAoMap, sampleUv).r : 1.0;
    float vis = pcfShadow(vWorldPos, n);
    vec3 viewDir = normalize(uCameraPos - vWorldPos);
    vec3 ambient = albedo * iblAmbient(n) * ao;
    vec3 lit = ambient
             + directSun(n, albedo, vis)
             + sunSpecular(n, viewDir, -uSunDir, roughness, uMaterialParams.y, vis)
             + albedo * uSelfIllum;
    int kind = int(uMaterialFx0.x + 0.5);
    if (kind == 1) {
        float fres = pow(1.0 - max(dot(n, viewDir), 0.0), 5.0) * max(0.25, uMaterialFx0.w);
        float sparkle = pow(max(dot(reflect(-viewDir, n), -uSunDir), 0.0), 24.0);
        vec3 waterDeep = vec3(0.05, 0.22, 0.34);
        vec3 waterShallow = vec3(0.10, 0.44, 0.60);
        vec3 waterTint = mix(waterDeep, waterShallow, clamp(tex.g * 0.9 + tex.b * 1.1 + 0.10, 0.0, 1.0));
        vec3 reflection = mix(uFogColor * 0.32 + iblAmbient(n) * 0.78, uSunColor * (1.05 + sparkle * 1.8) + iblAmbient(n), clamp(fres + sparkle * 0.35, 0.0, 1.0));
        vec3 body = waterTint * 0.84 + tex * 0.10 + iblAmbient(n) * 0.16;
        lit = mix(body, reflection + sunSpecular(n, viewDir, -uSunDir, 0.035, max(uMaterialParams.y, 0.42), 1.0), clamp(0.28 + fres * 0.78, 0.0, 1.0));
        lit += waterTint * sparkle * 0.28;
    } else if (kind == 2) {
        float pulse = 1.0 + sin(uTime * 4.0 + vWorldPos.x * 0.35 + vWorldPos.z * 0.28) * uMaterialFx1.w;
        float heat = 0.65 + 0.35 * sin(uTime * 2.5 + vWorldPos.x * 0.42 - vWorldPos.z * 0.33);
        vec3 hot = mix(vec3(0.32, 0.08, 0.02), vec3(1.00, 0.46, 0.08), clamp(tex.r * 1.4 + tex.g * 0.8, 0.0, 1.0));
        vec3 emissive = hot * uMaterialFx0.y * pulse * (0.85 + heat * 0.45);
        lit = hot * 0.24 + directSun(n, hot, vis) * 0.10 + emissive;
    }
    lit = applyFog(lit, vWorldPos, uMaterialParams.w);
    oColor = vec4(lit, 1.0);
    oViewNormal = vec4(normalize(vViewNormal), 1.0);
}
""";

    public const string PickupVert = WorldVert;
    public static readonly string PickupFrag = WorldFrag;

    public static readonly string TexturedModelFrag = """
#version 330 core
in vec3 vWorldPos;
in vec3 vNormal;
in vec3 vViewNormal;
in vec2 vUv;
layout(location=0) out vec4 oColor;
layout(location=1) out vec4 oViewNormal;
uniform sampler2D uBaseColor;
uniform sampler2D uNormalMap;
uniform sampler2D uRoughnessMap;
uniform sampler2D uAoMap;
uniform vec4 uBaseColorFactor;
uniform int  uHasTexture;
uniform int  uHasNormalMap;
uniform int  uHasRoughnessMap;
uniform int  uHasAoMap;
uniform int  uWriteNormal;
uniform int  uViewSpaceLighting;
uniform int  uApplyFog;
uniform vec2 uTexelSize;
uniform vec4 uMaterialParams;
uniform vec4 uMaterialFx0;
uniform vec4 uMaterialFx1;
""" + "\n" + LightingHeader + "\n" + """
void main(){
    vec4 base = (uHasTexture == 1) ? texture(uBaseColor, vUv) * uBaseColorFactor : uBaseColorFactor;
    if (base.a < 0.05) discard;
    vec3 pos = vWorldPos;
    vec3 geomN = normalize(uViewSpaceLighting == 1 ? vViewNormal : vNormal);
    vec3 n = (uHasNormalMap == 1)
        ? normalFromMap(uNormalMap, pos, vUv, geomN)
        : detailNormalFromAlbedo(uBaseColor, vUv, uTexelSize, geomN, uMaterialParams.z, uHasTexture);
    float roughness = (uHasRoughnessMap == 1) ? texture(uRoughnessMap, vUv).r : uMaterialParams.x;
    float ao = (uHasAoMap == 1) ? texture(uAoMap, vUv).r : 1.0;
    vec3 lightDir = normalize(uViewSpaceLighting == 1 ? uToSunView : -uSunDir);
    vec3 viewDir = normalize(uViewSpaceLighting == 1 ? -vWorldPos : (uCameraPos - vWorldPos));
    float vis = (uViewSpaceLighting == 1) ? 1.0 : pcfShadow(vWorldPos, normalize(vNormal));
    vec3 lit = base.rgb * iblAmbient(n) * ao
             + base.rgb * uSunColor * uSunIntensity * max(dot(n, lightDir), 0.0) * vis
             + sunSpecular(n, viewDir, lightDir, roughness, uMaterialParams.y, vis);
    if (uApplyFog == 1)
        lit = applyFog(lit, vWorldPos, uMaterialParams.w);
    oColor = vec4(lit, base.a);
    oViewNormal = (uWriteNormal == 1) ? vec4(normalize(vViewNormal), 1.0) : vec4(0.0);
}
""";
}
