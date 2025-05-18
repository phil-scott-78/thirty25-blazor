using RedPajama;

namespace Gbnf;

public class CustomSettingsExample
{
    public void CustomSettings()
    {
        var gbnfSettings = new GbnfGeneratorSettings
        {
            DefaultMinLength = 2,
            DefaultMaxLength = 1000,
            OpeningDelimiter = '<',  // Change from default ⟨
            ClosingDelimiter = '>'   // Change from default ⟩
        };

// Custom JSON sample generator settings
        var jsonSettings = new JsonSampleGeneratorSettings
        {
            OpeningDelimiter = '<',
            ClosingDelimiter = '>'
        };

        var gbnfGenerator = new GbnfGenerator(gbnfSettings);
        var jsonGenerator = new JsonSampleGenerator(jsonSettings);

        var typeModel = new TypeModelBuilder<User>().Build();
        var gbnf = gbnfGenerator.Generate(typeModel);
        var json = jsonGenerator.Generate(typeModel);
    }
}