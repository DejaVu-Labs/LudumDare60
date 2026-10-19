// Sprite fragment shader for the SDL_GPU (Vulkan) backend: the GLSL 4.50 twin of spriteFragment in the browser
// platform's wwwroot/shaders/sprite.glsl.js. Compiled to sprite.frag.spv, which is committed.
//
// Textures hold straight alpha; the output is premultiplied for blending with ONE, ONE_MINUS_SRC_ALPHA. SDL_GPU reads
// fragment samplers from descriptor set 2.
#version 450

layout(location = 0) in vec2 inUv;
layout(location = 1) in vec4 inTint;

layout(set = 2, binding = 0) uniform sampler2D spriteTexture;

layout(location = 0) out vec4 outColor;

void main()
{
    vec4 color = texture(spriteTexture, inUv) * inTint;
    outColor = vec4(color.rgb * color.a, color.a);
}
