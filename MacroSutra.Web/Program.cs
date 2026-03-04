using System.Text.Json.Serialization;
using MacroSutra.Brokers;
using MacroSutra.Data;
using MacroSutra.Services;
using MacroSutra.UI.Services;
using MacroSutra.Web.Components;
using MacroSutra.Web.Services;
using Daisi.SDK.Models;
using Daisi.SDK.Web.Extensions;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();
builder.Services.AddHttpClient();

// Daisi SSO authentication
builder.Services.AddDaisiForWeb()
                .AddDaisiMiddleware()
                .AddDaisiCookieKeyProvider();

// MacroSutra data layer (Singleton — thread-safe Cosmos client)
builder.Services.AddSingleton<MacroSutraCosmo>(sp =>
    new MacroSutraCosmo(builder.Configuration));

// MacroSutra services (Scoped — one per Blazor circuit)
builder.Services.AddScoped<UserManagementService>();
builder.Services.AddScoped<UserContext>();
builder.Services.AddScoped<StrategyService>();
builder.Services.AddScoped<TradeService>();
builder.Services.AddScoped<PortfolioService>();
builder.Services.AddScoped<PositionSyncService>();
builder.Services.AddScoped<SubscriptionService>();

// Phase 7: Strategy templates
builder.Services.AddSingleton<StrategyTemplateService>();

// Phase 3: Strategy evaluation engine
builder.Services.AddSingleton<MarketDataService>();
builder.Services.AddScoped<ConditionEvaluator>();
builder.Services.AddScoped<TradeExecutionService>();
builder.Services.AddHostedService<StrategyEvaluationService>();
builder.Services.AddHostedService<OrderStatusTracker>();

// Phase 4: Backtesting engine
builder.Services.AddScoped<BacktestEngine>();
builder.Services.AddScoped<BacktestService>();
builder.Services.AddScoped<WalkForwardService>();

// Phase 5: Community features
builder.Services.AddScoped<CommunityService>();
builder.Services.AddMemoryCache();

// Phase 6: Subscription dispatch and notifications
builder.Services.AddScoped<SubscriptionDispatchService>();
builder.Services.AddScoped<EmailNotificationService>();
builder.Services.AddScoped<WebhookDispatchService>();
builder.Services.AddScoped<PushNotificationService>();
builder.Services.AddHttpClient("Webhook", c => c.Timeout = TimeSpan.FromSeconds(10));
builder.Services.AddHttpClient("SendGrid", c => c.Timeout = TimeSpan.FromSeconds(10));
builder.Services.AddHttpClient("FCM", c => c.Timeout = TimeSpan.FromSeconds(10));

// Brokers
builder.Services.AddSingleton<PaperBrokerageProvider>();
builder.Services.AddSingleton<AlpacaBrokerageProvider>();
builder.Services.AddSingleton<WebullBrokerageProvider>();
builder.Services.AddScoped<BrokerageProviderFactory>();
builder.Services.AddHttpClient("Webull", c => c.Timeout = TimeSpan.FromSeconds(30));

// UI abstractions — Web implementations
builder.Services.AddScoped<IAuthProvider, WebAuthProvider>();
builder.Services.AddScoped<IDataProvider, WebDataProvider>();

// JSON enum serialization for API endpoints
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();
app.UseDaisiMiddleware();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();
app.MapStaticAssets();

// MacroSutra API endpoints (client-key authenticated)
app.MapMacroSutraApiEndpoints();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(MacroSutra.UI.Services.IAuthProvider).Assembly);

DaisiStaticSettings.LoadFromConfiguration(builder.Configuration.AsEnumerable().ToDictionary(keySelector: x => x.Key, elementSelector: x => x.Value));

app.Run();
