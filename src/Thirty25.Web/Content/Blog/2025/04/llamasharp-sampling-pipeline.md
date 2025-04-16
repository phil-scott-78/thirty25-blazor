---
title: "Mastering LlamaSharp's Sampling Pipeline"
description: "Fine-tuning Text Generation in Your .NET Applications"
series: "Intro to LlamaSharp"
date: April 3, 2025
tags:
  - llamasharp
---

In our [previous post](getting-started-with-llamasharp), we covered the basics of getting LlamaSharp up and running with
a local model in your .NET applications. We set up the environment, downloaded a model, and ran our first inference.. But if you've been experimenting with your implementation, you might have noticed that the quality of responses
can vary significantly.

That's where the `DefaultSamplingPipeline` comes in - it's essentially the control panel for how your model generates
text. Think of it as the difference between a basic camera in auto mode and a professional DSLR with manual settings.
Let's dive into how to fine-tune these settings to get exactly the output you want.

## Understanding Token Generation in Language Models

Before we get into the specific parameters, it's important to understand how models generate text. At each step, the
model:

1. Predicts a probability distribution across its entire vocabulary (tens of thousands of tokens)
2. Applies various sampling techniques to select the next token
3. Adds that token to the generated text
4. Repeats until a stopping condition is met

Without proper sampling parameters, you might end up with:

- Bland, generic responses (when sampling is too conservative)
- Wild, incoherent text (when sampling is too exploratory)
- Repetitive loops (when the model gets stuck in a pattern)
- Premature cutoffs (when the model generates stop sequences too early)

Let's see how to configure each parameter for optimal results.

## The DefaultSamplingPipeline in LlamaSharp

LlamaSharp packages sampling parameters inside the `InferenceParams` class — these are set per query and can be changed
between calls without reloading the model. (This is in contrast to model loading parameters, which are fixed once a model
is loaded and are much slower to change.)

Now, let’s walk through each parameter in the sampling pipeline and what it controls.

## Seed - Keep Things Consistent

Seed is the same as the seed for any other random generation. Without it being set, it'll default to the system random
number generator. For our purposes in playing with the settings, it's nice to have it set to a fixed value so we can
compare easier.

## Temperature: Controlling Creativity vs. Predictability

Temperature is perhaps the most intuitive parameter to understand. It controls the "randomness" or "creativity" of the
generated text. Lower = more predictable, higher = more diverse.


```csharp
var inferenceParams = new InferenceParams
{ 
    SamplingPipeline = new DefaultSamplingPipeline()
    {
        Temperature =  0.7f // A balanced setting
    }, 
};
```

- **Range**: Typically 0.0 to 1.5 (though technically unbounded)
- **Low values (0.1-0.4)**: More deterministic, predictable, and "safe" outputs. Great for factual Q&A, code generation,
  or any task where precision matters.
- **Medium values (0.5-0.8)**: A good balance between creativity and coherence. Works well for most conversational use
  cases.
- **High values (0.9-1.2)**: More creative, diverse, and sometimes surprising outputs. Better for creative writing,
  brainstorming, or generating varied ideas.
- **Very high values (>1.2)**: Can produce increasingly random and potentially incoherent text.

Temperature works by essentially adjusting how "confident" the model is about its predictions. Think of it like a focus
knob: at low temperatures, the model becomes laser-focused on its top choice, while at higher temperatures, it spreads
its attention more evenly across many possible words. This is why higher temperatures produce more varied and surprising
text, while lower temperatures stick to the "safe" predictions.

### Code Example: Temperature Comparison

```csharp
float[] temperatures = { 0.2f, 0.7f, 1.2f };
string prompt = "Generate a one-paragraph description of clouds on a summer day";

foreach (var temp in temperatures)
{
    Console.WriteLine($"\n--- Temperature: {temp} ---\n");
    
    var inferenceParams = new InferenceParams
    { 
        SamplingPipeline = new DefaultSamplingPipeline()
        {
            Temperature = temp
        }, 
    };
    
    await foreach (var result in executor.InferAsync(prompt, inferenceParams))
    {
        Console.Write(result);
    }
}
```

Results in output similar to this:

**Temperature: 0.2**

