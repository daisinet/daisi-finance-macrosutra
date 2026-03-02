using MacroSutra.Core.Enums;
using MacroSutra.Core.Models;
using MacroSutra.Services;
using Microsoft.AspNetCore.Mvc;

namespace MacroSutra.Web.Services;

public static class MacroSutraApiEndpoints
{
    public static void MapMacroSutraApiEndpoints(this WebApplication app)
    {
        var api = app.MapGroup("/api").AddEndpointFilter<ApiKeyAuthFilter>();

        // ── Users ──

        api.MapGet("/users/me", async (UserManagementService userSvc, HttpContext ctx) =>
        {
            var accountId = ctx.Items["accountId"] as string;
            var userId = ctx.Items["userId"] as string;
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(accountId))
                return Results.Unauthorized();

            var user = await userSvc.GetUserByDaisinetIdAsync(userId, accountId);
            return user != null ? Results.Ok(user) : Results.NotFound();
        });

        api.MapGet("/users", async (UserManagementService userSvc, HttpContext ctx) =>
        {
            var accountId = ctx.Items["accountId"] as string;
            var users = await userSvc.GetUsersAsync(accountId!);
            return Results.Ok(users);
        });

        // ── Strategies ──

        api.MapGet("/strategies", async (StrategyService svc, HttpContext ctx) =>
        {
            var accountId = ctx.Items["accountId"] as string;
            var strategies = await svc.GetStrategiesAsync(accountId!);
            return Results.Ok(strategies);
        });

        api.MapGet("/strategies/{id}", async (string id, StrategyService svc, HttpContext ctx) =>
        {
            var accountId = ctx.Items["accountId"] as string;
            var strategy = await svc.GetStrategyAsync(id, accountId!);
            return strategy != null ? Results.Ok(strategy) : Results.NotFound();
        });

        api.MapPost("/strategies", async ([FromBody] TradingStrategy strategy, StrategyService svc, HttpContext ctx) =>
        {
            var accountId = ctx.Items["accountId"] as string;
            strategy.AccountId = accountId!;
            var created = await svc.CreateStrategyAsync(strategy);
            return Results.Created($"/api/strategies/{created.id}", created);
        });

        api.MapPut("/strategies/{id}", async (string id, [FromBody] TradingStrategy strategy, StrategyService svc, HttpContext ctx) =>
        {
            var accountId = ctx.Items["accountId"] as string;
            strategy.id = id;
            strategy.AccountId = accountId!;
            var updated = await svc.UpdateStrategyAsync(strategy);
            return Results.Ok(updated);
        });

        api.MapDelete("/strategies/{id}", async (string id, StrategyService svc, HttpContext ctx) =>
        {
            var accountId = ctx.Items["accountId"] as string;
            await svc.DeleteStrategyAsync(id, accountId!);
            return Results.NoContent();
        });

        // ── Trades ──

        api.MapGet("/trades", async (StrategyService strategySvc, TradeService svc, HttpContext ctx) =>
        {
            var accountId = ctx.Items["accountId"] as string;

            string? symbol = ctx.Request.Query["symbol"].FirstOrDefault();
            TradeStatus? status = null;
            if (ctx.Request.Query.TryGetValue("status", out var statusVal) && Enum.TryParse<TradeStatus>(statusVal, true, out var parsed))
                status = parsed;
            string? strategyId = ctx.Request.Query["strategyId"].FirstOrDefault();

            var trades = await svc.GetTradesAsync(accountId!, symbol, status, strategyId);
            return Results.Ok(trades);
        });

        api.MapGet("/trades/{id}", async (string id, TradeService svc, HttpContext ctx) =>
        {
            var accountId = ctx.Items["accountId"] as string;
            var trade = await svc.GetTradeAsync(id, accountId!);
            return trade != null ? Results.Ok(trade) : Results.NotFound();
        });

        // ── Portfolio ──

        api.MapGet("/portfolio/accounts", async (PortfolioService svc, HttpContext ctx) =>
        {
            var accountId = ctx.Items["accountId"] as string;
            var accounts = await svc.GetBrokerageAccountsAsync(accountId!);
            return Results.Ok(accounts);
        });

        api.MapGet("/portfolio/positions", async (PortfolioService svc, HttpContext ctx) =>
        {
            var accountId = ctx.Items["accountId"] as string;
            string? brokerageAccountId = ctx.Request.Query["brokerageAccountId"].FirstOrDefault();
            var positions = await svc.GetPositionsAsync(accountId!, brokerageAccountId);
            return Results.Ok(positions);
        });
    }
}
