namespace Shooter.Render;

internal static partial class Shaders
{
    public const string SkyVert = """
#version 330 core
layout(location=0) in vec3 aPos;
uniform mat4 uViewNoTrans;
uniform mat4 uProj;
out vec3 vDir;
void main(){
    vDir = aPos;
    vec4 clip = uProj * uViewNoTrans * vec4(aPos, 1.0);
    gl_Position = clip.xyww;
}
""";

    public const string AtmosphereSnippet = """
uniform vec3 uToSun;
uniform float uTurbidity;
uniform vec3 uGroundAlbedo;

vec3 atmosphere(vec3 dir){
    float sunCos = clamp(dot(dir, uToSun), -1.0, 1.0);
    float sunDot = max(sunCos, 0.0);
    float t = clamp(1.0 - max(dir.y, 0.0), 0.0, 1.0);
    vec3 zenith  = vec3(0.32, 0.55, 1.05);
    vec3 horizon = vec3(0.95, 0.85, 0.75);
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

    public const string SkyFaceVert = """
#version 330 core
layout(location=0) in vec2 aQuad;
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

    public const string IrradianceConvolveVert = SkyFaceVert;

    public const string IrradianceConvolveFrag = """
#version 330 core
in vec3 vDir;
out vec4 FragColor;
uniform samplerCube uSky;
const float PI = 3.14159265359;
void main(){
    vec3 N = normalize(vDir);
    vec3 up = abs(N.y) < 0.999 ? vec3(0,1,0) : vec3(1,0,0);
    vec3 right = normalize(cross(up, N));
    up = normalize(cross(N, right));
    vec3 sum = vec3(0.0);
    int samples = 0;
    for (int p = 0; p < 8; ++p){
        for (int t = 0; t < 8; ++t){
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

    public const string SpecularPrefilterVert = SkyFaceVert;

    public const string SpecularPrefilterFrag = """
#version 330 core
in vec3 vDir;
out vec4 FragColor;
uniform samplerCube uSky;
uniform float uRoughness;
const float PI = 3.14159265359;

float RadicalInverseVdC(uint bits) {
    bits = (bits << 16u) | (bits >> 16u);
    bits = ((bits & 0x55555555u) << 1u) | ((bits & 0xAAAAAAAAu) >> 1u);
    bits = ((bits & 0x33333333u) << 2u) | ((bits & 0xCCCCCCCCu) >> 2u);
    bits = ((bits & 0x0F0F0F0Fu) << 4u) | ((bits & 0xF0F0F0F0u) >> 4u);
    bits = ((bits & 0x00FF00FFu) << 8u) | ((bits & 0xFF00FF00u) >> 8u);
    return float(bits) * 2.3283064365386963e-10;
}

vec2 Hammersley(uint i, uint N) {
    return vec2(float(i) / float(N), RadicalInverseVdC(i));
}

vec3 ImportanceSampleGGX(vec2 Xi, vec3 N, float roughness) {
    float a = roughness * roughness;
    float phi = 2.0 * PI * Xi.x;
    float cosTheta = sqrt((1.0 - Xi.y) / (1.0 + (a * a - 1.0) * Xi.y));
    float sinTheta = sqrt(max(1.0 - cosTheta * cosTheta, 0.0));

    vec3 H = vec3(cos(phi) * sinTheta, sin(phi) * sinTheta, cosTheta);
    vec3 up = abs(N.z) < 0.999 ? vec3(0.0, 0.0, 1.0) : vec3(1.0, 0.0, 0.0);
    vec3 tangent = normalize(cross(up, N));
    vec3 bitangent = cross(N, tangent);
    vec3 sampleVec = tangent * H.x + bitangent * H.y + N * H.z;
    return normalize(sampleVec);
}

void main(){
    vec3 N = normalize(vDir);
    vec3 V = N;
    vec3 prefiltered = vec3(0.0);
    float totalWeight = 0.0;
    const uint SAMPLE_COUNT = 64u;
    for (uint i = 0u; i < SAMPLE_COUNT; ++i) {
        vec2 Xi = Hammersley(i, SAMPLE_COUNT);
        vec3 H = ImportanceSampleGGX(Xi, N, max(uRoughness, 0.04));
        vec3 L = normalize(2.0 * dot(V, H) * H - V);
        float NdotL = max(dot(N, L), 0.0);
        if (NdotL > 0.0) {
            prefiltered += textureLod(uSky, L, 0.0).rgb * NdotL;
            totalWeight += NdotL;
        }
    }
    prefiltered = prefiltered / max(totalWeight, 0.0001);
    FragColor = vec4(prefiltered, 1.0);
}
""";

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
}
