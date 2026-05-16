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

    public static readonly string WaterVert = """
#version 330 core
layout(location=0) in vec3 aPos;
layout(location=1) in vec3 aNormal;
layout(location=2) in vec2 aUv;
layout(location=3) in vec3 aTangent;
layout(location=4) in vec3 aBitangent;
uniform mat4 uModel;
uniform mat4 uView;
uniform mat4 uViewProj;
uniform vec4 uMaterialFx1;
uniform float uTime;
out vec3 vWorldPos;
out vec3 vNormal;
out vec3 vViewNormal;
out vec2 vUv;
out mat3 vTbn;

float gerstnerWave(vec2 p, vec2 d, float steepness, float wavelength, float speed, float t) {
    float k = 2.0 * 3.14159265 / wavelength;
    float c = sqrt(9.8 / k);
    float f = k * (dot(d, p) - c * t * speed);
    return steepness * sin(f);
}

float proceduralWaterHeight(vec2 p, float t) {
    t *= 0.72;
    float h = 0.0;
    // Calm, broad pool swells
    h += gerstnerWave(p, normalize(vec2(1.0, 0.35)), 0.112, 7.2, 0.52, t);
    h += gerstnerWave(p, normalize(vec2(-0.65, 1.0)), 0.062, 4.4, 0.68, t);
    h += gerstnerWave(p, normalize(vec2(0.9, -0.8)), 0.022, 2.6, 0.92, t);
    // Very soft breakup only
    h += 0.003 * sin(p.x * 1.9 + t * 0.92) * cos(p.y * 2.2 + t * 1.04);
    return h;
}

vec3 proceduralWaterNormal(vec2 p, float t) {
    float eps = 0.035;
    float hx = proceduralWaterHeight(p + vec2(eps, 0.0), t) - proceduralWaterHeight(p - vec2(eps, 0.0), t);
    float hz = proceduralWaterHeight(p + vec2(0.0, eps), t) - proceduralWaterHeight(p - vec2(0.0, eps), t);
    return normalize(vec3(-hx * 10.0, 1.0, -hz * 10.0));
}

void main(){
    vec4 baseWp = uModel * vec4(aPos, 1.0);
    float flowMag = max(length(uMaterialFx1.xy), 0.02);
    vec2 flowDir = dot(uMaterialFx1.xy, uMaterialFx1.xy) > 0.0001
        ? normalize(uMaterialFx1.xy)
        : normalize(vec2(0.8, 0.35));
    // Slower, broader world-space wave field so the motion reads like water instead of chop
    vec2 wavePos = baseWp.xz * 0.82 + flowDir * uTime * (0.18 + flowMag * 1.25);
    float waveHeight = proceduralWaterHeight(wavePos, uTime) * 0.125;
    vec3 worldPos = baseWp.xyz + vec3(0.0, waveHeight, 0.0);
    vec3 wn = proceduralWaterNormal(wavePos, uTime);
    vec3 wt = normalize(vec3(1.0, 0.0, 0.0) - wn * dot(wn, vec3(1.0, 0.0, 0.0)));
    if (length(wt) < 0.001) wt = normalize(cross(vec3(0.0, 0.0, 1.0), wn));
    vec3 wb = normalize(cross(wn, wt));

    vWorldPos = worldPos;
    vNormal = wn;
    vViewNormal = mat3(uView) * wn;
    vUv = aUv;
    vTbn = mat3(wt, wb, wn);
    gl_Position = uViewProj * vec4(worldPos, 1.0);
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
    float eps = 0.05;
    float h1 = proceduralWaterHeight(p + vec2(eps, 0.0), t);
    float h2 = proceduralWaterHeight(p - vec2(eps, 0.0), t);
    float h3 = proceduralWaterHeight(p + vec2(0.0, eps), t);
    float h4 = proceduralWaterHeight(p - vec2(0.0, eps), t);
    // Increase the x/z components massively relative to Y to force deep normals
    return normalize(vec3((h1 - h2) * 50.0, 1.0, (h3 - h4) * 50.0));
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
    float reliefAmount = (kind == 0 && uEnableParallax == 1 && uHasHeightMap == 1)
        ? clamp(uParallaxScale / 0.12, 0.0, 1.0)
        : 0.0;
    bool useRelief = reliefAmount > 0.001;
    float reliefStrength = max(0.02, max(uMaterialParams.z, 0.35) * mix(0.12, 1.45, reliefAmount));
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
        ? reliefNormal(sampleUv, baseN, reliefStrength)
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
    
    float reliefBlend = useRelief ? mix(0.03, 0.36, reliefAmount) : 0.08;
    vec3 n = (uHasNormalMap == 1)
        ? normalize(mix(normalize(vTbn * mapN), detailN, reliefBlend))
        : detailN;
        
    // Override n directly for procedural water
    // Transform raw math normals into absolute world space. 
    // In our coordinate system, Y is up. The normal calculation returned (x, y, z) 
    // where x/z represent horizontal slopes, and y represents vertical up.
    if (kind == 1) {
        vec3 pN = proceduralWaterNormal(vWorldPos.xz * 6.0, uTime);
        n = normalize(vec3(pN.x, pN.y, pN.z));
    }
        
    float roughness = (uHasRoughnessMap == 1) ? texture(uRoughnessMap, sampleUv).r : uMaterialParams.x;
    roughness = clamp(max(roughness, uMaterialParams.x * 0.45), 0.02, 1.0);
    
    float metallic = (uHasMetallicMap == 1) ? texture(uMetallicMap, sampleUv).r : uMaterialParams.y;
    float ao = (uHasAoMap == 1) ? texture(uAoMap, sampleUv).r : 1.0;
    
    float vis = pcfShadow(vWorldPos, n);
    reliefShadow = useRelief ? mix(1.0, reliefShadowTerm(sampleUv, reliefStrength), reliefAmount) : 1.0;
    
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
        
        vec3 reflectDir = reflect(-viewDir, n);
        float sparkle = pow(max(dot(reflectDir, -uSunDir), 0.0), 192.0);
        
        // Real directional sky reflection. Using irradiance here was too blurred to reveal ripples.
        vec3 reflection = texture(uSkyCube, reflectDir).rgb * 1.35;
        
        lit = mix(waterAlbedo, reflection, clamp(fresnel, 0.0, 1.0));
        lit += uSunColor * sparkle * vis * 2.4;
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

    public static readonly string WaterFrag = """
