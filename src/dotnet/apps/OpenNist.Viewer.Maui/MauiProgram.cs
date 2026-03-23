namespace OpenNist.Viewer.Maui;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Hosting;
using Services;

internal static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>();

        builder.Services.AddSingleton<ViewerImageService>();
        builder.Services.AddSingleton<MainPage>();

        return builder.Build();
    }
}