> On a summer day, clouds drift lazily across the sky, casting dappled shadows and promising a warm, breezy afternoon.

**Temperature: 0.7**

> On a summer day, clouds drift lazily across the sky, casting dappled shadows and promising a warm, breezy afternoon.

**Temperature: 1.2**

> On a summer day, clouds drift lazily across the sky, casting dappled shadows and hinting at the promise of a
> refreshing breeze.


As you increase the temperature, the outputs become more varied — sometimes more expressive, but potentially less precise.
Maybe fine if we were writing
young adult literature. But if we were getting technical, it might not be the result we want. Take the output for this
technical prompt instead:

```csharp

await CompareTemperatures(executor, 
    """
    Summarize this method:

    float[] temperatures = { 0.2f, 0.7f, 1.2f };
    string prompt = "Generate a one-sentence description of clouds on a summer day";

    ```csharp
    foreach (var temp in temperatures)
    {
        Console.WriteLine($"\n--- Temperature: {temp} ---\n");
        
        var inferenceParams = new InferenceParams
        { 
            SamplingPipeline = new DefaultSamplingPipeline()
            {
                Temperature = temp
            }, 
        };
        
        await foreach (var result in executor.InferAsync(prompt, inferenceParams))
        {
            Console.Write(result);
        }
    }
    ```
    """);
```

**Temperature: 0.2**

> This C# code iterates through a list of temperatures (`temperatures`). For each temperature, it constructs an
> `InferenceParams` object, setting the `Temperature` property of a
> `DefaultSamplingPipeline` to the current temperature value. Then, it
> uses an `executor` object to asynchronously infer a description of
> clouds on a summer day based on the provided `prompt` and these
> `inferenceParams`. Finally, it prints the resulting inference output
> to the console, separated by temperature boundaries.
>
> In essence, the code is testing how different temperatures affect the output of an
> inference engine (likely a language model) when generating
> descriptions. It's a simple experiment to see if varying the
> temperature parameter influences the generated text.


**Temperature: 0.7**

> This C# code iterates through a list of temperatures (0.2f, 0.7f, 1.2f) and uses each temperature to generate
> a text description of clouds on a summer day. Here's a breakdown:
>
>
> 1. **Initialization:** It starts with a `temperatures` array containing three floating-point values representing
     > temperatures. It also defines a `prompt` string - a request for a one-sentence description of clouds.
>
> 2. **Iteration:** The `foreach` loop iterates through each `temp` value in the `temperatures` array.
>
> 3. **Inference Parameters:** Inside the loop, for each temperature, it creates an `InferenceParams` object. This
>    object is used to configure
>    an inference process (likely involving a large language model).
>    Critically, it sets the `Temperature` property of a
>    `DefaultSamplingPipeline` to the current `temp` value. This is likely
>    controlling the randomness or creativity of the generated text.
>
> 4. **Asynchronous Inference:** It then calls `executor.InferAsync(prompt, inferenceParams)`. This initiates an
>    asynchronous inference process, using the provided `prompt` and the
>    `inferenceParams` to generate text.  `await foreach` handles the
>    asynchronous nature of the inference and processes the results one at
>    a time.
>
> 5. **Output:** The `Console.Write(result)` line prints each generated text result to the console. The
>    `\n--- Temperature: {temp} ---\n`
>    lines provide clear separation and context for each temperature's
>    output.     
>    **In essence, the code performs multiple text generation attempts, each time adjusting the temperature parameter to influence the style
>    and randomness of the generated descriptions.**   It's a way to
>    explore how different temperatures affect the output of an AI text
>    generation model.

**Temperature: 1.2**

