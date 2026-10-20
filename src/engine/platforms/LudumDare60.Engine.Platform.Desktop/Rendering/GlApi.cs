using System.Runtime.InteropServices;

namespace LudumDare60.Engine.Platform.Desktop.Rendering;

/// <summary>
/// The OpenGL 3.3 core functions <see cref="GlGraphics"/> uses, as unmanaged function pointers loaded from the current
/// context (<c>SDL_GL_GetProcAddress</c>). No binding library: the set is small, and pointers need no reflection or
/// marshalling.
/// </summary>
internal sealed unsafe class GlApi
{
    public const uint Texture2D = 0x0DE1;
    public const uint Rgba = 0x1908;
    public const uint Rgba8 = 0x8058;
    public const uint UnsignedByte = 0x1401;
    public const uint UnsignedShort = 0x1403;
    public const uint Float = 0x1406;
    public const uint TextureMinFilter = 0x2801;
    public const uint TextureMagFilter = 0x2800;
    public const uint TextureWrapS = 0x2802;
    public const uint TextureWrapT = 0x2803;
    public const int Nearest = 0x2600;
    public const int ClampToEdge = 0x812F;
    public const uint UnpackAlignment = 0x0CF5;
    public const uint Blend = 0x0BE2;
    public const uint One = 1;
    public const uint OneMinusSrcAlpha = 0x0303;
    public const uint ColorBufferBit = 0x4000;
    public const uint Framebuffer = 0x8D40;
    public const uint ReadFramebuffer = 0x8CA8;
    public const uint DrawFramebuffer = 0x8CA9;
    public const uint ColorAttachment0 = 0x8CE0;
    public const uint FramebufferComplete = 0x8CD5;
    public const uint ArrayBuffer = 0x8892;
    public const uint DynamicDraw = 0x88E8;
    public const uint VertexShader = 0x8B31;
    public const uint FragmentShader = 0x8B30;
    public const uint CompileStatus = 0x8B81;
    public const uint LinkStatus = 0x8B82;
    public const uint InfoLogLength = 0x8B84;
    public const uint TriangleStrip = 0x0005;
    public const uint Texture0 = 0x84C0;
    public const uint Vendor = 0x1F00;
    public const uint Version = 0x1F02;
    public const uint Renderer = 0x1F01;

