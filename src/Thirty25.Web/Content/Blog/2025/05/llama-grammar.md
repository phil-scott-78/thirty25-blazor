---
title: "Exploring Llama.Grammar"
description: "A look at Llama.Grammar, its features, and how it compares to RedPajama for generating GBNF from C#."
series: "Intro to LlamaSharp"
date: May 21, 2025
tags:
  - llamasharp
repository: https://github.com/phil-scott-78/thirty25-blazor/tree/main/blog-projects/2025/GbnfGeneration/Gbnf
---

In our previous posts, we delved into [RedPajama](strong-typed-gbnf) and its [advanced features](redpajama-advanced-features) for generating GBNF grammars and JSON samples from C# classes. This approach provides strong typing and control over LLM outputs. However, the .NET ecosystem offers other tools that might be a better fit depending on your specific needs. Today, we'll explore one such alternative: [Llama.Grammar](https://github.com/jihadkhawaja/Llama.Grammar).

Llama.Grammar is a C# library designed to help you work with structured outputs from AI models by converting JSON Schemas or C# objects into GBNF grammars. This can be particularly useful when you need to ensure that an LLM's output conforms to a predefined structure.

## What is Llama.Grammar?

According to its documentation:

> **Llama.Grammar** is a C# class library that allows you to define JSON Schemas, C# objects dynamically and convert them to [GBNF (Grammar-Based Next-Token Format)](https://github.com/ggml-org/llama.cpp/blob/master/grammars/README.md) grammars. It is useful for working with structured outputs in AI models like LLaMA, Mistral, or GPT when paired with inference runtimes that support GBNF grammars.

With it going from C# to JSON Schema that will more closely align with what you'd see in other LLMs that do structured output.

## Key Features

Llama.Grammar offers several compelling features:

-   **Fluent builder API**: For creating JSON Schema objects directly in C#.
-   **JSON Schema to GBNF Conversion**: Translates JSON Schema definitions into GBNF grammars.
-   **C# Type to GBNF Conversion**: Directly generates GBNF from your existing C# classes.
-   **Complex Schema Support**: Handles nested objects, arrays (with min/max items), enums, constants, required fields, pattern matching, and nullable types.

## Usage Examples

Let's look at how you can use Llama.Grammar.

### 1. Generating GBNF from C# Types

Llama.Grammar can also generate GBNF directly from your C# classes. This closely aligns with the syntax from RedPajama.

Define your classes

```csharp:xmldocid
T:Gbnf.LlamaGrammar.TestPerson
T:Gbnf.LlamaGrammar.Address
```

And then get your GBNF
```csharp:xmldocid
M:Gbnf.LlamaGrammar.GetGrammar
```

This produces the GBNF of
```gbnf:xmldocid
M:Gbnf.LlamaGrammar.GetGrammar
```

Looking at the GBNF it should look pretty familiar. It is less opinionated and closely aligns the GBNF generated from other tools.

### 2. Generating GBNF using the Fluent API

You can define a JSON schema programmatically and then convert it to GBNF.

```csharp:xmldocid
M:Gbnf.LlamaGrammar.Schema
```

This gives us the GBNF
```gbnf:xmldocid
M:Gbnf.LlamaGrammar.Schema
```

This approach is useful when you want to dynamically construct your schema or if you prefer a fluent interface for schema definition. This syntax will also benefit if the project ever wants to go to a source generator.


## Pros of Llama.Grammar

-   **.NET 8 Support**: RedPajama targets only .NET 9, Llama.Grammar targets .NET 8,which is LTS. 
-   **Fluent JSON Schema Builder**: Offers a flexible and programmatic way to define schemas if you don't want to start from C# types or need dynamic schema generation.
-   **Direct C# Type Conversion**: Similar to RedPajama, it can generate GBNF from C# types, which is excellent for integrating with existing domain models.
-   **Based on Existing Work**: It ports and wraps the TypeScript logic from [json-schema-to-gbnf](https://github.com/adrienbrault/json-schema-to-gbnf), potentially benefiting from the maturity of that project in handling JSON schema features.
-   **Comprehensive Schema Support**: Its support for various JSON schema features like array constraints, enums, and pattern matching is robust.

## Cons and Considerations (Compared to RedPajama)

While Llama.Grammar is powerful, there are areas where RedPajama offers different functionalities or more specialized control:

-   **JSON Sample Generation**: RedPajama includes a `JsonSampleGenerator` to create placeholder JSON that can be used in prompts to guide the LLM. Llama.Grammar focuses solely on GBNF generation from schemas or types.
-   **Customizable Delimiters for Samples**: RedPajama's sample generator uses customizable delimiters (e.g., `⟨` and `⟩` instead of `<` and `>`) and modifies the GBNF to prevent the model from outputting these placeholder delimiters. This level of sample customization isn't a feature of Llama.Grammar.
-   **Fine-grained GBNF Control**:
    -   RedPajama allows for injecting raw GBNF snippets directly into property constraints (e.g., `[GbnfPattern("gbnf:<your custom rule>")]`). It's not clear if Llama.Grammar offers a similar mechanism for such low-level GBNF customization outside of standard JSON schema pattern properties.
    -   RedPajama provides built-in string formatting attributes like `[StringFormat(StringFormat.AlphaNumeric)]` and pattern templates (e.g., `(###) ###-####`). While Llama.Grammar supports "pattern matching" via JSON schema's `pattern` keyword, RedPajama's approach might be more direct for common cases via attributes.
-   **Configuration Object for Generation**: RedPajama uses settings objects (`GbnfGeneratorSettings`, `JsonSampleGeneratorSettings`) to control aspects like default string lengths and delimiter choices. Llama.Grammar's customization seems primarily driven by the JSON schema definition itself.

## Conclusion

Llama.Grammar is a valuable addition to the .NET ecosystem for anyone looking to enforce structured output from LLMs. Its strengths lie in its robust JSON Schema support, the fluent API for schema construction, and direct C# type-to-GBNF conversion.

If your primary goal is to convert JSON Schemas (perhaps defined elsewhere or built dynamically) or existing C# types to GBNF with good support for standard schema features, Llama.Grammar is an excellent choice and probably
the better choice long term to align with existing solutions out there.

