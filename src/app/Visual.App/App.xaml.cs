using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using Visual.App.ViewModels;
using Visual.AppCore.Interaction;
using Visual.AppCore.Runtime;
using Visual.AppCore.Settings;
using Visual.Engine.OpenCv.Contracts;
using Visual.IO.Contracts;
using Visual.Vision.Contracts;

namespace Visual.App;

/// <summary>
/// Interaction logic for App.xaml.
/// </summary>
public partial class App : Application
{
    private ServiceProvider? _services;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var services = new ServiceCollection();
        services.AddSingleton<IVisionEngineProvider, OpenCvEngineProvider>();
        services.AddSingleton<IFrameSourceFactory, OpenCvFrameSourceFactory>();
        services.AddSingleton<ModuleCatalog>();
        services.AddSingleton<AppSettingsService>();
        services.AddSingleton<ConfigurationService>();
        services.AddSingleton<RoiInteractionController>();
        services.AddSingleton<OverlayRenderer>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();
        _services = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        try
        {
            var viewModel = _services.GetRequiredService<MainViewModel>();
            await viewModel.InitializeAsync();
            var window = _services.GetRequiredService<MainWindow>();
            var smokeTest = e.Args.Contains("--smoke-test", StringComparer.OrdinalIgnoreCase);
            var videoSmokeTest = e.Args.Contains("--video-smoke-test", StringComparer.OrdinalIgnoreCase);
            if (smokeTest || videoSmokeTest)
            {
                window.ShowActivated = false;
                window.ShowInTaskbar = false;
                window.Opacity = 0;
                window.Loaded += async (_, _) =>
                {
                    if (videoSmokeTest)
                    {
                        viewModel.StartCommand.Execute(null);
                        await Task.Delay(TimeSpan.FromSeconds(30));
                    }

                    await viewModel.DisposeAsync();
                    window.Close();
                };
            }

            window.Show();
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, "启动失败", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_services is not null)
        {
            _services.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        base.OnExit(e);
    }
}
