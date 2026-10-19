using System.Runtime.InteropServices;
using SDL3;

namespace LudumDare60.Engine.Platform.Desktop.Rendering;

/// <summary>
/// <see cref="IGraphicsBackend"/> on SDL_GPU with the Vulkan driver: the desktop twin of the browser's
/// <c>webgl2.js</c>. Sprites are drawn into a render target of the virtual size, which <see cref="RenderCommandKind.Present"/>
/// scales onto the window with nearest filtering. Texture uploads and the frame's drawing go into one command buffer,
/// submitted at the end of <see cref="Submit"/>.
/// </summary>
internal sealed unsafe class GpuGraphics : IGraphicsBackend, IRenderCommandTarget
{
    /// <summary>
    /// The SDL_GPU driver: Vulkan, on every desktop OS (MoltenVK on macOS).
    /// </summary>
    public const string Driver = "vulkan";

    private const SDL.GPUTextureFormat TextureFormat = SDL.GPUTextureFormat.R8G8B8A8Unorm;

    private readonly TextureTable _textures = new();
    private nint _device;
    private nint _window;
    private nint _pipeline;
    private nint _sampler;
    private GpuUploadBuffer? _uploads;
    private GpuUploadBuffer? _instanceUploads;
    private nint _instanceBuffer;
    private uint _instanceBufferSize;
    private nint _target;
    private int _targetWidth;
    private int _targetHeight;
    private PixelRect _viewport;
    private nint _commands;
    private nint _renderPass;
    private bool _pipelineBound;

    /// <summary>
    /// Whether the last <see cref="Submit"/> showed a frame. False while the window is minimized: then nothing waits
    /// for vsync, and the platform paces the loop itself.
    /// </summary>
    public bool Presented { get; private set; }

    /// <summary>
    /// Number of live textures.
    /// </summary>
    public int TextureCount => _textures.Count;

    /// <summary>
    /// Creates the GPU device on <paramref name="window"/>, with vsync, and the sprite pipeline.
    /// </summary>
    /// <exception cref="InvalidOperationException">Vulkan is not available, or a GPU object could not be
    /// created.</exception>
    public void Initialize(nint window)
    {
        if (_device != 0)
        {
            throw new InvalidOperationException("The GPU is already initialized.");
        }

#if DEBUG
        const bool DebugMode = true;
#else
        const bool DebugMode = false;
#endif
        _device = Check(SDL.CreateGPUDevice(SDL.GPUShaderFormat.SPIRV, DebugMode, Driver), "Creating the Vulkan GPU device");
        _window = window;
        if (!SDL.ClaimWindowForGPUDevice(_device, window))
        {
            throw Failure("Claiming the window for the GPU");
        }

        if (!SDL.SetGPUSwapchainParameters(_device, window, SDL.GPUSwapchainComposition.SDR, SDL.GPUPresentMode.VSync))
        {
            throw Failure("Setting up the swapchain");
        }

        _pipeline = CreatePipeline();
        var samplerInfo = new SDL.GPUSamplerCreateInfo
        {
            MinFilter = SDL.GPUFilter.Nearest,
            MagFilter = SDL.GPUFilter.Nearest,
            MipmapMode = SDL.GPUSamplerMipmapMode.Nearest,
            AddressModeU = SDL.GPUSamplerAddressMode.ClampToEdge,
            AddressModeV = SDL.GPUSamplerAddressMode.ClampToEdge,
            AddressModeW = SDL.GPUSamplerAddressMode.ClampToEdge,
        };
        _sampler = Check(SDL.CreateGPUSampler(_device, in samplerInfo), "Creating the sampler");
        _uploads = new GpuUploadBuffer(_device);
        _instanceUploads = new GpuUploadBuffer(_device);
    }

    public void CreateTexture(int id, int width, int height, ReadOnlySpan<byte> pixels)
    {
        RequireDevice();
        _textures.CheckCreate(id, width, height, pixels.Length);
        var info = new SDL.GPUTextureCreateInfo
        {
            Type = SDL.GPUTextureType.TextureType2D,
            Format = TextureFormat,
            Usage = SDL.GPUTextureUsageFlags.Sampler,
            Width = (uint)width,
            Height = (uint)height,
            LayerCountOrDepth = 1,
            NumLevels = 1,
            SampleCount = SDL.GPUSampleCount.SampleCount1,
        };
        var texture = Check(SDL.CreateGPUTexture(_device, in info), $"Creating texture {id}");
        Upload(texture, 0, width, height, pixels);
        _textures.Add(id, texture, width, height);
    }

    public void UpdateTexture(int id, int y, int height, ReadOnlySpan<byte> rows)
    {
        RequireDevice();
        var texture = _textures.CheckUpdate(id, y, height, rows.Length);
        Upload(texture.Handle, y, texture.Width, height, rows);
    }

