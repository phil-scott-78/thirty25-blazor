---
title: "Structured Output with LlamaSharp: Reliable JSON Generation"
description: "Techniques for getting consistent structured data from Local LLMs"
series: "Intro to LlamaSharp"
date: April 10, 2025
isDraft: true
tags:
  - llamasharp
---

We've covered
the [basics of LlamaSharp](getting-started-with-llamasharp), [fine-tuned the sampling pipeline](llamasharp-sampling-pipeline),
and [optimized memory usage](llamasharp-memory-management) in our previous posts. Now let's tackle one of the most
practical challenges: getting your LLM to generate structured data in JSON format.

Why is this important? In real-world applications, you often want to extract specific data points from your model's
output to:

- Pass structured information to other parts of your system
- Store responses in databases with consistent schema
- Create APIs that return predictable JSON payloads
- Enable your app to make decisions based on the LLM's output

But if you've experimented with getting LLMs to output structured data, you've probably noticed it can be... unreliable.
Models love to wander off-format, add unnecessary explanations, or produce invalid JSON that breaks your parser. Let's
fix that!

## The JSON Generation Problem

The fundamental challenge with JSON generation from LLMs is that these models weren't specifically designed to output
structured data formats. They're trained on text, and while they've seen plenty of JSON during training, they don't have
built-in validators ensuring their output is syntactically correct.

After running hundreds of JSON generation experiments with various models, I've cataloged the most common failure modes.
Let's dive into the rogues' gallery of JSON generation problems:

### 1. The "Helpful" Prefixes and Suffixes

```
Here's the JSON data you requested:

{
  "name": "Jane Doe",
  "age": 32,
  "occupation": "Software Engineer"
}

I've included all the information from the text. Let me know if you need anything else!
```

This is perhaps the most common issue. The model wants to be helpful and conversational, but those extra explanations
outside the JSON block will break your parser. Even if you try to extract just the JSON part with regex, it's
error-prone when dealing with complex nested structures.

### 2. The Inline Commentator

```json
{
  "name": "Jane Doe",
  "age": 32,
  // Age extracted from profile
  "occupation": "Software Engineer",
  // Oops, I should also include education
  "education": "Computer Science",
  "skills": [
    "Python",
    "JavaScript",
    "C#"
  ]
  /* Main programming languages */
}
```

Many models love to add comments explaining their reasoning - perfectly natural in code but fatal for JSON parsing.
There's no valid comment syntax in JSON, despite what the model might think.

### 3. The Schema Explainer

```json
{
  "name": "Jane Doe",
  // The person's full name
  "age": 32,
  // Age in years (integer)
  "occupation": {
    "title": "Software Engineer",
    // Current job title
    "company": "Tech Corp",
    // Employer name
    "years": 5
    // Years in this position
  }
}
```

A variant of the commentator, but typically happens when you give complicated instructions. The model tries to explain
its schema choices inline, breaking the JSON.

### 4. The Trailing Comma Trap

```json
{
  "name": "Jane Doe",
  "age": 32,
  "skills": [
    "Python",
    "JavaScript",
    "C#",
  ]
}
```

That innocent-looking comma after the last item in the "skills" array will cause most JSON parsers to fail. In
programming languages like JavaScript, trailing commas are often allowed, but JSON is more strict.

### 5. The Malformed String Escaper

```json
{
  "name": "Jane Doe",
  "bio": "She said "programming is fun" and I agree",
  "query": "SELECT * FROM users WHERE name="Jane""
}
```

When generating strings containing quotes, models often forget to escape them properly. The correct version would be:

```json
{
  "name": "Jane Doe",
  "bio": "She said \"programming is fun\" and I agree",
  "query": "SELECT * FROM users WHERE name=\"Jane\""
}
```

### 6. The Inconsistent Quoter

```json
{
  "name": "Jane Doe",
  'age': 32,
  occupation: "Software Engineer"
}
```

Models sometimes mix different quoting styles or forget quotes on keys altogether. JSON strictly requires double quotes
for all keys and string values.

