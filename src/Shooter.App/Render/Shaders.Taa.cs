namespace Shooter.Render;

internal static partial class Shaders
{
    public const string TaaResolveFrag = """
#version 330 core
in vec2 vUv;
out vec4 FragColor;

uniform sampler2D uCurrent;
uniform sampler2D uHistory;
uniform sampler2D uVelocity;
uniform int uFirstFrame;

// RGB <-> YCoCg to help clamping (less color shift)
vec3 RGBToYCoCg(vec3 rgb) {
    float y  = dot(rgb, vec3( 0.25, 0.5,  0.25));
    float co = dot(rgb, vec3( 0.5,  0.0, -0.5));
    float cg = dot(rgb, vec3(-0.25, 0.5, -0.25));
    return vec3(y, co, cg);
}

vec3 YCoCgToRGB(vec3 ycocg) {
    float y  = ycocg.x;
    float co = ycocg.y;
    float cg = ycocg.z;
    return vec3(
        y + co - cg,
        y + cg,
        y - co - cg
    );
}

vec3 sampleBicubic(sampler2D tex, vec2 uv, vec2 texSize) {
    vec2 invTexSize = 1.0 / texSize;
    uv = uv * texSize - 0.5;
    vec2 f = fract(uv);
    vec2 p = floor(uv);

    float f2x = f.x * f.x;
    float f3x = f.x * f2x;
    float w0x = f3x - 2.0 * f2x + f.x;
    float w1x = -2.0 * f3x + 3.0 * f2x + 1.0;
    float w2x = f3x - 2.0 * f2x + f.x; // Wait, Catmull-Rom weights are standard, let's use the optimized 5-tap version.
    
    // Optimized Catmull-Rom (5 taps using bilinear hardware)
    vec2 w0 = f * (-0.5 + f * (1.0 - 0.5 * f));
    vec2 w1 = 1.0 + f * f * (-2.5 + 1.5 * f);
    vec2 w2 = f * (0.5 + f * (2.0 - 1.5 * f));
    vec2 w3 = f * f * (-0.5 + 0.5 * f);
    
    vec2 w12 = w1 + w2;
    vec2 offset12 = w2 / (w1 + w2);
    
    vec2 tex0 = p - 1.0;
    vec2 tex3 = p + 2.0;
    vec2 tex12 = p + offset12;
    
    tex0 *= invTexSize;
    tex3 *= invTexSize;
    tex12 *= invTexSize;
    
    vec3 result = vec3(0.0);
    result += texture(tex, vec2(tex0.x, tex12.y)).rgb * w0.x * w12.y;
    result += texture(tex, vec2(tex12.x, tex0.y)).rgb * w12.x * w0.y;
    result += texture(tex, vec2(tex12.x, tex12.y)).rgb * w12.x * w12.y;
    result += texture(tex, vec2(tex3.x, tex12.y)).rgb * w3.x * w12.y;
    result += texture(tex, vec2(tex12.x, tex3.y)).rgb * w12.x * w3.y;
    
    return max(result, 0.0);
}

void main() {
    vec3 current = texture(uCurrent, vUv).rgb;
    if (uFirstFrame == 1) {
        FragColor = vec4(current, 1.0);
        return;
    }

    vec2 velocity = texture(uVelocity, vUv).rg;
    vec2 prevUv = vUv - velocity;
    
    if (prevUv.x < 0.0 || prevUv.x > 1.0 || prevUv.y < 0.0 || prevUv.y > 1.0) {
        FragColor = vec4(current, 1.0);
        return;
    }

    vec2 texSize = vec2(textureSize(uCurrent, 0));
    vec2 texelSize = 1.0 / texSize;

    // Bicubic history sampling
    vec3 history = sampleBicubic(uHistory, prevUv, texSize);
    vec3 cTL = texture(uCurrent, vUv + vec2(-texelSize.x, -texelSize.y)).rgb;
    vec3 cTC = texture(uCurrent, vUv + vec2( 0.0,         -texelSize.y)).rgb;
    vec3 cTR = texture(uCurrent, vUv + vec2( texelSize.x, -texelSize.y)).rgb;
    vec3 cML = texture(uCurrent, vUv + vec2(-texelSize.x,  0.0)).rgb;
    vec3 cMC = current;
    vec3 cMR = texture(uCurrent, vUv + vec2( texelSize.x,  0.0)).rgb;
    vec3 cBL = texture(uCurrent, vUv + vec2(-texelSize.x,  texelSize.y)).rgb;
    vec3 cBC = texture(uCurrent, vUv + vec2( 0.0,          texelSize.y)).rgb;
    vec3 cBR = texture(uCurrent, vUv + vec2( texelSize.x,  texelSize.y)).rgb;

    // Convert to YCoCg for clamping
    vec3 minC = min(min(min(min(min(min(min(min(cTL, cTC), cTR), cML), cMC), cMR), cBL), cBC), cBR);
    vec3 maxC = max(max(max(max(max(max(max(max(cTL, cTC), cTR), cML), cMC), cMR), cBL), cBC), cBR);

    vec3 m1 = cTL + cTC + cTR + cML + cMC + cMR + cBL + cBC + cBR;
    vec3 m2 = cTL*cTL + cTC*cTC + cTR*cTR + cML*cML + cMC*cMC + cMR*cMR + cBL*cBL + cBC*cBC + cBR*cBR;
    
    vec3 mu = m1 / 9.0;
    vec3 sigma = sqrt(abs(m2 / 9.0 - mu * mu));
    vec3 minC2 = mu - sigma * 1.5;
    vec3 maxC2 = mu + sigma * 1.5;
    
    minC = max(minC, minC2);
    maxC = min(maxC, maxC2);

    history = RGBToYCoCg(history);
    minC = RGBToYCoCg(minC);
    maxC = RGBToYCoCg(maxC);
    history = clamp(history, minC, maxC);
    history = YCoCgToRGB(history);

    // Velocity weighting
    float velMag = length(velocity);
    float blend = mix(0.95, 0.85, clamp(velMag * 100.0, 0.0, 1.0));

    FragColor = vec4(mix(current, history, blend), 1.0);
}
""";
}
