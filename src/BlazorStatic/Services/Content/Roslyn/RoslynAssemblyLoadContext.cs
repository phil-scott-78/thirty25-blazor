using System.Reflection;
using System.Runtime.Loader;

namespace BlazorStatic.Services.Content.Roslyn;

internal class RoslynAssemblyLoadContext() : AssemblyLoadContext(isCollectible: true)
{
    protected override Assembly? Load(AssemblyName assemblyName) => null;
}
