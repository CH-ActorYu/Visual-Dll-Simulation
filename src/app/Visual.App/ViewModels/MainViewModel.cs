using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Visual.Abstractions.Contracts;
using Visual.AppCore.Interaction;
using Visual.AppCore.Runtime;
using Visual.AppCore.Settings;
using Visual.Distance.Contracts;
using Visual.IO.Contracts;
using Visual.Vision.Contracts;
using PixelFormat = Visual.Image.Contracts.PixelFormat;

namespace Visual.App.ViewModels;

public sealed class MainViewModel : ObservableObject, IAsyncDisposable
{
    private const string TargetId = "target-1";
    private const double PreviewFramesPerSecond = 15;
    private static readonly long PreviewIntervalTicks =
        Math.Max(1, (long)(Stopwatch.Frequency / PreviewFramesPerSecond));
    private readonly IFrameSourceFactory _sourceFactory;
    private readonly ModuleCatalog _catalog;
    private readonly ConfigurationService _configuration;
    private readonly RoiInteractionController _roiController;
    private readonly OverlayRenderer _overlayRenderer;
    private readonly Dispatcher _dispatcher;
    private readonly LatestDisposableSlot<FrameUiSnapshot> _pendingUiFrame = new();
    private readonly SemaphoreSlim _sourceCloseGate = new(1, 1);
    private readonly object _stateSync = new();
    private readonly DetectionProfile _profile = DetectionProfile.CreateDefault();
    private CancellationTokenSource? _runCancellation;
    private TaskCompletionSource? _runCompletion;
    private IFrameSource? _source;
    private IDistanceService? _distanceService;
    private string? _videoPath;
    private VisionEngineInfo? _selectedEngine;
    private string _statusText = "未启动";
    private string _distanceText = "--";
    private string _diagnosticText = "等待视频源";
    private ImageSource? _previewImage;
    private bool _isRunning;
    private ImageSize? _imageSize;
    private string? _currentSourceId;
    private double _viewportWidth;
    private double _viewportHeight;
    private long _nextPreviewTimestamp;
    private int _uiDrainScheduled;
    private int _disposeState;
    private WriteableBitmap? _previewBitmap;
    private ViewportOverlay _overlay = new(0, 0, 0, 0, false, 0, 0, 0, 0, false, 0, 0, false, string.Empty);

    public MainViewModel(
        IFrameSourceFactory sourceFactory,
        ModuleCatalog catalog,
        ConfigurationService configuration,
        RoiInteractionController roiController,
        OverlayRenderer overlayRenderer)
    {
        _sourceFactory = sourceFactory;
        _catalog = catalog;
        _configuration = configuration;
        _roiController = roiController;
        _overlayRenderer = overlayRenderer;
        _dispatcher = Dispatcher.CurrentDispatcher;
        Engines = catalog.GetEngines();
        SelectedEngine = Engines.FirstOrDefault(info => info.IsAvailable) ?? Engines.FirstOrDefault();
        Calibration = new CalibrationViewModel();
        StartCommand = new AsyncRelayCommand(StartAsync, () => !IsRunning);
        StopCommand = new AsyncRelayCommand(StopAsync, () => IsRunning);
        ClearRoiCommand = new RelayCommand(ClearRoi);
        ApplyCalibrationCommand = new AsyncRelayCommand(ApplyCalibrationAsync, () => _distanceService is not null && _imageSize is not null);
    }

