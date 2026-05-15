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

    // Bicubic or catmull-rom could be used here, but linear is a start
    vec3 history = texture(uHistory, prevUv).rgb;

    // Neighborhood clamping
    vec2 texelSize = 1.0 / vec2(textureSize(uCurrent, 0));
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
