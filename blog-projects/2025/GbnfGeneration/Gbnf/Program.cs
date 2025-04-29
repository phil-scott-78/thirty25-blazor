using Gbnf;
using Gbnf.AdvancedScenarios;

MyApp.GetSimpleGbnf();
MyApp.GetSimpleJson();

ProgrammaticallyEnhanced.ProgrammaticallyEnhancingTypeModels();

Console.WriteLine();
Console.WriteLine("=========================================");
Console.WriteLine("Running different advanced GBNF scenarios");
Console.WriteLine("=========================================");
Console.WriteLine();

var results = new ScenarioRunner()
    .ProcessAllTypesInNamespace();

foreach (var result in results)
{
    Console.WriteLine(result.Type.Name);
    Console.WriteLine();
    Console.WriteLine(result.JsonSample);
    Console.WriteLine();
    Console.WriteLine(result.Gbnf);
    Console.WriteLine();
    Console.WriteLine();
    Console.WriteLine();
}