    public delegate* unmanaged<uint, byte*> GetString;
    public delegate* unmanaged<int, int, int, int, void> Viewport;
    public delegate* unmanaged<float, float, float, float, void> ClearColor;
    public delegate* unmanaged<uint, void> Clear;
    public delegate* unmanaged<uint, void> Enable;
    public delegate* unmanaged<uint, uint, void> BlendFunc;
    public delegate* unmanaged<uint, int, void> PixelStorei;
    public delegate* unmanaged<int, uint*, void> GenTextures;
    public delegate* unmanaged<int, uint*, void> DeleteTextures;
    public delegate* unmanaged<uint, uint, void> BindTexture;
    public delegate* unmanaged<uint, int, int, int, int, int, uint, uint, void*, void> TexImage2D;
    public delegate* unmanaged<uint, int, int, int, int, int, uint, uint, void*, void> TexSubImage2D;
    public delegate* unmanaged<uint, uint, int, void> TexParameteri;
    public delegate* unmanaged<uint, void> ActiveTexture;
    public delegate* unmanaged<int, uint*, void> GenFramebuffers;
    public delegate* unmanaged<int, uint*, void> DeleteFramebuffers;
    public delegate* unmanaged<uint, uint, void> BindFramebuffer;
    public delegate* unmanaged<uint, uint, uint, uint, int, void> FramebufferTexture2D;
    public delegate* unmanaged<uint, uint> CheckFramebufferStatus;
    public delegate* unmanaged<int, int, int, int, int, int, int, int, uint, uint, void> BlitFramebuffer;
    public delegate* unmanaged<uint, uint> CreateShader;
    public delegate* unmanaged<uint, int, byte**, int*, void> ShaderSource;
    public delegate* unmanaged<uint, void> CompileShader;
    public delegate* unmanaged<uint, uint, int*, void> GetShaderiv;
    public delegate* unmanaged<uint, int, int*, byte*, void> GetShaderInfoLog;
    public delegate* unmanaged<uint, void> DeleteShader;
    public delegate* unmanaged<uint> CreateProgram;
    public delegate* unmanaged<uint, uint, void> AttachShader;
    public delegate* unmanaged<uint, void> LinkProgram;
    public delegate* unmanaged<uint, uint, int*, void> GetProgramiv;
    public delegate* unmanaged<uint, int, int*, byte*, void> GetProgramInfoLog;
    public delegate* unmanaged<uint, void> DeleteProgram;
    public delegate* unmanaged<uint, void> UseProgram;
    public delegate* unmanaged<uint, byte*, int> GetUniformLocation;
    public delegate* unmanaged<int, float, float, void> Uniform2f;
    public delegate* unmanaged<int, int, void> Uniform1i;
    public delegate* unmanaged<int, uint*, void> GenVertexArrays;
    public delegate* unmanaged<int, uint*, void> DeleteVertexArrays;
    public delegate* unmanaged<uint, void> BindVertexArray;
    public delegate* unmanaged<int, uint*, void> GenBuffers;
    public delegate* unmanaged<int, uint*, void> DeleteBuffers;
    public delegate* unmanaged<uint, uint, void> BindBuffer;
    public delegate* unmanaged<uint, nint, void*, uint, void> BufferData;
    public delegate* unmanaged<uint, nint, nint, void*, void> BufferSubData;
    public delegate* unmanaged<uint, void> EnableVertexAttribArray;
    public delegate* unmanaged<uint, int, uint, byte, int, nint, void> VertexAttribPointer;
    public delegate* unmanaged<uint, uint, void> VertexAttribDivisor;
    public delegate* unmanaged<uint, int, int, int, void> DrawArraysInstanced;