### 7. The Invalid Value Provider

```json
{
  "name": "Jane Doe",
  "age": thirty-two,
  "salary": $85,000,
  "isEmployed": Yes
}
```

Occasionally, models output non-JSON-compatible values. In JSON, strings need quotes, numbers can't have commas or
currency symbols, and boolean values must be lowercase `true` or `false`.

### 8. The Unclosed Structure

```json
{
  "name": "Jane Doe",
  "skills": 
  [
    "Python",
    "JavaScript",
    "C#"
  ,
  "projects":
  [
    {
      "title": "Website Redesign",
      "year": 2023
    },
    {
      "title": "API Integration",
      "year": 2022
    }
  ]
}
```

The missing closing bracket after the "skills" array makes this JSON invalid. These unclosed structures are particularly
common with deeply nested objects.

### 9. The Multi-line String Formatter

```json
{
  "name": "Jane Doe",
  "summary": "Experienced software engineer
  with a passion for clean code
  and scalable architecture.
  "
}
```

JSON doesn't support multi-line strings like this. The correct approach would use escape sequences (`\n`):

```json
{
  "name": "Jane Doe",
  "summary": "Experienced software engineer\nwith a passion for clean code\nand scalable architecture."
}
```

### 11. The Schema Guesser

```json
{
  "person": {
    "name": "Jane Doe",
    "age": 32,
    "occupation": "Software Engineer"
  }
}
```

When you ask for "information in JSON format" without specifying a schema, the model has to guess what structure you
want. Sometimes it wraps everything in a container object, other times it creates a flat structure, and occasionally it
invents fields you didn't ask for or omits ones you expected. The same input might produce:

```json
{
  "name": "Jane Doe",
  "age": 32,
  "occupation": "Software Engineer"
}
```

Or even:

```json
{
  "personalInfo": {
    "fullName": "Jane Doe",
    "ageInYears": 32
  },
  "professionalInfo": {
    "currentRole": "Software Engineer"
  }
}
```

This inconsistency makes parsing unpredictable, especially when you're building systems that need to reliably extract
specific fields. Without clear schema guidance, models will invent their own structure based on what they think is
most "natural" or "organized" for the data at hand.

## The Root Cause: LLMs Think Like Humans, Not Parsers

All these issues stem from the same root cause: LLMs were trained on human text where these conventions are common and
acceptable. They're trying to be helpful, explanatory, and clear—all great qualities for human communication but
problematic for machine parsing.

But there's hope! Let's look at three increasingly reliable techniques for getting clean JSON from LlamaSharp.

## Approach 1: Just Asking Nicely (The Naive Approach)

The simplest approach is to directly request JSON in your prompt. Let's see how this works:

```csharp
using LLama;
using LLama.Common;
using System.Text.Json;

// ... model loading code from previous posts ...

var executor = new StatelessExecutor(model, parameters) 
{
    ApplyTemplate = true
};

var jsonPrompt = """
Extract the following information as JSON:
Name: John Smith
Age: 42
Occupation: Data Scientist
Skills: Python, R, SQL, Machine Learning
Location: Boston, MA

Return ONLY valid JSON with no additional text before or after.
""";

string result = "";
await foreach (var chunk in executor.InferAsync(jsonPrompt))
{
    result += chunk;
}

// Try to parse the result
try 
{
    var parsed = JsonDocument.Parse(result);
    Console.WriteLine("Valid JSON generated!");
    
    // Pretty print the JSON
    var options = new JsonSerializerOptions { WriteIndented = true };
    string formatted = JsonSerializer.Serialize(parsed, options);
    Console.WriteLine(formatted);
}
catch (JsonException ex)
{
    Console.WriteLine($"Invalid JSON generated: {ex.Message}");
    Console.WriteLine(result);
}
```

This approach works surprisingly well with newer models like Gemma-3, but it's still prone to failure. The model might
generate comments, add explanations, or just make syntax errors.

### Success Rate Analysis