    public IReadOnlyList<VisionEngineInfo> Engines { get; }
    public CalibrationViewModel Calibration { get; }
    public AsyncRelayCommand StartCommand { get; }
    public AsyncRelayCommand StopCommand { get; }
    public RelayCommand ClearRoiCommand { get; }
    public AsyncRelayCommand ApplyCalibrationCommand { get; }
    public string? VideoPath { get => _videoPath; set => SetProperty(ref _videoPath, value); }
    public VisionEngineInfo? SelectedEngine { get => _selectedEngine; set => SetProperty(ref _selectedEngine, value); }
    public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }
    public string DistanceText { get => _distanceText; private set => SetProperty(ref _distanceText, value); }
    public string DiagnosticText { get => _diagnosticText; private set => SetProperty(ref _diagnosticText, value); }
    public ImageSource? PreviewImage { get => _previewImage; private set => SetProperty(ref _previewImage, value); }
    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (SetProperty(ref _isRunning, value))
            {
                StartCommand.NotifyCanExecuteChanged();
                StopCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public ViewportOverlay Overlay { get => _overlay; private set => SetProperty(ref _overlay, value); }

    public async Task InitializeAsync()
    {
        if (SelectedEngine is null)
        {
            StatusText = "无可用引擎";
            return;
        }

        await ReplaceDistanceServiceAsync(SelectedEngine.Id);
        try
        {
            var settings = await _configuration.RestoreAsync(_distanceService!);
            VideoPath = settings.VideoPath;
            _roiController.Restore(settings.TargetRoi);
            StatusText = _distanceService!.IsCalibrated ? "未启动" : "未标定";
        }
        catch (Exception exception)
        {
            StatusText = "配置无效";
            DiagnosticText = exception.Message;
        }

        ApplyCalibrationCommand.NotifyCanExecuteChanged();
    }

    public bool BeginRoi(Point2D viewportPoint)
    {
        lock (_stateSync)
        {
            if (_imageSize is null)
            {
                DiagnosticText = "请先启动视频，显示画面后再框选 ROI";
                return false;
            }
        }

        _roiController.Begin(viewportPoint);
        DiagnosticText = "正在框选 ROI…";
        return true;
    }

    public bool CompleteRoi(Point2D viewportPoint, double width, double height)
    {
        ImageSize? currentImageSize;
        lock (_stateSync)
        {
            currentImageSize = _imageSize;
        }

        if (currentImageSize is not { } imageSize || width <= 0 || height <= 0)
        {
            return false;
        }

        _viewportWidth = width;
        _viewportHeight = height;
        var accepted = _roiController.Complete(viewportPoint, new ViewportTransform(width, height, imageSize));
        UpdateOverlay(null, width, height);
        if (accepted is null)
        {
            DiagnosticText = "ROI 至少为 16×16 像素";
            return false;
        }

        DiagnosticText = $"ROI 已选定：{accepted.Value.X}, {accepted.Value.Y}, {accepted.Value.Width}×{accepted.Value.Height}";
        return true;
    }

    public void ResizeOverlay(double width, double height)
    {
        _viewportWidth = width;
        _viewportHeight = height;
        UpdateOverlay(null, width, height);
    }

    private async Task StartAsync()
    {
        if (SelectedEngine is null || string.IsNullOrWhiteSpace(VideoPath))
        {
            StatusText = "请选择视频文件";
            return;
        }

        if (!File.Exists(VideoPath))
        {
            StatusText = "视频文件不存在";
            return;
        }

        if (_distanceService is null || !string.Equals(_distanceService.ExportConfig().EngineId, SelectedEngine.Id, StringComparison.OrdinalIgnoreCase))
        {
            await ReplaceDistanceServiceAsync(SelectedEngine.Id);
        }

        var runCancellation = new CancellationTokenSource();
        var runCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _runCancellation = runCancellation;
        _runCompletion = runCompletion;
        var token = runCancellation.Token;
        _nextPreviewTimestamp = 0;
        _currentSourceId = System.IO.Path.GetFullPath(VideoPath);
        var source = _sourceFactory.Create(new FrameSourceDescriptor(_currentSourceId, FrameSourceKind.VideoFile, VideoPath));
        _source = source;
        IsRunning = true;
        StatusText = _distanceService!.IsCalibrated ? "测量中" : "未标定";

        try
        {
            await Task.Run(() => ProcessFramesAsync(source, token), CancellationToken.None);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            StatusText = "运行失败";
            DiagnosticText = exception.Message;
        }
        finally
        {
            await Task.Run(async () => await CloseSourceAsync(), CancellationToken.None);
            if (StatusText is "测量中" or "未检出")
            {
                StatusText = "未启动";
            }

            runCancellation.Dispose();
            _runCancellation = null;
            runCompletion.TrySetResult();
            _runCompletion = null;
            IsRunning = false;
        }
    }

    private async Task StopAsync()
    {
        _runCancellation?.Cancel();
        if (_runCompletion is not { } completion)
        {
            return;
        }

        try
        {
            await completion.Task.WaitAsync(TimeSpan.FromSeconds(3));
        }
        catch (TimeoutException)
        {
            StatusText = "停止超时";
            DiagnosticText = "视频任务未在 3 秒内停止，请关闭软件或检查视频解码器";
        }
    }

    private async Task ProcessFramesAsync(IFrameSource source, CancellationToken cancellationToken)
    {
        await source.OpenAsync(cancellationToken).ConfigureAwait(false);
        await source.StartAsync(cancellationToken).ConfigureAwait(false);
        await foreach (var lease in source.ReadFramesAsync(cancellationToken).ConfigureAwait(false))
        {
            using (lease)
            {
                var frame = lease.Frame;
                lock (_stateSync)
                {
                    _imageSize = frame.Info.Size;
                }

                var roi = _roiController.ClampTo(frame.Info.Size) ??
                          new RoiRect(0, 0, frame.Info.Size.Width, frame.Info.Size.Height);
                DistanceMeasurement? measurement = null;
                string statusText;
                string distanceText;
                try
                {
                    var results = await _distanceService!.MeasureAsync(
                        new DistanceRequest(frame, [new TargetRegion(TargetId, roi)]),
                        cancellationToken).ConfigureAwait(false);
                    measurement = results[0];
                    statusText = measurement.Status switch
                    {
                        VisionResultStatus.Valid => "测量中",
                        VisionResultStatus.NotDetected => "未检出",
                        VisionResultStatus.NotCalibrated => "未标定",
                        _ => "测量失败"
                    };
                    distanceText = measurement.Distance is { } distance
                        ? $"{distance:F1} {measurement.Unit}"
                        : "--";
                }
                catch (NotCalibratedException)
                {
                    statusText = "未标定";
                    distanceText = "--";
                }

                if (ShouldPublishPreview())
                {
                    var diagnosticText =
                        $"{frame.Info.Size.Width}×{frame.Info.Size.Height} | {frame.Info.FrameIndex} 帧 | 丢帧 {source.DroppedCount}";
                    PublishUiFrame(FrameUiSnapshot.Create(
                        frame,
                        measurement,
                        statusText,
                        distanceText,
                        diagnosticText));
                }
            }
        }
    }

    private bool ShouldPublishPreview()
    {
        var now = Stopwatch.GetTimestamp();
        if (now < _nextPreviewTimestamp)
        {
            return false;
        }

        _nextPreviewTimestamp = now + PreviewIntervalTicks;
        return true;
    }

    private void PublishUiFrame(FrameUiSnapshot snapshot)
    {
        try
        {
            _pendingUiFrame.Publish(snapshot);
            ScheduleUiDrain();
        }
        catch (ObjectDisposedException)
        {
            snapshot.Dispose();
        }
    }

    private void ScheduleUiDrain()
    {
        if (Interlocked.CompareExchange(ref _uiDrainScheduled, 1, 0) != 0)
        {
            return;
        }

        try
        {
            _ = _dispatcher.BeginInvoke(DispatcherPriority.Render, DrainUiFrame);
        }
        catch (InvalidOperationException)
        {
            Volatile.Write(ref _uiDrainScheduled, 0);
        }
    }

    private void DrainUiFrame()
    {
        try
        {
            using var snapshot = _pendingUiFrame.Take();
            if (snapshot is not null)
            {
                ApplyUiFrame(snapshot);
            }
        }
        finally
        {
            Volatile.Write(ref _uiDrainScheduled, 0);
            if (_pendingUiFrame.HasValue)
            {
                ScheduleUiDrain();
            }
        }
    }

    private void ApplyUiFrame(FrameUiSnapshot snapshot)
    {
        var format = ToWpfPixelFormat(snapshot.Format);
        if (_previewBitmap is null ||
            _previewBitmap.PixelWidth != snapshot.Width ||
            _previewBitmap.PixelHeight != snapshot.Height ||
            _previewBitmap.Format != format)
        {
            _previewBitmap = new WriteableBitmap(
                snapshot.Width,
                snapshot.Height,
                96,
                96,
                format,
                null);
            PreviewImage = _previewBitmap;
        }

        _previewBitmap.WritePixels(
            new Int32Rect(0, 0, snapshot.Width, snapshot.Height),
            snapshot.Buffer,
            snapshot.Stride,
            0);
        StatusText = snapshot.StatusText;
        DistanceText = snapshot.DistanceText;
        DiagnosticText = snapshot.DiagnosticText;
        ApplyCalibrationCommand.NotifyCanExecuteChanged();
        UpdateOverlay(snapshot.Measurement);
    }

    private async Task ApplyCalibrationAsync()
    {
        ImageSize? currentImageSize;
        lock (_stateSync)
        {
            currentImageSize = _imageSize;
        }

        if (_distanceService is null || currentImageSize is not { } imageSize)
        {
            StatusText = "请先启动视频以取得图像尺寸";
            return;
        }

        try
        {
            if (string.IsNullOrWhiteSpace(_currentSourceId))
            {
                StatusText = "请先启动视频以确定来源";
                return;
            }

            _distanceService.SetCalibration(Calibration.Create(_currentSourceId, imageSize, _profile));
            await PersistAsync();
            StatusText = IsRunning ? "测量中" : "未启动";
            DiagnosticText = "标定已应用并保存";
        }
        catch (Exception exception)
        {
            StatusText = "标定无效";
            DiagnosticText = exception.Message;
        }
    }

    private async Task ReplaceDistanceServiceAsync(string engineId)
    {
        if (_distanceService is not null)
        {
            await _distanceService.DisposeAsync();
        }

        _distanceService = _catalog.CreateDistanceService(engineId, _profile);
    }

    private void ClearRoi()
    {
        _roiController.Clear();
        Overlay = new ViewportOverlay(0, 0, 0, 0, false, 0, 0, 0, 0, false, 0, 0, false, string.Empty);
        DiagnosticText = "ROI 已清除";
    }

    private void UpdateOverlay(DistanceMeasurement? measurement, double? width = null, double? height = null)
    {
        ImageSize? currentImageSize;
        lock (_stateSync)
        {
            currentImageSize = _imageSize;
        }

        if (currentImageSize is not { } imageSize)
        {
            return;
        }

        var viewportWidth = width ?? (_viewportWidth > 0 ? _viewportWidth : imageSize.Width);
        var viewportHeight = height ?? (_viewportHeight > 0 ? _viewportHeight : imageSize.Height);
        var snapshot = _overlayRenderer.Build(_roiController.CurrentRoi, measurement);
        Overlay = _overlayRenderer.Project(snapshot, new ViewportTransform(viewportWidth, viewportHeight, imageSize));
    }

    private async Task PersistAsync()
    {
        if (_distanceService is not null && SelectedEngine is not null)
        {
            await _configuration.PersistAsync(_distanceService, SelectedEngine.Id, VideoPath, _roiController.CurrentRoi);
        }
    }

    private async ValueTask CloseSourceAsync()
    {
        await _sourceCloseGate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            var source = Interlocked.Exchange(ref _source, null);
            if (source is null)
            {
                return;
            }

            try { await source.StopAsync(CancellationToken.None).ConfigureAwait(false); } catch { }
            try { await source.CloseAsync(CancellationToken.None).ConfigureAwait(false); } catch { }
            try { await source.DisposeAsync().ConfigureAwait(false); } catch { }
        }
        finally
        {
            _sourceCloseGate.Release();
        }
    }

    private static System.Windows.Media.PixelFormat ToWpfPixelFormat(PixelFormat format) => format switch
    {
        PixelFormat.Gray8 => PixelFormats.Gray8,
        PixelFormat.Bgr24 => PixelFormats.Bgr24,
        PixelFormat.Bgra32 => PixelFormats.Bgra32,
        PixelFormat.Rgb24 => PixelFormats.Rgb24,
        _ => throw new NotSupportedException($"Unsupported pixel format {format}.")
    };

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeState, 1) != 0)
        {
            return;
        }

        var runStopped = true;
        _runCancellation?.Cancel();
        if (_runCompletion is { } completion)
        {
            try
            {
                await completion.Task.WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (TimeoutException)
            {
                runStopped = false;
                try
                {
                    await CloseSourceAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2));
                }
                catch (TimeoutException)
                {
                }
            }
        }
        else
        {
            try
            {
                await CloseSourceAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2));
            }
            catch (TimeoutException)
            {
                runStopped = false;
            }
        }

        _pendingUiFrame.Dispose();
        if (runStopped && _distanceService is not null)
        {
            await PersistAsync();
            await _distanceService.DisposeAsync();
            _distanceService = null;
        }
    }
}