> This C# code iterates through a set of temperature values (0.2f, 0.7f, 1.2f) and uses each temperature to
> generate a description of clouds on a summer day. Here's a breakdown:
>
> 1. **Initialization:** It starts with a `temperatures` array containing floating-point temperatures and a `prompt`
>    string asking for a one-sentence description of clouds.
>
> 2. **Looping through Temperatures:** The `foreach` loop iterates through each temperature value in the `temperatures` array.
>
> 3. **Creating Inference Parameters:** Inside the loop, it creates an `InferenceParams` object. This object configures an `Inference`
>    process, likely using a language model. Crucially, it sets the
>    `Temperature` parameter of a `DefaultSamplingPipeline` to the current
>    `temp` value. The temperature parameter likely controls the
>    randomness of the generated text.
>
> 4. **Asynchronous Inference:** The code then uses `executor.InferAsync(prompt, inferenceParams)` to perform an
>    asynchronous inference operation. This likely sends the `prompt` and
>    `inferenceParams` to a language model to generate text. The `await
>    foreach` loop waits for the inference to complete and processes each generated result.
>
> 5. **Outputting Results:** Finally, inside the loop, it prints the generated text `result` to the console, preceded by the current
>    temperature for clarity.     
>    **In essence, the code runs the same text generation process (likely with a language model) multiple times, each
>    time using a slightly different temperature to explore how the randomness of the output 
>    changes.** The temperatures are used to adjust the sampling process during text generation.

You’ll notice that as temperature increases, so does verbosity — and with smaller models or constrained context windows, 
that can quickly derail relevance.

## TopP (Nucleus Sampling): Focusing on Likely Tokens

TopP, also known as nucleus sampling, is a clever technique that dynamically limits the token selection to the most
probable tokens:

```csharp
var inferenceParams = new InferenceParams
{ 
    SamplingPipeline = new DefaultSamplingPipeline()
    {
        TopP = 0.9f // Consider only tokens in the top 90% of probability mass
    }, 
};
```

- **Range**: 0.0 to 1.0
- **How it works**: Instead of considering all tokens, TopP selects only the most likely tokens whose cumulative
  probability exceeds the specified threshold.
- **Default value**: Usually around 0.9, which works well for most use cases.
- **Low values (0.5 or less)**: Very conservative selection, limiting the model to only the most predictable tokens.
- **High values (close to 1.0)**: Includes more varied tokens, increasing diversity.

TopP is often preferred over temperature adjustments because it's more adaptive and context-aware. Think of it like
this: when choosing the next word in a sentence like "The capital of France is ___", there are only a few reasonable
options, with "Paris" being highly likely. In this case, TopP will consider just a few candidates. But for a more
open-ended prompt like "My favorite hobby is ___", there are dozens of valid options. Here, TopP will automatically
consider more possibilities. This self-adjusting behavior helps maintain both accuracy and creativity.

## TopK: A Simple Token Shortlist

TopK is the simplest form of filtering - it just keeps the K most likely next tokens and zeroes out everything else:

```csharp
var inferenceParams = new InferenceParams
{ 
    SamplingPipeline = new DefaultSamplingPipeline()
    {
        TopK = 40; // Consider only the top 40 most likely tokens
    }, 
};
```

- **Range**: Usually from 0 (disabled) to 100
- **How it works**: Only the top K tokens with the highest probabilities are considered for sampling.
- **Default value**: Commonly set around 40-50.
- **Low values (10 or less)**: Very restrictive, potentially limiting creative expressions.
- **High values (>100)**: Has less effect as it includes most of the meaningful probability mass anyway.

TopK is often used alongside TopP and temperature for tighter control. Think of it like a preliminary filter - if your
model has 50,000 tokens in its vocabulary, using TopK = 40 means immediately eliminating 49,960 words from consideration,
keeping only the top 40 candidates. This acts as a guardrail, preventing the model from picking truly bizarre words even
when using high temperature settings. For example, when completing "I went to the store to buy some ___", even at high
creativity settings, TopK ensures you're not getting words like "battleships" or "skyscrapers" as suggestions.

## Repeat Penalties: Avoiding the Loops

One of the most frustrating issues with models is when they get stuck in repetitive loops. LlamaSharp provides several
parameters to address this:

```csharp
inferenceParams.RepeatPenalty = 1.1f;      // General repeat penalty
inferenceParams.FrequencyPenalty = 0.0f;    // Penalize by frequency of previous occurrences
inferenceParams.PresencePenalty = 0.0f;     // Penalize by mere presence in previous text
```

**RepeatPenalty**

- **Range**: 1.0 (no penalty) to about 1.5 (strong penalty)
- **How it works**: Reduces the probability of tokens that have appeared in the previous N tokens.
- **Default**: Usually around 1.1 to 1.2.

