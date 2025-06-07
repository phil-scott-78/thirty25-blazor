using System.CodeDom.Compiler;
using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace EfCoreTagging.SourceGenerator;

[Generator]
public class EfCoreTaggingGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var methodCalls = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => IsLikelyEfCoreTerminalMethod(node),
                transform: static (ctx, _) => GetMethodCallInfo(ctx))
            .Where(m => m != null)
            .Select((info, _) => info!);

        context.RegisterSourceOutput(
            methodCalls.Collect(),
            static (spc, methodCalls) => Execute(spc, methodCalls));
    }

    private static bool IsLikelyEfCoreTerminalMethod(SyntaxNode node)
    {
        // First, check if it's a method invocation at all
        if (node is not InvocationExpressionSyntax invocation)
            return false;

        // Check if the method is being called on something (has a member access expression)
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            return false;

        // Get the method name - this is available at the syntax level
        var methodName = memberAccess.Name.Identifier.ValueText;

        // Filter based on known EF Core terminal method names
        // This gives us a significant first-pass filter
        return MethodsCalls.Contains(methodName);
    }

    private static MethodCallInfo? GetMethodCallInfo(GeneratorSyntaxContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Get the invoked method symbol
        if (context.SemanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol methodSymbol)
            return null;

        // Check if this is a call to an EntityFrameworkQueryableExtensions terminal method
        if (!IsEfCoreTerminalMethod(methodSymbol))
            return null;

        // Get the InterceptableLocation
        if (context.SemanticModel.GetInterceptableLocation(invocation) is not { } interceptableLocation)
            return null;

        // Get location information
        var location = invocation.GetLocation();
        var lineSpan = location.GetLineSpan();
        var filePath = lineSpan.Path;
        var displayLocation =
            $"{Path.GetFileName(filePath)}({lineSpan.StartLinePosition.Line + 1},{lineSpan.StartLinePosition.Character + 1})";

        // Get the full method call directly from syntax
        var fullMethodCall = invocation.ToString();

        // Get caller information
        var callerInfo = GetCallerInfo(invocation);


        // This is likely an overload with additional parameters
        // Extract the signature directly for accuracy
        var signature = ExtractMethodSignature(methodSymbol);

        return new MethodCallInfo(
            methodSymbol,
            filePath,
            displayLocation,
            interceptableLocation,
            fullMethodCall,
            callerInfo,
            signature);
    }

    private static MethodSignature ExtractMethodSignature(IMethodSymbol methodSymbol)
    {
        // First, ensure we're working with the original method definition
        // For extension methods, we need the non-reduced form to get the correct signature
        var originalMethodSymbol = methodSymbol.ReducedFrom != null 
            ? methodSymbol.ReducedFrom.OriginalDefinition 
            : methodSymbol.OriginalDefinition;

        // Get return type from the original definition to preserve type parameters
        // This ensures we get Task<List<TSource>> instead of Task<List<Blog>>
        var returnType = originalMethodSymbol.ReturnType.ToDisplayString();

        // Get type parameters
        var typeParameters = originalMethodSymbol.IsGenericMethod
            ? $"<{string.Join(", ", originalMethodSymbol.TypeParameters.Select(t => t.Name))}>"
            : "";

        // Build parameters string
        var parameters = new StringBuilder();
        var constraints = new StringBuilder();

        // Extension methods always have at least one parameter (the 'this' parameter)
        var isFirst = true;
        var parametersToUse = originalMethodSymbol.ReducedFrom?.Parameters ?? originalMethodSymbol.Parameters;

        foreach (var param in parametersToUse)
        {
            if (!isFirst)
            {
                parameters.Append(", ");
            }

            if (isFirst && originalMethodSymbol.IsExtensionMethod)
            {
                parameters.Append("this ");
            }

            // Use parameter types from the original definition to preserve type parameters
            var paramType = param.Type.ToDisplayString(
                new SymbolDisplayFormat(
                    typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
                    genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters
                )
            );

            parameters.Append(paramType);
            parameters.Append(" ");
            parameters.Append(param.Name);

            // Handle default values
            if (param.HasExplicitDefaultValue)
            {
                var defaultValue = param.ExplicitDefaultValue;
                switch (defaultValue)
                {
                    case null:
                        parameters.Append(" = default");
                        break;
                    case string:
                        parameters.Append(" = \"").Append(defaultValue).Append("\"");
                        break;
                    default:
                        parameters.Append(" = ").Append(defaultValue);
                        break;
                }
            }

            isFirst = false;
        }

        // everything *should* be a generic method, but just in case we can bail here
        if (!methodSymbol.IsGenericMethod)
        {
            return new MethodSignature(
                returnType,
                typeParameters,
                parameters.ToString(),
                constraints.ToString()
            );
            
        }
        
        // Add constraints for type parameters if needed
        foreach (var typeParam in methodSymbol.TypeParameters.Where(typeParam => typeParam.ConstraintTypes.Length > 0 || typeParam.HasReferenceTypeConstraint || typeParam.HasValueTypeConstraint || typeParam.HasNotNullConstraint))
        {
            if (constraints.Length > 0)
                constraints.AppendLine();

            constraints.Append("where ").Append(typeParam.Name).Append(" : ");

            if (typeParam.HasReferenceTypeConstraint)
                constraints.Append("class, ");
            if (typeParam.HasValueTypeConstraint)
                constraints.Append("struct, ");
            if (typeParam.HasNotNullConstraint)
                constraints.Append("notnull, ");

            if (typeParam.ConstraintTypes.Length > 0)
            {
                constraints.Append(string.Join(", ",
                    typeParam.ConstraintTypes.Select(t => t.ToDisplayString())));
            }

            // Remove trailing comma and space if present
            if (constraints[constraints.Length - 1] == ' ' && constraints[constraints.Length - 2] == ',')
                constraints.Length -= 2;
        }

        return new MethodSignature(
            returnType,
            typeParameters,
            parameters.ToString(),
            constraints.ToString()
        );
    }

    private static readonly List<string> MethodsCalls =
    [
        "ToList",
        "ToListAsync",
        "ToArray",
        "ToArrayAsync",
        "Count",
        "CountAsync",
        "First",
        "FirstAsync",
        "FirstOrDefault",
        "FirstOrDefaultAsync",
        "Single",
        "SingleAsync",
        "SingleOrDefault",
        "SingleOrDefaultAsync",
        "Any",
        "AnyAsync",
        "All",
        "AllAsync",
        "Sum",
        "SumAsync",
        "Average",
        "AverageAsync",
        "Min",
        "MinAsync",
        "Max",
        "MaxAsync",
        "ToDictionary",
        "ToDictionaryAsync",
        "ToHashSet",
        "ToHashSetAsync",
        "Contains",
        "ContainsAsync"
    ];

    private static bool IsEfCoreTerminalMethod(IMethodSymbol methodSymbol)
    {
        // Check if this is a method from EntityFrameworkQueryableExtensions
        if (methodSymbol.ContainingType?.Name != "EntityFrameworkQueryableExtensions")
            return false;

        // Check if this is a terminal method (not returning IQueryable<T>)
        if (methodSymbol.ReturnType is INamedTypeSymbol { Name: "IQueryable", IsGenericType: true })
            return false;

        // Check if we have this method name in our list
        return MethodsCalls.Contains(methodSymbol.Name);
    }

    private static string GetCallerInfo(InvocationExpressionSyntax invocation)
    {
        // Find containing method or property declaration
        var containingMember = invocation.Ancestors()
            .FirstOrDefault(a => a is MethodDeclarationSyntax or PropertyDeclarationSyntax);

        var memberName = containingMember switch
        {
            MethodDeclarationSyntax methodDecl => methodDecl.Identifier.Text,
            PropertyDeclarationSyntax propertyDecl => propertyDecl.Identifier.Text,
            _ => "<unknown>"
        };

        var location = invocation.GetLocation();
        var lineSpan = location.GetLineSpan();
        var filePath = lineSpan.Path;

        return $"{memberName} - {filePath}:{lineSpan.StartLinePosition.Line + 1}";
    }

    private static void Execute(SourceProductionContext context, ImmutableArray<MethodCallInfo> methodCalls)
    {
        // Group by containing file
        foreach (var fileGroup in methodCalls.GroupBy(m => m.FilePath))
        {
            var className = $"EfCoreTaggingInterceptors_{SanitizeFileName(fileGroup.Key)}";

            using var stringWriter = new StringWriter();
            using var writer = new IndentedTextWriter(stringWriter, "    ");
            // Add the InterceptsLocationAttribute
            writer.WriteLine("using System;");
            writer.WriteLine("using System.Collections.Generic;");
            writer.WriteLine("using System.Diagnostics;");
            writer.WriteLine("using System.Linq;");
            writer.WriteLine("using System.Linq.Expressions;");
            writer.WriteLine("using System.Threading;");
            writer.WriteLine("using System.Threading.Tasks;");
            writer.WriteLine("using Microsoft.EntityFrameworkCore;");
            writer.WriteLine();

            writer.WriteLine("#nullable enable");
            writer.WriteLine("namespace System.Runtime.CompilerServices");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("[Conditional(\"DEBUG\")]");
            writer.WriteLine("[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]");
            writer.WriteLine("sealed file class InterceptsLocationAttribute : Attribute");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("public InterceptsLocationAttribute(int version, string data)");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("_ = version;");
            writer.WriteLine("_ = data;");
            writer.Indent--;
            writer.WriteLine("}");
            writer.Indent--;
            writer.WriteLine("}");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine();
            writer.WriteLine("namespace EfCoreTagging");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("static file class " + className);
            writer.WriteLine("{");
            writer.Indent++;

            // Generate interceptors for each method call
            var methodIndex = 0;
            foreach (var methodCall in fileGroup.Where(i => i != null))
            {
                GenerateInterceptorMethod(writer, methodCall, methodIndex++);
            }

            writer.Indent--;
            writer.WriteLine("}");
            writer.Indent--;
            writer.WriteLine("}");

            context.AddSource($"{className}.g.cs", SourceText.From(stringWriter.ToString(), Encoding.UTF8));
        }
    }

