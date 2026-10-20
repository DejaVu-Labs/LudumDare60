using System.Runtime.InteropServices;
using System.Text;
using SDL3;

namespace LudumDare60.Engine.Platform.Desktop.Rendering;

/// <summary>
/// <see cref="IGraphicsBackend"/> on OpenGL 3.3 core: the desktop port of the browser's <c>webgl2.js</c>, command for
/// command, for machines whose Vulkan driver is missing or broken. Sprites are drawn into a render target of the virtual
/// size, which <see cref="RenderCommandKind.Present"/> scales onto the window with nearest filtering.
/// </summary>
/// <remarks>The context lives on the thread that runs the frame loop, like every call into this class.</remarks>
internal sealed unsafe class GlGraphics : IDesktopGraphics, IRenderCommandTarget
{
    private readonly TextureTable _textures = new();
    private GlApi? _gl;
    private nint _window;
    private nint _context;
    private uint _program;
    private int _screenSizeLocation;
    private int _textureSizeLocation;
    private uint _vao;
    private uint _instanceBuffer;
    private nint _instanceBufferSize;
    private uint _target;
    private uint _targetTexture;
    private int _targetWidth;
    private int _targetHeight;
    private PixelRect _viewport;

    /// <inheritdoc />
    public SDL.WindowFlags WindowFlags => SDL.WindowFlags.OpenGL;

    /// <inheritdoc />
    public bool Presented { get; private set; }

    /// <summary>
    /// Number of live textures.
    /// </summary>
    public int TextureCount => _textures.Count;

    /// <summary>
    /// The context's version and renderer, for the log.
    /// </summary>
    public string Description { get; private set; } = "";

    /// <inheritdoc />
    /// <remarks>Asks for an OpenGL 3.3 core context, double-buffered, without depth or stencil.</remarks>
    public void PrepareWindow()
    {
        SDL.GLSetAttribute(SDL.GLAttr.ContextMajorVersion, 3);
        SDL.GLSetAttribute(SDL.GLAttr.ContextMinorVersion, 3);
        SDL.GLSetAttribute(SDL.GLAttr.ContextProfileMask, (int)SDL.GLProfile.Core);
        SDL.GLSetAttribute(SDL.GLAttr.DoubleBuffer, 1);
        SDL.GLSetAttribute(SDL.GLAttr.DepthSize, 0);
        SDL.GLSetAttribute(SDL.GLAttr.StencilSize, 0);
    }

    /// <inheritdoc />
    public void Initialize(nint window)
    {
        if (_context != 0)
        {
            throw new InvalidOperationException("OpenGL is already initialized.");
        }

        _context = SDL.GLCreateContext(window);
        if (_context == 0)
        {
            throw new InvalidOperationException($"Creating the OpenGL 3.3 context failed: {SDL.GetError()}");
        }

        _window = window;
        if (!SDL.GLMakeCurrent(window, _context))
        {
            throw new InvalidOperationException($"Making the OpenGL context current failed: {SDL.GetError()}");
        }

        // Adaptive vsync where the driver has it, plain vsync otherwise.
        if (!SDL.GLSetSwapInterval(-1))
        {
            SDL.GLSetSwapInterval(1);
        }

        var gl = GlApi.Load(SDL.GLGetProcAddress);
        _gl = gl;
        Description = $"{gl.Describe(GlApi.Version)} ({gl.Describe(GlApi.Renderer)})";
        _program = CreateProgram(gl, GlShaders.SpriteVertex, GlShaders.SpriteFragment);
        gl.UseProgram(_program);
        _screenSizeLocation = Uniform(gl, _program, "u_screenSize"u8);
        _textureSizeLocation = Uniform(gl, _program, "u_textureSize"u8);
        gl.Uniform1i(Uniform(gl, _program, "u_texture"u8), 0);

        uint vao;
        uint buffer;
        gl.GenVertexArrays(1, &vao);
        gl.GenBuffers(1, &buffer);
        _vao = vao;
        _instanceBuffer = buffer;
        gl.BindVertexArray(vao);
        gl.BindBuffer(GlApi.ArrayBuffer, buffer);
        for (uint location = 0; location <= 5; location++)
        {
            gl.EnableVertexAttribArray(location);
            gl.VertexAttribDivisor(location, 1);
        }

        gl.BindVertexArray(0);
        gl.PixelStorei(GlApi.UnpackAlignment, 1);
        gl.Enable(GlApi.Blend);
        gl.BlendFunc(GlApi.One, GlApi.OneMinusSrcAlpha);
    }