**FrequencyPenalty**

- **Range**: 0.0 to 2.0
- **How it works**: Penalizes tokens based on how often they've appeared in the generation so far.

**PresencePenalty**

- **Range**: 0.0 to 2.0
- **How it works**: Penalizes tokens based on whether they've appeared at all, regardless of frequency.

A moderate `RepeatPenalty` of 1.1 to 1.2 generally works well. If your model tends to get stuck
in repetition loops, try:

```csharp
        var inferenceParams = new InferenceParams
        { 
            SamplingPipeline = new DefaultSamplingPipeline()
            {
                RepeatPenalty = 1.2f,
                PenaltyCount = 64,        // How far back to check for repetitions
                FrequencyPenalty = 0.03f, // Slight penalty for frequent tokens
                PresencePenalty = 0.01f   // Minimal presence penalty
            },            
        };
```

Let's break down what these values actually do with a concrete example.

Imagine your model is generating a product description and starts to loop like this:

> This powerful laptop features a fast processor. It has a fast processor and comes with a fast processor...


Here's how each parameter would help:

* RepeatPenalty = 1.2f: This multiplies the probability of any repeated token by 1/1.2 (about 0.83). So after "fast"
  appears once, it's 17% less likely to be chosen again.
* PenaltyCount = 64: This tells the model to only look at the last 64 tokens (roughly 50 words) when applying the
  repeat penalty. So repetitions from earlier in the text won't be penalized.
* FrequencyPenalty = 0.03f: If the word "processor" appears 3 times already, its score gets reduced by 0.03 × 3 = 0.09,
  making it less likely to appear a fourth time. Words that appear more frequently get penalized more.
* PresencePenalty = 0.01f: Every unique word that has appeared at all gets a fixed 0.01 penalty, regardless of how many
  times it appeared. This gently encourages the model to use fresh vocabulary.

With these settings, our problematic text would be nudged toward something like:

> This powerful laptop features a fast processor. It has a speedy CPU and comes with high-performance computing
> capabilities...

## Stopping Conditions: Knowing When to Quit

While not part of the sampling pipeline, controlling when the generation stops is just as important as controlling how
it generates:

* **MaxTokens**: This is a hard limit on generation length. For example, if set to 2048, the model will stop after
  generating 2048 tokens (about 1500-2000 words) regardless of whether it's in the middle of a sentence or finished its
  thought.
* **AntiPrompts**: These are specific strings that signal "stop generating when you see this." Also known as
  StopSequences

For example:

```csharp
var inferenceParams = new InferenceParams
{ 
    MaxTokens = 2048, // Maximum generation length
    AntiPrompts = [ "###", "User:", "<end_of_turn>", "\n\n\n"] // Stop when these are generated
};
```

Each stop sequence has a specific purpose:

- `"###"` - Common convention for section separators; prevents the model from creating new sections
- `"User:"` - In chat applications, prevents the model from "hallucinating" user messages
- `"<end_of_turn>"` - For Gemma and similar models, this is a special token marking the end of assistant output
- `"\n\n\n"` - Three consecutive newlines, useful to stop after a completed paragraph

In practice, stop sequences are essential for chat applications. For example, imagine this conversation flow:

```
User: What's the capital of France?
Assistant: The capital of France is Paris.
User: What about Germany?
```

Without proper stop sequences, the model might continue and generate its own user messages:

```
User: What's the capital of France?
Assistant: The capital of France is Paris.
User: What about Germany?
Assistant: The capital of Germany is Berlin.
User: Thanks for the information!  <-- Model hallucination
```

With stop sequences like `"User:"`, the model would stop at the appropriate point:

```
User: What's the capital of France?
Assistant: The capital of France is Paris.
User: What about Germany?
```

This prevents the model from trying to simulate both sides of the conversation and lets your application control the
conversational flow. A good prompt template can often lower, or completely eliminate the need for stop sequences. You'll
see them used in older examples far more frequently than recently demos.

## Putting It All Together: A Complete Example

Here's a more complete example that combines all these settings:

