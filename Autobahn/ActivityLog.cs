using System.Text.Json;
using System.IO;

namespace Autobahn;

public sealed class ActivityLog
{
    public string DirectoryPath { get; }
    public long Total { get; private set; }
    public DateTimeOffset? LastSubmit { get; private set; }
    public string? Warning { get; private set; }
    public List<ActivityEntry> Recent { get; } = new();
    private string FilePath => Path.Combine(DirectoryPath, "activity.jsonl");

    public ActivityLog(string? directoryPath = null)
    {
        DirectoryPath = directoryPath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AntigravityAutobahn");
        try
        {
            Directory.CreateDirectory(DirectoryPath);
            if (!File.Exists(FilePath)) return;
            foreach (string line in File.ReadLines(FilePath))
            {
                try
                {
                    var entry = JsonSerializer.Deserialize<ActivityEntry>(line);
                    if (entry is null) continue;
                    if (entry.Kind == "submit") { Total++; LastSubmit = entry.Time; }
                    Remember(entry);
                }
                catch (JsonException) { Warning = "An incomplete log entry was skipped."; }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { Warning = "Could not read saved history: " + ex.Message; }
    }

    public ActivityEntry Add(string kind, string message, DateTimeOffset? time = null, string? messageKey = null)
    {
        var entry = new ActivityEntry(time ?? DateTimeOffset.Now, kind, message, messageKey);
        if (kind == "submit") { Total++; LastSubmit = entry.Time; }
        Remember(entry);
        try { File.AppendAllText(FilePath, JsonSerializer.Serialize(entry) + Environment.NewLine); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { Warning = "Could not save activity: " + ex.Message; }
        return entry;
    }

    private void Remember(ActivityEntry entry) { Recent.Add(entry); if (Recent.Count > 500) Recent.RemoveAt(0); }
}

public sealed record ActivityEntry(DateTimeOffset Time, string Kind, string Message, string? MessageKey = null)
{
    public override string ToString() => $"{Time:HH:mm:ss} · {Message}";
}
