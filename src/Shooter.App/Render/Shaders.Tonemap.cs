namespace Shooter.Render;

internal static partial class Shaders
{
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
uniform int uFxaaEnabled;

vec3 aces(vec3 x){
    const float a = 2.51;
    const float b = 0.03;
    const float c = 2.43;
    const float d = 0.59;
    const float e = 0.14;
    return clamp((x * (a * x + b)) / (x * (c * x + d) + e), 0.0, 1.0);
}
vec3 tonemap(vec2 uv){
    vec3 hdr = texture(uHdr, uv).rgb;
    vec3 bloom = texture(uBloom, uv).rgb;
    float ao = texture(uAo, uv).r;
    float aoMul = mix(1.0, ao, clamp(uAoStrength, 0.0, 1.0));
    vec3 c = (hdr * aoMul + bloom * uBloomStrength) * uExposure;
    c = aces(c);
    float lum = dot(c, vec3(0.2126, 0.7152, 0.0722));
    vec3 cool = vec3(1.0 - uShadowCool * 0.35, 1.0, 1.0 + uShadowCool);
    vec3 warm = vec3(1.0 + uHighlightWarm, 1.0 + uHighlightWarm * 0.35, 1.0 - uHighlightWarm * 0.55);
    c *= mix(cool, warm, smoothstep(0.18, 0.90, lum));
    c = mix(vec3(lum), c, uSaturation);
    c = (c - 0.5) * uContrast + 0.5;
    return clamp(c, 0.0, 1.0);
}

float luma(vec3 c){ return dot(c, vec3(0.299, 0.587, 0.114)); }

void main(){
    vec3 c = tonemap(vUv);
    if (uFxaaEnabled == 1) {
        vec2 texel = 1.0 / vec2(textureSize(uHdr, 0));
        vec3 rgbNW = tonemap(vUv + texel * vec2(-1.0, -1.0));
        vec3 rgbNE = tonemap(vUv + texel * vec2( 1.0, -1.0));
        vec3 rgbSW = tonemap(vUv + texel * vec2(-1.0,  1.0));
        vec3 rgbSE = tonemap(vUv + texel * vec2( 1.0,  1.0));
        float lumaNW = luma(rgbNW);
        float lumaNE = luma(rgbNE);
        float lumaSW = luma(rgbSW);
        float lumaSE = luma(rgbSE);
        float lumaM  = luma(c);
        float lumaMin = min(lumaM, min(min(lumaNW, lumaNE), min(lumaSW, lumaSE)));
        float lumaMax = max(lumaM, max(max(lumaNW, lumaNE), max(lumaSW, lumaSE)));
        vec2 dir;
        dir.x = -((lumaNW + lumaNE) - (lumaSW + lumaSE));
        dir.y =  ((lumaNW + lumaSW) - (lumaNE + lumaSE));
        float dirReduce = max((lumaNW + lumaNE + lumaSW + lumaSE) * 0.25 * 0.25, 0.0001);
        float rcpDirMin = 1.0 / (min(abs(dir.x), abs(dir.y)) + dirReduce);
        dir = clamp(dir * rcpDirMin * texel * 1.5, -8.0 * texel, 8.0 * texel);
        vec3 rgbA = 0.5 * (tonemap(vUv + dir * (1.0 / 3.0 - 0.5)) + tonemap(vUv + dir * (2.0 / 3.0 - 0.5)));
        vec3 rgbB = rgbA * 0.5 + 0.25 * (tonemap(vUv + dir * -0.5) + tonemap(vUv + dir * 0.5));
        float lumaB = luma(rgbB);
        c = (lumaB < lumaMin || lumaB > lumaMax) ? rgbA : rgbB;
    } else {
        // Optional subtle RCAS / Unsharp mask if FXAA is OFF (used alongside TAA)
        vec2 texel = 1.0 / vec2(textureSize(uHdr, 0));
        vec3 cTL = tonemap(vUv + texel * vec2(-1.0, -1.0));
        vec3 cTC = tonemap(vUv + texel * vec2( 0.0, -1.0));
        vec3 cTR = tonemap(vUv + texel * vec2( 1.0, -1.0));
        vec3 cML = tonemap(vUv + texel * vec2(-1.0,  0.0));
        vec3 cMC = c;
        vec3 cMR = tonemap(vUv + texel * vec2( 1.0,  0.0));
        vec3 cBL = tonemap(vUv + texel * vec2(-1.0,  1.0));
        vec3 cBC = tonemap(vUv + texel * vec2( 0.0,  1.0));
        vec3 cBR = tonemap(vUv + texel * vec2( 1.0,  1.0));
        
        vec3 blurred = (cTL + cTC*2.0 + cTR + cML*2.0 + cMC*4.0 + cMR*2.0 + cBL + cBC*2.0 + cBR) / 16.0;
        // Moderate unsharp mask
        c = clamp(c + (c - blurred) * 1.5, 0.0, 1.0);
    }
    c = pow(c, vec3(1.0 / 2.2));
    vec2 q = vUv * 2.0 - 1.0;
    float vignette = 1.0 - dot(q, q) * 0.22 * uVignetteStrength;
    c *= clamp(vignette, 0.0, 1.0);
    FragColor = vec4(c, 1.0);
}
""";
}
