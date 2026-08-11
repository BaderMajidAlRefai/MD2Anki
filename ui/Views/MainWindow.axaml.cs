using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using ui.ViewModels;

namespace ui.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
        SettingsPage.CloseRequested += SettingsPage_CloseRequested;
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
}
