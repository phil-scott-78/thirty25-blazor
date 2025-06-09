using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace MyLittleContentEngine.MonorailCss;

public static class MonorailServiceExtensions
{
    public static IServiceCollection AddMonorailCss(this IServiceCollection services, MonorailCssOptions? options = null)
    {
        services.AddSingleton(options ?? new MonorailCssOptions());
        services.AddSingleton<CssClassCollector>();
        services.AddSingleton<MonorailCssService>();

        // Register the CSS class collector as a singleton
        services.AddSingleton<CssClassCollector>();

        // Register the MonorailCssService
        services.AddSingleton<MonorailCssService>();

        return services;
    }

    public static WebApplication UseMonorailCss(this WebApplication app, string path = "/styles.css")
    {
        // Ensure the MonorailCssService is available
        if (app.Services.GetService<MonorailCssService>() is null)
        {
            throw new InvalidOperationException(
                "MonorailCssService is not registered. Please call AddMonorailCss() in ConfigureServices.");
        }

        // Ensure the CssClassCollector is available
        if (app.Services.GetService<CssClassCollector>() is null)
        {
            throw new InvalidOperationException(
                "CssClassCollector is not registered. Please call AddMonorailCss() in ConfigureServices.");
        }

        // Custom CSS. The Blazor Static service will discover the mapped URL automatically
        // and include it with the static generation.
        app.UseMiddleware<CssClassCollectorMiddleware>();
        app.MapGet(path, (MonorailCssService cssService) => Results.Content(cssService.GetStyleSheet(), "text/css"));

        return app;
    }
}