// Sprite vertex shader for the SDL_GPU (Vulkan) backend: the GLSL 4.50 twin of spriteVertex in the browser platform's
// wwwroot/shaders/sprite.glsl.js. Change both together. Compiled to sprite.vert.spv, which is committed: see
// docs/tech/architecture.md#desktop-renderer.
//
// One instance per sprite, four vertices per instance (a triangle strip). The attributes mirror SpriteInstance (C#).
// SDL_GPU reads vertex uniform buffers from descriptor set 1.
#version 450

layout(location = 0) in vec2 inPosition;
layout(location = 1) in vec2 inOrigin;
layout(location = 2) in vec2 inScale;
layout(location = 3) in float inRotation;
layout(location = 4) in vec4 inTint;
layout(location = 5) in uvec4 inSource;

layout(set = 1, binding = 0) uniform Sizes
{
    vec2 screenSize;
    vec2 textureSize;
};

layout(location = 0) out vec2 outUv;
layout(location = 1) out vec4 outTint;

void main()
{
    // Strip order: (0,0) (1,0) (0,1) (1,1).
    vec2 corner = vec2(float(gl_VertexIndex & 1), float(gl_VertexIndex >> 1));
    vec2 size = vec2(inSource.zw);
    vec2 pixel;
    if (inRotation == 0.0)
    {
        // Snap the top-left corner, so every texel covers whole virtual pixels.
        pixel = floor(inPosition - inOrigin * inScale + 0.5) + corner * size * inScale;
    }
    else
    {
        vec2 local = (corner * size - inOrigin) * inScale;
        float c = cos(inRotation);
        float s = sin(inRotation);
        pixel = floor(inPosition + 0.5) + vec2(local.x * c - local.y * s, local.x * s + local.y * c);
    }

    // SDL_GPU's normalized device coordinates have y up on every backend, as in WebGL.
    vec2 clip = pixel / screenSize * 2.0 - 1.0;
    gl_Position = vec4(clip.x, -clip.y, 0.0, 1.0);
    outUv = (vec2(inSource.xy) + corner * size) / textureSize;
    outTint = inTint;
}
