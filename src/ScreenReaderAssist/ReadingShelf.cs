namespace ScreenReaderAssist;

/// <summary>
/// An intentionally volatile, bounded reading shelf. It contains OCR/UI/clipboard
/// text but never writes user content to disk or diagnostic logs.
/// </summary>
public sealed class ReadingShelf
{
    public const int Capacity = 30;
    public const int MaximumCharacters = 20_000;
    private readonly List<ReadingEntry> _entries = new();
    public IReadOnlyList<ReadingEntry> Entries => _entries;
    public bool Add(string? source, string? text, DateTimeOffset? time = null)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        string value = text.Trim();
        if (value.Length > MaximumCharacters) value = value[..MaximumCharacters];
        string name = string.IsNullOrWhiteSpace(source) ? "読み取り" : source.Trim();
        if (name.Length > 40) name = name[..40];
        // Avoid dozens of identical hover results or duplicate manual reads.
        if (_entries.Count > 0 && _entries[0].Text == value &&
            _entries[0].Source == name) return false;
        _entries.Insert(0, new ReadingEntry(name, value, time ?? DateTimeOffset.Now));
        if (_entries.Count > Capacity) _entries.RemoveRange(Capacity, _entries.Count-Capacity);
        return true;
    }
    public void Clear() => _entries.Clear();
    public IReadOnlyList<ReadingEntry> Search(string? query)
    {
        if (string.IsNullOrWhiteSpace(query)) return _entries;
        string q = query.Trim();
        return _entries.Where(e => e.Source.Contains(q, StringComparison.CurrentCultureIgnoreCase) ||
            e.Text.Contains(q, StringComparison.CurrentCultureIgnoreCase)).ToArray();
    }
}

public sealed record ReadingEntry(string Source, string Text, DateTimeOffset Time)
{
    public override string ToString()
    {
        string firstLine = Text.Replace("\r", " ").Replace("\n", " ");
        if (firstLine.Length > 65) firstLine = firstLine[..65]+"…";
        return Time.ToString("HH:mm") + " " + Source + "  " + firstLine;
    }
}
