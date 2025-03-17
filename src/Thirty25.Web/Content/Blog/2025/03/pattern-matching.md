---
title: Exploring C# 13's New Pattern Matching Extensions
description: A look at how C# 13 takes pattern matching to the next level with powerful new capabilities.
date: 2025-03-15
tags:
  - csharp
  - dotnet
  - pattern-matching
---

# Exploring C# 13's New Pattern Matching Extensions

Pattern matching has been evolving in C# for several versions now, and C# 13 takes it to an impressive new level with extended pattern capabilities that make our code even more expressive and concise.

## The Problem with Current Patterns

Before C# 13, we had several powerful pattern matching tools, but there were still scenarios where the syntax felt clumsy or required extra boilerplate. Consider this common validation scenario:

```csharp
if (customer is { Orders: var o } && o.Count > 0 && o.Any(order => order.Total > 1000))
{
    // Process high-value customers
}
```


While functional, this approach requires intermediate variables and doesn't read as fluidly as it could.

## Enter Extended Collection Patterns

C# 13 introduces extended collection patterns that allow us to directly match against collection properties. Here's the same logic with the new syntax:

```csharp
if (customer is { Orders: [> 0] and { Any(order => order.Total > 1000) } })
{
    // Process high-value customers
}
```

This new pattern matching approach lets us:

1. Check that Orders contains elements `[> 0]`
2. Apply a LINQ-style predicate directly within the pattern using the new `Any()` pattern extension

## Real-World Benefits

This seemingly small enhancement brings substantial benefits:

- **Readability**: The intent is clearer and more declarative
- **Fewer variables**: No need for intermediate variables that pollute the scope
- **Composability**: Patterns can be combined with `and`, `or`, and `not` for complex conditions
- **Conciseness**: Less code to write and maintain

## Practical Example

Here's a more complex example showing how collection patterns can be composed:

```csharp
public bool IsEligibleForDiscount(Customer customer) =>
    customer is 
    { 
        MembershipLevel: "Gold" or "Platinum",
        Orders: [> 5] and { All(o => o.Status is "Completed") },
        PaymentMethods: { Count: > 0 } and { Any(p => p is CreditCard { IsValid: true }) }
    };
```

The new syntax brings pattern matching closer to natural language, making the code's intent clearer while reducing potential bugs from variable management.

## Conclusion

C# 13's extended pattern matching capabilities continue Microsoft's trend of making the language more expressive and powerful. By allowing direct pattern matching against collections and their properties, we can write more declarative, readable code with less ceremony.

While a seemingly small addition, these pattern extensions have an outsized impact on code quality and developer productivity in real-world applications.

What pattern matching features would you like to see in future C# versions? Let me know in the comments below!