namespace Shooter.Render;

/// <summary>GLSL source for the small set of shaders the game uses.
///
/// All shaders are GL 3.3 core. World-space lit shaders share a common preamble:
/// `WorldVert` outputs world position + normal + uv, and the lit fragments sample
/// a shadow map (sampler2DShadow), an irradiance cubemap (samplerCube), and apply
/// Lambert + visibility against the sun.</summary>
internal static class Shaders
{
    /// <summary>Snippet appended into lit fragment shaders. Provides:
    /// `vec3 iblAmbient(vec3 n)`, `float pcfShadow(vec4 lsPos)` and the standard
    /// uniform set. Inserted via string concat where used.</summary>
    public const string LightingHeader = """
uniform sampler2DShadow uShadowMap;
uniform samplerCube uIrradiance;
uniform mat4  uLightSpace;
uniform vec3  uSunDir;          // direction light travels (normalized)
uniform vec3  uSunColor;        // linear HDR
uniform float uSunIntensity;    // HDR multiplier
uniform float uIrradianceIntensity;
uniform float uShadowSoftness;
uniform vec3  uCameraPos;
uniform vec3  uToSunView;
uniform vec3  uFogColor;
uniform float uFogDensity;
uniform float uFogStart;
uniform float uFogHeightFalloff;
uniform float uFogBaseHeight;
uniform float uTime;
uniform int   uReceiveShadows;  // 0 = always lit, 1 = sample shadow map

vec3 iblAmbient(vec3 n){
    return texture(uIrradiance, n).rgb * uIrradianceIntensity;
}

vec3 tangentFromNormal(vec3 n){
    vec3 up = abs(n.y) < 0.999 ? vec3(0.0, 1.0, 0.0) : vec3(1.0, 0.0, 0.0);
    return normalize(cross(up, n));
}

mat3 cotangentFrame(vec3 n, vec3 p, vec2 uv){
    vec3 dp1 = dFdx(p);
    vec3 dp2 = dFdy(p);
    vec2 duv1 = dFdx(uv);
    vec2 duv2 = dFdy(uv);
    vec3 dp2perp = cross(dp2, n);
    vec3 dp1perp = cross(n, dp1);
    vec3 t = dp2perp * duv1.x + dp1perp * duv2.x;
    vec3 b = dp2perp * duv1.y + dp1perp * duv2.y;
    float invmax = inversesqrt(max(dot(t, t), dot(b, b)));
    return mat3(t * invmax, b * invmax, n);
}

vec3 normalFromMap(sampler2D normalMap, vec3 p, vec2 uv, vec3 n){
    vec3 tnorm = texture(normalMap, uv).xyz * 2.0 - 1.0;
    return normalize(cotangentFrame(n, p, uv) * tnorm);
}

vec3 detailNormalFromAlbedo(sampler2D tex, vec2 uv, vec2 texel, vec3 n, float strength, int hasTexture){
    if (hasTexture == 0 || strength <= 0.0001 || texel.x <= 0.0 || texel.y <= 0.0)
        return normalize(n);
    vec3 luma = vec3(0.2126, 0.7152, 0.0722);
    float left  = dot(texture(tex, uv - vec2(texel.x, 0.0)).rgb, luma);
    float right = dot(texture(tex, uv + vec2(texel.x, 0.0)).rgb, luma);
    float down  = dot(texture(tex, uv - vec2(0.0, texel.y)).rgb, luma);
    float up    = dot(texture(tex, uv + vec2(0.0, texel.y)).rgb, luma);
    vec3 t = tangentFromNormal(n);
    vec3 b = normalize(cross(n, t));
    vec3 mapN = normalize(vec3((left - right) * strength * 6.0, (down - up) * strength * 6.0, 1.0));
    return normalize(mat3(t, b, n) * mapN);
}

float pcfShadow(vec3 worldPos, vec3 n){
    if (uReceiveShadows == 0) return 1.0;
    vec4 lp = uLightSpace * vec4(worldPos, 1.0);
    vec3 proj = lp.xyz / lp.w;
    proj = proj * 0.5 + 0.5;
    if (proj.z > 1.0 || proj.x < 0.0 || proj.x > 1.0 || proj.y < 0.0 || proj.y > 1.0)
        return 1.0; // outside frustum: fully lit
    float bias = max(0.00022 * (1.0 - max(dot(n, -uSunDir), 0.0)), 0.00005);
    float depth = proj.z - bias;
    vec2 texel = 1.0 / vec2(textureSize(uShadowMap, 0));
    float distToCamera = length(uCameraPos - worldPos);
    float adaptiveSoftness = mix(0.75, 1.65, clamp((distToCamera - 6.0) / 28.0, 0.0, 1.0)) * uShadowSoftness;
    vec2 taps[16] = vec2[](
        vec2(-0.326, -0.406), vec2(-0.840, -0.074), vec2(-0.696,  0.457), vec2(-0.203,  0.621),
        vec2( 0.962, -0.195), vec2( 0.473, -0.480), vec2( 0.519,  0.767), vec2( 0.185, -0.893),
        vec2( 0.507,  0.064), vec2( 0.896,  0.412), vec2(-0.322, -0.933), vec2(-0.792, -0.598),
        vec2(-0.118,  0.122), vec2( 0.142, -0.132), vec2(-0.452,  0.212), vec2( 0.328,  0.286)
    );
    float sum = 0.0;
    for (int i = 0; i < 16; ++i)
        sum += texture(uShadowMap, vec3(proj.xy + taps[i] * texel * adaptiveSoftness, depth));
    return sum / 16.0;
}

vec3 directSun(vec3 n, vec3 albedo, float visibility){
    float ndl = max(dot(n, -uSunDir), 0.0);
    return albedo * uSunColor * uSunIntensity * ndl * visibility;
}

vec3 sunSpecular(vec3 n, vec3 viewDir, vec3 lightDir, float roughness, float specularStrength, float visibility){
    float ndl = max(dot(n, lightDir), 0.0);
    float ndv = max(dot(n, viewDir), 0.0);
    if (ndl <= 0.0 || ndv <= 0.0 || specularStrength <= 0.0001) return vec3(0.0);
    vec3 h = normalize(viewDir + lightDir);
    float ndh = max(dot(n, h), 0.0);
    float shininess = mix(96.0, 12.0, clamp(roughness, 0.0, 1.0));
    float spec = pow(ndh, shininess) * ndl * visibility;
    float fres = mix(0.04, 1.0, pow(1.0 - ndv, 5.0));
    return uSunColor * uSunIntensity * spec * fres * specularStrength;
}

vec3 applyFog(vec3 color, vec3 worldPos, float applyAmount){
    if (applyAmount <= 0.0) return color;
    float dist = max(length(uCameraPos - worldPos) - uFogStart, 0.0);
    float fog = 1.0 - exp(-dist * uFogDensity);
    float height = clamp(exp(-(worldPos.y - uFogBaseHeight) * uFogHeightFalloff), 0.0, 1.0);
    fog = clamp(fog * height, 0.0, 1.0);
    return mix(color, uFogColor, fog);
}
""";

