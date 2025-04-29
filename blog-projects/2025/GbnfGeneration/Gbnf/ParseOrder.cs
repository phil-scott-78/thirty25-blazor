using System.Text;
using System.Text.Json;
using LLama;
using LLama.Common;
using LLama.Sampling;
using RedPajama;

namespace Gbnf;

public class ParseOrder
{
    public class User
    {
        public required string FirstName { get; init; }
        public required string LastName { get; init; }
        public required int Age { get; init; }
        public required bool IsMember { get; init; }
    }

    public static async Task<User> Parse(string modelPath)
    {
        // Define our sample text prompt that contains user information to be extracted
        const string prompt = """
                              Extract the user information from this email:

                              ```
                              Hey, when you get a chance the member Terry Mitchell, 
                              age 28, needs to be contacted.
                              ```
                              """;
    
        // Configure model parameters, in the real world we wouldn't be loading this in the method but
        // rather pass it as a parameter.
        var parameters = new ModelParams(modelPath) { ContextSize = 1000, GpuLayerCount = -1, };
        using var model = await LLamaWeights.LoadFromFileAsync(parameters);
        
        // Build type structure metadata from the User class to guide extraction
        // and GBNF and JSON sample generators
        var typeModelBuilder = new TypeModelBuilder<User>();
        var gbnfGenerator = new GbnfGenerator();
        var jsonGenerator = new JsonSampleGenerator();

        // Generate the type model, grammar, and JSON sample for the User class
        var typeModel = typeModelBuilder.Build();
        var gbnf = gbnfGenerator.Generate(typeModel);
        var jsonSample = jsonGenerator.Generate(typeModel);
    
        // Initialize a stateless executor for performing inference with the model
        var executor = new StatelessExecutor(model, parameters)
        {
            ApplyTemplate = true
        };

        // Combine the extraction prompt with the JSON format instructions
        var promptWithTemplate = $"""
                              {prompt}
                              
                              Output in JSON, using this format:
                              {jsonSample}
                              """;

        // Run inference with the model using the prompt and GBNF grammar constraints
        var response = executor.InferAsync(promptWithTemplate, new InferenceParams()
        {
            SamplingPipeline = new DefaultSamplingPipeline()
            {
                // The Grammar enforces output to match the User class structure
                // This prevents hallucinated fields or malformed JSON
                Grammar = new Grammar(gbnf, "root")
            }
        });

        // Collect the streaming response tokens into a complete string
        var sb = new StringBuilder();
        await foreach (var s in response)
        {
            sb.Append(s);
        }

        // Parse the JSON response into a User object and deserialize
        var json = sb.ToString();
        return JsonSerializer.Deserialize<User>(json) 
               ?? throw new InvalidOperationException($"Invalid JSON: {json}");
    }
}