    /// <remarks>SDL_GPU frees the texture once the GPU is done with it, so sprites already submitted still show.
    /// After <see cref="Shutdown"/> it only forgets the id: the engine destroys its textures after the run.</remarks>
    public void DestroyTexture(int id)
    {
        var texture = _textures.Remove(id);
        if (_device != 0)
        {
            SDL.ReleaseGPUTexture(_device, texture);
        }
    }

    public void Submit(ReadOnlySpan<byte> commands, ReadOnlySpan<SpriteInstance> instances)
    {
        RequireDevice();
        Presented = false;
        var commandBuffer = GetCommandBuffer();
        try
        {
            if (!instances.IsEmpty)
            {
                UploadInstances(MemoryMarshal.AsBytes(instances));
            }

            RenderCommandReader.Run(commands, instances.Length, this);
        }
        finally
        {
            EndRenderPass();
            _commands = 0;
            if (!SDL.SubmitGPUCommandBuffer(commandBuffer))
            {
                // Throwing here would hide an exception from the commands; the next frame reports it again.
                Presented = false;
            }
        }
    }

    void IRenderCommandTarget.SetSurface(int surfaceWidth, int surfaceHeight, int virtualWidth, int virtualHeight, PixelRect viewport)
    {
        if (_target == 0 || virtualWidth != _targetWidth || virtualHeight != _targetHeight)
        {
            if (_target != 0)
            {
                SDL.ReleaseGPUTexture(_device, _target);
                _target = 0;
            }

            var info = new SDL.GPUTextureCreateInfo
            {
                Type = SDL.GPUTextureType.TextureType2D,
                Format = TextureFormat,
                Usage = SDL.GPUTextureUsageFlags.ColorTarget | SDL.GPUTextureUsageFlags.Sampler,
                Width = (uint)virtualWidth,
                Height = (uint)virtualHeight,
                LayerCountOrDepth = 1,
                NumLevels = 1,
                SampleCount = SDL.GPUSampleCount.SampleCount1,
            };
            _target = Check(SDL.CreateGPUTexture(_device, in info), "Creating the render target");
            _targetWidth = virtualWidth;
            _targetHeight = virtualHeight;
        }

        // The swapchain follows the window by itself; the surface size only matters for clamping the viewport.
        _ = surfaceWidth;
        _ = surfaceHeight;
        _viewport = viewport;
    }

    void IRenderCommandTarget.Clear(Color color)
    {
        EndRenderPass();
        BeginRenderPass(SDL.GPULoadOp.Clear, new SDL.FColor
        {
            R = color.R / 255f,
            G = color.G / 255f,
            B = color.B / 255f,
            A = color.A / 255f,
        });
    }

    void IRenderCommandTarget.DrawSprites(int textureId, int firstInstance, int instanceCount)
    {
        var texture = _textures.Contains(textureId)
            ? _textures.Get(textureId)
            : throw new InvalidOperationException($"DrawSprites uses texture {textureId}, which does not exist.");
        if (_renderPass == 0)
        {
            BeginRenderPass(SDL.GPULoadOp.Load, default);
        }

        if (!_pipelineBound)
        {
            SDL.BindGPUGraphicsPipeline(_renderPass, _pipeline);
            Span<SDL.GPUBufferBinding> buffers = [new SDL.GPUBufferBinding { Buffer = _instanceBuffer, Offset = 0 }];
            SDL.BindGPUVertexBuffers(_renderPass, 0, buffers, 1);
            _pipelineBound = true;
        }

        Span<SDL.GPUTextureSamplerBinding> samplers = [new SDL.GPUTextureSamplerBinding { Texture = texture.Handle, Sampler = _sampler }];
        SDL.BindGPUFragmentSamplers(_renderPass, 0, samplers, 1);
        Span<float> sizes = [_targetWidth, _targetHeight, texture.Width, texture.Height];
        SDL.PushGPUVertexUniformData(_commands, 0, MemoryMarshal.AsBytes(sizes), sizeof(float) * 4);
        SDL.DrawGPUPrimitives(_renderPass, 4, (uint)instanceCount, 0, (uint)firstInstance);
    }

