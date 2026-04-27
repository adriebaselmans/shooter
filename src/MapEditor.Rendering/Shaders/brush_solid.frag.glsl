#version 410 core

in vec3 vFragPos;
in vec3 vNormal;
in vec2 vTexCoord;

uniform vec3 uAmbientColor;
uniform vec4 uObjectColor;
uniform bool uIsSubtractive;
uniform bool uUseTexture;
uniform bool uIsAnimatedTexture;
uniform int uAnimationKind;
uniform float uTimeSeconds;
uniform float uFlowSpeed;
uniform float uPulseStrength;
uniform sampler2D uTexture0;

struct Light {
    vec3  position;
    vec3  color;
    float intensity;
    float range;
};

uniform int    uLightCount;
uniform Light  uLights[8];

out vec4 FragColor;

void main()
{
    vec3 norm = normalize(vNormal);
    vec2 uv = vTexCoord;
    if (uUseTexture && uIsAnimatedTexture)
    {
        float flow = uTimeSeconds * uFlowSpeed;
        if (uAnimationKind == 1)
        {
            uv += vec2(flow, sin((uv.x + uTimeSeconds * 0.18) * 6.28318) * 0.018);
        }
        else if (uAnimationKind == 2)
        {
            uv += vec2(flow * 0.55, flow * 0.25);
        }
        else if (uAnimationKind == 3)
        {
            uv += vec2(sin(uTimeSeconds * 0.8) * 0.03, flow * 0.2);
        }
    }

    vec3 textureColor = uUseTexture ? texture(uTexture0, uv).rgb : vec3(1.0);
    if (uUseTexture && uIsAnimatedTexture)
    {
        float pulse = sin(uTimeSeconds * 6.28318 * 0.9) * 0.5 + 0.5;
        if (uAnimationKind == 1)
        {
            textureColor = mix(textureColor, vec3(0.45, 0.82, 1.0), pulse * uPulseStrength);
        }
        else if (uAnimationKind == 2)
        {
            textureColor += vec3(1.0, 0.32, 0.04) * pulse * uPulseStrength;
        }
        else if (uAnimationKind == 3)
        {
            textureColor = mix(textureColor, vec3(0.80, 0.55, 1.0), pulse * uPulseStrength);
        }
    }

    vec3 baseTint = textureColor * uObjectColor.rgb;
    vec3 litResult = uAmbientColor * baseTint;

    for (int i = 0; i < uLightCount && i < 8; i++)
    {
        vec3  lightDir = normalize(uLights[i].position - vFragPos);
        float dist     = length(uLights[i].position - vFragPos);
        float atten    = clamp(1.0 - dist / uLights[i].range, 0.0, 1.0);
        float diff     = max(dot(norm, lightDir), 0.0);
        litResult += diff * uLights[i].color * uLights[i].intensity * atten * baseTint;
    }

    // Keep the editor brush tint readable even in dark scenes with no placed lights.
    vec3 result = max(litResult, baseTint * 0.55);

    if (uIsSubtractive)
        result = mix(result, vec3(0.8, 0.1, 0.1), 0.2);

    FragColor = vec4(result, uObjectColor.a);
}
