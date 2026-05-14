namespace Shooter.Render;

internal static partial class Shaders
{
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
    float contrib = (bright > 0.0) ? max(soft, max(bright - 1.0, 0.0)) / max(bright, 1e-4) : 0.0;
    FragColor = vec4(c * contrib, 1.0);
}
""";

    public const string BloomDownFrag = """
#version 330 core
in vec2 vUv;
out vec4 FragColor;
uniform sampler2D uSrc;
uniform vec2 uTexel;
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

    public const string BloomUpFrag = """
#version 330 core
in vec2 vUv;
out vec4 FragColor;
uniform sampler2D uSrc;
uniform vec2 uTexel;
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
}
