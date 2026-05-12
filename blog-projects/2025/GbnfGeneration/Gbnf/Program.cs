using Gbnf;
using Gbnf.AdvancedScenarios;

var outputRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "output"));
if (Directory.Exists(outputRoot)) Directory.Delete(outputRoot, recursive: true);
Directory.CreateDirectory(outputRoot);

var advDir = Path.Combine(outputRoot, "AdvancedScenarios");
Directory.CreateDirectory(advDir);

var llamaGrammar = new LlamaGrammar();
File.WriteAllText(Path.Combine(outputRoot, "LlamaGrammar.GetGrammar.gbnf"), llamaGrammar.GetGrammar());
File.WriteAllText(Path.Combine(outputRoot, "LlamaGrammar.Schema.gbnf"), llamaGrammar.Schema());

File.WriteAllText(Path.Combine(outputRoot, "MyApp.GetSimpleGbnf.gbnf"), MyApp.GetSimpleGbnf());
File.WriteAllText(Path.Combine(outputRoot, "MyApp.GetSimpleJson.json"), MyApp.GetSimpleJson());

var scenarioRunner = new ScenarioRunner();
foreach (var (key, value) in scenarioRunner.ProcessAllTypesInNamespace())
{
    var lastDash = key.LastIndexOf('-');
    var typeFullName = key[..lastDash];
    var ext = key[(lastDash + 1)..];
    var shortName = typeFullName.Split('.').Last();
    File.WriteAllText(Path.Combine(advDir, $"{shortName}.{ext}"), value);
}

foreach (var (ext, value) in ProgrammaticallyEnhanced.ProgrammaticallyEnhancingTypeModels())
{
    File.WriteAllText(Path.Combine(outputRoot, $"ProgrammaticallyEnhanced.{ext}"), value);
}

Console.WriteLine($"Wrote artifacts to {outputRoot}");