private static void GenerateInterceptorMethod(IndentedTextWriter writer, MethodCallInfo methodCall, int methodIndex)
{
    var methodSymbol = methodCall.MethodSymbol;
    var signature = methodCall.Signature;

    // Create a unique method name
    var methodName = $"Intercept_{methodSymbol.Name}_{methodIndex}";

    // Add the interceptor attribute
    writer.WriteLine("[DebuggerStepThrough]");
    writer.WriteLine(
        $"[global::System.Runtime.CompilerServices.InterceptsLocation({methodCall.InterceptableLocation.Version}, \"{methodCall.InterceptableLocation.Data}\")] // {methodCall.DisplayLocation}");

    // Begin method declaration with the signature 
    writer.Write($"public static {signature.ReturnType} {methodName}{signature.TypeParameters}(");
    writer.WriteLine($"{signature.Parameters})");

    // Add type parameter constraints if any
    if (!string.IsNullOrEmpty(signature.Constraints))
    {
        writer.WriteLine(signature.Constraints);
    }

    // Begin method body
    writer.WriteLine("{");
    writer.Indent++;

    // Always use 'source' as the source parameter name in EF Core extension methods
    // The first parameter is always the IQueryable<T> source in EF Core methods
    writer.WriteLine("var taggedSource = source.TagWith(");
    writer.WriteLine("\"\"\"");
    var methodCallFullMethodCall = string.Join(string.Empty,
        methodCall.FullMethodCall.Split('\n').Select(i => i.Trim()).ToArray());
    writer.WriteLine($"{methodCallFullMethodCall.Replace("\n", "").Replace("\r", "")}");
    writer.WriteLine($"    at {methodCall.CallerInfo}");
    writer.WriteLine("\"\"\");");
    writer.WriteLine();

    // Call the original method
    if (signature.ReturnType != "void")
    {
        writer.Write("return ");
    }

    writer.Write("taggedSource.");
    writer.Write(methodSymbol.Name);
    
    // Add type arguments if generic
    if (methodSymbol.IsGenericMethod)
    {
        var typeArgs = string.Join(", ", methodSymbol.TypeParameters.Select(t => t.Name));
        writer.Write($"<{typeArgs}>");
    }

    // Add parameters, skipping the first 'this' parameter which is now 'taggedSource'
    writer.Write("(");
    
    // Get the correct parameters to use
    var parametersToUse = methodSymbol.Parameters;
    if (methodSymbol.ReducedFrom != null)
    {
        parametersToUse = methodSymbol.ReducedFrom.Parameters;
    }
    
    // Skip the first parameter (source) which is now 'taggedSource'
    var parameterStrings = new List<string>();
    for (var i = 1; i < parametersToUse.Length; i++)
    {
        var param = parametersToUse[i];
        parameterStrings.Add(param.Name);
    }
    
    writer.Write(string.Join(", ", parameterStrings));
    writer.WriteLine(");");

    // End method
    writer.Indent--;
    writer.WriteLine("}");
    writer.WriteLine();
}

    private static string SanitizeFileName(string filePath)
    {
        // Convert a file path to a valid part of a C# identifier
        return Path.GetFileNameWithoutExtension(filePath)
            .Replace(".", "_")
            .Replace("-", "_")
            .Replace(" ", "_");
    }


    // Class for storing method signature information
    private class MethodSignature(string returnType, string typeParameters, string parameters, string constraints = "")
    {
        public string ReturnType { get; } = returnType;
        public string TypeParameters { get; } = typeParameters;
        public string Parameters { get; } = parameters;
        public string Constraints { get; } = constraints;
    }

    // Class for storing method call information
    private class MethodCallInfo(
        IMethodSymbol methodSymbol,
        string filePath,
        string displayLocation,
        InterceptableLocation interceptableLocation,
        string fullMethodCall,
        string callerInfo,
        MethodSignature signature)
    {
        public IMethodSymbol MethodSymbol { get; } = methodSymbol;
        public string FilePath { get; } = filePath;
        public string DisplayLocation { get; } = displayLocation;
        public InterceptableLocation InterceptableLocation { get; } = interceptableLocation;
        public string FullMethodCall { get; } = fullMethodCall;
        public string CallerInfo { get; } = callerInfo;
        public MethodSignature Signature { get; } = signature;
    }
}