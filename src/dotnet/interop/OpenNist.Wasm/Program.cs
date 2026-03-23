namespace OpenNist.Wasm;

using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

internal static class Program
{
    private static Task Main(string[] args)
    {
        var builder = WebAssemblyHostBuilder.CreateDefault(args);
        return builder.Build().RunAsync();
    }
}
