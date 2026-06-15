using D2RItemInspector;

// Standalone entry point: collect every character + shared stash in the save directory, then print.
// Other apps reference this assembly and call SaveInspector.Collect() to get the same data as an
// InspectionResult (dictionaries of CharacterData / StashData) instead of console output.
const string saveDir = @"etc\SavedGames";
const string resourceDir = @"D2SLib-D2R\src\Resources";

var inspector = new SaveInspector(saveDir, resourceDir);
InspectionResult result = inspector.Collect();
ConsoleReportRenderer.Print(result);