#version 330 core
in vec3 vWorldPos;
in vec3 vNormal;
in vec3 vViewNormal;
in vec2 vUv;
in mat3 vTbn;
layout(location=0) out vec4 oColor;
layout(location=1) out vec4 oViewNormal;
uniform sampler2D uSceneColor;
uniform sampler2D uSceneDepth;
uniform vec2 uInvViewport;
uniform mat4 uInvProj;
uniform vec3 uTint;
uniform vec4 uMaterialFx0;
uniform vec4 uMaterialFx1;
""" + "\n" + LightingHeader + "\n" + AtmosphereSnippet + "\n" + """
float gerstnerWave(vec2 p, vec2 d, float steepness, float wavelength, float speed, float t) {
    float k = 2.0 * 3.14159265 / wavelength;
    float c = sqrt(9.8 / k);
    float f = k * (dot(d, p) - c * t * speed);
    return steepness * sin(f);
}

float proceduralWaterHeight(vec2 p, float t) {
    t *= 0.72;
    float h = 0.0;
    h += gerstnerWave(p, normalize(vec2(1.0, 0.35)), 0.112, 7.2, 0.52, t);
    h += gerstnerWave(p, normalize(vec2(-0.65, 1.0)), 0.062, 4.4, 0.68, t);
    h += gerstnerWave(p, normalize(vec2(0.9, -0.8)), 0.022, 2.6, 0.92, t);
    h += 0.003 * sin(p.x * 1.9 + t * 0.92) * cos(p.y * 2.2 + t * 1.04);
    return h;
}

