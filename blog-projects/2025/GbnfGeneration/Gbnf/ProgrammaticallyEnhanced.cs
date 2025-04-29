using RedPajama;

namespace Gbnf;

public class ProgrammaticallyEnhanced
{
    public class User
    {
        public required string FirstName { get; init; }
        public required string LastName { get; init; }
        public required string Title { get; init; }
    }
    
    public static void ProgrammaticallyEnhancingTypeModels()
    {
        var typeModelBuilder = new TypeModelBuilder<User>();
        var typeModel = typeModelBuilder.Build();

        // Add descriptions
        typeModel = typeModel
            .WithDescription<User, string>(u => u.FirstName, "User's given name")
            .WithDescription<User, string>(u => u.LastName, "User's family name");

        // Add allowed values for a property
        typeModel = typeModel
            .WithAllowedValues<User, string>(u => u.Title, ["Mr", "Mrs", "Ms", "Dr", "Prof"]);

        // Generate GBNF with the enhanced type model
        var gbnfGenerator = new GbnfGenerator();
        var gbnf = gbnfGenerator.Generate(typeModel);

        var jsonGenerator = new JsonSampleGenerator();
        var jsonSample = jsonGenerator.Generate(typeModel);

        Console.WriteLine(gbnf);
        Console.WriteLine(jsonSample);
    }
}