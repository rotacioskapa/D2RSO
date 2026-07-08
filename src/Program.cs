using System.Diagnostics;
using D2RItemInspector.Model;
using D2RItemInspector.SaveInspectors;
using D2RItemInspector.Report;

// Default: collect all saves and write/open a self-contained items.html report.
// `--print`: print the text report to the console instead (the old behaviour).
// Real Diablo II: Resurrected save folder (characters + shared stash).
const string saveDirPath = @"%USERPROFILE%\Saved Games\Diablo II Resurrected";
string saveDir = Environment.ExpandEnvironmentVariables(saveDirPath);
// Data tables are copied next to the executable (and bundled into the single-file exe), so resolve
// them relative to the app, not the current working directory.
string resourceDir = Path.Combine(AppContext.BaseDirectory, "Resources");
const string outputFile = "items.html";

var inspector = new SaveInspector(saveDir, resourceDir);
InspectionResult result = inspector.Collect();

if (args.Contains("--print"))
{
    ConsoleReportRenderer.Print(result);
    return;
}

HtmlReport.Write(result, outputFile);
string fullPath = Path.GetFullPath(outputFile);
Console.WriteLine($"Item report written to {fullPath}");
try
{
    Process.Start(new ProcessStartInfo(fullPath) { UseShellExecute = true });
}
catch (Exception ex)
{
    Console.WriteLine($"(could not auto-open browser: {ex.Message}) — open the file manually.");
}
