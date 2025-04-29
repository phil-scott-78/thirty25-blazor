---
title: "JSON Generation with LlamaSharp"
description: "Techniques for getting consistent structured data from Local Language Models"
series: "Intro to LlamaSharp"
date: April 10, 2025
tags:
  - llamasharp
---

We've covered
the [basics of LlamaSharp](getting-started-with-llamasharp), [fine-tuned the sampling pipeline](llamasharp-sampling-pipeline),
and [optimized memory usage](llamasharp-memory-management) in our previous posts. Now let's tackle one of the most
practical challenges: getting your models to generate structured data in JSON format.

Why is this important? In real-world applications, you often want to extract specific data points from your model's
output to:

- Pass structured information to other parts of your system
- Store responses in databases with consistent schema
- Create APIs that return predictable JSON payloads
- Enable your app to make decisions based on the model's output

But if you've experimented with getting models to output structured data, you've probably noticed it can be...
unreliable.
Models love to wander off-format, add unnecessary explanations, or produce invalid JSON that breaks your parser. Let's
fix that!

## The JSON Generation Problem

The fundamental challenge with JSON generation from models is that these models weren't specifically designed to output
structured data formats. They're trained on text, and while they've seen plenty of JSON during training, they don't have
built-in validators ensuring their output is syntactically correct.

Here are some of the top types of issues you'll see when asking for JSON from a model:

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

```
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

```
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

```
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

```
{
  "name": "Jane Doe",
  "bio": "She said "programming is fun" and I agree",
  "query": "SELECT * FROM users WHERE name="Jane""
}
```

When generating strings containing quotes, models often forget to escape them properly. The correct version would be:

```
{
  "name": "Jane Doe",
  "bio": "She said \"programming is fun\" and I agree",
  "query": "SELECT * FROM users WHERE name=\"Jane\""
}
```

### 6. The Inconsistent Quoter

```
{
  "name": "Jane Doe",
  'age': 32,
  occupation: "Software Engineer"
}
```

Models sometimes mix different quoting styles or forget quotes on keys altogether. JSON strictly requires double quotes
for all keys and string values.

### 7. The Invalid Value Provider

```
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

```
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

```
{
  "name": "Jane Doe",
  "summary": "Experienced software engineer
              with a passion for clean code
              and scalable architecture."
}
```

JSON doesn't support multi-line strings like this. The correct approach would use escape sequences (`\n`):

```json
{
  "name": "Jane Doe",
  "summary": "Experienced software engineer\nwith a passion for clean code\nand scalable architecture."
}
```

### 10. The Schema Guesser

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

## The Root Cause: Models Think Like Humans, Not Parsers

All these issues stem from the same root cause: models were trained on human text where these conventions are common and
acceptable. They're trying to be helpful, explanatory, and clear—all great qualities for human communication but
problematic for machine parsing.

But there's hope! Let's look at three increasingly reliable techniques for getting clean JSON from LlamaSharp.

## Approach 1: Asking Nicely

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

This approach works surprisingly well generating JSON with newer models like Gemma-3, but it's still prone to failure.
The biggest problem is it doesn't know what JSON schema to use, so it just makes a best guess. Then the model might
generate comments, add explanations, or just make syntax errors.

Let's improve on this.

## Approach 2: Guidance Through Examples

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
- Ensure all brackets { } and [] are properly closed and matched
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

### Success Rate Analysis

This technique raises the success rate to around 90-95% for most models. The schema guidance significantly improves the
model's ability to follow the expected format. However, it can still struggle with very complex nested structures.

But what if we need near-100% reliability? That's where our third approach comes in.

## Approach 3: Grammar Constraints with GBNF

For maximum reliability, LlamaSharp supports a powerful feature inherited from llama.cpp: grammar-constrained generation
using GBNF (Guided Backus–Naur Form) specifications.

GBNF allows us to define a formal grammar that the model **must** follow during token generation. It's like putting
rails on the generation process, making it impossible for the model to generate invalid JSON.

Here's a basic [JSON grammar](https://github.com/ggml-org/llama.cpp/blob/master/grammars/json.gbnf) written in GBNF from
the llama.cpp project:

```gbnf
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

