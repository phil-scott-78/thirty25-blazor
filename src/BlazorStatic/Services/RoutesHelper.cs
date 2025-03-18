using Microsoft.AspNetCore.Components;
using System.Reflection;
using BlazorStatic.Models;
using Microsoft.AspNetCore.Components.Endpoints;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;

namespace BlazorStatic.Services;

/// <summary>
/// Service for discovering and processing routes in a Blazor application.
/// Handles both component-based routes and MapGet-style endpoints.
/// </summary>
/// <remarks>
/// This code is adapted from: https://andrewlock.net/finding-all-routable-components-in-a-webassembly-app/
/// with modifications to support static site generation needs.
/// </remarks>
internal class RoutesHelperService
{
    private readonly EndpointDataSource _endpointDataSource;

    /// <summary>
    /// Initializes a new instance of the <see cref="RoutesHelperService"/> class.
    /// </summary>
    /// <param name="endpointDataSource">The endpoint data source to extract route information from.</param>
    public RoutesHelperService(EndpointDataSource endpointDataSource)
    {
        _endpointDataSource = endpointDataSource;
    }

    /// <summary>
    /// Discovers all static routes from Blazor components in the specified assembly.
    /// </summary>
    /// <param name="assembly">The assembly containing Blazor components to analyze.</param>
    /// <returns>A list of route templates (as strings) that don't contain parameters.</returns>
    /// <remarks>
    /// Only includes routes from components that:
    /// - Have a <see cref="RouteAttribute"/>
    /// - Don't have route parameters (no '{parameter}' in route template)
    /// </remarks>
    public List<string> GetRoutesToRender(Assembly assembly)
    {
        // Get all the components whose base class is ComponentBase
        var components = assembly
            .ExportedTypes
            .Where(t => t.IsSubclassOf(typeof(ComponentBase)));

        // get all the routes that don't contain parameters
        List<string> routes = components
            .Select(GetRouteFromComponent)
            .Where(route => route is not null)
            .ToList()!; // Previous null check guarantees no nulls

        return routes;
    }

    private static string? GetRouteFromComponent(Type component)
    {
        var attributes = component.GetCustomAttributes(true);

        // Can't work with parameterized pages (such pages have params defined with {paramName})
        return attributes.OfType<RouteAttribute>()
            .FirstOrDefault(x => !x.Template.Contains('{'))?.Template;
    }
    
    /// <summary>
    /// Discovers all statically renderable GET routes defined using endpoint routing.
    /// </summary>
    /// <returns>
    /// A collection of <see cref="PageToGenerate"/> objects representing routes that can be statically generated.
    /// </returns>
    public IEnumerable<PageToGenerate> GetMapGetRoutes()
    {
        var endpoints = _endpointDataSource.Endpoints;

        var getRoutes = endpoints
            .OfType<RouteEndpoint>() // Move type checking earlier to filter more quickly
            .Where(endpoint => 
                // Then check metadata - HTTP method first as it's most likely to filter out endpoints
                endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains("GET") == true &&
                endpoint.Metadata.GetMetadata<PageActionDescriptor>() == null &&
                endpoint.Metadata.GetMetadata<ComponentTypeMetadata>() == null &&
                endpoint.RoutePattern.RawText != null && !string.IsNullOrWhiteSpace(endpoint.RoutePattern.RawText) && !endpoint.RoutePattern.RawText.Contains("_framework") &&
                endpoint.DisplayName?.Contains("static files") != true &&
                // FallbackMetadata isn't public, so check by name
                endpoint.Metadata.All(metadata => metadata.GetType().Name != "FallbackMetadata"))
            .Select(endpoint => endpoint.RoutePattern.RawText!);
        

        foreach (var route in getRoutes)
        {
            yield return new PageToGenerate(route, route);
        }
    }
}