```csharp
using System.Diagnostics;
using LLama;
using LLama.Common;
using LLama.Native;
using LLama.Sampling;

NativeLogConfig.llama_log_set((a, b) => { Debug.WriteLine($"[{a}] - {b.Trim()} "); });


// Model setup from previous post
var parameters = new ModelParams(@"b:\models\google_gemma-3-4b-it-Q6_K.gguf")
{
    ContextSize = 2048,
    GpuLayerCount = -1,
    BatchSize = 128
};

using var model = await LLamaWeights.LoadFromFileAsync(parameters);
var executor = new StatelessExecutor(model, parameters) { ApplyTemplate = true };

// Our optimized inference parameters
var inferenceParams = new InferenceParams
{
    SamplingPipeline = new DefaultSamplingPipeline()
    {
        // Balance of creativity and coherence
        Temperature = 0.7f,
        TopP = 0.9f,
        TopK = 40,

        // Anti-repetition measures
        RepeatPenalty = 1.1f,
        PenaltyCount = 64,
        FrequencyPenalty = 0.02f,
        PresencePenalty = 0.01f,

    },

    // Generation limits
    MaxTokens = 2048,
    AntiPrompts = new List<string> { "###", "User:", "\n\n\n" },
};

var prompt = "Write a concise explanation of how token sampling works in LlamaSharp.";

Console.WriteLine("Generating response with optimized parameters...\n");
await foreach (var result in executor.InferAsync(prompt, inferenceParams))
{
    Console.Write(result);
}
```

## Finding Your Ideal Configuration

Different use cases call for different sampling configurations. Here are some starting points:

### For Factual Q&A or Technical Tasks

```csharp
new DefaultSamplingPipeline()
{
    Temperature = 0.3f,    // Lower temperature reduces randomness for more deterministic, factual responses
    TopP = 0.85f,          // Moderately restrictive nucleus sampling to focus on likely correct tokens
    TopK = 40,             // Limits sampling to only consider the 40 most probable tokens, reducing errors
    RepeatPenalty = 1.2f,  // Higher repeat penalty prevents redundant explanations in technical content
};
```

### For Creative Writing

```csharp
new DefaultSamplingPipeline()
{
    Temperature = 1.0f,       // Higher temperature increases randomness for diverse, creative outputs
    TopP = 0.95f,             // Less restrictive sampling allows for more novel word combinations
    TopK = 60,                // Broader token selection enables more varied vocabulary and phrasing
    RepeatPenalty = 1.1f,     // Moderate repeat penalty prevents redundancy while allowing stylistic repetition
    FrequencyPenalty = 0.04f, // Discourages overuse of common words to enhance writing variety
};
```

### For Chat/Conversational Agents

```csharp
new DefaultSamplingPipeline()
{
    Temperature = 0.7f,       // Balanced temperature creates natural-sounding yet consistent responses
    TopP = 0.9f,              // Moderately diverse sampling mimics human conversational flexibility
    TopK = 40,                // Provides enough variety while keeping responses coherent and on-topic
    RepeatPenalty = 1.1f,     // Prevents repetitive phrases while maintaining conversational flow
    FrequencyPenalty = 0.02f, // Slight penalty to common words helps responses sound more natural
    PresencePenalty = 0.01f,  // Encourages introducing new topics and information into the conversation
};
```

## Performance Considerations

It's worth noting that some sampling parameters can affect performance:

- **Higher MaxTokens** increases total generation time (but doesn't affect tokens/second)
- **Very low Temperature** with high TopK can sometimes cause the model to spend more time in the sampling stage
- **PenaltyCount** with large values (above 256) may noticeably impact performance in long conversations due to the increased history scanning required.

## Wrapping Up

Mastering the sampling pipeline is as much an art as it is a science. While I've provided some guidelines and starting
points, the "right" configuration depends on your specific use case, model, and personal preference.

Some key takeaways:

1. **Temperature** is your primary creative dial - lower for precision, higher for creativity
2. **TopP and TopK** work together to focus generation on plausible tokens
3. **Repeat penalties** help avoid those frustrating loops
4. **Different tasks need different configurations** - what works for chat may not work for code generation

Experiment with different settings on your own prompts, and share what combinations give you the best results.
In the next post, we'll dive into customizing prompt templates for chat-style interactions.