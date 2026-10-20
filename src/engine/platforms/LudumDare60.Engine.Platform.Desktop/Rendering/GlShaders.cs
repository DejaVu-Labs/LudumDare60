namespace LudumDare60.Engine.Platform.Desktop.Rendering;

/// <summary>
/// GLSL 3.30 core sprite shaders for <see cref="GlGraphics"/>: the twins of the browser's
/// <c>wwwroot/shaders/sprite.glsl.js</c> (GLSL ES 3.00) and of <c>Shaders/sprite.vert</c> and <c>sprite.frag</c>
/// (Vulkan). Change all three together. OpenGL compiles them at startup, so they ship as source.
/// </summary>
internal static class GlShaders
{
    /// <summary>
    /// Vertex shader. One instance per sprite, four vertices per instance (a triangle strip); the attributes mirror
    /// <see cref="SpriteInstance"/>. Positions are virtual pixels, y down; <c>u_screenSize</c> is the virtual screen
    /// size.
    /// </summary>
    public const string SpriteVertex = """
        #version 330 core
        layout(location = 0) in vec2 a_position;
        layout(location = 1) in vec2 a_origin;
        layout(location = 2) in vec2 a_scale;
        layout(location = 3) in float a_rotation;
        layout(location = 4) in vec4 a_tint;
        layout(location = 5) in vec4 a_source;

        uniform vec2 u_screenSize;
        uniform vec2 u_textureSize;

        out vec2 v_uv;
        out vec4 v_tint;

        void main() {
          // Strip order: (0,0) (1,0) (0,1) (1,1).
          vec2 corner = vec2(float(gl_VertexID & 1), float(gl_VertexID >> 1));
          vec2 size = a_source.zw;
          vec2 pixel;
          if (a_rotation == 0.0) {
            // Snap the top-left corner, so every texel covers whole virtual pixels.
            pixel = floor(a_position - a_origin * a_scale + 0.5) + corner * size * a_scale;
          } else {
            vec2 local = (corner * size - a_origin) * a_scale;
            float c = cos(a_rotation);
            float s = sin(a_rotation);
            pixel = floor(a_position + 0.5) + vec2(local.x * c - local.y * s, local.x * s + local.y * c);
          }

          vec2 clip = pixel / u_screenSize * 2.0 - 1.0;
          gl_Position = vec4(clip.x, -clip.y, 0.0, 1.0);
          v_uv = (a_source.xy + corner * size) / u_textureSize;
          v_tint = a_tint;
        }
        """;

    /// <summary>
    /// Fragment shader. Textures hold straight alpha; the output is premultiplied for blending with
    /// <c>ONE, ONE_MINUS_SRC_ALPHA</c>.
    /// </summary>
    public const string SpriteFragment = """
        #version 330 core
        uniform sampler2D u_texture;

        in vec2 v_uv;
        in vec4 v_tint;

        out vec4 outColor;

        void main() {
          vec4 color = texture(u_texture, v_uv) * v_tint;
          outColor = vec4(color.rgb * color.a, color.a);
        }
        """;
}