    public void CreateTexture(int id, int width, int height, ReadOnlySpan<byte> pixels)
    {
        var gl = RequireGl();
        _textures.CheckCreate(id, width, height, pixels.Length);
        uint texture;
        gl.GenTextures(1, &texture);
        gl.BindTexture(GlApi.Texture2D, texture);
        fixed (byte* data = pixels)
        {
            gl.TexImage2D(GlApi.Texture2D, 0, (int)GlApi.Rgba8, width, height, 0, GlApi.Rgba, GlApi.UnsignedByte, data);
        }

        SetNearest(gl);
        _textures.Add(id, (nint)texture, width, height);
    }

    public void UpdateTexture(int id, int y, int height, ReadOnlySpan<byte> rows)
    {
        var gl = RequireGl();
        var texture = _textures.CheckUpdate(id, y, height, rows.Length);
        gl.BindTexture(GlApi.Texture2D, (uint)texture.Handle);
        fixed (byte* data = rows)
        {
            gl.TexSubImage2D(GlApi.Texture2D, 0, 0, y, texture.Width, height, GlApi.Rgba, GlApi.UnsignedByte, data);
        }
    }

    /// <remarks>After <see cref="Shutdown"/> it only forgets the id: the engine destroys its textures after the run.</remarks>
    public void DestroyTexture(int id)
    {
        var texture = (uint)_textures.Remove(id);
        if (_gl is { } gl)
        {
            gl.DeleteTextures(1, &texture);
        }
    }

    public void Submit(ReadOnlySpan<byte> commands, ReadOnlySpan<SpriteInstance> instances)
    {
        var gl = RequireGl();
        Presented = false;
        if (!instances.IsEmpty)
        {
            var bytes = MemoryMarshal.AsBytes(instances);
            gl.BindBuffer(GlApi.ArrayBuffer, _instanceBuffer);
            if (bytes.Length > _instanceBufferSize)
            {
                _instanceBufferSize = Math.Max(bytes.Length, 2 * _instanceBufferSize);
                gl.BufferData(GlApi.ArrayBuffer, _instanceBufferSize, null, GlApi.DynamicDraw);
            }

            fixed (byte* data = bytes)
            {
                gl.BufferSubData(GlApi.ArrayBuffer, 0, bytes.Length, data);
            }
        }

        RenderCommandReader.Run(commands, instances.Length, this);
    }

    void IRenderCommandTarget.SetSurface(int surfaceWidth, int surfaceHeight, int virtualWidth, int virtualHeight, PixelRect viewport)
    {
        var gl = _gl!;
        if (_target == 0 || virtualWidth != _targetWidth || virtualHeight != _targetHeight)
        {
            DeleteTarget(gl);
            uint texture;
            gl.GenTextures(1, &texture);
            gl.BindTexture(GlApi.Texture2D, texture);
            gl.TexImage2D(GlApi.Texture2D, 0, (int)GlApi.Rgba8, virtualWidth, virtualHeight, 0, GlApi.Rgba, GlApi.UnsignedByte, null);
            SetNearest(gl);
            uint framebuffer;
            gl.GenFramebuffers(1, &framebuffer);
            gl.BindFramebuffer(GlApi.Framebuffer, framebuffer);
            gl.FramebufferTexture2D(GlApi.Framebuffer, GlApi.ColorAttachment0, GlApi.Texture2D, texture, 0);
            if (gl.CheckFramebufferStatus(GlApi.Framebuffer) != GlApi.FramebufferComplete)
            {
                throw new InvalidOperationException("The OpenGL render target is incomplete.");
            }

            _targetTexture = texture;
            _target = framebuffer;
            _targetWidth = virtualWidth;
            _targetHeight = virtualHeight;
        }

        // The window's drawable follows the window by itself; the surface size only matters for clamping the viewport.
        _ = surfaceWidth;
        _ = surfaceHeight;
        _viewport = viewport;
    }

