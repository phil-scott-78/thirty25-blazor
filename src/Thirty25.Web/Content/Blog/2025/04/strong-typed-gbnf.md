---
title: "Strongly Typed GBNF Generation"
description: "Creating GBNF and JSON Samples from C# Classes"
series: "Intro to LlamaSharp"
date: April 17, 2025
tags:
  - llamasharp
---

In the previous post, we looked at using GBNF to force our models to give us a valid json structure. But what if we took
it a step further and generated a GBNF that wasn't just valid JSON, but also matched our schema?

## Data Type Enforced GBNFs

Let's say we wanted to generate a GBNF that matched the following JSON:

```json
{
  "firstName": "Lamar",
  "lastName": "Bridgewater",
  "age": 30,
  "isMember": true
}
```

Here we need to take what we learned from our previous post about forcing the next token. There we were just concerned 
with making sure the next token was valid JSON, now we can extend it so that we are also looking for valid field names 
and appropriate data types. Our GBNF for this JSON would be:

```gbnf
string ::= "\""   ([^"]*)   "\""
boolean ::= "true" | "false"
number ::= [0-9]+   "."?   [0-9]*
ws ::= [ \t\n]*
Person ::= "{"   ws   "\"firstName\":"   ws   string   ","   ws   "\"lastName\":"   ws   string   ","   ws   "\"age\":"   ws   number   ","   ws   "\"isMember\":"   ws   boolean   "}"
root ::= Person
```

Let's break this down, line by line.

### String Rule

```gbnf
string ::= "\"" ([^"]*) "\""
``` 

* `"\""` — an opening double quote
* `([^"]*)` — any number of characters that are not double quotes
* `"\""` — a closing double quote

So this captures a basic string like "John" or "Doe".

### Boolean Rule

