using MacroSutra.App.Services;
using MacroSutra.SDK;
using MacroSutra.UI.Services;
using Microsoft.Extensions.Logging;
using MudBlazor.Services;

namespace MacroSutra.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddMudServices();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        // MacroSutra SDK client factory
        var baseUrl = "https://localhost:5301";
        builder.Services.AddSingleton(new MacroSutraClientFactory(baseUrl));

        // UI abstractions — MAUI implementations
        builder.Services.AddSingleton<MauiAuthProvider>();
        builder.Services.AddSingleton<IAuthProvider>(sp => sp.GetRequiredService<MauiAuthProvider>());
        builder.Services.AddTransient<IDataProvider, SdkDataProvider>();

        return builder.Build();
    }
}