    void IRenderCommandTarget.Clear(Color color)
    {
        var gl = _gl!;
        BindTarget(gl);
        gl.ClearColor(color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f);
        gl.Clear(GlApi.ColorBufferBit);
    }

    void IRenderCommandTarget.DrawSprites(int textureId, int firstInstance, int instanceCount)
    {
        var gl = _gl!;
        var texture = _textures.Contains(textureId)
            ? _textures.Get(textureId)
            : throw new InvalidOperationException($"DrawSprites uses texture {textureId}, which does not exist.");
        BindTarget(gl);
        gl.UseProgram(_program);
        gl.Uniform2f(_screenSizeLocation, _targetWidth, _targetHeight);
        gl.Uniform2f(_textureSizeLocation, texture.Width, texture.Height);
        gl.ActiveTexture(GlApi.Texture0);
        gl.BindTexture(GlApi.Texture2D, (uint)texture.Handle);
        gl.BindVertexArray(_vao);
        gl.BindBuffer(GlApi.ArrayBuffer, _instanceBuffer);

        // OpenGL 3.3 has no base instance, so the attributes start at the run's first instance, as in webgl2.js.
        var start = (nint)firstInstance * SpriteInstance.Size;
        gl.VertexAttribPointer(0, 2, GlApi.Float, 0, SpriteInstance.Size, start);
        gl.VertexAttribPointer(1, 2, GlApi.Float, 0, SpriteInstance.Size, start + 8);
        gl.VertexAttribPointer(2, 2, GlApi.Float, 0, SpriteInstance.Size, start + 16);
        gl.VertexAttribPointer(3, 1, GlApi.Float, 0, SpriteInstance.Size, start + 24);
        gl.VertexAttribPointer(4, 4, GlApi.UnsignedByte, 1, SpriteInstance.Size, start + 28);
        gl.VertexAttribPointer(5, 4, GlApi.UnsignedShort, 0, SpriteInstance.Size, start + 32);
        gl.DrawArraysInstanced(GlApi.TriangleStrip, 0, 4, instanceCount);
        gl.BindVertexArray(0);
    }

    void IRenderCommandTarget.Present()
    {
        var gl = _gl!;
        if (_target == 0)
        {
            throw new InvalidOperationException("SetSurface must be the first render command.");
        }

        // A minimized window shows nothing, and swapping would not wait for vsync.
        var flags = SDL.GetWindowFlags(_window);
        if (flags.HasFlag(SDL.WindowFlags.Minimized) || flags.HasFlag(SDL.WindowFlags.Hidden) || flags.HasFlag(SDL.WindowFlags.Occluded))
        {
            return;
        }

        SDL.GetWindowSizeInPixels(_window, out var width, out var height);
        gl.BindFramebuffer(GlApi.Framebuffer, 0);
        gl.Viewport(0, 0, width, height);
        gl.ClearColor(0, 0, 0, 1);
        gl.Clear(GlApi.ColorBufferBit);

        // The viewport is clamped to the drawable, which can be one frame behind a resize. OpenGL counts rows from
        // the bottom.
        var view = _viewport.ClampTo(width, height);
        if (view.Width > 0 && view.Height > 0)
        {
            var bottom = height - view.Y - view.Height;
            gl.BindFramebuffer(GlApi.ReadFramebuffer, _target);
            gl.BlitFramebuffer(
                0, 0, _targetWidth, _targetHeight,
                view.X, bottom, view.X + view.Width, bottom + view.Height,
                GlApi.ColorBufferBit, (uint)GlApi.Nearest);
            gl.BindFramebuffer(GlApi.ReadFramebuffer, 0);
        }

        SDL.GLSwapWindow(_window);
        Presented = true;
    }

    /// <inheritdoc />
    public void Shutdown()
    {
        if (_gl is not { } gl)
        {
            return;
        }

        foreach (var handle in _textures.GetHandles())
        {
            var texture = (uint)handle;
            gl.DeleteTextures(1, &texture);
        }

        DeleteTarget(gl);
        var vao = _vao;
        var buffer = _instanceBuffer;
        gl.DeleteVertexArrays(1, &vao);
        gl.DeleteBuffers(1, &buffer);
        gl.DeleteProgram(_program);
        _gl = null;
        SDL.GLMakeCurrent(_window, 0);
        SDL.GLDestroyContext(_context);
        _context = 0;
        _window = 0;
    }

