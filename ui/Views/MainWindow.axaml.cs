using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using ui.Services;
using ui.ViewModels;

namespace ui.Views;

public partial class MainWindow : Window
{
    private MainViewModel? _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        SettingsPage.CloseRequested += SettingsPage_CloseRequested;
        SettingsPage.SettingsSaved += SettingsPage_SettingsSaved;
    }

    public MainWindow(MainViewModel viewModel, ApiClient apiClient)
        : this()
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        SettingsPage.Initialize(apiClient);
    }

    private async void OpenSettings_Click(object? sender, RoutedEventArgs eventArgs)
    {
        MainPage.IsVisible = false;
        SettingsPage.IsVisible = true;
        await SettingsPage.LoadSettingsAsync();
    }

    private void SettingsPage_CloseRequested(object? sender, EventArgs eventArgs)
    {
        SettingsPage.IsVisible = false;
        MainPage.IsVisible = true;
    }

    private async void SettingsPage_SettingsSaved(object? sender, EventArgs eventArgs)
    {
        if (_viewModel is not null)
        {
            await _viewModel.RefreshConnectionStatusAsync();
        }
    }
}
