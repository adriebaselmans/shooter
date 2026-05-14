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
}