    public const string WorldVert = """
#version 330 core
layout(location=0) in vec3 aPos;
layout(location=1) in vec3 aNormal;
layout(location=2) in vec2 aUv;
uniform mat4 uModel;
uniform mat4 uView;       // for view-space normal output (SSAO input)
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

    /// <summary>World brush + pickup fragment. HDR linear out. Uses IBL ambient + Lambert direct
    /// with PCF shadows. `uTint` is the surface albedo; `uSelfIllum` lets pickups read above
    /// surrounding ambient (small additive term).</summary>
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
uniform vec4 uMaterialParams; // x roughness, y specular, z detail-normal strength, w apply fog
uniform vec4 uMaterialFx0;    // x kind(0 std,1 water,2 lava), y emissive, z opacity, w fresnel
uniform vec4 uMaterialFx1;    // x flowU, y flowV, z distortion, w pulse
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

    /// <summary>Pickup uses the same shader as the world; the pickup pass sets uSelfIllum > 0.
    /// Kept as separate constants so callers can be explicit even though they alias.</summary>
    public const string PickupVert = WorldVert;
    public static readonly string PickupFrag = WorldFrag;

    /// <summary>Textured GLB model fragment shader: weapon viewmodel + rocket.
    /// Receives shadows only when uReceiveShadows == 1 (rocket yes, viewmodel no).</summary>
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
uniform int  uWriteNormal;     /* 0 = view-space objects (skip normal write) */
uniform int  uViewSpaceLighting;
uniform int  uApplyFog;
uniform vec2 uTexelSize;
uniform vec4 uMaterialParams;  /* x roughness, y specular, z detail, w applyFog */
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

    // ---------------------------------------------------------------------
    // Sky (analytic, HDR)
    // ---------------------------------------------------------------------

    /// <summary>Cube vertex shader for the sky. Uses the depth=1 trick so the cube sits at the
    /// far plane and any opaque geometry drawn before depth-test occludes it correctly.</summary>
    public const string SkyVert = """
#version 330 core
layout(location=0) in vec3 aPos;
uniform mat4 uViewNoTrans;  // view matrix with translation removed
uniform mat4 uProj;
out vec3 vDir;
void main(){
    vDir = aPos;
    vec4 clip = uProj * uViewNoTrans * vec4(aPos, 1.0);
    gl_Position = clip.xyww; // z = w → depth = 1
}
""";

    /// <summary>Common atmosphere snippet shared by the in-scene sky and the IBL-probe sky.</summary>
    public const string AtmosphereSnippet = """
uniform vec3 uToSun;       // normalized direction toward the sun
uniform float uTurbidity;  // 2..10
uniform vec3 uGroundAlbedo;

vec3 atmosphere(vec3 dir){
    float sunCos = clamp(dot(dir, uToSun), -1.0, 1.0);
    float sunDot = max(sunCos, 0.0);
    float t = clamp(1.0 - max(dir.y, 0.0), 0.0, 1.0);
    vec3 zenith  = vec3(0.32, 0.55, 1.05);   // blue HDR
    vec3 horizon = vec3(0.95, 0.85, 0.75);   // warm pale
    vec3 sky = mix(zenith, horizon, pow(t, 2.5));
    float mie = pow(sunDot, 8.0) * 0.5 + pow(sunDot, 256.0) * 8.0;
    sky += vec3(1.6, 1.2, 0.85) * mie;
    if (dir.y < 0.0){
        float gy = clamp(-dir.y * 5.0, 0.0, 1.0);
        sky = mix(sky, uGroundAlbedo * 0.25, gy);
    }
    sky *= mix(0.85, 1.35, clamp((uTurbidity - 2.0) / 8.0, 0.0, 1.0));
    return sky;
}
""";

    /// <summary>In-scene sky: writes to both color and normal attachments of HdrTarget.</summary>
    public static readonly string SkyFrag = """
#version 330 core
in vec3 vDir;
layout(location=0) out vec4 oColor;
layout(location=1) out vec4 oViewNormal;
""" + "\n" + AtmosphereSnippet + "\n" + """
void main(){
    vec3 dir = normalize(vDir);
    oColor = vec4(atmosphere(dir), 1.0);
    oViewNormal = vec4(0.0);
}
""";

    /// <summary>Used by IblProbe when rendering the sky into 6 cubemap faces. Vertex emits a
    /// per-face direction; fragment evaluates the same atmosphere function. Single-attachment
    /// FBO so this variant outputs only one color.</summary>
    public const string SkyFaceVert = """
#version 330 core
layout(location=0) in vec2 aQuad; // [-1,1]
uniform vec3 uFaceRight;
uniform vec3 uFaceUp;
uniform vec3 uFaceForward;
out vec3 vDir;
void main(){
    vDir = normalize(uFaceForward + aQuad.x * uFaceRight + aQuad.y * uFaceUp);
    gl_Position = vec4(aQuad, 0.0, 1.0);
}
""";

    public static readonly string SkyFaceFrag = """
#version 330 core
in vec3 vDir;
out vec4 FragColor;
""" + "\n" + AtmosphereSnippet + "\n" + """
void main(){
    vec3 dir = normalize(vDir);
    FragColor = vec4(atmosphere(dir), 1.0);
}
""";

    // ---------------------------------------------------------------------
    // IBL convolution
    // ---------------------------------------------------------------------

    /// <summary>Convolves a sky cubemap into a tiny diffuse irradiance cubemap. Each output texel
    /// integrates cosine-weighted samples over the hemisphere of its own normal direction.</summary>
    public const string IrradianceConvolveVert = SkyFaceVert;

    public const string IrradianceConvolveFrag = """
#version 330 core
in vec3 vDir;
out vec4 FragColor;
uniform samplerCube uSky;
const float PI = 3.14159265359;
void main(){
    vec3 N = normalize(vDir);
    // Build a tangent basis at N.
    vec3 up = abs(N.y) < 0.999 ? vec3(0,1,0) : vec3(1,0,0);
    vec3 right = normalize(cross(up, N));
    up = normalize(cross(N, right));
    vec3 sum = vec3(0.0);
    int samples = 0;
    // Stratified hemisphere walk.
    for (int p = 0; p < 8; ++p){       // phi steps
        for (int t = 0; t < 8; ++t){   // theta steps
            float phi = (float(p) + 0.5) / 8.0 * 2.0 * PI;
            float thetaN = (float(t) + 0.5) / 8.0;
            float theta = thetaN * 0.5 * PI;
            float sinT = sin(theta), cosT = cos(theta);
            vec3 dir = right * (sinT * cos(phi)) + up * (sinT * sin(phi)) + N * cosT;
            sum += texture(uSky, dir).rgb * cosT * sinT;
            samples++;
        }
    }
    sum *= PI / float(samples);
    FragColor = vec4(sum, 1.0);
}
""";

    // ---------------------------------------------------------------------
    // Shadow map (depth-only)
    // ---------------------------------------------------------------------

    public const string ShadowDepthVert = """
#version 330 core
layout(location=0) in vec3 aPos;
uniform mat4 uModel;
uniform mat4 uLightSpace;
void main(){
    gl_Position = uLightSpace * uModel * vec4(aPos, 1.0);
}
""";

    public const string ShadowDepthFrag = """
#version 330 core
void main(){}
""";

    // ---------------------------------------------------------------------
    // Decals / tracers (HDR-aware: output linear values that read sensibly)
    // ---------------------------------------------------------------------

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
    oColor = vec4(0.025, 0.018, 0.015, 1.0); // dark, alpha-blended; survives ACES.
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

    // ---------------------------------------------------------------------
    // HUD (LDR pass after tonemap)
    // ---------------------------------------------------------------------

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

    // ---------------------------------------------------------------------
    // Muzzle flash (HDR-boosted so it punches through ACES)
    // ---------------------------------------------------------------------

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
uniform float uIntensity;   // 0..1
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

    // ---------------------------------------------------------------------
    // Scorch (HDR-aware: dark sooty material; minor self-illum at core for bloom punch)
    // ---------------------------------------------------------------------

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

    // ---------------------------------------------------------------------
    // Bloom + ACES post
    // ---------------------------------------------------------------------

    /// <summary>Fullscreen-triangle vertex shader used by every fullscreen post pass.
    /// Generates clip-space position + uv from gl_VertexID, no VBO required by callers
    /// other than a 3-vertex empty draw.</summary>
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

    /// <summary>Bloom threshold: keep the bright HDR content above ~1.0 with a soft knee.</summary>
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

    /// <summary>Karis-average 13-tap downsample. Reads 13 samples from <c>uSrc</c> at <c>uTexel</c>
    /// resolution and averages them with the standard down filter weights.</summary>
    public const string BloomDownFrag = """
#version 330 core
in vec2 vUv;
out vec4 FragColor;
uniform sampler2D uSrc;
uniform vec2 uTexel; // 1.0 / src size
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

