using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using System;
using System.Threading.Tasks;
using ui.Services;
using ui.ViewModels;
using ui.Views;

namespace ui;

public partial class App : Application
{
    private ApiClient? _apiClient;
    private BackendProcessManager? _backendProcessManager;
    private bool _isShuttingDown;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _apiClient = new ApiClient();
            _backendProcessManager = new BackendProcessManager(_apiClient);

            var mainViewModel = new MainViewModel(_apiClient);
            _backendProcessManager.UnexpectedExit += message =>
            {
                if (_isShuttingDown)
                {
                    return;
                }

                Dispatcher.UIThread.Post(() =>
                    mainViewModel.SetBackendError(message));
            };

            desktop.MainWindow = new MainWindow(mainViewModel, _apiClient);
            desktop.Exit += (_, _) => ShutdownBackend();

            _ = StartBackendAsync(mainViewModel);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private async Task StartBackendAsync(MainViewModel mainViewModel)
    {
        try
        {
            await _backendProcessManager!.StartAsync();

            if (!_isShuttingDown)
            {
                await Dispatcher.UIThread.InvokeAsync(mainViewModel.SetBackendReady);
            }
        }
        catch (Exception exception)
        {
            if (!_isShuttingDown)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                    mainViewModel.SetBackendError(
                        $"Could not start the local backend: {exception.Message}"));
            }
        }
    }

    private void ShutdownBackend()
    {
        _isShuttingDown = true;
        _backendProcessManager?.Dispose();
        _backendProcessManager = null;
        _apiClient?.Dispose();
        _apiClient = null;
    }
}
