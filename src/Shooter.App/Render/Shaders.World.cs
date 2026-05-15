namespace Shooter.Render;

internal static partial class Shaders
{
    public const string WorldVert = """
#version 330 core
layout(location=0) in vec3 aPos;
layout(location=1) in vec3 aNormal;
layout(location=2) in vec2 aUv;
layout(location=3) in vec3 aTangent;
layout(location=4) in vec3 aBitangent;
uniform mat4 uModel;
uniform mat4 uView;
uniform mat4 uViewProj;
uniform mat4 uNormalMat;
out vec3 vWorldPos;
out vec3 vNormal;
out vec3 vViewNormal;
out vec2 vUv;
out mat3 vTbn;
void main(){
    vec4 wp = uModel * vec4(aPos,1.0);
    vWorldPos = wp.xyz;
    vec3 wn = normalize((uNormalMat * vec4(aNormal,0.0)).xyz);
    vec3 wt = normalize((uNormalMat * vec4(aTangent,0.0)).xyz);
    vec3 wb = normalize((uNormalMat * vec4(aBitangent,0.0)).xyz);
    vNormal = wn;
    vViewNormal = mat3(uView) * wn;
    vUv = aUv;
    vTbn = mat3(wt, wb, wn);
    gl_Position = uViewProj * wp;
}
""";

