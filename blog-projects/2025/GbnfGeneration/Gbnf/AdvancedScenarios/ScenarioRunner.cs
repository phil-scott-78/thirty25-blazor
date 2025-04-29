using System.Reflection;
using RedPajama;

namespace Gbnf.AdvancedScenarios;

public class ScenarioRunner
{
    public class ProcessResult
    {
        public required Type Type { get; init; }
        public required string Gbnf { get; init; }
        public required string JsonSample { get; init; }
    }

    public List<ProcessResult> ProcessAllTypesInNamespace()
    {
        var results = new List<ProcessResult>();

        // Get the assembly that contains the namespace
        var assembly = Assembly.GetExecutingAssembly();

        // Find all types in the specified namespace
        var types = assembly.GetTypes()
            .Where(t => t.Namespace != null && t.Namespace.StartsWith("Gbnf.AdvancedScenarios") && t.IsNested == false && t.FullName != GetType().FullName)
            
            .ToList();

        foreach (var type in types)
        {
            try
            {
                // Create a generic method to process the type
                var result = ProcessType(type);
                results.Add(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing type {type.FullName}: {ex.Message}");
            }
        }

        return results;
    }

    private ProcessResult ProcessType(Type type)
    {
        // Create the generic method to call with the given type
        var method = typeof(ScenarioRunner).GetMethod(nameof(ProcessTypeGeneric),
            BindingFlags.NonPublic | BindingFlags.Instance);

        var genericMethod = method!.MakeGenericMethod(type);

        // Invoke the generic method
        return (ProcessResult)genericMethod.Invoke(this, null)!;
    }

    private ProcessResult ProcessTypeGeneric<T>()
    {
        var typeModelBuilder = new TypeModelBuilder<T>();
        var gbnfGenerator = new GbnfGenerator();
        var jsonGenerator = new JsonSampleGenerator();

        // Generate the type model, grammar, and JSON sample for the class
        var typeModel = typeModelBuilder.Build();
        var gbnf = gbnfGenerator.Generate(typeModel);
        var jsonSample = jsonGenerator.Generate(typeModel);

        return new ProcessResult
        {
            Type = typeof(T),
            Gbnf = gbnf,
            JsonSample = jsonSample
        };
    }
}