    void IRenderCommandTarget.Present()
    {
        EndRenderPass();
        if (!SDL.WaitAndAcquireGPUSwapchainTexture(_commands, _window, out var swapchain, out var width, out var height))
        {
            throw Failure("Acquiring the swapchain texture");
        }

        if (swapchain == 0)
        {
            // Minimized or occluded: nothing to show.
            return;
        }

        var targets = new SDL.GPUColorTargetInfo
        {
            Texture = swapchain,
            ClearColor = new SDL.FColor { R = 0, G = 0, B = 0, A = 1 },
            LoadOp = SDL.GPULoadOp.Clear,
            StoreOp = SDL.GPUStoreOp.Store,
        };
        SDL.EndGPURenderPass(SDL.BeginGPURenderPass(_commands, [targets], 1, 0));

        var view = _viewport.ClampTo((int)width, (int)height);
        if (view.Width > 0 && view.Height > 0)
        {
            var blit = new SDL.GPUBlitInfo
            {
                Source = new SDL.GPUBlitRegion { Texture = _target, W = (uint)_targetWidth, H = (uint)_targetHeight },
                Destination = new SDL.GPUBlitRegion
                {
                    Texture = swapchain,
                    X = (uint)view.X,
                    Y = (uint)view.Y,
                    W = (uint)view.Width,
                    H = (uint)view.Height,
                },
                LoadOp = SDL.GPULoadOp.Load,
                Filter = SDL.GPUFilter.Nearest,
            };
            SDL.BlitGPUTexture(_commands, in blit);
        }

        Presented = true;
    }

    /// <summary>
    /// Waits for the GPU, releases everything and gives the window back. Does nothing when the GPU is not initialized.
    /// Textures still alive are released on the GPU but keep their ids: the engine destroys them after the run, and
    /// <see cref="DestroyTexture"/> then only forgets them.
    /// </summary>
    public void Shutdown()
    {
        if (_device == 0)
        {
            return;
        }

        if (_commands != 0)
        {
            SDL.SubmitGPUCommandBuffer(_commands);
            _commands = 0;
        }

        SDL.WaitForGPUIdle(_device);
        foreach (var texture in _textures.GetHandles())
        {
            SDL.ReleaseGPUTexture(_device, texture);
        }

        Release(ref _target, SDL.ReleaseGPUTexture);
        Release(ref _instanceBuffer, SDL.ReleaseGPUBuffer);
        Release(ref _sampler, SDL.ReleaseGPUSampler);
        Release(ref _pipeline, SDL.ReleaseGPUGraphicsPipeline);
        _uploads?.Release();
        _instanceUploads?.Release();
        _uploads = null;
        _instanceUploads = null;
        SDL.ReleaseWindowFromGPUDevice(_device, _window);
        SDL.DestroyGPUDevice(_device);
        _device = 0;
        _window = 0;
    }

    private nint CreatePipeline()
    {
        var vertexShader = CreateShader(SpriteShaders.Vertex, SDL.GPUShaderStage.Vertex, samplers: 0, uniformBuffers: 1);
        var fragmentShader = CreateShader(SpriteShaders.Fragment, SDL.GPUShaderStage.Fragment, samplers: 1, uniformBuffers: 0);
        try
        {
            ReadOnlySpan<SDL.GPUVertexBufferDescription> buffers =
            [
                new SDL.GPUVertexBufferDescription
                {
                    Slot = 0,
                    Pitch = SpriteInstance.Size,
                    InputRate = SDL.GPUVertexInputRate.Instance,
                },
            ];

            // The layout of SpriteInstance.
            ReadOnlySpan<SDL.GPUVertexAttribute> attributes =
            [
                new SDL.GPUVertexAttribute { Location = 0, Format = SDL.GPUVertexElementFormat.Float2, Offset = 0 },
                new SDL.GPUVertexAttribute { Location = 1, Format = SDL.GPUVertexElementFormat.Float2, Offset = 8 },
                new SDL.GPUVertexAttribute { Location = 2, Format = SDL.GPUVertexElementFormat.Float2, Offset = 16 },
                new SDL.GPUVertexAttribute { Location = 3, Format = SDL.GPUVertexElementFormat.Float, Offset = 24 },
                new SDL.GPUVertexAttribute { Location = 4, Format = SDL.GPUVertexElementFormat.Ubyte4Norm, Offset = 28 },
                new SDL.GPUVertexAttribute { Location = 5, Format = SDL.GPUVertexElementFormat.Ushort4, Offset = 32 },
            ];
            ReadOnlySpan<SDL.GPUColorTargetDescription> targets =
            [
                new SDL.GPUColorTargetDescription
                {
                    Format = TextureFormat,
                    BlendState = new SDL.GPUColorTargetBlendState
                    {
                        EnableBlend = true,
                        SrcColorBlendFactor = SDL.GPUBlendFactor.One,
                        DstColorBlendFactor = SDL.GPUBlendFactor.OneMinusSrcAlpha,
                        ColorBlendOp = SDL.GPUBlendOp.Add,
                        SrcAlphaBlendFactor = SDL.GPUBlendFactor.One,
                        DstAlphaBlendFactor = SDL.GPUBlendFactor.OneMinusSrcAlpha,
                        AlphaBlendOp = SDL.GPUBlendOp.Add,
                    },
                },
            ];
            var info = new SDL.GPUGraphicsPipelineCreateInfo
            {
                VertexShader = vertexShader,
                FragmentShader = fragmentShader,
                PrimitiveType = SDL.GPUPrimitiveType.TriangleStrip,
                RasterizerState = new SDL.GPURasterizerState
                {
                    FillMode = SDL.GPUFillMode.Fill,
                    CullMode = SDL.GPUCullMode.None,
                },
                MultisampleState = new SDL.GPUMultisampleState { SampleCount = SDL.GPUSampleCount.SampleCount1 },
            };
            return Check(SDL.CreateGPUGraphicsPipeline(_device, in info, buffers, attributes, targets), "Creating the sprite pipeline");
        }
        finally
        {
            SDL.ReleaseGPUShader(_device, vertexShader);
            SDL.ReleaseGPUShader(_device, fragmentShader);
        }
    }