    public static readonly string WorldFrag = """
#version 330 core
in vec3 vWorldPos;
in vec3 vNormal;
in vec3 vViewNormal;
in vec2 vUv;
in mat3 vTbn;
layout(location=0) out vec4 oColor;
layout(location=1) out vec4 oViewNormal;
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
uniform float uSelfIllum;
uniform vec2 uTexelSize;
uniform float uParallaxScale;
uniform vec4 uMaterialParams;
uniform vec4 uMaterialFx0;
uniform vec4 uMaterialFx1;
""" + "\n" + LightingHeader + "\n" + """
float heightFromMap(vec2 uv){
    return texture(uHeightMap, uv).r;
}

// Procedural water waves via Gerstner octaves
float gerstnerWave(vec2 p, vec2 d, float steepness, float wavelength, float speed, float t) {
    float k = 2.0 * 3.14159265 / wavelength;
    float c = sqrt(9.8 / k);
    float f = k * (dot(d, p) - c * t * speed);
    return steepness * sin(f);
}

float proceduralWaterHeight(vec2 p, float t) {
    float h = 0.0;
    // Scale time to make it animate much more energetically
    t *= 2.5; 
    h += gerstnerWave(p, normalize(vec2(1.0, 1.2)), 0.08, 2.0, 2.0, t);
    h += gerstnerWave(p, normalize(vec2(-1.0, 0.8)), 0.06, 1.2, 2.4, t);
    h += gerstnerWave(p, normalize(vec2(0.8, -1.1)), 0.04, 0.6, 3.2, t);
    h += gerstnerWave(p, normalize(vec2(-0.6, -0.9)), 0.02, 0.3, 4.0, t);
    // Add high frequency noise-like micro-ripples
    h += 0.01 * sin(p.x * 12.0 + t * 4.0) * cos(p.y * 14.0 + t * 4.5);
    return h;
}

vec3 proceduralWaterNormal(vec2 p, float t) {
    float eps = 0.01;
    float h1 = proceduralWaterHeight(p + vec2(eps, 0.0), t);
    float h2 = proceduralWaterHeight(p - vec2(eps, 0.0), t);
    float h3 = proceduralWaterHeight(p + vec2(0.0, eps), t);
    float h4 = proceduralWaterHeight(p - vec2(0.0, eps), t);
    return normalize(vec3(h2 - h1, 2.0 * eps, h4 - h3));
}

vec3 reliefNormal(vec2 uv, vec3 geomN, float strength){
    if (uEnableParallax == 0 || uHasHeightMap == 0 || strength <= 0.0001)
        return normalize(geomN);
    vec2 texel = max(uTexelSize, vec2(0.0005));
    float left  = heightFromMap(uv - vec2(texel.x, 0.0));
    float right = heightFromMap(uv + vec2(texel.x, 0.0));
    float down  = heightFromMap(uv - vec2(0.0, texel.y));
    float up    = heightFromMap(uv + vec2(0.0, texel.y));
    vec3 t = tangentFromNormal(geomN);
    vec3 b = normalize(cross(geomN, t));
    vec3 mapN = normalize(vec3((left - right) * strength * 10.0, (down - up) * strength * 10.0, 1.0));
    return normalize(mat3(t, b, geomN) * mapN);
}

float reliefShadowTerm(vec2 uv, float strength){
    if (uEnableParallax == 0 || uHasHeightMap == 0 || strength <= 0.0001)
        return 1.0;
    vec2 texel = max(uTexelSize, vec2(0.0005));
    float center = heightFromMap(uv);
    float left   = heightFromMap(uv - vec2(texel.x, 0.0));
    float right  = heightFromMap(uv + vec2(texel.x, 0.0));
    float down   = heightFromMap(uv - vec2(0.0, texel.y));
    float up     = heightFromMap(uv + vec2(0.0, texel.y));
    float crevice = clamp(1.0 - center, 0.0, 1.0);
    float slope = clamp((abs(left - right) + abs(up - down)) * 1.6, 0.0, 1.0);
    float shadow = 1.0 - (crevice * 0.22 + slope * 0.10) * clamp(strength * 6.0, 0.0, 1.0);
    return clamp(shadow, 0.72, 1.0);
}

void main(){
    vec3 viewDir = normalize(uCameraPos - vWorldPos);
    vec3 baseN = normalize(vNormal);
    int kind = int(uMaterialFx0.x + 0.5);
    bool useRelief = (kind == 0 && uEnableParallax == 1 && uHasHeightMap == 1);
    float reliefShadow = 1.0;
    vec2 baseUv = vUv;
    vec2 flowUvA = baseUv + uMaterialFx1.xy * uTime;
    
    // For water (kind == 1), only the normal maps cross-scroll. 
    // The base texture is drawn cleanly with a simple slow pan.
    // We scale the normal maps relative to the base texture so the ripples match the high resolution
    vec2 normalScrollA = (baseUv * 2.5) + uMaterialFx1.xy * uTime * 2.0;
    vec2 normalScrollB = (baseUv * 2.5) + vec2(-uMaterialFx1.y, uMaterialFx1.x) * (uTime * 1.6); // perpendicular scroll
    
    // Lava (kind == 2) maintains the chaotic color warping
    vec2 rippleA = (kind == 2) ? vec2(
        sin((vWorldPos.x * 2.0 + uTime * 0.9) * 1.7),
        cos((vWorldPos.z * 2.0 - uTime * 0.7) * 1.4)) * (uMaterialFx1.z * 0.02) : vec2(0.0);
    vec2 rippleB = (kind == 2) ? vec2(
        cos((vWorldPos.z * 1.5 + uTime * 0.6) * 1.2),
        sin((vWorldPos.x * 1.5 - uTime * 0.5) * 1.5)) * (uMaterialFx1.z * 0.015) : vec2(0.0);
        
    vec2 sampleUv = flowUvA + rippleA;
    vec2 sampleUv2 = flowUvA + rippleB - uMaterialFx1.xy * (uTime * 0.73 + 0.17); // Only used for Lava color blending
    
    vec3 texA = (uHasTexture == 1) ? texture(uBaseColor, sampleUv).rgb : vec3(1.0);
    vec3 texB = (uHasTexture == 1) ? texture(uBaseColor, sampleUv2).rgb : vec3(1.0);
    
    float blend = (kind == 2) ? (0.5 + 0.5 * sin(vWorldPos.x * 2.1 + vWorldPos.z * 1.7 + uTime * 0.8)) : 0.0;
    vec3 tex = mix(texA, texB, blend);
    
    vec3 albedo = tex * uTint;
    
    vec3 detailN = useRelief
        ? reliefNormal(sampleUv, baseN, max(0.08, uMaterialParams.z * 0.55 + uParallaxScale * 4.5))
        : detailNormalFromAlbedo(uBaseColor, sampleUv, uTexelSize, baseN, uMaterialParams.z, uHasTexture);
        
    // Dual-scrolling normals for water
    vec3 mapN;
    if (kind == 1) {
        // Procedural normals via Gerstner math
        // Scale world pos significantly to map waves cleanly to the small basin size
        vec3 pN = proceduralWaterNormal(vWorldPos.xz * 6.0, uTime); 
        mapN = normalize(pN); // Transform to tangent space
    } else {
        mapN = texture(uNormalMap, sampleUv).xyz * 2.0 - 1.0;
    }
    
    vec3 n = (uHasNormalMap == 1)
        ? normalize(mix(normalize(vTbn * mapN), detailN, useRelief ? 0.20 : 0.08))
        : detailN;
        
    // Override n directly for procedural water to avoid normal map flattening
    // Note: The world positions are usually small numbers in this engine (e.g. 0 to 10)
    // multiplying by 6.0 forces much tighter waves.
    if (kind == 1) n = normalize(vTbn * proceduralWaterNormal(vWorldPos.xz * 6.0, uTime));
        
    float roughness = (uHasRoughnessMap == 1) ? texture(uRoughnessMap, sampleUv).r : uMaterialParams.x;
    roughness = clamp(max(roughness, uMaterialParams.x * 0.45), 0.02, 1.0);
    
    float metallic = (uHasMetallicMap == 1) ? texture(uMetallicMap, sampleUv).r : uMaterialParams.y;
    float ao = (uHasAoMap == 1) ? texture(uAoMap, sampleUv).r : 1.0;
    
    float vis = pcfShadow(vWorldPos, n);
    reliefShadow = useRelief ? reliefShadowTerm(sampleUv, max(0.08, uMaterialParams.z * 0.55 + uParallaxScale * 4.5)) : 1.0;
    
    vec3 f0 = mix(vec3(0.04), albedo, metallic);
    vec3 diffuseColor = albedo * (1.0 - metallic);
    vec3 ambient = diffuseColor * iblAmbient(n) * ao * reliefShadow;
    vec3 lit = ambient
             + directSun(n, diffuseColor, vis) * mix(0.68, 1.0, reliefShadow)
             + sunSpecular(n, viewDir, -uSunDir, roughness, f0, vis) * mix(0.82, 1.0, reliefShadow)
             + albedo * uSelfIllum;
    if (kind == 1) {
        // Procedural Deep & Shallow Colors
        float h = proceduralWaterHeight(vWorldPos.xz * 6.0, uTime);
        vec3 deepColor = vec3(0.01, 0.1, 0.15);
        vec3 shallowColor = vec3(0.05, 0.3, 0.4);
        vec3 waterAlbedo = mix(deepColor, shallowColor, clamp((h + 0.1) * 5.0, 0.0, 1.0));
        
        float baseFresnel = uMaterialFx0.w * 0.1; 
        float fresnel = baseFresnel + (1.0 - baseFresnel) * pow(1.0 - max(dot(n, viewDir), 0.0), 5.0);
        
        float sparkle = pow(max(dot(reflect(-viewDir, n), -uSunDir), 0.0), 256.0); // Extremely sharp sun reflection
        
        vec3 reflection = iblAmbient(n) * 1.5; // Skybox reflection
        
        lit = mix(waterAlbedo, reflection, clamp(fresnel, 0.0, 1.0));
        lit += uSunColor * sparkle * vis * 2.0; // Add intense sun glint on wave peaks
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
in mat3 vTbn;
layout(location=0) out vec4 oColor;
layout(location=1) out vec4 oViewNormal;
uniform sampler2D uBaseColor;
uniform sampler2D uNormalMap;
uniform sampler2D uRoughnessMap;
uniform sampler2D uMetallicMap;
uniform sampler2D uAoMap;
uniform vec4 uBaseColorFactor;
uniform int  uHasTexture;
uniform int  uHasNormalMap;
uniform int  uHasRoughnessMap;
uniform int  uHasMetallicMap;
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
    vec3 mapN = texture(uNormalMap, vUv).xyz * 2.0 - 1.0;
    vec3 n = (uHasNormalMap == 1)
        ? normalize(vTbn * mapN)
        : detailNormalFromAlbedo(uBaseColor, vUv, uTexelSize, geomN, uMaterialParams.z, uHasTexture);
    float roughness = (uHasRoughnessMap == 1) ? texture(uRoughnessMap, vUv).r : uMaterialParams.x;
    float metallic = (uHasMetallicMap == 1) ? texture(uMetallicMap, vUv).r : uMaterialParams.y;
    float ao = (uHasAoMap == 1) ? texture(uAoMap, vUv).r : 1.0;
    vec3 lightDir = normalize(uViewSpaceLighting == 1 ? uToSunView : -uSunDir);
    vec3 viewDir = normalize(uViewSpaceLighting == 1 ? -vWorldPos : (uCameraPos - vWorldPos));
    float vis = (uViewSpaceLighting == 1) ? 1.0 : pcfShadow(vWorldPos, normalize(vNormal));
    
    vec3 albedo = base.rgb;
    vec3 f0 = mix(vec3(0.04), albedo, metallic);
    vec3 diffuseColor = albedo * (1.0 - metallic);

    vec3 lit = diffuseColor * iblAmbient(n) * ao
             + diffuseColor * uSunColor * uSunIntensity * max(dot(n, lightDir), 0.0) * vis
             + sunSpecular(n, viewDir, lightDir, roughness, f0, vis);
    if (uApplyFog == 1)
        lit = applyFog(lit, vWorldPos, uMaterialParams.w);
    oColor = vec4(lit, base.a);
    oViewNormal = (uWriteNormal == 1) ? vec4(normalize(vViewNormal), 1.0) : vec4(0.0);
}
""";
}