    /// <summary>Tent-filter upsample. Adds <c>uSrc</c> (smaller mip) into the destination at the
    /// caller's blend state.</summary>
    public const string BloomUpFrag = """
#version 330 core
in vec2 vUv;
out vec4 FragColor;
uniform sampler2D uSrc;
uniform vec2 uTexel;       // 1.0 / src size
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

    /// <summary>Final post pass: HDR + bloom → SSAO modulate → ACES → gamma → default FB.</summary>
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

    // ---------------------------------------------------------------------
    // SSAO + bilateral-ish blur
    // ---------------------------------------------------------------------

    /// <summary>Screen-space ambient occlusion. Reads depth + view-space normal, samples
    /// a hemisphere of 16 cosine-distributed offsets oriented along the surface normal,
    /// counts how many are occluded by depth-buffer geometry. Outputs 1.0 (no AO) where
    /// depth == 1.0 (sky / nothing rendered).</summary>
    public const string SsaoFrag = """
#version 330 core
in vec2 vUv;
out float oAO;
uniform sampler2D uDepth;
uniform sampler2D uNormal;
uniform sampler2D uNoise;
uniform vec2 uNoiseScale;     // screen / 4
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
    if (d >= 0.99999) { oAO = 1.0; return; }   // sky / cleared
    vec3 P = viewPos(vUv);
    vec3 N = texture(uNormal, vUv).xyz;
    if (dot(N, N) < 0.001) { oAO = 1.0; return; }
    N = normalize(N);
    vec3 rnd = texture(uNoise, vUv * uNoiseScale).xyz;
    rnd = vec3(rnd.xy * 2.0 - 1.0, 0.0); // tangent-plane rotation only
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

    /// <summary>Simple 4×4 box blur over the AO buffer to hide noise. Edge-preserving by
    /// rejecting samples whose depth differs significantly from the centre pixel.</summary>
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

    // ---------------------------------------------------------------------
    // Auto-exposure: log-luminance pass; mipmaps generated externally.
    // ---------------------------------------------------------------------

    /// <summary>Reads HDR scene color, outputs log-luminance into a single-channel texture.
    /// The renderer then calls glGenerateMipmap and reads the 1×1 mip on the CPU.</summary>
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
