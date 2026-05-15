namespace Shooter.Render;

/// <summary>Shared shader snippets and partial shader source declarations.</summary>
internal static partial class Shaders
{
    /// <summary>Common lighting helpers used by lit fragment shaders.</summary>
    public const string LightingHeader = """
uniform sampler2DShadow uShadowMap;
uniform samplerCube uIrradiance;
uniform samplerCube uSkyCube;
uniform samplerCube uSpecularEnv;
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

vec3 detailNormalFromHeight(sampler2D heightMap, vec2 uv, vec2 texel, vec3 n, float strength, int hasHeightMap){
    if (hasHeightMap == 0 || strength <= 0.0001 || texel.x <= 0.0 || texel.y <= 0.0)
        return normalize(n);
    float left  = texture(heightMap, uv - vec2(texel.x, 0.0)).r;
    float right = texture(heightMap, uv + vec2(texel.x, 0.0)).r;
    float down  = texture(heightMap, uv - vec2(0.0, texel.y)).r;
    float up    = texture(heightMap, uv + vec2(0.0, texel.y)).r;
    vec3 t = tangentFromNormal(n);
    vec3 b = normalize(cross(n, t));
    vec3 mapN = normalize(vec3((left - right) * strength * 8.0, (down - up) * strength * 8.0, 1.0));
    return normalize(mat3(t, b, n) * mapN);
}

float pcfShadow(vec3 worldPos, vec3 n){
    if (uReceiveShadows == 0) return 1.0;
    vec4 lp = uLightSpace * vec4(worldPos, 1.0);
    vec3 proj = lp.xyz / lp.w;
    proj = proj * 0.5 + 0.5;
    if (proj.z > 1.0 || proj.x < 0.0 || proj.x > 1.0 || proj.y < 0.0 || proj.y > 1.0)
        return 1.0;
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

vec3 directSun(vec3 n, vec3 diffuseColor, float visibility){
    float ndl = max(dot(n, -uSunDir), 0.0);
    return diffuseColor * uSunColor * uSunIntensity * ndl * visibility;
}

// GGX NDF
float D_GGX(float ndh, float roughness) {
    float a = roughness * roughness;
    float a2 = a * a;
    float ndh2 = ndh * ndh;
    float num = ndh2 * (a2 - 1.0) + 1.0;
    return a2 / (3.14159265 * num * num);
}

// Smith Geometry
float G_SchlickGGX(float ndv, float roughness) {
    float r = roughness + 1.0;
    float k = (r * r) / 8.0;
    return ndv / (ndv * (1.0 - k) + k);
}
float GeometrySmith(float ndv, float ndl, float roughness) {
    float ggx1 = G_SchlickGGX(ndv, roughness);
    float ggx2 = G_SchlickGGX(ndl, roughness);
    return ggx1 * ggx2;
}

// Fresnel Schlick
vec3 fresnelSchlick(float cosTheta, vec3 f0) {
    return f0 + (1.0 - f0) * pow(clamp(1.0 - cosTheta, 0.0, 1.0), 5.0);
}

vec3 sunSpecular(vec3 n, vec3 viewDir, vec3 lightDir, float roughness, vec3 f0, float visibility){
    float ndl = max(dot(n, lightDir), 0.0);
    float ndv = max(dot(n, viewDir), 0.0);
    if (ndl <= 0.0 || ndv <= 0.0) return vec3(0.0);
    
    vec3 h = normalize(viewDir + lightDir);
    float ndh = max(dot(n, h), 0.0);
    float hdv = max(dot(h, viewDir), 0.0);

    float NDF = D_GGX(ndh, roughness);
    float G = GeometrySmith(ndv, ndl, roughness);
    vec3 F = fresnelSchlick(hdv, f0);

    vec3 numerator = NDF * G * F;
    float denominator = 4.0 * ndv * ndl + 0.0001;
    vec3 specular = numerator / denominator;

    return uSunColor * uSunIntensity * specular * ndl * visibility;
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
}
