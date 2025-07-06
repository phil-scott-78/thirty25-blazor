---
title: "Advanced RedPajama Features"
description: "Taking control of model output with advanced type modeling"
series: "Intro to LlamaSharp"
date: April 24, 2025
tags:
  - llamasharp
  - redpajama
repository: https://github.com/phil-scott-78/thirty25-blazor/tree/main/blog-projects/2025/GbnfGeneration/Gbnf
---

In our [previous post](strong-typed-gbnf), we introduced the basic concepts of RedPajama, showing how to generate GBNF
grammars and JSON samples from your4 C# classes. This approach lets you constrain LLM output to valid, properly typed
data structures that can be directly deserialized into your domain objects.

The more advanced features of RedPajama give you even finer control over your model outputs.

## Customizing Generation Settings

Both the GBNF and JSON sample generators accept settings objects that allow you to customize their behavior:

```csharp:xmldocid,bodyonly
M:Gbnf.CustomSettingsExample.CustomSettings
```

These settings allow you to control the appearance and behavior of the generated artifacts to match your specific needs
and preferences.

### Default Max and Min Length

RedPajama has some default settings that might not be obvious. The first is the automatic max length set to 512. Due to the
nature of how these models process text, it is wise to not let them just generate text without a limit. This is 
especially true for models with lower parameter count or low repeat settings. They can easily get caught in a cycle where
they might accidentally keep generating text forever. A hard rule of only allowing 512 characters simply keeps things
sensible. 

### Default Delimiter

The second setting is the default delimiter of `⟨` and `⟩`. This is a bit of trickery. Remember, we are giving the models
a sample JSON. For example, we might have this:

```
Extract the first and last name of the subject from this sentence: 

===
On October 13th, Mr. Johnson awoke. His wife took one look at him and said, 
"Ben, you look terrible."
===

Return the result in JSON in this format
{
  "FirstName": "<string value>",
  "LastName": "<string value>"
}
```

This, at a glance, isn't a big deal. However, with some models, especially the smaller ones, might get confused and start
returning the sample phrase! It will ignore Ben Johnson and give us a user response filled with the placeholder values.
But we can trick it with two phases:

1. We don't use `<` and `>`, but rather `⟨` and `⟩`.
2. We then block `⟨` and `⟩` from being used in the output via our GBNF

If you remember from the last chapter, our allowed string values were rather complex

```gbnf
char ::= [^"\\\x7F\x00-\x1F\u27E8\u27E9] | [\\] (["\\bfnrt] | "u" [0-9a-fA-F]{4})
```

If you look close, we are not allowing `\u27E8` or `\u27E9` at all within a string. That's `⟨` and `⟩` escaped for the 
GBNF. They are almost certain not to appear in the output naturally. With them not being allowed, the model when trying 
to use the template text fails and has to try again, still allowing `<` and `>` to be in our strings.

Additionally, `JsonSampleGenerator` has an additional helper that returns a string to be used with directions to replace 
the placeholder text.

```csharp
public string SampleInstructions()
{
    return
        $"Replace all placeholders, ⟨...⟩, in the format with the actual values extracted from the text. Do not return placeholders in the final output.";
}
```
These placeholders are customizable. Here are a few others. The key is to make sure they are unlikely to appear in your 
output because we are going to never allow them.

| Start Delimiter | End Delimiter | Notes                                                             |
|-----------------|---------------|-------------------------------------------------------------------|
| `⟨` (U+27E8)    | `⟩` (U+27E9)  | "Mathematical Left/Right Angle Bracket (default)"                 |
| `⧼` (U+29FC)    | `⧽` (U+29FD)  | "Left/Right Double Angle Bracket with Dot"; rare, readable        |
| `⟦` (U+27E6)    | `⟧` (U+27E7)  | "Mathematical Left/Right White Square Bracket"                    |
| `‹` (U+2039)    | `›` (U+203A)  | "Single Left/Right Pointing Angle Quotation Mark"                 |
| `❬` (U+276C)    | `❭` (U+276D)  | Ornamental angle brackets                                         |
| `⁅` (U+2045)    | `⁆` (U+2046)  | "Left/Right Square Bracket with Quill" — very rare                |
| `〈` (U+2329)    | `〉` (U+232A)  | "Left/Right-Pointing Angle Bracket"                               |
| `《` (U+300A)    | `》` (U+300B)  | East Asian double brackets — visible and uncommon in English text |

## Adding Descriptions to Properties

One of the simplest enhancements is adding descriptions to properties. These descriptions don't affect the GBNF grammar
but appear as comments in the generated JSON sample, providing valuable context to guide the model.

```csharp:xmldocid tabs=true
T:Gbnf.AdvancedScenarios.Product
```
```json:xmldocid data="Gbnf.AdvancedScenarios.Product-json"
M:Gbnf.AdvancedScenarios.ScenarioRunner.ProcessAllTypesInNamespace
```
```gbnf:xmldocid data="Gbnf.AdvancedScenarios.Product-gbnf"
M:Gbnf.AdvancedScenarios.ScenarioRunner.ProcessAllTypesInNamespace
```

The descriptions help guide the model about what each field represents without modifying the grammar constraints. This
is especially useful when field names might be ambiguous (like "Name" which could be many things).

## Constraining String Length

As we saw in the settings, we default to a minimum length of 0 characters and a maximum of 512 per field. The defaults
are set to keep the models from generating too much text in case they get caught in a repetition loop and running 
forever.

