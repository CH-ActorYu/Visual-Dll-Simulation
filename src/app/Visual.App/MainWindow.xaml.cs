using Microsoft.Win32;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using Visual.Abstractions.Contracts;
using Visual.App.ViewModels;

namespace Visual.App;

/// <summary>
/// Interaction logic for MainWindow.xaml.
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private bool _allowClose;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        Closing += MainWindow_Closing;
    }

    private void BrowseVideo_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择视频文件",
            Filter = "视频文件|*.mp4;*.avi;*.mov;*.mkv;*.wmv|所有文件|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) == true)
        {
            _viewModel.VideoPath = dialog.FileName;
        }
    }

    private void Preview_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var point = e.GetPosition(PreviewViewport);
        _viewModel.BeginRoi(new Point2D(point.X, point.Y));
        PreviewViewport.CaptureMouse();
    }

    private void Preview_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        var point = e.GetPosition(PreviewViewport);
        _viewModel.CompleteRoi(
            new Point2D(point.X, point.Y),
            PreviewViewport.ActualWidth,
            PreviewViewport.ActualHeight);
        PreviewViewport.ReleaseMouseCapture();
    }

    private void Preview_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        _viewModel.ResizeOverlay(e.NewSize.Width, e.NewSize.Height);
    }

    private async void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (_allowClose)
        {
            return;
        }

        e.Cancel = true;
        IsEnabled = false;
        await _viewModel.DisposeAsync();
        _allowClose = true;
        _ = Dispatcher.BeginInvoke(Close);
    }
}