Now, let's use this grammar to constrain the model's output. You do this using the `Grammar` property on the
`SamplingPileline`, ensuring you match the `root` to what is defined in your gbnf:

```csharp
// Define the JSON grammar
string gbnf = File.ReadAllText("json.gbnf");

var inferenceParams = new InferenceParams
{
    var grammar = new Grammar(gbnf, "root");
    SamplingPipeline = new DefaultSamplingPipeline 
    {
        Grammar = grammar, 
    }
    
};

var jsonPrompt = """
Extract the following information as JSON:
Name: John Smith
Age: 42
Occupation: Data Scientist
Skills: Python, R, SQL, Machine Learning
Location: Boston, MA
""";

var result = new StringBuilder();
await foreach (var chunk in executor.InferAsync(jsonPrompt, inferenceParams))
{
    result.Append(chunk);
}

// This should always be valid JSON
var parsed = JsonDocument.Parse(json);
```

### How Grammar Constraints Work Token by Token

To understand the power of grammar constraints, let's examine how the models generates JSON step by step when
constrained
by our grammar. When you provide the GBNF grammar, here's exactly what happens during generation:

1. The grammar is parsed into a state machine that tracks valid transitions between states
2. The model begins generation with the initial state corresponding to the `root` rule

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

### What Happens If the Model Predicts an Invalid Token?

This is where grammar enforcement becomes especially powerful. At each step, the model generates a probability
distribution (logits) over the entire vocabulary. However, before sampling or selecting the next token, this
distribution is filtered based on the current valid grammar transitions.

If the token the model wants to produce isn't allowed by the grammar at that point, it is masked out by having its
probability
is set to zero. The model is then forced to pick a valid token from the remaining options. In effect, the grammar acts
like a strict gatekeeper, guiding the model token-by-token and preventing invalid sequences from ever being output.

The model isn't "aware" it's being blocked; it simply sees a narrowed set of choices. Over time, it learns to work
within these constraints, adapting its internal predictions to produce sequences that both follow the grammar and remain
semantically coherent.

For example, after closing an object with }, the grammar would only allow a comma (if in an array or object), a closing
brace, or whitespace. It would be impossible for the model to suddenly inject text like "I hope this helps!" after the
JSON.

This token-by-token enforcement ensures perfect JSON syntax every time. The model still has freedom within the
constraints—it can choose property names, values, and structure—but it cannot break the JSON format rules.

## Performance and Practical Considerations

When implementing JSON generation in your applications, consider:

1. **Memory Overhead**: Grammar parsing and constraint enforcement does add some processing overhead
2. **Latency**: Grammar-constrained generation might be slightly slower
3. **Model Size**: Larger models (7B+) tend to understand JSON structure better than smaller ones
4. **Post-processing**: Even with grammar constraints, you might want to validate field types and values
5. **Schema Validation**: Even with a GBNF and an example schema, the JSON generated might be valid JSON but with no
   guarantees about the schema.

## Conclusion

Getting reliable structured data from model is a matter of applying the right constraints. We've explored three
approaches with increasing reliability:

1. **Simple prompting**: Works ~70-80% of the time, good for quick prototyping
2. **Schema-guided generation**: Works ~90-95% of the time, good for most production use cases
3. **Grammar-constrained generation**: Works ~100% of the time for syntactic correctness, ideal for critical
   applications

For most applications, the schema-guided approach provides a good balance of reliability and flexibility. But with C#
and JsonSerialization, we need precision. The grammar options of llama.cpp and LlamaSharp gives us this ability.

## What's Next?

In our next post, we'll take this grammar approach and built out some grammar helpers automatically and really start
to force these models to color within the lines.