But often you'll also want to limit the length of string fields, particularly for values like usernames, postal codes, 
or other standardized data.

```csharp:xmldocid tabs=true
T:Gbnf.AdvancedScenarios.User
```
```json:xmldocid data="Gbnf.AdvancedScenarios.User-json"
M:Gbnf.AdvancedScenarios.ScenarioRunner.ProcessAllTypesInNamespace
```
```gbnf:xmldocid data="Gbnf.AdvancedScenarios.User-gbnf"
M:Gbnf.AdvancedScenarios.ScenarioRunner.ProcessAllTypesInNamespace
```

This is particularly powerful for enforcing data validation rules directly at the generation level. If your zip code
must be exactly five characters, the model physically cannot generate anything else.

## Working with Arrays

RedPajama handles array properties gracefully, ensuring both the array structure and its elements follow the correct
format:

```csharp:xmldocid tabs=true
T:Gbnf.AdvancedScenarios.ShoppingCart
T:Gbnf.AdvancedScenarios.CartItem
```
```json:xmldocid data="Gbnf.AdvancedScenarios.ShoppingCart-json"
M:Gbnf.AdvancedScenarios.ScenarioRunner.ProcessAllTypesInNamespace
```
```gbnf:xmldocid data="Gbnf.AdvancedScenarios.ShoppingCart-gbnf"
M:Gbnf.AdvancedScenarios.ScenarioRunner.ProcessAllTypesInNamespace
```

Notice how the JSON sample includes three example items to make it clear to the model that multiple entries should be
generated. The grammar allows for zero or more items, so the LLM can generate as many as appropriate based on the input
context.

## String Formatting (Patterns)

For strings that should follow specific patterns, RedPajama provides several formatting options:

```csharp:xmldocid tabs=true
T:Gbnf.AdvancedScenarios.Contact
```
```json:xmldocid data="Gbnf.AdvancedScenarios.Contact-json"
M:Gbnf.AdvancedScenarios.ScenarioRunner.ProcessAllTypesInNamespace
```
```gbnf:xmldocid data="Gbnf.AdvancedScenarios.Contact-gbnf"
M:Gbnf.AdvancedScenarios.ScenarioRunner.ProcessAllTypesInNamespace
```

RedPajama supports several built-in formats:

- `alpha`: Alphabetic characters only (a-z, A-Z)
- `alpha-space`: Alphabetic characters and spaces
- `alphanumeric`: Letters and numbers only
- `lowercase`: Only lowercase letters
- `uppercase`: Only uppercase letters
- `numeric`: Digits only
- `hex`: Hexadecimal characters (0-9, a-f, A-F)
- Pattern templates like `(###) ###-####` where `#` represents a digit
- Patterns with `A` for uppercase letters and `a` for lowercase letters

## Custom GBNF Patterns

For absolute control, RedPajama allows you to specify custom GBNF patterns directly:

```csharp:xmldocid tabs=true
T:Gbnf.AdvancedScenarios.Document
```
```json:xmldocid data="Gbnf.AdvancedScenarios.Document-json"
M:Gbnf.AdvancedScenarios.ScenarioRunner.ProcessAllTypesInNamespace
```
```gbnf:xmldocid data="Gbnf.AdvancedScenarios.Document-gbnf"
M:Gbnf.AdvancedScenarios.ScenarioRunner.ProcessAllTypesInNamespace
```

The `gbnf:` prefix allows you to inject raw GBNF patterns for complete control over string validation. This is
particularly useful for highly specialized formats that aren't covered by the built-in patterns. Since we can't guess
the format, you'll want to use `[Description]` to help direct the model in its generation.

## Defining Allowed Values

For properties that should only accept specific values, RedPajama supports constraining to a predefined set:

```csharp:xmldocid tabs=true
T:Gbnf.AdvancedScenarios.Order
```

```json:xmldocid data="Gbnf.AdvancedScenarios.Order-json"
M:Gbnf.AdvancedScenarios.ScenarioRunner.ProcessAllTypesInNamespace
```
```gbnf:xmldocid data="Gbnf.AdvancedScenarios.Order-gbnf"
M:Gbnf.AdvancedScenarios.ScenarioRunner.ProcessAllTypesInNamespace
```

This approach is more flexible than C# enums because:

1. It works with string properties (no need to convert to and from enum types, although enums are supported)
2. The allowed values can be completely dynamic (can be different for each property)
3. You can add new allowed values without changing your C# enum definition


## Programmatically Enhancing Type Models

In addition to using attributes, RedPajama allows you to enhance your type models programmatically via `WithDescription`
and `WithAllowedValues`:

```csharp:xmldocid tabs=true
M:Gbnf.ProgrammaticallyEnhanced.ProgrammaticallyEnhancingTypeModels
```
```json:xmldocid data="json"
M:Gbnf.ProgrammaticallyEnhanced.ProgrammaticallyEnhancingTypeModels
```
```gbnf:xmldocid data="gbnf"
M:Gbnf.ProgrammaticallyEnhanced.ProgrammaticallyEnhancingTypeModels
```

This allows for scenarios where the allowed values or description might not be known until runtime.
With this pattern, you can run the more expensive `TypeModelBuilder` once per type and adjust values per call as needed.


## Conclusion

With these tools in hand, we can start forcing even the smallest models to produce data in formats we can trust. In the
next post, we'll put it all together to build a complete example using some reusable patterns. 