```gbnf
boolean ::= "true" | "false"`
```

* Literal match for `true` or `false` (no quotes in output, just raw booleans).

### Number Rule

```gbnf
number ::= [0-9]+ "."? [0-9]*
```

Token-by-token:
* `[0-9]+` — one or more digits (e.g., 123)
* `"."?` — an optional decimal point
* `[0-9]*` — zero or more digits after the decimal

This matches integers like 45, or decimals like 45.67, or 45. (trailing dot allowed but no digits after), or 123.0.

### Whitespace Rule

```gbnf
ws ::= [ \t\n]*
```

* Zero or more spaces, tabs, or newline characters

This allows for flexible formatting with whitespace between elements.

### Person Rule

```gbnf
Person ::= "{" ws "\"firstName\":" ws string "," ws "\"lastName\":" ws string "," ws "\"age\":" ws number "," ws "\"isMember\":" ws boolean "}"
```

This rule describes a JSON-like Person object with four fields.

Token-by-token:

* `"{"` — a literal open curly brace
* `ws` — whitespace, zero or more spaces, tabs, or newline characters
* `"\"firstName\":"` - literal string "firstName":, with escaped quotes
* `ws` — optional whitespace
* `string` — a quoted string for first name
* `","` — comma separator
* `ws`
* `"\"lastName\":"` — literal string "lastName":, with escaped quotes
* `ws`
* `string` — quoted string for last name
* `","`
* `ws`
* `"\"age\":"` — literal string "age":, with escaped quotes
* `ws`
* `number` — a numeric value for age
* `","`
* `ws`
* `"\"isMember\":"` — literal string "isMember":, with escaped quotes
* `ws`
* `boolean` — either true or false
* `"}"` — closing curly brace

### Top-Level Rule - `root`

```gbnf
root ::= Person
```

* root is the starting point of this grammar.
* It consists of a single `Person` object.

### Generated Output

The grammar forces the output to not only be valid JSON but also ensures each of our fields is included and is in the
correct format. It does, however, force the generated output to have the fields in a set order.

## From C# to GBNF

The next step is to take our C# classes and generate the GBNF. I've automated this into my 
[RedPajama project](https://github.com/phil-scott-78/RedPajama). RedPajama does this by:

1. Reading C# class definitions
2. Analyzing their properties and types
3. Converting this type information into GBNF grammar rules
4. Generating constraints that ensure the output is both valid JSON and matches your schema

This isn't anything new. RedPajama builds on established approaches like
* llama.cpp has a suite of tools for converting [JSON Schemas to GBNF](https://github.com/ggml-org/llama.cpp/blob/master/grammars/README.md#json-schemas--gbnf).
* [gbnfgen](https://github.com/IntrinsicLabsAI/gbnfgen) is a library for generating grammars based on your typed JSON objects, described through normal TypeScript interfaces and enums.

RedPajama works more closely to gbnfgen. To use it, we need to create a TypeModelBuilder for our class.

```csharp:xmldocid
T:Gbnf.User
```

Given this class, to use RedPajama to generate it, we need to

1. Create a new instance of the GbnfGenerator using the TypeModel.
2. Get a new instance of the TypeModelBuilder using User as the generic type.
3. Call the `Build` method on the TypeModelBuilder to get the TypeModel.
4. Call the `Generate` method on the GbnfGenerator to get the GBNF.
 
```csharp:xmldocid,bodyonly
M:Gbnf.MyApp.GetSimpleGbnf
```

Running this, we get the following GBNF:

```gbnf
char ::= [^"\\\x7F\x00-\x1F⟨⟩] | [\\] (["\\bfnrt] | "u" [0-9a-fA-F]{4})
space ::= | " " | "\n" [ \t]{0,20}
root-firstname-kv ::= "\"FirstName\"" space ":" space "\"" char{1, 512} "\"" space
root-lastname-kv ::= "\"LastName\"" space ":" space "\"" char{1, 512} "\"" space
root-age-kv ::= "\"Age\"" space ":" space ("-"? [0] | [1-9] [0-9]{0,15}) space
root-ismember-kv ::= "\"IsMember\"" space ":" space ("true" | "false") space
root ::= "{" space root-firstname-kv "," space root-lastname-kv "," space root-age-kv "," space root-ismember-kv "}" space
```

This is a bit more complex than the GBNF we were rolling by hand. Let's break down what each part does:

### Character Definition

```gbnf
char ::= [^"\\\x7F\x00-\x1F⟨⟩] | [\\] (["\\bfnrt] | "u" [0-9a-fA-F]{4})
```

This defines what characters are allowed in our strings:
* `[^"\\\x7F\x00-\x1F⟨⟩]` - any character except quotes, backslashes, control characters, and special brackets
* `[\\] (["\\bfnrt] | "u" [0-9a-fA-F]{4})` - escape sequences like `\"`, `\\`, `\b`, `\f`, etc., plus Unicode escapes like `\u00A9`

This properly handles JSON string escaping, which our simplified version didn't cover.

### Whitespace Handling

```gbnf
space ::= | " " | "\n" [ \t]{0,20}
```

This defines optional whitespace that can be:
* Empty (no space)
* A single space
* A newline followed by up to 20 spaces or tabs

This allows for nice formatting without going overboard with indentation.

### Key-Value Pairs

Each property in our User class gets its own key-value rule:

```gbnf
root-firstname-kv ::= "\"FirstName\"" space ":" space "\"" char{1, 512} "\"" space
```


Breaking this down:
* `"\"FirstName\""` - The JSON key "FirstName" (with escaped quotes)
* `space ":" space` - Colon with optional whitespace on either side
* `"\"" char{1, 512} "\""` - A string value between 1 and 512 characters long. The typemodel builder uses 512 is the 
  default maximum length, but we'll see how we can override that later as well as the minimum length.
* `space` - Optional trailing whitespace

The other key-value pairs follow the same pattern but with type-specific rules. For example, age uses:

```gbnf
root-age-kv ::= "\"Age\"" space ":" space ("-"? [0] | [1-9] [0-9]{0,15}) space
```


This ensures age is either 0 or a number that doesn't start with leading zeros, with an optional negative sign.

For booleans, we see:

```gbnf
root-ismember-kv ::= "\"IsMember\"" space ":" space ("true" | "false") space
```


This strictly enforces that IsMember is either `true` or `false`.

### The Root Object

```gbnf
root ::= "{" space root-firstname-kv "," space root-lastname-kv "," space root-age-kv "," space root-ismember-kv "}" space
```


This assembles all the key-value pairs into a complete JSON object with the exact properties we want.

## From C# to JSON Samples

In the previous article, we saw that giving the model a JSON sample drastically improved its success rate. So while we 
have a representation of the type, let's get a helper that does that too.

```csharp:xmldocid
M:Gbnf.MyApp.GetSimpleJson
```

Running this will give us a templated JSON output that we can use in our prompt.

```json
{
  "FirstName": "⟨string value⟩",
  "LastName": "⟨string value⟩",
  "Age": ⟨integer value⟩,
  "IsMember": ⟨true or false⟩
}
```

## Putting It All Together

So now we have a way to automate GBNF generation, and sample JSON generation. Let's build a full example.

```csharp:xmldocid,bodyonly
T:Gbnf.ParseOrder
```

With all our new tools, we were able to give the model everything it needs. A good sample so it has directions on the
expected content plus a grammar that forces the format just in case the model decides to color outside the lines a bit.

With those two things in place, we can safely deserialize into our User object using `System.Text.Json` knowing that we
have not only valid JSON, but one that adheres to our scheme.

In the next blog post, we'll get into more customization options for building the JSON and GBNF for more complex
scenarios.