In my testing with Gemma-3 4B, this approach has roughly a 70-80% success rate for simple schemas. But it quickly
deteriorates with complex nested structures or when the model decides to be "helpful" by adding explanations.

Let's improve on this.

## Approach 2: Schema Guidance Through Examples

A more reliable approach is to provide a clear example of the exact JSON structure you expect. This gives the model a
template to follow:

```csharp
var schemaGuidedPrompt = """
Extract structured information from the text below into JSON. 
Follow this EXACT format:

{
  "name": "Example Name",
  "age": 30,
  "occupation": "Example Job",
  "skills": ["Skill 1", "Skill 2"],
  "location": {
    "city": "City Name",
    "state": "State"
  }
}

Text to extract from:
Name: Jane Doe
Age: 35
Occupation: Software Architect
Skills: .NET, Azure, Microservices, Kubernetes
Location: Seattle, Washington

Return ONLY valid JSON with no explanations or additional text.
""";
```

This approach works significantly better because the model now has:

1. An exact template showing the expected structure
2. Examples of how to convert the input data
3. A clear demonstration of proper JSON syntax

For even better results, we can add parsing hints:

```csharp
var enhancedPrompt = """
Extract structured information from the text below into valid JSON.

Important parsing rules:
- Return ONLY the JSON object with no explanations before or after
- Ensure all brackets {} and [] are properly closed and matched
- Use double quotes for keys and string values
- Do not include trailing commas
- Do not include comments

Expected format:
{
  "name": "Example Name",
  "age": 30,
  "occupation": "Example Job",
  "skills": ["Skill 1", "Skill 2"],
  "location": {
    "city": "City Name",
    "state": "State"
  }
}

Text to extract from:
Name: Jane Doe
Age: 35
Occupation: Software Architect
Skills: .NET, Azure, Microservices, Kubernetes
Location: Seattle, Washington
""";
```

### Building a JSON Helper Function

Let's package this approach into a reusable method:

```csharp
public static async Task<T?> ExtractJsonWithSchema<T>(
    StatelessExecutor executor, 
    string text, 
    T schemaExample) where T : class
{
    // Serialize the example to create a schema template
    var options = new JsonSerializerOptions { WriteIndented = true };
    string exampleJson = JsonSerializer.Serialize(schemaExample, options);
    
    var prompt = $"""
    Extract structured information from the text below into valid JSON.

    Important parsing rules:
    - Return ONLY the JSON object with no explanations before or after
    - Ensure all brackets {{}} and [] are properly closed and matched
    - Use double quotes for keys and string values
    - Do not include trailing commas
    - Do not include comments

    Expected format:
    {exampleJson}

    Text to extract from:
    {text}
    """;
    
    string result = "";
    await foreach (var chunk in executor.InferAsync(prompt))
    {
        result += chunk;
    }
    
    // Try to clean up the result (remove common issues)
    result = CleanJsonResult(result);
    
    try 
    {
        // Attempt to deserialize directly to the target type
        return JsonSerializer.Deserialize<T>(result);
    }
    catch (JsonException ex)
    {
        Console.WriteLine($"JSON parsing error: {ex.Message}");
        return null;
    }
}

private static string CleanJsonResult(string result)
{
    // Find the first { and last } to strip any surrounding text
    int firstBrace = result.IndexOf('{');
    int lastBrace = result.LastIndexOf('}');
    
    if (firstBrace >= 0 && lastBrace > firstBrace)
    {
        return result.Substring(firstBrace, lastBrace - firstBrace + 1);
    }
    
    return result;
}
```

With this helper, we can easily extract structured data:

