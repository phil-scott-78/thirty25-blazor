using RedPajama;

namespace Gbnf;

public class MyApp
{

    public static string GetSimpleGbnf()
    {
        // these settings enable or disable thinking tags for models
        // with reasoning, we'll disable them for now
        var gbnfGeneratorSettings = new GbnfGeneratorSettings
        {
            IncludeThinkingTags = false
        };
        
        var gbnfBuilder = new GbnfGenerator(gbnfGeneratorSettings);
        var typeModelBuilder = new TypeModelBuilder<User>();
        
        var typeModel = typeModelBuilder.Build();
        var gbnf = gbnfBuilder.Generate(typeModel);

        return gbnf;
    }


    public static string GetSimpleJson()
    {
        var jsonBuilder = new JsonSampleGenerator();
        var typeModelBuilder = new TypeModelBuilder<User>();

        var typeModel = typeModelBuilder.Build();
        var json = jsonBuilder.Generate(typeModel);

        return json;
    }
}

public class User
{
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required int Age { get; init; }
    public required bool IsMember { get; init; }
}