    /// <summary>
    /// Loads every function from the context that is current on this thread.
    /// </summary>
    /// <param name="getProcAddress">Looks a function up by name; 0 when the context has none.</param>
    /// <exception cref="InvalidOperationException">A function is missing: the context is older than OpenGL 3.3.</exception>
    public static GlApi Load(Func<string, nint> getProcAddress)
    {
        ArgumentNullException.ThrowIfNull(getProcAddress);
        nint Get(string name)
        {
            var address = getProcAddress(name);
            return address != 0 ? address : throw new InvalidOperationException($"OpenGL function {name} is missing: OpenGL 3.3 is required.");
        }

        return new GlApi
        {
            GetString = (delegate* unmanaged<uint, byte*>)Get("glGetString"),
            Viewport = (delegate* unmanaged<int, int, int, int, void>)Get("glViewport"),
            ClearColor = (delegate* unmanaged<float, float, float, float, void>)Get("glClearColor"),
            Clear = (delegate* unmanaged<uint, void>)Get("glClear"),
            Enable = (delegate* unmanaged<uint, void>)Get("glEnable"),
            BlendFunc = (delegate* unmanaged<uint, uint, void>)Get("glBlendFunc"),
            PixelStorei = (delegate* unmanaged<uint, int, void>)Get("glPixelStorei"),
            GenTextures = (delegate* unmanaged<int, uint*, void>)Get("glGenTextures"),
            DeleteTextures = (delegate* unmanaged<int, uint*, void>)Get("glDeleteTextures"),
            BindTexture = (delegate* unmanaged<uint, uint, void>)Get("glBindTexture"),
            TexImage2D = (delegate* unmanaged<uint, int, int, int, int, int, uint, uint, void*, void>)Get("glTexImage2D"),
            TexSubImage2D = (delegate* unmanaged<uint, int, int, int, int, int, uint, uint, void*, void>)Get("glTexSubImage2D"),
            TexParameteri = (delegate* unmanaged<uint, uint, int, void>)Get("glTexParameteri"),
            ActiveTexture = (delegate* unmanaged<uint, void>)Get("glActiveTexture"),
            GenFramebuffers = (delegate* unmanaged<int, uint*, void>)Get("glGenFramebuffers"),
            DeleteFramebuffers = (delegate* unmanaged<int, uint*, void>)Get("glDeleteFramebuffers"),
            BindFramebuffer = (delegate* unmanaged<uint, uint, void>)Get("glBindFramebuffer"),
            FramebufferTexture2D = (delegate* unmanaged<uint, uint, uint, uint, int, void>)Get("glFramebufferTexture2D"),
            CheckFramebufferStatus = (delegate* unmanaged<uint, uint>)Get("glCheckFramebufferStatus"),
            BlitFramebuffer = (delegate* unmanaged<int, int, int, int, int, int, int, int, uint, uint, void>)Get("glBlitFramebuffer"),
            CreateShader = (delegate* unmanaged<uint, uint>)Get("glCreateShader"),
            ShaderSource = (delegate* unmanaged<uint, int, byte**, int*, void>)Get("glShaderSource"),
            CompileShader = (delegate* unmanaged<uint, void>)Get("glCompileShader"),
            GetShaderiv = (delegate* unmanaged<uint, uint, int*, void>)Get("glGetShaderiv"),
            GetShaderInfoLog = (delegate* unmanaged<uint, int, int*, byte*, void>)Get("glGetShaderInfoLog"),
            DeleteShader = (delegate* unmanaged<uint, void>)Get("glDeleteShader"),
            CreateProgram = (delegate* unmanaged<uint>)Get("glCreateProgram"),
            AttachShader = (delegate* unmanaged<uint, uint, void>)Get("glAttachShader"),
            LinkProgram = (delegate* unmanaged<uint, void>)Get("glLinkProgram"),
            GetProgramiv = (delegate* unmanaged<uint, uint, int*, void>)Get("glGetProgramiv"),
            GetProgramInfoLog = (delegate* unmanaged<uint, int, int*, byte*, void>)Get("glGetProgramInfoLog"),
            DeleteProgram = (delegate* unmanaged<uint, void>)Get("glDeleteProgram"),
            UseProgram = (delegate* unmanaged<uint, void>)Get("glUseProgram"),
            GetUniformLocation = (delegate* unmanaged<uint, byte*, int>)Get("glGetUniformLocation"),
            Uniform2f = (delegate* unmanaged<int, float, float, void>)Get("glUniform2f"),
            Uniform1i = (delegate* unmanaged<int, int, void>)Get("glUniform1i"),
            GenVertexArrays = (delegate* unmanaged<int, uint*, void>)Get("glGenVertexArrays"),
            DeleteVertexArrays = (delegate* unmanaged<int, uint*, void>)Get("glDeleteVertexArrays"),
            BindVertexArray = (delegate* unmanaged<uint, void>)Get("glBindVertexArray"),
            GenBuffers = (delegate* unmanaged<int, uint*, void>)Get("glGenBuffers"),
            DeleteBuffers = (delegate* unmanaged<int, uint*, void>)Get("glDeleteBuffers"),
            BindBuffer = (delegate* unmanaged<uint, uint, void>)Get("glBindBuffer"),
            BufferData = (delegate* unmanaged<uint, nint, void*, uint, void>)Get("glBufferData"),
            BufferSubData = (delegate* unmanaged<uint, nint, nint, void*, void>)Get("glBufferSubData"),
            EnableVertexAttribArray = (delegate* unmanaged<uint, void>)Get("glEnableVertexAttribArray"),
            VertexAttribPointer = (delegate* unmanaged<uint, int, uint, byte, int, nint, void>)Get("glVertexAttribPointer"),
            VertexAttribDivisor = (delegate* unmanaged<uint, uint, void>)Get("glVertexAttribDivisor"),
            DrawArraysInstanced = (delegate* unmanaged<uint, int, int, int, void>)Get("glDrawArraysInstanced"),
        };
    }

    /// <summary>
    /// A string of the context (<see cref="Version"/>, <see cref="Renderer"/>).
    /// </summary>
    public string Describe(uint name) => Marshal.PtrToStringUTF8((nint)GetString(name)) ?? "";
}
