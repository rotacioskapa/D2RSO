namespace D2RItemInspector;

/// <summary>
/// Entry point for reading a Diablo II save directory. <see cref="Collect"/> returns all items from
/// every character (.d2s) and shared stash (.d2i) as a structured <see cref="InspectionResult"/>
/// (dictionaries keyed by file name) — this is what other apps consume. The console program renders
/// the same result to stdout via <see cref="ConsoleReportRenderer"/>.
/// </summary>
public sealed class SaveInspector
{
    private readonly string _saveDir;
    private readonly CharacterInspector _characters;
    private readonly SharedStashInspector _stashes;

    public SaveInspector(string saveDir, string resourceDir)
    {
        _saveDir = saveDir;
        var mapping = new ItemMapping(new ItemNameResolver(resourceDir));
        _characters = new CharacterInspector(mapping);
        _stashes = new SharedStashInspector(mapping);
    }

    /// <summary>Reads every .d2s and .d2i in the save directory. Each file is parsed independently.</summary>
    public InspectionResult Collect()
    {
        var result = new InspectionResult();
        foreach (string file in Directory.GetFiles(_saveDir, "*.d2s").OrderBy(f => f))
            result.Characters[Path.GetFileName(file)] = _characters.Collect(file);
        foreach (string file in Directory.GetFiles(_saveDir, "*.d2i").OrderBy(f => f))
            result.Stashes[Path.GetFileName(file)] = _stashes.Collect(file);
        return result;
    }
}
