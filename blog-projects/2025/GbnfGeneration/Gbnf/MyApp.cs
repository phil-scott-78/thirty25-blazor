using RedPajama;

namespace Gbnf;

public class MyApp
{
    public static void GetSimpleGbnf()
    {
        var gbnfBuilder = new GbnfGenerator();
        var typeModelBuilder = new TypeModelBuilder<User>();
        
        var typeModel = typeModelBuilder.Build();
        var gbnf = gbnfBuilder.Generate(typeModel);
        
        Console.WriteLine(gbnf);
    }
    
    public static void GetSimpleJson()
    {
        var jsonBuilder = new JsonSampleGenerator();
        var typeModelBuilder = new TypeModelBuilder<User>();
        
        var typeModel = typeModelBuilder.Build();
        var json = jsonBuilder.Generate(typeModel);
        
        Console.WriteLine(json);
    }
}

public class User
{
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required int Age { get; init; }
    public required bool IsMember { get; init; }
}
