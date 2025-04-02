---
title: Getting Started with LlamaSharp
description: "Running Local LLMs in Your .NET Applications"
date: April 2, 2025
tags:
- llamasharp
---

Like the idea of using a LLM in .NET to do some small tasks, but don't want to destroy the half the rain forest per
query to do so? That's where [LlamaSharp](https://github.com/SciSharp/LLamaSharp) comes in)

## What is LlamaSharp?

LlamaSharp is a .NET wrapper around [llama.cpp](https://github.com/mmoskal/llama.cpp), the popular C++ library that
allows efficient inference of LLaMA-based
language models on consumer hardware. What does that mean in practical terms? It lets you run state-of-the-art language
models directly in your .NET applications without sending data to external APIs.

The key benefits of LlamaSharp include:

- **Local inference**: Run models on your own hardware, maintaining complete data privacy
- **No API costs**: Once you've downloaded the model, there are no ongoing API charges
- **Low latency**: Eliminate network delays with local execution
- **Full integration**: Works seamlessly with the .NET ecosystem and supports both Windows and Linux

Under the hood, LlamaSharp leverages P/Invoke to call into the native llama.cpp library, wrapping up functionality in a
more C# friendly way.

## Setting Up LlamaSharp on Windows with CUDA Support

You can run it against a CPU, but it shines with a video card. I only have an NVIDIA card, so that's what we are gonna focus on.
Getting LlamaSharp running on a Windows machine with an NVIDIA GPU involves a few steps. Let's break it down:

### 1. CUDA Requirements for NVIDIA GPUs

To leverage your NVIDIA GPU for faster inference, you'll need:

- **CUDA Toolkit**: Install at least version 11.8 or compatible
  version [from NVIDIA's website](https://developer.nvidia.com/cuda-11-8-0-download-archive)
- **cuDNN**: Download the compatible cuDNN library [from NVIDIA Developer portal](https://developer.nvidia.com/cudnn) (
  requires free developer account)

After downloading cuDNN, extract the files and add them to your CUDA installation directory (typically
`C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\vX.X`).

Wait, why these specific versions? LlamaSharp's native binaries are compiled against specific CUDA versions, so matching
these requirements ensures compatibility. If you're already using other CUDA software, you might need to manage multiple
CUDA installations carefully.

### 2. Installing LlamaSharp via NuGet

Now for the easy part. Add LlamaSharp to your .NET project using NuGet:

```shell
dotnet add package LLamaSharp
```

The second step is adding a package that matches your hardware.

* `LLamaSharp.Backend.Cpu`: Pure CPU for Windows & Linux & MAC. Metal (GPU) support for MAC.
* `LLamaSharp.Backend.Cuda11`: CUDA 11 for Windows & Linux.
* `LLamaSharp.Backend.Cuda12`: CUDA 12 for Windows & Linux.
* `LLamaSharp.Backend.OpenCL`: OpenCL for Windows & Linux.

I have a GeForce 4080, so I'm using CUDA 12 for my project. If you have a slightly older video card, your best bet is
to
Google your video card and see what version of CUDA will run best. Newer cards are going to be safe with CUDA 12
these days.

For a typical ASP.NET Core or console application, your project file might look something like:

```xml
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <OutputType>Exe</OutputType>
        <TargetFramework>net8.0</TargetFramework>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
    </PropertyGroup>

    <ItemGroup>
        <PackageReference Include="LLamaSharp" Version="0.23.0"/>
        <PackageReference Include="LLamaSharp.Backend.Cuda12.Windows" Version="0.23.0"/>
    </ItemGroup>
</Project>
```

## Finding and Downloading GGUF Models

Now we need a model! GGUF (GGML GPU Unified Format) is the standard format for optimized language models in the
llama.cpp ecosystem. It evolved from the older GGML format and has become the de facto standard for efficiently running
LLMs on consumer hardware. GGUF files are essentially containers that package the model's weights, architecture
information, vocabulary, and metadata together in a highly optimized binary format.

The internal structure of a GGUF file includes several key components: a header with format version and metadata, the
model's hyperparameters (like embedding size, context length, and layer count), a tokenizer vocabulary with special
token definitions, and the compressed weight matrices for each layer of the neural network. Additionally, GGUF files
often store custom metadata that helps applications understand the model's intended
use case, appropriate prompt templates, and licensing information, making them more than just raw neural network data
but complete, deployable AI components.

Let's use Google's Gemma 3 as our example. It's relatively small but surprisingly capable.

### Downloading from Hugging Face

Currently, the best source for finding AI models is [Hugging Face](https://huggingface.co/). Hugging Face started as a
simple open-source library
for natural language processing but quickly evolved into something much more ambitious. Today, it's essentially the
GitHub of machine learning – a platform where researchers, developers, and companies share, discover, and collaborate on
ML models and datasets.

For us in the .NET world, Hugging Face is particularly valuable when working with libraries like LlamaSharp. Instead of
training models from scratch (which would require tons of computing resources), we can browse the thousands of
pre-trained models on Hugging Face, download them in GGUF format, and plug them directly into our applications.

Hugging Face contains all kinds of models in all sorts of formats. We only want the GGUF though, and many times
researchers
do not release those on their own.

For example, let's look at [Google's Gemma-3 4b IT model](https://huggingface.co/google/gemma-3-4b-it). If you look at
the files and versions tab,
you'll see all kinds of stuff about tensors, tokens and vocab, but no GGUF. llama.cpp offers tools that we can use to
take these files
and convert them into the GGUF format, but thankfully due to the openness of the models a community around doing this
already exists.

A great resource for finding GGUFs is Colin Kealty's account, [Bartowski1182](https://huggingface.co/bartowski). He's an
employee of [LM Studio](https://lmstudio.ai/), a popular LLM tool
based on llama.cpp. Because LM Studio is based on llama.cpp, it has an interest in ensuring good GGUFs exist for their
users, and
the Barowksi1182 account is where you'll find them. His profile also has recommended models, large and small, which is
helpful when exploring
what's out there. As of writing he has 1800 models on his account, so the recommendations are quite helpful.

#### Let's grab his version of Gemma 3 in GGUF format

1. Visit [google_gemma-3-4b-it-GGUF](https://huggingface.co/bartowski/google_gemma-3-4b-it-GGUF)
2. Navigate to the "Files and versions" tab
3. Look for files with the `.gguf` extension. Let's grab the google_gemma-3-4b-it-Q6_K.gguf file. I have a folder named
   models I store everything in.

### Choosing the Right GGUF Variant

Ok, so there were a ton of files in here. Why the Q6_K? GGUF models come in different "quantized" versions, varying in
size and precision.
The filename usually indicates the quantization level, like `google_gemma-3-4b-it-Q2_K_L` or
`google_gemma-3-4b-it-Q2_K.gguf`.

Here's a quick guide to choosing the right one based on your hardware:

The quantization level you choose represents a crucial trade-off between model quality, memory usage, and inference
speed. Standard GGUF quantization formats include options like Q2_K through Q8_0, with the number roughly corresponding
to how many bits are used to store each weight. Q2_K uses just 2 bits per weight (resulting in tiny files that run
quickly but with noticeable quality loss), while Q8_0 uses 8 bits (preserving more of the original model's capabilities
but requiring more memory and processing power). The suffix letters like "K" (K-quants) or "M" (medium) indicate the
specific quantization algorithm used, with K-quants generally offering better quality-to-size ratios through more
sophisticated compression techniques that preserve important weight distributions.

I typically recommend Q6_K as the "goldilocks" quantization level for most use cases. It strikes an excellent balance
between quality and performance, preserving roughly 95% of the full model's capabilities while still offering
significant size reduction and speed improvements. Only consider dropping to Q4_K if you're targeting
resource-constrained environments or need maximum speed, and only move up to Q8_0
if you're working on applications where that last 5% of quality improvement truly matters.

### But wait, what's this 4B part?

If you were poking around on the models page, you might have also discovered this

* [Gemma-3 1b](https://huggingface.co/bartowski/google_gemma-3-1b-it-GGUF)
* [Gemma-3 4b](https://huggingface.co/bartowski/google_gemma-3-4b-it-GGUF)
* [Gemma-3 12b](https://huggingface.co/bartowski/google_gemma-3-12b-it-GGUF)
* [Gemma-3 27b](https://huggingface.co/bartowski/google_gemma-3-27b-it-GGUF)

The numbers in model names like "Gemma-3 4b" or "Gemma-3 27b" refer to the parameter count – roughly how many
individual values or "weights" make up the neural network. Think of parameters as the knobs and dials that the model
adjusts during training to capture patterns in data. More parameters generally mean more capacity to learn complex
relationships, but with corresponding increases in computational requirements and memory usage. A 1B model contains
approximately 1 billion parameters, a 4B has 4 billion, and so on.

These different size options create a spectrum of trade-offs between power and practicality. The smaller 1B models are
lightning-fast and can run on almost any hardware (even phones!), but without lots of guidance and direct queries can be
wildly wrong.

The mid-range 4B and 7B models hit a sweet spot for many applications, offering impressive capabilities
while still running comfortably on consumer GPUs. The larger 12B models exhibit notably stronger reasoning and knowledge
but demand serious GPU resources, while the big 27B+ models approach API-level quality but require high-end hardware
most developers don't have access to on their local box.

### I'm reading a getting started guide, just tell me what to do

My rule of thumb is choose the parameter sized model whose Q6_K file size lines up with your VRAM size minus 4GB. I have 16GB, so a
Gemma-3 12B runs just fine.  I'll talk about why the extra 4GB is needed in a bit. I could, theoretically, run Gemma-3
27b with a
Q2_K quant, but the drop in quality from Q6 to Q2 is enough that I feel like you might as well stick to the 12b. There is a
reason there are
a variety though, so pick and test.

## Creating Your First LlamaSharp Application

Ok, we've configured our machine. We've downloaded a model. Let's put this to work. I'm going to assume you downloaded the
google_gemma-3-4b-it-Q6_K.gguf into
a folder named models in your `B:` drive.

```csharp
using LLama;
using LLama.Common;

// Set the path to your downloaded GGUF file
const string modelPath = @"b:\models\google_gemma-3-4b-it-Q6_K.gguf";
        
// Configure model parameters
var parameters = new ModelParams(modelPath);

Console.WriteLine("Loading model...");
using var model = await LLamaWeights.LoadFromFileAsync(parameters);
        
// Create a stateless executor (simpler API for basic generation)
var executor = new StatelessExecutor(model, parameters);
        
var prompt = "Write me a blog post about llamasharp?";
Console.WriteLine($"Prompt: {prompt}\n");
        
Console.WriteLine("Generating response...");
await foreach (var result in executor.InferAsync(prompt))
{
    Console.Write(result);
}
```

Let's break down what's happening here:

1. We load the model using `LLamaWeights.LoadFromFileAsync`, specifying our parameters
2. We create a `StatelessExecutor` which provides a simple, stateless API for inference
3. We call `InferAsync` with our prompt and inference parameters
4. The model generates a response based on the prompt

The `StatelessExecutor` is perfect for simple applications where you don't need to maintain conversation history or
complex state.

## Running the Application

When you run this application, you'll see the model load (which might take a moment) along with a whole mess of log
statements, followed by the generated response to your prompt.

Did it work? Nice! You're now running a local LLM directly in your .NET application.

## Time To Dig Into Logs

The first thing to do is clear up those log statements. But there is a reason they are on by default.  
To adjust them, tap into `NativeLogConfig` before executing any LlamaSharp code.
We don't want to disable it for now, because it has some important info we should look at. Let's dump them to the Debug
Console.

```csharp
NativeLogConfig.llama_log_set((d, msg) =>
{
    Debug.WriteLine($"[{d}] - {msg.Trim()} ");
});
```

Just include this before any other LlamaSharp code. It'll redirect the output to the debug window and include
only warnings and errors, sufficient until we attach a better logging infrastructure.

Let's take a look at the logs. There is a lot in there, especially if you are including debug outputs.
Here are some items to look for in our logs when running these statements. Only one is `Debug`, but that data is
summarized
in `Info` later. You can generally be safe to leave `Debug` off.

#### GPU Utilization

```
[Info] - ggml_cuda_init: found 1 CUDA devices: 
[Info] - Device 0: NVIDIA GeForce RTX 4080 SUPER, compute capability 8.9, VMM: yes 
[Info] - llama_model_load_from_file_impl: using device CUDA0 (NVIDIA GeForce RTX 4080 SUPER) - 15035 MiB free 
```

This shows your available GPU (NVIDIA RTX 4080 SUPER) with 15GB of free VRAM, which is a good amount for running
medium-sized models.

```
[Debug] - load_tensors: layer   0 assigned to device CPU 
...
[Debug] - load_tensors: layer  13 assigned to device CPU 
[Debug] - load_tensors: layer  14 assigned to device CUDA0 
...
[Debug] - load_tensors: layer  33 assigned to device CUDA0 
[Debug] - load_tensors: layer  34 assigned to device CPU 
```

These statements show how the model layers are distributed between CPU and GPU. In this case, layers 14-33 are assigned
to the GPU, while the rest are on the CPU. Think of LLM layers like stages in a data processing pipeline, similar to
how you might chain middleware in a web app.

A big model like GPT-4 has around 100 of these layers stacked sequentially. The more layers, the deeper the model can
analyze patterns in the data. When running these models locally, you're essentially deciding which parts of this
pipeline run on your GPU versus CPU. Since GPUs excel at the parallel matrix operations these layers perform, offloading
more layers to the GPU dramatically speeds up inference - but each layer consumes precious VRAM. That's why the log is
showing you which layers are assigned where.

```
[Info] - load_tensors: offloading 20 repeating layers to GPU 
[Info] - load_tensors: offloaded 20/35 layers to GPU 
[Info] - load_tensors:        CUDA0 model buffer size =  1477.38 MiB 
[Info] - load_tensors:   CPU_Mapped model buffer size =  1559.18 MiB 
```

This summary confirms that 20 out of 35 total layers are running on the GPU. The model itself uses about 1.5GB of VRAM.
With 15GB available, we definitely want to offload more layers to the GPU for better performance.

To adjust the layers the GPU is assigned, you can use the `GpuLayerCount` property of the `ModelParams` class. We could
set this to `35`, which we know is all the layers, or we could be lazy and set it to `-1` which will use them all.

```csharp
var parameters = new ModelParams(modelPath)
{
    GpuLayerCount = -1,          // Increased from 20
};
```

If you run the app again, you'll notice significant performance improvements, assuming everything still fits in memory
on your GPU. This is a setting you'll need to feel out based on your hardware and model; there is no right answer for
all,
but honestly I stick to models where `-1` always works.

#### Context Length

Context length is essentially the model's working memory size - like the difference between a 4GB and 64GB RAM machine.
When an LLM processes text, it can only 'see' and reference information within its context window.
If you set it to 8K tokens, that's roughly 6,000 words or 24 pages of text that the model can consider at once.

The catch is that the memory requirements grow quadratically with context length due to the attention mechanism. It's
like if each word needed to store its relationship with every other word in memory. That's why your log shows a massive
17GB just for the KV cache with a 131K context. Most consumer applications use 4K-8K contexts for the sweet spot of
performance and utility.

```
[Info] - gemma3.context_length u32              = 131072 
```

This is the model's maximum **supported** context length - an extremely large value of 131,072 tokens.

```
[Info] - llama_context: n_ctx         = 131072 
[Info] - llama_context: n_ctx_per_seq = 131072 
```

These lines show that you're actually using the maximum context length of 131,072 tokens. This will consume a large
amount of memory.

```
[Info] - init: kv_size = 131072, offload = 1, type_k = 'f16', type_v = 'f16', n_layer = 34, can_shift = 1 
...
[Info] - init:      CUDA0 KV buffer size = 10240.00 MiB 
[Info] - init:        CPU KV buffer size =  7168.00 MiB 
[Info] - llama_context: KV self size  = 17408.00 MiB, K (f16): 8704.00 MiB, V (f16): 8704.00 MiB 
```

These are critical lines showing the memory impact of the large context window. The KV (key-value) cache requires over
17GB of memory! This is split between GPU (10GB) and CPU (7GB). Reducing the context length would significantly decrease
these memory requirements. When running these models, we want to always fit the KV cache into our GPU memory.

We do not need this much context, plus it is bogging us down quite a bit because that much data won't fit in my graphics
card. And even if it did we'd wait for allocation anyway. I tend to stick to around `4096` as my context size. Anything
larger and most of these local LLMs start to lose the plot anyways.

```csharp
var parameters = new ModelParams(modelPath)
{
    ContextSize = 4096,      // Reduced from 131072
    GpuLayerCount = -1,      // Increased from 20
};
```

#### Batch Size

Batch size in LLMs is like thread pooling. It defines how many tokens the model processes in parallel during inference.

When generating text, the model needs to run a full forward pass through all layers for each new token. With a batch
size of 512 like in your logs, the GPU is calculating 512 potential next tokens simultaneously. This dramatically
improves throughput compared to generating one token at a time, similar to how batching database operations outperforms
individual queries.

However, there's a trade-off. Larger batch sizes consume more VRAM (your log shows 3.3GB for compute buffers) and can
introduce more latency for the first token. For interactive chatbots, smaller batches around 128 often feel more
responsive, while for bulk text generation, larger batches improve overall throughput.

It's essentially parallelism vs. latency - the same optimization problem you'd face in any high-performance system.

```
[Info] - llama_context: n_batch       = 512 
[Info] - llama_context: n_ubatch      = 512 
```

These lines show that the batch size is set to 512 tokens. This is quite large and means the model processes 512 tokens
at once during inference.

This is all app specific, but I tend to stick to `128` as my default, favoring responsiveness.

```csharp
var parameters = new ModelParams(modelPath)
{
    ContextSize = 2048,      // Reduced from 131072
    GpuLayerCount = -1,      // Increased from 20
    BatchSize = 128,         // Reduced from 512
};
```

These settings all apply to the model that is loaded in memory. Loading and unloading the model is a costly operation.
Loading it once and performing multiple queries with the same settings is a very common pattern. Switching these
parameters around from the defaults to something tuned for your hardware hopefully should net you some
serious gains.

## Improving Our Prompt

GGUF prompt formats define how input text is structured and tokenized before being processed by the model. Think of them
as the language interface between you and the LLM - each model family (like Llama, Mistral, or Falcon) expects inputs
formatted in specific ways. The prompt format determines everything from system prompts and role markers to special
tokens that separate turns in a conversation. Getting this format right is crucial, as even small deviations can
significantly impact output quality or cause the model to misinterpret your instructions entirely.

If you check the Model Card on Hugging Face, you can see the gemma 3 prompt format is

```text
<bos><start_of_turn>user
{system_prompt}

{prompt}<end_of_turn>
<start_of_turn>
```

* `<bos>` is the "beginning of sequence" token that signals the start of input
* `<start_of_turn>` and `<end_of_turn>` are conversation markers that separate different speakers' turns
* `user` identifies who is speaking (the human user in this case)
* `{system_prompt}` and `{prompt}` are Jinja template variables

Quick Jinja explanation: Jinja is a templating language for Python that allows you to insert dynamic content into
otherwise static text. The curly braces `{system_prompt}` and `{prompt}` are placeholders that will be replaced with
actual
content when the template is rendered. The system prompt typically contains instructions for the AI, while the prompt is
the user's actual query.

This format essentially structures how the model receives input, with clear delineation between system instructions and
user queries, helping the LLM understand the conversational context.

For example, if we had a chat that went:

* **User**: What is the capital of France?
* **Assistant**: Paris is the capital of France.

Then the user was sending a second message of "How many people live there?" this would be the entire message sent to the
LLM (note: the jinja template also specifies to convert "assistant" to "model")

```text
<bos><start_of_turn>user
You are a helpful AI assistant that provides brief, accurate answers.

What is the capital of France?<end_of_turn>
<start_of_turn>model
Paris is the capital of France.<end_of_turn>
<start_of_turn>user
How many people live there?<end_of_turn>
<start_of_turn>model
```

Remember - LLMs are at their core text completion engines. The tokens defined here and then ending the prompt with
`<start_of_turn>model`
gives the LLM a starting point to begin "completing" the message and gives us a return. The prompt also specifies that
`<end_of_turn>` is how to complete the turn. With many of these LLMs, they have a tendency to start rambling without
being given the proper prompt template because they don't know how or when to stop.

This would change our code to now be:

```csharp
var template = new LLamaTemplate(model);
template.Add("user", "Write me a blog post about llamasharp?");

var prompt = PromptTemplateTransformer.ToModelPrompt(template);

await foreach (var result in executor.InferAsync(prompt))
{
    Console.Write(result);
}
```

Because applying this template is so fundamental, most Executors in LlamaSharp will apply this automatically, some by
default. With `StatelessExecutor`, we need to be explicit when creating it. Doing so reduces our code to:

```csharp
var executor = new StatelessExecutor(model, parameters) { ApplyTemplate = true };
var prompt = "Write me a blog post about llamasharp?";

await foreach (var result in executor.InferAsync(prompt))
{
    Console.Write(result);
}
```

With `ApplyTemplate` being set, the executor will apply the template before sending the prompt.

One caveat - this relies on an internal method in llama.cpp that needs to be updated to keep up. It does not actually
run the jinja template, but rather mimics the behavior. When trying out a new model, it is best to check that its
template is one of the ones supported on
their [supported template list](https://github.com/ggml-org/llama.cpp/wiki/Templates-supported-by-llama_chat_apply_template).
If not, you may need to write the templating code yourself.

For now, we are lucky that ours is a standard one so it works out of the box. That leaves us with a, for now, complete
query executing chunk of code:

```csharp
using System.Diagnostics;
using LLama;
using LLama.Common;
using LLama.Native;

NativeLogConfig.llama_log_set((a, b) => { Debug.WriteLine($"[{a}] - {b.Trim()} "); });

// Set the path to your downloaded GGUF file
const string modelPath = @"b:\models\google_gemma-3-4b-it-Q6_K.gguf";

// Configure model parameters
var parameters = new ModelParams(modelPath)
{
    ContextSize = 2048, // Reduced from 131072
    GpuLayerCount = -1, // Increased from 20
    BatchSize = 128, // Reduced from 512
};

Console.WriteLine("Loading model...");
using var model = await LLamaWeights.LoadFromFileAsync(parameters);

// Create a stateless executor (simpler API for basic generation)
var executor = new StatelessExecutor(model, parameters) { ApplyTemplate = true };
var prompt = "Write me a blog post about llamasharp?";

await foreach (var result in executor.InferAsync(prompt))
{
    Console.Write(result);
}
```

## What's Left to Tweak?

One very important thing we can configure is `DefaultSampling` parameters, the algorithm that shapes how an LLM
generates text. While context length and layer distribution handle the "how much" and "where" of model execution, these
sampling parameters control the "what" and "how" of token generation.

Sampling Pipelines warrant their own post. We'll look at how temperature adjusts creativity
versus predictability, how top-p nucleus sampling prevents low-probability nonsense without limiting expression, and how
repetition penalties help avoid those frustrating loops where models get stuck repeating themselves.

## Quick Summary of What We Covered

We've covered quite a bit in this post, so let's summarize the key points:

* **LlamaSharp basics**: We explored what LlamaSharp is—a .NET wrapper around llama.cpp that lets you run LLMs locally in your applications
* **Setup and installation**: We walked through setting up CUDA for NVIDIA GPUs and installing the right NuGet packages
* **Finding models**: We navigated the world of GGUF models, learning how to find them on Hugging Face and understanding the differences between model sizes (1B to 27B) and quantization levels (Q2_K to Q8_0)
* **Fine-tuning performance**: We examined the logs to optimize GPU layer allocation, context length, and batch size for your specific hardware
* **Improving prompts**: We learned about prompt templates and how to structure your inputs for better results