using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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
    private readonly IFrameSourceFactory _sourceFactory;
    private readonly ModuleCatalog _catalog;
    private readonly ConfigurationService _configuration;
    private readonly RoiInteractionController _roiController;
    private readonly OverlayRenderer _overlayRenderer;
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
    private int _disposeState;
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
        if (_imageSize is null)
        {
            DiagnosticText = "请先启动视频，显示画面后再框选 ROI";
            return false;
        }

        _roiController.Begin(viewportPoint);
        DiagnosticText = "正在框选 ROI…";
        return true;
    }

    public bool CompleteRoi(Point2D viewportPoint, double width, double height)
    {
        if (_imageSize is not { } imageSize || width <= 0 || height <= 0)
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

        _runCancellation = new CancellationTokenSource();
        _runCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var token = _runCancellation.Token;
        _currentSourceId = System.IO.Path.GetFullPath(VideoPath);
        _source = _sourceFactory.Create(new FrameSourceDescriptor(_currentSourceId, FrameSourceKind.VideoFile, VideoPath));
        IsRunning = true;
        StatusText = _distanceService!.IsCalibrated ? "测量中" : "未标定";

        try
        {
            await _source.OpenAsync(token);
            await _source.StartAsync(token);
            await foreach (var lease in _source.ReadFramesAsync(token))
            {
                using (lease)
                {
                    var frame = lease.Frame;
                    _imageSize = frame.Info.Size;
                    PreviewImage = CreateBitmap(frame);
                    ApplyCalibrationCommand.NotifyCanExecuteChanged();
                    var roi = _roiController.ClampTo(frame.Info.Size) ??
                              new RoiRect(0, 0, frame.Info.Size.Width, frame.Info.Size.Height);
                    DistanceMeasurement? measurement = null;
                    try
                    {
                        var results = await _distanceService.MeasureAsync(
                            new DistanceRequest(frame, [new TargetRegion(TargetId, roi)]), token);
                        measurement = results[0];
                        StatusText = measurement.Status switch
                        {
                            VisionResultStatus.Valid => "测量中",
                            VisionResultStatus.NotDetected => "未检出",
                            VisionResultStatus.NotCalibrated => "未标定",
                            _ => "测量失败"
                        };
                        DistanceText = measurement.Distance is { } distance ? $"{distance:F1} {measurement.Unit}" : "--";
                    }
                    catch (NotCalibratedException)
                    {
                        StatusText = "未标定";
                        DistanceText = "--";
                    }

                    DiagnosticText = $"{frame.Info.Size.Width}×{frame.Info.Size.Height} | {frame.Info.FrameIndex} 帧 | 丢帧 {_source.DroppedCount}";
                    UpdateOverlay(measurement);
                }
            }
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
            await CloseSourceAsync();
            IsRunning = false;
            if (StatusText is "测量中" or "未检出")
            {
                StatusText = "未启动";
            }

            _runCompletion.TrySetResult();
        }
    }

    private async Task StopAsync()
    {
        _runCancellation?.Cancel();
        await Task.Yield();
    }

    private async Task ApplyCalibrationAsync()
    {
        if (_distanceService is null || _imageSize is not { } imageSize)
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
        if (_imageSize is not { } imageSize)
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
        if (_source is null)
        {
            return;
        }

        try { await _source.StopAsync(CancellationToken.None); } catch { }
        try { await _source.CloseAsync(CancellationToken.None); } catch { }
        await _source.DisposeAsync();
        _source = null;
        _runCancellation?.Dispose();
        _runCancellation = null;
    }

    private static BitmapSource CreateBitmap(Visual.Image.Contracts.ImageFrame frame)
    {
        var format = frame.Format switch
        {
            PixelFormat.Gray8 => PixelFormats.Gray8,
            PixelFormat.Bgr24 => PixelFormats.Bgr24,
            PixelFormat.Bgra32 => PixelFormats.Bgra32,
            PixelFormat.Rgb24 => PixelFormats.Rgb24,
            _ => throw new NotSupportedException($"Unsupported pixel format {frame.Format}.")
        };
        var bitmap = BitmapSource.Create(
            frame.Info.Size.Width,
            frame.Info.Size.Height,
            96,
            96,
            format,
            null,
            frame.Data.ToArray(),
            frame.Stride);
        bitmap.Freeze();
        return bitmap;
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeState, 1) != 0)
        {
            return;
        }

        _runCancellation?.Cancel();
        if (_runCompletion is { } completion)
        {
            await completion.Task;
        }
        else
        {
            await CloseSourceAsync();
        }

        if (_distanceService is not null)
        {
            await PersistAsync();
            await _distanceService.DisposeAsync();
            _distanceService = null;
        }
    }
}
