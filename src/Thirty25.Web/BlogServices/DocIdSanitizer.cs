using System.Text;

namespace Thirty25.Web.BlogServices;

public class DocIdSanitizer
{
    public static string SanitizeXmlDocId(string xmlDocId)
    {
        // If the xmlDocId doesn't have parameters, return it as is
        var paramStart = xmlDocId.IndexOf('(');
        if (paramStart == -1)
            return xmlDocId;

        var paramEnd = xmlDocId.LastIndexOf(')');
        if (paramEnd == -1)
            return xmlDocId;

        // Extract the parts
        var prefix = xmlDocId[..(paramStart + 1)]; // Include the opening parenthesis
        var parameters = xmlDocId.Substring(paramStart + 1, paramEnd - paramStart - 1);
        var suffix = xmlDocId[paramEnd..]; // This will be just ')'

        // If there are no parameters, return original
        if (string.IsNullOrWhiteSpace(parameters))
            return xmlDocId;

        // Parse and sanitize the parameters
        var sanitizedParams = new List<string>();
        var currentParam = new StringBuilder();
        var nestedBraces = 0;

        foreach (var c in parameters)
        {
            switch (c)
            {
                case '{':
                    nestedBraces++;
                    currentParam.Append(c);
                    break;
                case '}':
                    nestedBraces--;
                    currentParam.Append(c);
                    break;
                case ',' when nestedBraces == 0:
                    // Complete the current parameter and start a new one
                    sanitizedParams.Add(SanitizeParameterType(currentParam.ToString()));
                    currentParam.Clear();
                    break;
                default:
                    currentParam.Append(c);
                    break;
            }
        }

        // Add the last parameter
        if (currentParam.Length > 0)
        {
            sanitizedParams.Add(SanitizeParameterType(currentParam.ToString()));
        }

        // Reconstruct the xmlDocId
        return prefix + string.Join(",", sanitizedParams) + suffix;
    }

    private static string SanitizeParameterType(string paramType)
    {
        paramType = paramType.Trim();

        // Handle generic parameters with nested types. No curly brackets we can return.
        if (!paramType.Contains('{') || !paramType.Contains('}'))
        {
            return RemoveNamespace(paramType);
        }
        
        var genericStart = paramType.IndexOf('{');
        var genericEnd = paramType.LastIndexOf('}') + 1; // Include the closing brace

        // Extract parts
        var beforeGeneric = paramType[..genericStart];
        var genericPart = paramType.Substring(genericStart, genericEnd - genericStart);
        var afterGeneric = string.Empty;

        if (genericEnd < paramType.Length)
        {
            afterGeneric = paramType[genericEnd..];
        }

        // Handle the main type (before generic)
        beforeGeneric = RemoveNamespace(beforeGeneric);

        // Handle the generic parameters recursively
        var innerGeneric = genericPart.Substring(1, genericPart.Length - 2); // Remove { }
        var innerParams = SplitGenericParams(innerGeneric);

        for (var i = 0; i < innerParams.Length; i++)
        {
            innerParams[i] = SanitizeParameterType(innerParams[i]);
        }

        return $"{beforeGeneric}{{{string.Join(",", innerParams)}}}{afterGeneric}";

    }

    private static string[] SplitGenericParams(string genericParams)
    {
        var result = new List<string>();
        var currentParam = new StringBuilder();
        var nestedBraces = 0;

        foreach (var c in genericParams)
        {
            switch (c)
            {
                case '{':
                    nestedBraces++;
                    currentParam.Append(c);
                    break;
                case '}':
                    nestedBraces--;
                    currentParam.Append(c);
                    break;
                case ',' when nestedBraces == 0:
                    result.Add(currentParam.ToString());
                    currentParam.Clear();
                    break;
                default:
                    currentParam.Append(c);
                    break;
            }
        }

        if (currentParam.Length > 0)
        {
            result.Add(currentParam.ToString());
        }

        return result.ToArray();
    }

    private static string RemoveNamespace(string typeName)
    {
        // Remove namespace and keep only the type name
        var lastDotIndex = typeName.LastIndexOf('.');

        return lastDotIndex != -1 
            ? typeName[(lastDotIndex + 1)..] 
            : typeName;
    }
}