```csharp
// Define our target class
public class Person
{
    public string Name { get; set; } = "";
    public int Age { get; set; }
    public string Occupation { get; set; } = "";
    public List<string> Skills { get; set; } = new();
    public Location Location { get; set; } = new();
}

public class Location
{
    public string City { get; set; } = "";
    public string State { get; set; } = "";
}

// Create a schema example
var example = new Person
{
    Name = "Example Name",
    Age = 30,
    Occupation = "Example Job",
    Skills = new List<string> { "Skill 1", "Skill 2" },
    Location = new Location
    {
        City = "City Name",
        State = "State"
    }
};

// Extract data
var text = """
Name: Jane Doe
Age: 35
Occupation: Software Architect
Skills: .NET, Azure, Microservices, Kubernetes
Location: Seattle, Washington
""";

var person = await ExtractJsonWithSchema(executor, text, example);

if (person != null)
{
    Console.WriteLine($"Extracted: {person.Name}, {person.Age}, {person.Occupation}");
    Console.WriteLine($"Skills: {string.Join(", ", person.Skills)}");
    Console.WriteLine($"Location: {person.Location.City}, {person.Location.State}");
}
```

### Success Rate Analysis

This technique raises the success rate to around 90-95% for most models. The schema guidance significantly improves the
model's ability to follow the expected format. However, it can still struggle with very complex nested structures.

But what if we need near-100% reliability? That's where our third approach comes in.

## Approach 3: JSON Grammar Constraints with JBNF

For maximum reliability, LlamaSharp supports a powerful feature inherited from llama.cpp: grammar-constrained generation
using JBNF (JSON Backus-Naur Form) specifications.

JBNF allows us to define a formal grammar that the model **must** follow during token generation. It's like putting
rails on the generation process, making it impossible for the model to generate invalid JSON.

Here's a basic JSON grammar written in JBNF:

```
root   ::= object
value  ::= object | array | string | number | ("true" | "false" | "null") ws

object ::=
  "{" ws (
            string ":" ws value
    ("," ws string ":" ws value)*
  )? "}" ws

array  ::=
  "[" ws (
            value
    ("," ws value)*
  )? "]" ws

string ::=
  "\"" (
    [^"\\\x7F\x00-\x1F] |
    "\\" (["\\bfnrt] | "u" [0-9a-fA-F]{4}) # escapes
  )* "\"" ws

number ::= ("-"? ([0-9] | [1-9] [0-9]{0,15})) ("." [0-9]+)? ([eE] [-+]? [0-9] [1-9]{0,15})? ws

# Optional space: by convention, applied in this grammar after literal chars when allowed
ws ::= | " " | "\n" [ \t]{0,20}
```

Now, let's use this grammar to constrain the model's output:

```csharp
// Define the JSON grammar
string jsonJbnf = @"
root   ::= object
value  ::= object | array | string | number | (""true"" | ""false"" | ""null"") ws

object ::=
  ""{"" ws (
            string "":"" ws value
    ("","" ws string "":"" ws value)*
  )? ""}"" ws

array  ::=
  ""["" ws (
            value
    ("","" ws value)*
  )? ""]"" ws

string ::=
  ""\"""" (
    [^""\\\x7F\x00-\x1F] |
    ""\\"" ([""\\bfnrt] | ""u"" [0-9a-fA-F]{4}) # escapes
  )* ""\"""" ws

number ::= (""-""? ([0-9] | [1-9] [0-9]{0,15})) (""."" [0-9]+)? ([eE] [-+]? [0-9] [1-9]{0,15})? ws

# Optional space: by convention, applied in this grammar after literal chars when allowed
ws ::= | "" "" | ""\n"" [ \t]{0,20}
";

// Parse the grammar into a llama.cpp compatible format
using LLama.Grammar;
var grammar = LLamaGrammar.FromJBNF(jsonJbnf);

var inferenceParams = new InferenceParams
{
    Grammar = grammar,
    MaxTokens = 1024,
};

var jsonPrompt = """
Extract the following information as JSON:
Name: John Smith
Age: 42
Occupation: Data Scientist
Skills: Python, R, SQL, Machine Learning
Location: Boston, MA
""";

string result = "";
await foreach (var chunk in executor.InferAsync(jsonPrompt, inferenceParams))
{
    result += chunk;
}

// This should always be valid JSON!
var parsed = JsonDocument.Parse(result);
```

### How Grammar Constraints Work Token by Token