    private void BindTarget(GlApi gl)
    {
        if (_target == 0)
        {
            throw new InvalidOperationException("SetSurface must be the first render command.");
        }

        gl.BindFramebuffer(GlApi.Framebuffer, _target);
        gl.Viewport(0, 0, _targetWidth, _targetHeight);
    }

    private void DeleteTarget(GlApi gl)
    {
        if (_target == 0)
        {
            return;
        }

        var framebuffer = _target;
        var texture = _targetTexture;
        gl.DeleteFramebuffers(1, &framebuffer);
        gl.DeleteTextures(1, &texture);
        _target = 0;
        _targetTexture = 0;
    }

    private GlApi RequireGl()
        => _gl ?? throw new InvalidOperationException("OpenGL is not initialized: DesktopPlatform.RunAsync sets it up before the first frame.");

    private static void SetNearest(GlApi gl)
    {
        gl.TexParameteri(GlApi.Texture2D, GlApi.TextureMinFilter, GlApi.Nearest);
        gl.TexParameteri(GlApi.Texture2D, GlApi.TextureMagFilter, GlApi.Nearest);
        gl.TexParameteri(GlApi.Texture2D, GlApi.TextureWrapS, GlApi.ClampToEdge);
        gl.TexParameteri(GlApi.Texture2D, GlApi.TextureWrapT, GlApi.ClampToEdge);
    }

    private static int Uniform(GlApi gl, uint program, ReadOnlySpan<byte> name)
    {
        // The name needs a terminating zero.
        Span<byte> text = stackalloc byte[name.Length + 1];
        name.CopyTo(text);
        fixed (byte* pointer = text)
        {
            return gl.GetUniformLocation(program, pointer);
        }
    }

    private static uint CreateProgram(GlApi gl, string vertexSource, string fragmentSource)
    {
        var vertex = CompileShader(gl, GlApi.VertexShader, vertexSource);
        var fragment = CompileShader(gl, GlApi.FragmentShader, fragmentSource);
        var program = gl.CreateProgram();
        gl.AttachShader(program, vertex);
        gl.AttachShader(program, fragment);
        gl.LinkProgram(program);
        gl.DeleteShader(vertex);
        gl.DeleteShader(fragment);
        int linked;
        gl.GetProgramiv(program, GlApi.LinkStatus, &linked);
        if (linked == 0)
        {
            var log = InfoLog(gl, program, isProgram: true);
            gl.DeleteProgram(program);
            throw new InvalidOperationException($"Linking the sprite shaders failed: {log}");
        }

        return program;
    }

    private static uint CompileShader(GlApi gl, uint kind, string source)
    {
        var shader = gl.CreateShader(kind);
        var bytes = Encoding.UTF8.GetBytes(source);
        fixed (byte* text = bytes)
        {
            var length = bytes.Length;
            gl.ShaderSource(shader, 1, &text, &length);
        }

        gl.CompileShader(shader);
        int compiled;
        gl.GetShaderiv(shader, GlApi.CompileStatus, &compiled);
        if (compiled == 0)
        {
            var log = InfoLog(gl, shader, isProgram: false);
            gl.DeleteShader(shader);
            throw new InvalidOperationException($"Compiling a sprite shader failed: {log}");
        }

        return shader;
    }

    private static string InfoLog(GlApi gl, uint handle, bool isProgram)
    {
        int length;
        if (isProgram)
        {
            gl.GetProgramiv(handle, GlApi.InfoLogLength, &length);
        }
        else
        {
            gl.GetShaderiv(handle, GlApi.InfoLogLength, &length);
        }

        var log = new byte[Math.Max(length, 1)];
        fixed (byte* text = log)
        {
            int written;
            if (isProgram)
            {
                gl.GetProgramInfoLog(handle, log.Length, &written, text);
            }
            else
            {
                gl.GetShaderInfoLog(handle, log.Length, &written, text);
            }

            return Encoding.UTF8.GetString(log, 0, Math.Clamp(written, 0, log.Length)).Trim();
        }
    }
}
