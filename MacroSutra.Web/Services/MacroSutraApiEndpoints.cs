using System.Text.Json;
using MacroSutra.Brokers;
using MacroSutra.Core.Enums;
using MacroSutra.Core.Models;
using MacroSutra.Services;
using Microsoft.AspNetCore.Mvc;

namespace MacroSutra.Web.Services;

public static class MacroSutraApiEndpoints
{
    public static void MapMacroSutraApiEndpoints(this WebApplication app)
    {
        // ── Public community endpoints (no auth required) ──
        var community = app.MapGroup("/api/community");

        community.MapGet("/strategies", async (CommunityService svc, HttpContext ctx) =>
        {
            int page = int.TryParse(ctx.Request.Query["page"], out var p) ? p : 0;
            int pageSize = int.TryParse(ctx.Request.Query["pageSize"], out var ps) ? ps : 20;
            string? sortBy = ctx.Request.Query["sortBy"].FirstOrDefault();
            var strategies = await svc.GetPublicStrategiesAsync(page, pageSize, sortBy);
            return Results.Ok(strategies);
        });

        community.MapGet("/strategies/{id}", async (string id, CommunityService svc) =>
        {
            var strategy = await svc.GetPublicStrategyAsync(id);
            if (strategy == null) return Results.NotFound();

            var stats = await svc.GetCommunityStatsAsync(id);
            var reviews = await svc.GetReviewsAsync(id);
            return Results.Ok(new { Strategy = strategy, Stats = stats, Reviews = reviews });
        });

        community.MapGet("/leaderboard", async (CommunityService svc, HttpContext ctx) =>
        {
            string sortBy = ctx.Request.Query["sortBy"].FirstOrDefault() ?? "sharpe";
            int limit = int.TryParse(ctx.Request.Query["limit"], out var l) ? l : 25;
            var entries = await svc.GetLeaderboardAsync(sortBy, limit);
            return Results.Ok(entries);
        });

        // ── Authenticated endpoints ──
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

        api.MapGet("/strategies/templates", (StrategyTemplateService templateSvc) =>
        {
            var templates = templateSvc.GetTemplates();
            return Results.Ok(templates);
        });

        api.MapGet("/strategies/templates/{id}", (string id, StrategyTemplateService templateSvc) =>
        {
            var template = templateSvc.GetTemplate(id);
            return template != null ? Results.Ok(template) : Results.NotFound();
        });

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

        api.MapPost("/strategies/{id}/evaluate", async (string id, StrategyService svc, StrategyEvaluationService evalService, HttpContext ctx) =>
        {
            var accountId = ctx.Items["accountId"] as string;
            var strategy = await svc.GetStrategyAsync(id, accountId!);
            if (strategy == null) return Results.NotFound();
            var result = await evalService.EvaluateSingleAsync(strategy);
            return Results.Ok(result);
        });

        api.MapPost("/strategies/{id}/activate", async (string id, StrategyService svc, HttpContext ctx) =>
        {
            var accountId = ctx.Items["accountId"] as string;
            var strategy = await svc.GetStrategyAsync(id, accountId!);
            if (strategy == null) return Results.NotFound();
            var result = await svc.ActivateStrategyAsync(id, accountId!);
            return Results.Ok(result);
        });

        api.MapPost("/strategies/{id}/deactivate", async (string id, StrategyService svc, HttpContext ctx) =>
        {
            var accountId = ctx.Items["accountId"] as string;
            var strategy = await svc.GetStrategyAsync(id, accountId!);
            if (strategy == null) return Results.NotFound();
            var result = await svc.DeactivateStrategyAsync(id, accountId!);
            return Results.Ok(result);
        });

        api.MapPost("/strategies/{id}/fork", async (string id, CommunityService commSvc, HttpContext ctx) =>
        {
            var accountId = ctx.Items["accountId"] as string;
            var userId = ctx.Items["userId"] as string;
            var forked = await commSvc.ForkStrategyAsync(id, accountId!, userId!);
            return Results.Created($"/api/strategies/{forked.id}", forked);
        });

        api.MapPost("/strategies/{id}/reviews", async (string id, [FromBody] JsonElement body, CommunityService commSvc, HttpContext ctx) =>
        {
            var accountId = ctx.Items["accountId"] as string;
            var userId = ctx.Items["userId"] as string;
            var userName = ctx.Items["userName"] as string ?? "";
            var rating = body.GetProperty("rating").GetInt32();
            var text = body.TryGetProperty("text", out var t) ? t.GetString() : null;
            var review = await commSvc.CreateReviewAsync(id, accountId!, userId!, userName, rating, text);
            return Results.Created($"/api/strategies/{id}/reviews/{review.id}", review);
        });

        api.MapPut("/strategies/{id}/reviews/{reviewId}", async (string id, string reviewId, [FromBody] JsonElement body, CommunityService commSvc, HttpContext ctx) =>
        {
            var accountId = ctx.Items["accountId"] as string;
            var rating = body.GetProperty("rating").GetInt32();
            var text = body.TryGetProperty("text", out var t) ? t.GetString() : null;
            var updated = await commSvc.UpdateReviewAsync(reviewId, id, accountId!, rating, text);
            return Results.Ok(updated);
        });

        api.MapDelete("/strategies/{id}/reviews/{reviewId}", async (string id, string reviewId, CommunityService commSvc, HttpContext ctx) =>
        {
            var accountId = ctx.Items["accountId"] as string;
            await commSvc.DeleteReviewAsync(reviewId, id, accountId!);
            return Results.NoContent();
        });

        // ── Strategy Performance ──

        api.MapGet("/strategies/{id}/performance", async (string id, StrategyPerformanceService perfSvc, HttpContext ctx) =>
        {
            var accountId = ctx.Items["accountId"] as string;
            var summary = await perfSvc.GetPerformanceSummaryAsync(accountId!, id);
            return Results.Ok(summary);
        });

        api.MapGet("/strategies/{id}/triggers", async (string id, StrategyPerformanceService perfSvc, HttpContext ctx) =>
        {
            var accountId = ctx.Items["accountId"] as string;
            var triggers = await perfSvc.GetTriggerHistoryAsync(accountId!, id);
            return Results.Ok(triggers);
        });

        // ── Subscriptions ──

        api.MapGet("/subscriptions", async (SubscriptionService svc, HttpContext ctx) =>
        {
            var accountId = ctx.Items["accountId"] as string;
            var subscriptions = await svc.GetSubscriptionsBySubscriberAsync(accountId!);
            return Results.Ok(subscriptions);
        });

        api.MapGet("/subscriptions/publisher", async (SubscriptionService svc, HttpContext ctx) =>
        {
            var accountId = ctx.Items["accountId"] as string;
            var subscriptions = await svc.GetPublisherSubscriptionsAsync(accountId!);
            return Results.Ok(subscriptions);
        });

        api.MapGet("/subscriptions/{id}", async (string id, SubscriptionService svc, HttpContext ctx) =>
        {
            var accountId = ctx.Items["accountId"] as string;
            var subscription = await svc.GetSubscriptionAsync(id, accountId!);
            return subscription != null ? Results.Ok(subscription) : Results.NotFound();
        });

        api.MapPost("/subscriptions", async ([FromBody] Subscription subscription, SubscriptionService svc, HttpContext ctx) =>
        {
            var accountId = ctx.Items["accountId"] as string;
            var userId = ctx.Items["userId"] as string;
            subscription.AccountId = accountId!;
            subscription.SubscriberUserId = userId!;
            var created = await svc.SubscribeAsync(subscription);
            return Results.Created($"/api/subscriptions/{created.id}", created);
        });

        api.MapDelete("/subscriptions/{id}", async (string id, SubscriptionService svc, HttpContext ctx) =>
        {
            var accountId = ctx.Items["accountId"] as string;
            await svc.CancelSubscriptionAsync(id, accountId!);
            return Results.NoContent();
        });

        api.MapGet("/subscriptions/{id}/actions", async (string id, SubscriptionService svc, HttpContext ctx) =>
        {
            var accountId = ctx.Items["accountId"] as string;
            var actions = await svc.GetSubscriptionActionsAsync(accountId!, id);
            return Results.Ok(actions);
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

        api.MapPost("/portfolio/accounts", async ([FromBody] BrokerageAccount account, PortfolioService svc, HttpContext ctx) =>
        {
            var accountId = ctx.Items["accountId"] as string;
            account.AccountId = accountId!;
            var created = await svc.ValidateAndCreateBrokerageAccountAsync(account);
            // Strip credentials from response
            created.CredentialData = "";
            created.CredentialRef = "";
            return Results.Created($"/api/portfolio/accounts/{created.id}", created);
        });

        api.MapPut("/portfolio/accounts/{id}", async (string id, [FromBody] BrokerageAccount account, PortfolioService svc, HttpContext ctx) =>
        {
            var accountId = ctx.Items["accountId"] as string;
            account.id = id;
            account.AccountId = accountId!;
            var updated = await svc.UpdateBrokerageAccountAsync(account);
            // Strip credentials from response
            updated.CredentialData = "";
            updated.CredentialRef = "";
            return Results.Ok(updated);
        });

        api.MapPost("/portfolio/accounts/{id}/sync", async (string id, PortfolioService portfolioSvc, PositionSyncService syncSvc, HttpContext ctx) =>
        {
            var accountId = ctx.Items["accountId"] as string;
            var account = await portfolioSvc.GetBrokerageAccountAsync(id, accountId!);
            if (account == null) return Results.NotFound();
            var result = await syncSvc.SyncAccountAsync(account);
            return Results.Ok(result);
        });

        api.MapPost("/portfolio/sync", async (PositionSyncService syncSvc, HttpContext ctx) =>
        {
            var accountId = ctx.Items["accountId"] as string;
            var results = await syncSvc.SyncAllAccountsAsync(accountId!);
            return Results.Ok(results);
        });

        api.MapDelete("/portfolio/accounts/{id}", async (string id, PortfolioService svc, HttpContext ctx) =>
        {
            var accountId = ctx.Items["accountId"] as string;
            await svc.DeactivateBrokerageAccountAsync(id, accountId!);
            return Results.NoContent();
        });

        // ── Push Tokens ──

        api.MapPost("/push-tokens", async ([FromBody] PushToken token, PushNotificationService pushSvc, HttpContext ctx) =>
        {
            var accountId = ctx.Items["accountId"] as string;
            var userId = ctx.Items["userId"] as string;
            token.AccountId = accountId!;
            token.UserId = userId!;
            var created = await pushSvc.RegisterTokenAsync(token);
            return Results.Created($"/api/push-tokens/{created.id}", created);
        });

        api.MapDelete("/push-tokens/{id}", async (string id, PushNotificationService pushSvc, HttpContext ctx) =>
        {
            var accountId = ctx.Items["accountId"] as string;
            await pushSvc.RemoveTokenAsync(id, accountId!);
            return Results.NoContent();
        });

        // ── Backtests ──

        api.MapPost("/backtests", async ([FromBody] JsonElement body, BacktestService svc, HttpContext ctx) =>
        {
            var accountId = ctx.Items["accountId"] as string;
            var userId = ctx.Items["userId"] as string;

            var strategyId = body.GetProperty("strategyId").GetString()!;
            var symbol = body.GetProperty("symbol").GetString()!;
            var startDate = DateOnly.Parse(body.GetProperty("startDate").GetString()!);
            var endDate = DateOnly.Parse(body.GetProperty("endDate").GetString()!);
            var initialCapital = body.GetProperty("initialCapital").GetDecimal();
            var slippageBps = body.TryGetProperty("slippageBps", out var sb) ? sb.GetDecimal() : 0;
            var commissionPerTrade = body.TryGetProperty("commissionPerTrade", out var cpt) ? cpt.GetDecimal() : 0;
            var timeFrame = body.TryGetProperty("timeFrame", out var tf) ? tf.GetString() : null;

            var result = await svc.CreateAndRunBacktestAsync(strategyId, accountId!, userId!, symbol, startDate, endDate, initialCapital, slippageBps, commissionPerTrade, timeFrame);
            return Results.Created($"/api/backtests/{result.id}", result);
        });

        api.MapPost("/backtests/walk-forward", async ([FromBody] JsonElement body, WalkForwardService walkForwardSvc, HttpContext ctx) =>
        {
            var accountId = ctx.Items["accountId"] as string;
            var userId = ctx.Items["userId"] as string;

            var strategyId = body.GetProperty("strategyId").GetString()!;
            var symbol = body.GetProperty("symbol").GetString()!;
            var startDate = DateOnly.Parse(body.GetProperty("startDate").GetString()!);
            var endDate = DateOnly.Parse(body.GetProperty("endDate").GetString()!);
            var initialCapital = body.GetProperty("initialCapital").GetDecimal();
            var inSampleDays = body.TryGetProperty("inSampleDays", out var isd) ? isd.GetInt32() : 252;
            var outOfSampleDays = body.TryGetProperty("outOfSampleDays", out var oosd) ? oosd.GetInt32() : 63;
            var slippageBps = body.TryGetProperty("slippageBps", out var sb) ? sb.GetDecimal() : 0;
            var commissionPerTrade = body.TryGetProperty("commissionPerTrade", out var cpt) ? cpt.GetDecimal() : 0;

            var result = await walkForwardSvc.RunAsync(strategyId, accountId!, userId!, symbol, startDate, endDate, initialCapital, inSampleDays, outOfSampleDays, slippageBps, commissionPerTrade);
            return Results.Ok(result);
        });

        api.MapGet("/backtests", async (BacktestService svc, HttpContext ctx) =>
        {
            var accountId = ctx.Items["accountId"] as string;
            string? strategyId = ctx.Request.Query["strategyId"].FirstOrDefault();
            var backtests = await svc.GetBacktestsAsync(accountId!, strategyId);
            return Results.Ok(backtests);
        });

        api.MapGet("/backtests/{id}", async (string id, BacktestService svc, HttpContext ctx) =>
        {
            var accountId = ctx.Items["accountId"] as string;
            var backtest = await svc.GetBacktestAsync(id, accountId!);
            return backtest != null ? Results.Ok(backtest) : Results.NotFound();
        });

        api.MapDelete("/backtests/{id}", async (string id, BacktestService svc, HttpContext ctx) =>
        {
            var accountId = ctx.Items["accountId"] as string;
            await svc.DeleteBacktestAsync(id, accountId!);
            return Results.NoContent();
        });

        // ── Trade Export ──

        api.MapGet("/trades/export", async (TradeExportService exportSvc, HttpContext ctx) =>
        {
            var accountId = ctx.Items["accountId"] as string;
            string? symbol = ctx.Request.Query["symbol"].FirstOrDefault();
            string? strategyId = ctx.Request.Query["strategyId"].FirstOrDefault();
            string format = ctx.Request.Query["format"].FirstOrDefault() ?? "csv";

            if (format == "pdf")
            {
                var pdf = await exportSvc.ExportPdfAsync(accountId!, symbol, strategyId);
                return Results.File(pdf, "application/pdf", "trades.pdf");
            }

            var csv = await exportSvc.ExportCsvAsync(accountId!, symbol, strategyId);
            return Results.File(csv, "text/csv", "trades.csv");
        });

        // ── Historical Market Data ──

        api.MapGet("/market/bars", async (MarketDataService marketSvc, HttpContext ctx) =>
        {
            string symbol = ctx.Request.Query["symbol"].FirstOrDefault() ?? "";
            var from = DateOnly.TryParse(ctx.Request.Query["from"].FirstOrDefault(), out var f) ? f : DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-1));
            var to = DateOnly.TryParse(ctx.Request.Query["to"].FirstOrDefault(), out var t) ? t : DateOnly.FromDateTime(DateTime.UtcNow);
            var timeFrame = ctx.Request.Query["timeFrame"].FirstOrDefault() ?? "Day";
            var barTimeFrame = Enum.TryParse<BarTimeFrame>(timeFrame, true, out var tf) ? tf : BarTimeFrame.Day;
            var bars = await marketSvc.GetHistoricalBarsAsync(symbol, from, to, barTimeFrame);
            return Results.Ok(bars);
        });

        // ── DCA Schedules ──

        api.MapGet("/dca", async (DcaService dcaSvc, HttpContext ctx) =>
        {
            var accountId = ctx.Items["accountId"] as string;
            var schedules = await dcaSvc.GetSchedulesAsync(accountId!);
            return Results.Ok(schedules);
        });

        api.MapGet("/dca/{id}", async (string id, DcaService dcaSvc, HttpContext ctx) =>
        {
            var accountId = ctx.Items["accountId"] as string;
            var schedule = await dcaSvc.GetScheduleAsync(id, accountId!);
            return schedule != null ? Results.Ok(schedule) : Results.NotFound();
        });

        api.MapPost("/dca", async ([FromBody] DcaSchedule schedule, DcaService dcaSvc, HttpContext ctx) =>
        {
            var accountId = ctx.Items["accountId"] as string;
            var userId = ctx.Items["userId"] as string;
            schedule.AccountId = accountId!;
            schedule.UserId = userId!;
            var created = await dcaSvc.CreateScheduleAsync(schedule);
            return Results.Created($"/api/dca/{created.id}", created);
        });

        api.MapPut("/dca/{id}", async (string id, [FromBody] DcaSchedule schedule, DcaService dcaSvc, HttpContext ctx) =>
        {
            var accountId = ctx.Items["accountId"] as string;
            schedule.id = id;
            schedule.AccountId = accountId!;
            var updated = await dcaSvc.UpdateScheduleAsync(schedule);
            return Results.Ok(updated);
        });

        api.MapDelete("/dca/{id}", async (string id, DcaService dcaSvc, HttpContext ctx) =>
        {
            var accountId = ctx.Items["accountId"] as string;
            await dcaSvc.DeleteScheduleAsync(id, accountId!);
            return Results.NoContent();
        });

        api.MapPost("/dca/{id}/activate", async (string id, DcaService dcaSvc, HttpContext ctx) =>
        {
            var accountId = ctx.Items["accountId"] as string;
            var schedule = await dcaSvc.GetScheduleAsync(id, accountId!);
            if (schedule == null) return Results.NotFound();
            await dcaSvc.ActivateScheduleAsync(schedule);
            return Results.Ok(schedule);
        });

        api.MapPost("/dca/{id}/deactivate", async (string id, DcaService dcaSvc, HttpContext ctx) =>
        {
            var accountId = ctx.Items["accountId"] as string;
            var schedule = await dcaSvc.GetScheduleAsync(id, accountId!);
            if (schedule == null) return Results.NotFound();
            await dcaSvc.DeactivateScheduleAsync(schedule);
            return Results.Ok(schedule);
        });

        // ── Portfolio Rebalancing ──

        api.MapGet("/portfolio/rebalance", async (RebalanceService rebalanceSvc, HttpContext ctx) =>
        {
            var accountId = ctx.Items["accountId"] as string;
            var targets = await rebalanceSvc.GetTargetsAsync(accountId!);
            return Results.Ok(targets);
        });

        api.MapGet("/portfolio/rebalance/{id}", async (string id, RebalanceService rebalanceSvc, HttpContext ctx) =>
        {
            var accountId = ctx.Items["accountId"] as string;
            var target = await rebalanceSvc.GetTargetAsync(id, accountId!);
            return target != null ? Results.Ok(target) : Results.NotFound();
        });

        api.MapPost("/portfolio/rebalance", async ([FromBody] RebalanceTarget target, RebalanceService rebalanceSvc, HttpContext ctx) =>
        {
            var accountId = ctx.Items["accountId"] as string;
            target.AccountId = accountId!;
            var created = await rebalanceSvc.CreateTargetAsync(target);
            return Results.Created($"/api/portfolio/rebalance/{created.id}", created);
        });

        api.MapPut("/portfolio/rebalance/{id}", async (string id, [FromBody] RebalanceTarget target, RebalanceService rebalanceSvc, HttpContext ctx) =>
        {
            var accountId = ctx.Items["accountId"] as string;
            target.id = id;
            target.AccountId = accountId!;
            var updated = await rebalanceSvc.UpdateTargetAsync(target);
            return Results.Ok(updated);
        });

        api.MapDelete("/portfolio/rebalance/{id}", async (string id, RebalanceService rebalanceSvc, HttpContext ctx) =>
        {
            var accountId = ctx.Items["accountId"] as string;
            await rebalanceSvc.DeleteTargetAsync(id, accountId!);
            return Results.NoContent();
        });

        api.MapGet("/portfolio/rebalance/{id}/analyze", async (string id, RebalanceService rebalanceSvc, HttpContext ctx) =>
        {
            var accountId = ctx.Items["accountId"] as string;
            var analysis = await rebalanceSvc.AnalyzeAsync(id, accountId!);
            return Results.Ok(analysis);
        });

        api.MapPost("/portfolio/rebalance/{id}/execute", async (string id, RebalanceService rebalanceSvc, HttpContext ctx) =>
        {
            var accountId = ctx.Items["accountId"] as string;
            var trades = await rebalanceSvc.ExecuteRebalanceAsync(id, accountId!);
            return Results.Ok(trades);
        });

        // ── Tax-Loss Harvesting ──

        api.MapGet("/portfolio/tax-loss-harvesting", async (TaxLossHarvestingService tlhSvc, HttpContext ctx) =>
        {
            var accountId = ctx.Items["accountId"] as string;
            string? brokerageAccountId = ctx.Request.Query["brokerageAccountId"].FirstOrDefault();
            var report = await tlhSvc.AnalyzeAsync(accountId!, brokerageAccountId);
            return Results.Ok(report);
        });

        // ── Options ──

        api.MapGet("/options/chain", async (PortfolioService portfolioSvc, BrokerageProviderFactory factory, HttpContext ctx) =>
        {
            var accountId = ctx.Items["accountId"] as string;
            string brokerageAccountId = ctx.Request.Query["brokerageAccountId"].FirstOrDefault() ?? "";
            string symbol = ctx.Request.Query["symbol"].FirstOrDefault() ?? "";
            DateOnly? expiration = DateOnly.TryParse(ctx.Request.Query["expiration"].FirstOrDefault(), out var exp) ? exp : null;

            var account = await portfolioSvc.GetBrokerageAccountAsync(brokerageAccountId, accountId!);
            if (account == null) return Results.NotFound();
            var provider = factory.GetProvider(account.Provider);
            var chain = await provider.GetOptionsChainAsync(account.CredentialData, symbol, expiration);
            return Results.Ok(chain);
        });

        api.MapPost("/options/orders", async ([FromBody] Trade trade, TradeService tradeSvc, PortfolioService portfolioSvc, BrokerageProviderFactory factory, HttpContext ctx) =>
        {
            var accountId = ctx.Items["accountId"] as string;
            var userId = ctx.Items["userId"] as string;
            trade.AccountId = accountId!;
            trade.UserId = userId!;
            trade = await tradeSvc.RecordTradeAsync(trade);

            var account = await portfolioSvc.GetBrokerageAccountAsync(trade.BrokerageAccountId!, accountId!);
            if (account == null) return Results.NotFound();
            var provider = factory.GetProvider(account.Provider);
            var externalId = await provider.PlaceOptionsOrderAsync(account.CredentialData, trade);
            trade.ExternalOrderId = externalId;
            trade.Status = TradeStatus.Submitted;
            await tradeSvc.UpdateTradeStatusAsync(trade.id, trade.AccountId, TradeStatus.Submitted);
            return Results.Created($"/api/trades/{trade.id}", trade);
        });
    }
}