To understand the power of grammar constraints, let's examine how the LLM generates JSON step by step when constrained
by our grammar. When you provide the JBNF grammar, here's exactly what happens during generation:

1. The grammar is parsed into a state machine that tracks valid transitions between states
2. The LLM begins generation with the initial state corresponding to the `root` rule

Let's see how this plays out token by token:

#### Step 1: Starting Generation

- Initial state: `root`
- Grammar rule: `root ::= object`
- Valid next tokens: Only those that can start an `object`, which is `{`
- Model prediction: The model may want to generate "Here's your JSON:" but the grammar only allows `{`
- Actual output: `{`

#### Step 2: After Opening Brace

- Current state: Inside `object`, after `{`
- Grammar rule: `object ::= "{" ws (...)`
- Valid next tokens: Either whitespace (via `ws`) or a string opening quote `"`
- Model prediction: The model might want to generate various tokens, but is constrained
- Actual output: `"` (starting a property name)

#### Step 3: Property Name

- Current state: Inside `string` in an `object`
- Grammar rule: `string ::= "\"" (...) "\"" ws`
- Valid next tokens: Any valid string character or closing quote
- Model prediction: The model predicts "name" as the property name
- Actual output: `name`

#### Step 4: Closing Quote and Colon

- Current state: After property name, inside `string`
- Grammar rule: `string ::= "\"" (...) "\"" ws`
- Valid next tokens: Only closing quote `"`
- Actual output: `"` followed by mandated `:` and possibly whitespace

This pattern continues for the entire generation process. At each step, the grammar constrains which tokens are valid,
effectively forcing the model to produce syntactically correct JSON.

For example, after closing an object with `}`, the grammar would only allow comma (if in an array or object) or
whitespace. It would be impossible for the model to suddenly inject text like "I hope this helps!" after the JSON.

This token-by-token enforcement ensures perfect JSON syntax every time. The model still has freedom within the
constraints - it can choose property names, values, and structure - but it cannot break the JSON format rules.

// Additional helper methods for collections, primitives, etc.

```

### Creating a Simple JSON Extraction Helper

Let's put it all together into a straightforward JSON extraction utility that uses our grammar constraint:

```csharp
public static async Task<T?> ExtractStructuredJson<T>(
    StatelessExecutor executor,
    string text,
    bool useGrammarConstraint = true) where T : class, new()
{
    var example = new T();
    var options = new JsonSerializerOptions { WriteIndented = true };
    
    // Create a sample with default values for better example clarity
    PopulateWithExampleValues(example);
    string exampleJson = JsonSerializer.Serialize(example, options);
    
    var prompt = $"""
    Extract structured information from the text below into valid JSON.

    Follow this EXACT format:
    {exampleJson}

    Text to extract from:
    {text}

    Return ONLY valid JSON with no additional text.
    """;
    
    InferenceParams? inferenceParams = null;
    
    if (useGrammarConstraint)
    {
        // Basic JSON grammar that ensures valid syntax
        string jsonJbnf = @"
        root   ::= object
        value  ::= object | array | string | number | (""true"" | ""false"" | ""null"") ws

        object ::=
          ""{"" ws (
                    string "":"" ws value
            ("","" ws string "":"" ws value)*
          )? ""}"" ws

        array  ::=
          ""["" ws (
                    value
            ("","" ws value)*
          )? ""]"" ws

        string ::=
          ""\"""" (
            [^""\\\x7F\x00-\x1F] |
            ""\\"" ([""\\bfnrt] | ""u"" [0-9a-fA-F]{4})
          )* ""\"""" ws

        number ::= (""-""? ([0-9] | [1-9] [0-9]{0,15})) (""."" [0-9]+)? ([eE] [-+]? [0-9] [1-9]{0,15})? ws

        ws ::= | "" "" | ""\n"" [ \t]{0,20}
        ";
        
        var grammar = LLamaGrammar.FromJBNF(jsonJbnf);
        inferenceParams = new InferenceParams
        {
            Grammar = grammar,
            MaxTokens = 2048,
        };
    }
    
    string result = "";
    await foreach (var chunk in executor.InferAsync(prompt, inferenceParams))
    {
        result += chunk;
    }
    
    try 
    {
        // Attempt to deserialize to the target type
        return JsonSerializer.Deserialize<T>(result);
    }
    catch (JsonException ex)
    {
        Console.WriteLine($"JSON parsing error: {ex.Message}");
        Console.WriteLine(result);
        return null;
    }
}