    private nint CreateShader(string name, SDL.GPUShaderStage stage, uint samplers, uint uniformBuffers)
    {
        var code = SpriteShaders.Load(name);
        var info = new SDL.GPUShaderCreateInfo
        {
            Format = SDL.GPUShaderFormat.SPIRV,
            Stage = stage,
            NumSamplers = samplers,
            NumUniformBuffers = uniformBuffers,
        };
        return Check(SDL.CreateGPUShader(_device, in info, code, "main"), $"Creating the shader {name}");
    }

    private void Upload(nint texture, int y, int width, int height, ReadOnlySpan<byte> data)
    {
        var transfer = _uploads!.Write(data);
        var copyPass = SDL.BeginGPUCopyPass(GetCommandBuffer());
        var source = new SDL.GPUTextureTransferInfo
        {
            TransferBuffer = transfer,
            Offset = 0,
            PixelsPerRow = (uint)width,
            RowsPerLayer = (uint)height,
        };
        var destination = new SDL.GPUTextureRegion
        {
            Texture = texture,
            Y = (uint)y,
            W = (uint)width,
            H = (uint)height,
            D = 1,
        };
        SDL.UploadToGPUTexture(copyPass, in source, in destination, cycle: false);
        SDL.EndGPUCopyPass(copyPass);
    }

    private void UploadInstances(ReadOnlySpan<byte> data)
    {
        var size = (uint)data.Length;
        if (size > _instanceBufferSize)
        {
            Release(ref _instanceBuffer, SDL.ReleaseGPUBuffer);
            _instanceBufferSize = Math.Max(size, 2 * _instanceBufferSize);
            var info = new SDL.GPUBufferCreateInfo { Usage = SDL.GPUBufferUsageFlags.Vertex, Size = _instanceBufferSize };
            _instanceBuffer = Check(SDL.CreateGPUBuffer(_device, in info), "Creating the instance buffer");
        }

        var transfer = _instanceUploads!.Write(data);
        var copyPass = SDL.BeginGPUCopyPass(_commands);
        var source = new SDL.GPUTransferBufferLocation { TransferBuffer = transfer, Offset = 0 };
        var destination = new SDL.GPUBufferRegion { Buffer = _instanceBuffer, Offset = 0, Size = size };

        // Cycling gives the frame a fresh buffer while the GPU may still draw the previous one.
        SDL.UploadToGPUBuffer(copyPass, in source, in destination, cycle: true);
        SDL.EndGPUCopyPass(copyPass);
    }

    private void BeginRenderPass(SDL.GPULoadOp loadOp, SDL.FColor clearColor)
    {
        var target = new SDL.GPUColorTargetInfo
        {
            Texture = _target,
            ClearColor = clearColor,
            LoadOp = loadOp,
            StoreOp = SDL.GPUStoreOp.Store,
        };
        _renderPass = Check(SDL.BeginGPURenderPass(_commands, [target], 1, 0), "Beginning a render pass");
        _pipelineBound = false;
    }

    private void EndRenderPass()
    {
        if (_renderPass != 0)
        {
            SDL.EndGPURenderPass(_renderPass);
            _renderPass = 0;
        }
    }

    private nint GetCommandBuffer()
    {
        if (_commands == 0)
        {
            _commands = Check(SDL.AcquireGPUCommandBuffer(_device), "Acquiring a command buffer");
        }

        return _commands;
    }

    private void RequireDevice()
    {
        if (_device == 0)
        {
            throw new InvalidOperationException("The GPU is not initialized: DesktopPlatform.RunAsync sets it up before the first frame.");
        }
    }

    private void Release(ref nint handle, Action<nint, nint> release)
    {
        if (handle != 0)
        {
            release(_device, handle);
            handle = 0;
        }
    }

    private static nint Check(nint handle, string what) => handle != 0 ? handle : throw Failure(what);

    private static InvalidOperationException Failure(string what) => new($"{what} failed: {SDL.GetError()}");
}
