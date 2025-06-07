using Llama.Grammar.Helper;
using Llama.Grammar.Service;

namespace Gbnf;

public class LlamaGrammar
{
    public class TestPerson
    {
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
        public List<string> Nicknames { get; set; } = new();
        public Address? HomeAddress { get; set; }
    }

    public class Address
    {
        public string Street { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string? ZipCode { get; set; }
    }

    public string GetGrammar()
    {
        var grammar = new GbnfGrammar();
        var gbnf = grammar.ConvertTypeToGbnf<TestPerson>();
        return gbnf;
    }

    public string Schema()
    {
        var schemaBuilder = new SchemaBuilder()
            .Type("object")
            .Properties(p => p
                .Add("name", s => s.Type("string"))
                .Add("age", s => s.Type("integer"))
                .Add("nicknames", s => s.Type("array")
                    .MinItems(1)
                    .MaxItems(3)
                    .Items(i => i.Type("string"))))
            .Required("name", "age");

        string json = schemaBuilder.ToJson();

        IGbnfGrammar grammar = new GbnfGrammar();
        return grammar.ConvertJsonSchemaToGbnf(json);
    }
}