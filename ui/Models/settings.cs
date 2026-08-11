namespace ui.Models;

public sealed class ApplicationSettings
{
    public ObsidianSettings Obsidian { get; set; } = new();
    public AnkiSettings Anki { get; set; } = new();
}

public sealed class ObsidianSettings
{
    public string ObsidianRoot { get; set; } = string.Empty;
    public string NotesPattern { get; set; } = string.Empty;
}

public sealed class AnkiSettings
{
    public string AnkiConnectUrl { get; set; } = string.Empty;
    public int AnkiConnectVersion { get; set; }
}