// Helper to populate example values
private static void PopulateWithExampleValues(object obj)
{
    foreach (var prop in obj.GetType().GetProperties())
    {
        if (prop.PropertyType == typeof(string))
        {
            prop.SetValue(obj, $"Example {prop.Name}");
        }
        else if (prop.PropertyType == typeof(int))
        {
            prop.SetValue(obj, 42);
        }
        else if (prop.PropertyType == typeof(bool))
        {
            prop.SetValue(obj, true);
        }
        else if (prop.PropertyType.IsClass && prop.PropertyType != typeof(string))
        {
            if (typeof(IList).IsAssignableFrom(prop.PropertyType))
            {
                // Handle lists by adding example items
                Type itemType = prop.PropertyType.GetGenericArguments()[0];
                var listType = typeof(List<>).MakeGenericType(itemType);
                var list = Activator.CreateInstance(listType);
                
                // Create and add sample items
                if (itemType == typeof(string))
                {
                    ((IList)list).Add($"Example {prop.Name} Item 1");
                    ((IList)list).Add($"Example {prop.Name} Item 2");
                }
                else
                {
                    // Add a simple example item for other types
                    var item = Activator.CreateInstance(itemType);
                    if (item != null)
                    {
                        PopulateWithExampleValues(item);
                        ((IList)list).Add(item);
                    }
                }
                
                prop.SetValue(obj, list);
            }
            else
            {
                // Handle nested objects
                var nestedObj = Activator.CreateInstance(prop.PropertyType);
                if (nestedObj != null)
                {
                    PopulateWithExampleValues(nestedObj);
                    prop.SetValue(obj, nestedObj);
                }
            }
        }
    }
}
```

### Success Rate Analysis

With grammar constraints, the success rate jumps to essentially 100% for JSON syntax. The model literally cannot
generate invalid JSON. However, there are a few trade-offs:

1. Grammar constraints can sometimes make the model more "rigid" in its responses
2. Very complex nested structures might confuse the model, even with constraints
3. The model might struggle with semantic accuracy while maintaining perfect syntax

## Performance and Practical Considerations

When implementing JSON generation in your applications, consider:

1. **Memory Overhead**: Grammar parsing and constraint enforcement does add some processing overhead
2. **Latency**: Grammar-constrained generation might be slightly slower
3. **Model Size**: Larger models (7B+) tend to understand JSON structure better than smaller ones
4. **Post-processing**: Even with grammar constraints, you might want to validate field types and values

## Real-world Example: Building a Structured Data Extraction API

Let's put everything together into a practical example - a simple API that extracts structured information from text:

```csharp
public class ExtractorService
{
    private readonly StatelessExecutor _executor;
    private readonly LLamaGrammar _jsonGrammar;
    
    public ExtractorService(StatelessExecutor executor)
    {
        _executor = executor;
        
        // Initialize the JSON grammar once for reuse
        string jsonJbnf = @"
        root   ::= object
        value  ::= object | array | string | number | (""true"" | ""false"" | ""null"") ws

        object ::=
          ""{"" ws (
                    string "":"" ws value
            ("","" ws string "":"" ws value)*
          )? ""}"" ws

        array  ::=
          ""["" ws (
                    value
            ("","" ws value)*
          )? ""]"" ws

        string ::=
          ""\"""" (
            [^""\\\x7F\x00-\x1F] |
            ""\\"" ([""\\bfnrt] | ""u"" [0-9a-fA-F]{4})
          )* ""\"""" ws

        number ::= (""-""? ([0-9] | [1-9] [0-9]{0,15})) (""."" [0-9]+)? ([eE] [-+]? [0-9] [1-9]{0,15})? ws

        ws ::= | "" "" | ""\n"" [ \t]{0,20}
        ";
        
        _jsonGrammar = LLamaGrammar.FromJBNF(jsonJbnf);
    }
    