vec3 proceduralWaterNormal(vec2 p, float t) {
    float eps = 0.035;
    float hx = proceduralWaterHeight(p + vec2(eps, 0.0), t) - proceduralWaterHeight(p - vec2(eps, 0.0), t);
    float hz = proceduralWaterHeight(p + vec2(0.0, eps), t) - proceduralWaterHeight(p - vec2(0.0, eps), t);
    return normalize(vec3(-hx * 10.0, 1.0, -hz * 10.0));
}

float linearViewDepth(vec2 uv, float rawDepth) {
    vec4 clip = vec4(uv * 2.0 - 1.0, rawDepth * 2.0 - 1.0, 1.0);
    vec4 view = uInvProj * clip;
    return -view.z / max(view.w, 0.0001);
}

void main(){
    vec2 screenUv = gl_FragCoord.xy * uInvViewport;
    vec3 geomN = normalize(vNormal);
    vec3 viewDir = normalize(uCameraPos - vWorldPos);
    float flowMag = max(length(uMaterialFx1.xy), 0.02);
    vec2 flowDir = dot(uMaterialFx1.xy, uMaterialFx1.xy) > 0.0001
        ? normalize(uMaterialFx1.xy)
        : normalize(vec2(0.8, 0.35));
    vec2 wavePos = vWorldPos.xz * 0.82 + flowDir * uTime * (0.18 + flowMag * 1.25);

    // Keep distortion/macro motion stable, but add a slightly finer shading normal
    // so highlights break up into smaller pieces without bringing back jitter.
    vec3 nDistort = normalize(mix(geomN, proceduralWaterNormal(wavePos * 0.92 + flowDir * uTime * 0.04, uTime), 0.035));
    vec3 nShade = normalize(mix(geomN, proceduralWaterNormal(wavePos * 2.05 + flowDir * uTime * 0.08, uTime), 0.11));
    float vis = mix(0.86, 1.0, pcfShadow(vWorldPos, nShade));

    float waterDepth = linearViewDepth(screenUv, gl_FragCoord.z);
    float sceneRawDepth = texture(uSceneDepth, screenUv).r;
    float floorDepth = linearViewDepth(screenUv, sceneRawDepth);
    float thickness = max(floorDepth - waterDepth, 0.0);
    float shallow = 1.0 - smoothstep(0.10, 1.35, thickness);

    float topMask = clamp(dot(geomN, vec3(0.0, 1.0, 0.0)), 0.0, 1.0);
    float distortionStrength = mix(0.0006, 0.0034, shallow) * mix(0.90, 1.30, clamp(uMaterialFx1.z * 6.0, 0.0, 1.0));
    vec2 distortion = mix(geomN.xz, nDistort.xz, 0.30) * distortionStrength * topMask;
    vec2 refractUv = clamp(screenUv + distortion, vec2(0.001), vec2(0.999));
    vec3 floorColor = texture(uSceneColor, refractUv).rgb;

    vec3 reflectDir = reflect(-viewDir, nShade);
    vec3 skyReflectDir = normalize(vec3(reflectDir.x, max(reflectDir.y, -0.02), reflectDir.z));
    vec3 reflection = atmosphere(skyReflectDir) * 1.12;

    vec3 deepColor = vec3(0.05, 0.15, 0.17);
    vec3 shallowColor = vec3(0.10, 0.31, 0.34);
    vec3 tint = mix(deepColor, shallowColor, shallow) * mix(vec3(1.0), uTint, 0.16);
    vec3 transmitted = mix(floorColor, floorColor * tint, mix(0.10, 0.26, shallow));

    float baseFresnel = uMaterialFx0.w * 0.06;
    float fresnel = baseFresnel + (1.0 - baseFresnel) * pow(1.0 - max(dot(nShade, viewDir), 0.0), 5.0);
    float sparkle = pow(max(dot(reflectDir, -uSunDir), 0.0), 160.0);

    vec3 lit = mix(transmitted, reflection, clamp(fresnel, 0.0, 1.0));
    lit += uSunColor * sparkle * vis * mix(1.1, 1.8, topMask);
    lit = applyFog(lit, vWorldPos, 1.0);

    oColor = vec4(lit, 1.0);
    oViewNormal = vec4(0.0);
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
