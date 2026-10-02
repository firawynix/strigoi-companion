using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Strigoi.Companion.Core;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Graphics.Imaging;

namespace Strigoi.Companion;

// All lifecycle calls and notifications belong to the WPF dispatcher. The native
// frame pool has its own worker; the application only copies at most two frames/s.
internal sealed class WindowCapture
{
    private CancellationTokenSource? cancellation;
    private Task? loop;
    private volatile bool closed;
    private DateTimeOffset? startedAt;
    private DateTimeOffset? endedAt;
    private string targetTitle = "";
    private uint targetProcessId;
    private int visualChanges;
    private VisionImage? latestForAsk;
    public VisualChanges History { get; } = new();
    public bool Running => cancellation is not null;
    public string Status { get; private set; } = "Captura desligada.";
    public int Frames { get; private set; }
    public Guid SessionId { get; private set; }
    public string TargetTitle => targetTitle;
    public event Action<byte[], int, int, double, bool>? Frame;
    public event Action? Updated;

    public VisionImage? LatestImage()
    {
        return Running ? latestForAsk?.Copy() : null;
    }

    public bool IsCurrent(Guid sessionId) => Running && SessionId == sessionId;

    public void Start(CaptureTarget target)
    {
        if (Running) throw new InvalidOperationException("Pare a captura anterior primeiro.");
        if (!GraphicsCaptureSession.IsSupported()) throw new InvalidOperationException("Captura de janela indisponível neste Windows.");
        if (!CaptureNative.Matches(target) || CaptureNative.IsIconic(target.Handle)) throw new InvalidOperationException("Abra a janela escolhida e selecione-a novamente.");
        History.Clear(); latestForAsk = null; Frames = 0; visualChanges = 0; closed = false; SessionId = Guid.NewGuid();
        startedAt = DateTimeOffset.Now; endedAt = null; targetTitle = target.Title; targetProcessId = target.ProcessId;
        cancellation = new(); Status = "Iniciando captura local…"; Updated?.Invoke();
        loop = Run(target, cancellation);
    }
    public async Task Stop(string status = "Captura desligada. Imagens descartadas.")
    {
        var pending = loop;
        cancellation?.Cancel();
        if (pending is not null) await pending;
        History.Clear(); latestForAsk = null; Status = status; Updated?.Invoke();
    }
    private async Task Run(CaptureTarget target, CancellationTokenSource stop)
    {
        IDirect3DDevice? device = null;
        GraphicsCaptureItem? item = null;
        Direct3D11CaptureFramePool? pool = null;
        GraphicsCaptureSession? session = null;
        void OnClosed(GraphicsCaptureItem sender, object args) => closed = true;
        // Defer native setup so Start has assigned the task before notifications.
        await Task.Yield();
        try
        {
            stop.Token.ThrowIfCancellationRequested();
            device = CaptureNative.CreateDevice(); item = CaptureNative.CreateItem(target.Handle);
            var size = item.Size; ValidateSize(size.Width, size.Height);
            item.Closed += OnClosed;
            pool = Direct3D11CaptureFramePool.CreateFreeThreaded(device, DirectXPixelFormat.B8G8R8A8UIntNormalized, 2, size);
            session = pool.CreateCaptureSession(item); session.IsCursorCaptureEnabled = false;
            if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 26100))
                session.MinUpdateInterval = TimeSpan.FromMilliseconds(500);
            session.StartCapture();
            var clock = Stopwatch.StartNew(); double lastFrame = 0;
            while (!stop.IsCancellationRequested)
            {
                await Task.Delay(500, stop.Token);
                if (closed || !CaptureNative.Matches(target)) { Status = "A janela foi fechada. Captura encerrada."; break; }
                if (CaptureNative.IsIconic(target.Handle)) { Status = "Janela minimizada. Captura encerrada; restaure-a para iniciar novamente."; break; }
                using var frame = pool.TryGetNextFrame();
                if (frame is null)
                {
                    if (clock.Elapsed.TotalSeconds - lastFrame > 10)
                    {
                        Status = Frames == 0 ? "Aguardando imagem. A janela pode não permitir captura." : "Captura ativa · aguardando novas imagens da janela.";
                        Updated?.Invoke();
                    }
                    continue;
                }
                ValidateSize(frame.ContentSize.Width, frame.ContentSize.Height);
                if (frame.ContentSize.Width != size.Width || frame.ContentSize.Height != size.Height)
                {
                    size = frame.ContentSize; frame.Dispose();
                    pool.Recreate(device, DirectXPixelFormat.B8G8R8A8UIntNormalized, 2, size); continue;
                }
                using var bitmap = await SoftwareBitmap.CreateCopyFromSurfaceAsync(frame.Surface);
                if (stop.IsCancellationRequested || closed) break;
                var reduced = CaptureNative.Reduce(bitmap, size.Width, size.Height);
                // Keep one sharper local-only frame for explicit Ask/Talk. Replay
                // remains at its small fixed budget, so passive capture cost stays bounded.
                var askImage = CaptureNative.Reduce(bitmap, size.Width, size.Height, 1280, 720);
                latestForAsk = new VisionImage(askImage.Pixels, askImage.Width, askImage.Height);
                var change = History.Add(reduced.Pixels, reduced.Width, reduced.Height, clock.Elapsed);
                if (change.Changed) visualChanges++;
                Frames++; lastFrame = clock.Elapsed.TotalSeconds;
                Status = $"Capturando apenas a janela escolhida · {Frames} imagens · {History.Events} mudanças visuais";
                Frame?.Invoke(reduced.Pixels, reduced.Width, reduced.Height, change.Difference, change.Changed);
                Updated?.Invoke();
            }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        catch (Exception ex) { Status = "Captura encerrada: " + ex.Message; }
        finally
        {
            if (item is not null) item.Closed -= OnClosed;
            session?.Dispose(); pool?.Dispose(); device?.Dispose();
            endedAt = DateTimeOffset.Now; History.Clear(); cancellation = null; stop.Dispose(); Updated?.Invoke();
        }
    }
    public CaptureSessionSnapshot? Snapshot(bool replayOpened, bool pauseUsed, bool resumeUsed)
    {
        if (startedAt is null) return null;
        return new CaptureSessionSnapshot(SessionId, startedAt.Value, endedAt, targetTitle, targetProcessId,
            Frames, visualChanges, Status, replayOpened, pauseUsed, resumeUsed);
    }
    private static void ValidateSize(int width, int height)
    {
        if (width < 1 || height < 1 || (long)width * height > 8_847_360)
            throw new InvalidOperationException("Esta prévia aceita janelas de até 8,8 milhões de pixels (incluindo 4K). Reduza a resolução da janela.");
    }
}
