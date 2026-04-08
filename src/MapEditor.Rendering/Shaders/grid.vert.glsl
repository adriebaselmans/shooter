#version 450 core

layout(location = 0) in vec2 aPosition;

uniform mat4 uViewProjection;
uniform float uGridSpacing;
uniform vec3 uCameraPos;

out vec2 vUV;
out float vFade;

void main()
{
    // Grid rendered as a full-screen quad; fragment shader draws lines
    gl_Position = vec4(aPosition, 0.0, 1.0);
    vUV = aPosition;
}