internal sealed class FrameUiSnapshot : IDisposable
{
    private byte[]? _buffer;

    private FrameUiSnapshot(
        byte[] buffer,
        int width,
        int height,
        int stride,
        PixelFormat format,
        DistanceMeasurement? measurement,
        string statusText,
        string distanceText,
        string diagnosticText)
    {
        _buffer = buffer;
        Width = width;
        Height = height;
        Stride = stride;
        Format = format;
        Measurement = measurement;
        StatusText = statusText;
        DistanceText = distanceText;
        DiagnosticText = diagnosticText;
    }

    public byte[] Buffer => _buffer ?? throw new ObjectDisposedException(nameof(FrameUiSnapshot));
    public int Width { get; }
    public int Height { get; }
    public int Stride { get; }
    public PixelFormat Format { get; }
    public DistanceMeasurement? Measurement { get; }
    public string StatusText { get; }
    public string DistanceText { get; }
    public string DiagnosticText { get; }

    public static FrameUiSnapshot Create(
        Visual.Image.Contracts.ImageFrame frame,
        DistanceMeasurement? measurement,
        string statusText,
        string distanceText,
        string diagnosticText)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(frame.Data.Length);
        frame.Data.Span.CopyTo(buffer);
        return new FrameUiSnapshot(
            buffer,
            frame.Info.Size.Width,
            frame.Info.Size.Height,
            frame.Stride,
            frame.Format,
            measurement,
            statusText,
            distanceText,
            diagnosticText);
    }

    public void Dispose()
    {
        var buffer = Interlocked.Exchange(ref _buffer, null);
        if (buffer is not null)
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
