using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using ui.Models;

namespace ui.Views;

public partial class SettingsView : UserControl
{
    private const string BackendUrl = "http://127.0.0.1:8000";
    private static readonly HttpClient HttpClient = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private ApplicationSettings? _originalSettings;

    public event EventHandler? CloseRequested;

    public SettingsView()
    {
        InitializeComponent();
    }

    public async Task LoadSettingsAsync()
    {
        SetBusy(true);
        SetStatus("Loading settings…", false);

        try
        {
            _originalSettings = await HttpClient.GetFromJsonAsync<ApplicationSettings>(
                $"{BackendUrl}/settings",
                JsonOptions)
                ?? throw new InvalidOperationException("The backend returned empty settings.");

            RootPathTextBox.Text = _originalSettings.Obsidian.ObsidianRoot;
            PatternTextBox.Text = _originalSettings.Obsidian.NotesPattern;
            AnkiVersionInput.Value = _originalSettings.Anki.AnkiConnectVersion;
            AnkiUrlTextBox.Text = _originalSettings.Anki.AnkiConnectUrl;
            StatusText.IsVisible = false;
        }
        catch (Exception exception)
        {
            SetStatus($"Could not load settings: {exception.Message}", true);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void BrowseRoot_Click(object? sender, RoutedEventArgs eventArgs)
    {
        var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storageProvider is null)
        {
            SetStatus("The folder picker is not available.", true);
            return;
        }

        var folders = await storageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions
            {
                Title = "Choose Obsidian vault root",
                AllowMultiple = false,
            });

        if (folders.Count > 0)
        {
            RootPathTextBox.Text = folders[0].TryGetLocalPath();
        }
    }

    private async void Save_Click(object? sender, RoutedEventArgs eventArgs)
    {
        var rootPath = RootPathTextBox.Text?.Trim() ?? string.Empty;
        var pattern = PatternTextBox.Text ?? string.Empty;
        var ankiUrl = AnkiUrlTextBox.Text?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(rootPath))
        {
            SetStatus("Choose an Obsidian vault folder.", true);
            return;
        }

        if (string.IsNullOrWhiteSpace(pattern))
        {
            SetStatus("Enter a card extraction pattern.", true);
            return;
        }

        if (AnkiVersionInput.Value is not decimal versionValue)
        {
            SetStatus("Enter an AnkiConnect version.", true);
            return;
        }

        if (!Uri.TryCreate(ankiUrl, UriKind.Absolute, out var parsedUrl)
            || (parsedUrl.Scheme != Uri.UriSchemeHttp && parsedUrl.Scheme != Uri.UriSchemeHttps))
        {
            SetStatus("Enter a valid AnkiConnect HTTP URL.", true);
            return;
        }

        var ankiVersion = decimal.ToInt32(versionValue);
        SetBusy(true);
        SetStatus("Saving settings…", false);

        try
        {
            if (_originalSettings is null
                || rootPath != _originalSettings.Obsidian.ObsidianRoot)
            {
                await PostSettingAsync(
                    "/settings/obsidian/root",
                    new { ObsidianRoot = rootPath });
            }

            if (_originalSettings is null
                || pattern != _originalSettings.Obsidian.NotesPattern)
            {
                await PostSettingAsync(
                    "/settings/obsidian/notespattern",
                    new { PatternUpdate = pattern });
            }

            if (_originalSettings is null
                || ankiVersion != _originalSettings.Anki.AnkiConnectVersion)
            {
                await PostSettingAsync(
                    "/settings/anki/version",
                    new { AnkiVersionUpdate = ankiVersion });
            }

            if (_originalSettings is null
                || ankiUrl != _originalSettings.Anki.AnkiConnectUrl)
            {
                await PostSettingAsync(
                    "/settings/anki/url",
                    new { AnkiUrlUpdate = ankiUrl });
            }

            SetStatus("Settings saved.", false);
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception exception)
        {
            SetStatus($"Could not save settings: {exception.Message}", true);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private static async Task PostSettingAsync<T>(string route, T payload)
    {
        using var response = await HttpClient.PostAsJsonAsync(
            $"{BackendUrl}{route}",
            payload,
            JsonOptions);

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException(
                $"{route} returned {(int)response.StatusCode}: {responseBody}");
        }
    }

    private void Cancel_Click(object? sender, RoutedEventArgs eventArgs)
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void SetBusy(bool isBusy)
    {
        SaveButton.IsEnabled = !isBusy;
    }

    private void SetStatus(string message, bool isError)
    {
        StatusText.Text = message;
        StatusText.Foreground = isError ? Avalonia.Media.Brushes.IndianRed : Avalonia.Media.Brushes.DarkGray;
        StatusText.IsVisible = true;
    }
}
