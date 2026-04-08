#version 450 core

in vec3 vFragPos;
in vec3 vNormal;

uniform vec3 uAmbientColor;
uniform vec4 uObjectColor;
uniform bool uIsSubtractive;

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
    vec3 baseTint = uObjectColor.rgb;
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