    public async Task<Person?> ExtractPersonInfo(string text)
    {
        return await ExtractStructuredJson<Person>(_executor, text, _jsonGrammar);
    }
    
    public async Task<Product?> ExtractProductInfo(string text)
    {
        return await ExtractStructuredJson<Product>(_executor, text, _jsonGrammar);
    }
    
    public async Task<Event?> ExtractEventInfo(string text)
    {
        return await ExtractStructuredJson<Event>(_executor, text, _jsonGrammar);
    }
    
    // Generic extraction with custom example schema
    public async Task<JsonDocument?> ExtractWithCustomExample(string text, string jsonExample)
    {
        // Create a prompt with the custom example
        var prompt = $"""
        Extract structured information from the text below into valid JSON.
        
        Follow this EXACT format:
        {jsonExample}
        
        Text to extract from:
        {text}
        
        Return ONLY valid JSON with no additional text.
        """;
        
        // Use the pre-initialized grammar for guaranteed JSON validity
        var inferenceParams = new InferenceParams
        {
            Grammar = _jsonGrammar,
            MaxTokens = 2048,
        };
        
        string result = "";
        await foreach (var chunk in _executor.InferAsync(prompt, inferenceParams))
        {
            result += chunk;
        }
        
        try 
        {
            return JsonDocument.Parse(result);
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"JSON parsing error: {ex.Message}");
            return null;
        }
    }
}
```

Usage:

```csharp
// Set up the extractor service
var parameters = new ModelParams(@"b:\models\google_gemma-3-4b-it-Q6_K.gguf")
{
    ContextSize = 4096,
    GpuLayerCount = -1,
    BatchSize = 128
};

using var model = await LLamaWeights.LoadFromFileAsync(parameters);
var executor = new StatelessExecutor(model, parameters) { ApplyTemplate = true };
var extractor = new ExtractorService(executor);

// Extract structured information
var productText = """
The Ultimate Coffee Maker 5000
Price: $299.99
Description: Our premium coffee maker features 15 bar pressure, integrated grinder, and smart temperature control.
Available in: Black, Silver, Red
Dimensions: 12" x 8" x 15"
Weight: 10.5 lbs
Energy Rating: A+++
""";

var product = await extractor.ExtractProductInfo(productText);

if (product != null)
{
    Console.WriteLine($"Extracted product: {product.Name}");
    Console.WriteLine($"Price: {product.Price:C}");
    Console.WriteLine($"Description: {product.Description}");
    Console.WriteLine($"Colors: {string.Join(", ", product.AvailableColors)}");
    Console.WriteLine($"Dimensions: {product.Dimensions.Width}\" x {product.Dimensions.Height}\" x {product.Dimensions.Depth}\"");
}
```

## Conclusion

Getting reliable structured data from LLMs is a matter of applying the right constraints. We've explored three
approaches with increasing reliability:

1. **Simple prompting**: Works ~70-80% of the time, good for quick prototyping
2. **Schema-guided generation**: Works ~90-95% of the time, good for most production use cases
3. **Grammar-constrained generation**: Works ~100% of the time for syntactic correctness, ideal for critical
   applications

For most applications, the schema-guided approach provides a good balance of reliability and flexibility. But when you
absolutely need guaranteed valid JSON, grammar constraints are your best friend.

LlamaSharp's integration with llama.cpp makes it easy to implement all these approaches in your .NET applications. Happy
JSON hunting!

## What's Next?

In our next post, we'll explore fine-tuning techniques for domain-specific applications, showing how to adapt
open-source models to your specific needs through Parameter-Efficient Fine-Tuning (PEFT) methods like LoRA.