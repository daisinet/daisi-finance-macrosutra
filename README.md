# MacroSutra

Automated stock trading platform for the Daisinet ecosystem. Users define trigger-based trading strategies across multiple brokerage accounts, backtest them against historical data, and optionally share strategies and live trades with the community.

## Overview

MacroSutra lets users:

- **Define trading triggers** — Create rule-based strategies using technical indicators, price thresholds, volume patterns, time-of-day conditions, and compound logic (AND/OR/NOT). Triggers fire automatically and execute trades through connected brokerage accounts.
- **Connect multiple brokerages** — Link accounts from supported brokerages through a unified provider abstraction. Trade across brokers from a single dashboard.
- **Backtest strategies** — Run any strategy against historical market data over any supported date range to validate expected outcomes before risking real capital. View simulated P&L, win rate, max drawdown, and other performance metrics.
- **Share strategies** — Optionally publish trading strategies to the MacroSutra community. Other users can browse, fork, and deploy shared strategies on their own accounts.
- **Subscribe to traders** — Follow other users' live trades. Subscribers choose what happens when the trader acts: receive an email alert, push notification, or automatically mimic the trade on their own account. Subscriptions are paid via **Daisi Credits**, with earnings allocated to the sharer at their account's RevShare percentage (configured in Daisi admin, default 70%).
- **Privacy controls** — Users choose whether their trades and strategies are visible to others. Sharing is always opt-in.

## Architecture

MacroSutra ships as two applications sharing a common UI layer:

- **Web** (`MacroSutra.Web`) — Blazor Server app that connects directly to the database and service layer. Full-featured, runs in any browser.
- **Mobile & Desktop** (`MacroSutra.App`) — .NET MAUI Blazor Hybrid app for iOS, Android, Windows, and macOS. Connects exclusively through the SDK/API — no direct database access.
- **Shared UI** (`MacroSutra.UI`) — Razor Class Library containing all shared pages, components, layouts, and styles used by both Web and MAUI apps.

## Projects

| Project | Description |
|---------|-------------|
| `MacroSutra.Core` | Domain models, enums, interfaces, and DTOs |
| `MacroSutra.Data` | Cosmos DB data access layer (repositories, container config) |
| `MacroSutra.Services` | Business logic — trading engine, trigger evaluation, backtesting, subscriptions |
| `MacroSutra.Brokers` | Brokerage provider integrations (Alpaca, Schwab, Webull, etc.) |
| `MacroSutra.UI` | Shared Razor Class Library — pages, components, and layouts for Web and MAUI |
| `MacroSutra.Web` | Blazor Server web application (direct DB access) |
| `MacroSutra.App` | .NET MAUI Blazor Hybrid app (mobile + desktop, SDK-only access) |
| `MacroSutra.SDK` | SDK and API client for MAUI app and external consumers |
| `MacroSutra.Tools` | Daisinet bot tool integration |
| `MacroSutra.Tests` | Unit and integration tests |

## Supported Brokerages

### Full Trading API Support

| Brokerage | API | Trades | Market Data | Commission | Notes |
|-----------|-----|--------|-------------|------------|-------|
| **Alpaca** | [REST + WebSocket](https://docs.alpaca.markets/) | Stocks, ETFs, options, crypto | Real-time (IEX), historical | Free | Most developer-friendly; C# SDK available; paper trading |
| **Charles Schwab** | [REST (OAuth 2.0)](https://developer.schwab.com/) | Stocks, ETFs, options | Real-time + historical | Free (stocks/ETFs) | Replaced TD Ameritrade; no minimum balance |
| **Interactive Brokers** | [TWS API + Client Portal](https://www.interactivebrokers.com/en/trading/ib-api.php) | Stocks, options, futures, forex, crypto | Real-time + historical | Free on IBKR Lite | Most comprehensive multi-asset; 150+ markets; C# SDK |
| **Tradier** | [REST](https://docs.tradier.com/) | Stocks, options | Real-time, streaming, historical | Free equities (Pro $10/mo) | API-first brokerage; paper trading |
| **Webull** | [REST + MQTT](https://developer.webull.com/) | Stocks, ETFs, options | Real-time + historical | Free | Official OpenAPI; 100+ order types |
| **TradeStation** | [REST + WebSocket](https://api.tradestation.com/docs/) | Stocks, options, futures | Real-time, streaming, historical | Free (under 10K shares) | Built-in backtesting; EasyLanguage support |
| **Tastytrade** | [REST](https://developer.tastytrade.com/) | Stocks, ETFs, options, futures, crypto | Real-time + options chains | Free (stocks/ETFs) | Options-focused; strong analytics |
| **Public.com** | [REST](https://public.com/api) | Stocks, ETFs, options | Portfolio + pricing data | Free + rebates | Zero-cost; fractional shares; options rebate program |
| **moomoo** | [OpenAPI SDK](https://openapi.moomoo.com/) | Stocks, options | Real-time, streaming, historical | Free (US stocks/options) | C# SDK; requires OpenD gateway |

### Planned / Limited Support

| Brokerage | Status | Notes |
|-----------|--------|-------|
| **Robinhood** | Crypto API only | No official stock trading API; crypto-only endpoint available |
| **SoFi** | No public API | Robo-advisor only; no developer API for programmatic trading |
| **Fidelity** | No public API | Read-only via third-party aggregators; no trade execution API |
| **E\*TRADE** | Limited | Requires daily manual browser login for OAuth refresh; impractical for automation |

> Brokerage support is implemented through a provider abstraction (`IBrokerageProvider`). New brokerages can be added by implementing the interface without changes to the core trading engine.

## Subscription & Monetization

MacroSutra integrates with the Daisinet credit and marketplace system:

- **Subscribers** pay Daisi Credits to follow a trader's live actions
- **Sharers** earn credits at their account's **RevShare percentage** (default 70%, admin-configurable per provider in Daisi Manager)
- **Platform** retains the remainder (default 30%)
- Subscription billing is handled by the Daisinet `SubscriptionRenewalService` (daily renewal cycle)
- Credit transactions appear in the user's Daisi credit ledger with type `MarketplacePurchase` (subscriber) and `ProviderEarning` (sharer)

### Subscriber Actions

When a followed trader executes a trade, subscribers can configure one or more automatic actions:

| Action | Description |
|--------|-------------|
| **Email alert** | Receive an email with trade details (ticker, direction, quantity, price) |
| **Push notification** | Mobile/desktop push notification via MAUI app |
| **Mimic trade** | Automatically execute the same trade on the subscriber's connected brokerage account (proportional to account size or fixed quantity) |
| **Webhook** | POST trade data to a custom URL for external integrations |

## Getting Started

### Prerequisites

- .NET 10 SDK
- Azure Cosmos DB account (or emulator)
- At least one brokerage account with API access enabled

### Configuration

Set the Cosmos DB connection string in user secrets:

```bash
cd MacroSutra.Web
dotnet user-secrets set "Cosmo:ConnectionString" "AccountEndpoint=https://...;AccountKey=..."
```

### Running the Web App

```bash
cd MacroSutra.Web
dotnet run --launch-profile https
```

### Running the MAUI App

```bash
cd MacroSutra.App
dotnet build -t:Run -f net10.0-android   # Android
dotnet build -t:Run -f net10.0-ios        # iOS
dotnet build -t:Run -f net10.0-windows    # Windows
dotnet build -t:Run -f net10.0-maccatalyst # macOS
```

### Running Tests

```bash
dotnet test MacroSutra.Tests
```

## Roadmap

### Phase 1: Foundation ✅

- [x] Core domain models — User, BrokerageAccount, TradingStrategy, Trade, Position, Subscription, BacktestResult
- [x] 13 domain enums — TradeStatus, TradeSide, ConditionType, ConditionOperator, QuantityType, etc.
- [x] Cosmos DB data layer — container-per-entity pattern with 7 partial classes, 60+ CRUD methods
- [x] Service layer — 19 services covering all domain logic
- [x] Shared Razor Class Library (`MacroSutra.UI`) — layout, navigation, MudBlazor theme, 12 pages
- [x] Blazor Server web app (`MacroSutra.Web`) — wired to services and Cosmos DB
- [x] MAUI Blazor Hybrid app (`MacroSutra.App`) — scaffold with SDK data provider
- [x] SDK project (`MacroSutra.SDK`) — 7 REST clients, 32+ methods
- [x] REST API — 27 Minimal API endpoints with API key authentication
- [x] User authentication via Daisinet SSO (`AddDaisiForWeb()`)
- [x] User management — roles (Viewer, Trader, Manager, Owner), team import from Daisinet
- [x] Test suite — 202 tests across 23 test classes

### Phase 2: Brokerage Integration ✅

- [x] `IBrokerageProvider` abstraction — validate, get positions, place orders, get order status, get balance, refresh credentials
- [x] Alpaca provider — Alpaca.Markets SDK, paper + live trading
- [x] Webull provider — REST via HttpClient, sandbox mode, OAuth token refresh
- [x] Paper trading provider — mock provider for development and testing
- [x] `BrokerageProviderFactory` — routes by `BrokerageProvider` enum via DI
- [x] Brokerage account linking UI — provider-specific credential forms with validation
- [x] Portfolio dashboard — unified positions, balances, P&L across all brokerages
- [x] Position sync service — fetch remote positions, upsert/delete stale, cache balance
- [x] Credential management — per-provider credential schemas with encrypted storage

### Phase 3: Strategy Engine ✅

- [x] Market data service — Alpaca free API for snapshots and historical bars, 30s in-memory cache
- [x] Technical indicators — SMA, EMA, RSI, MACD, Bollinger Bands
- [x] Condition evaluator — Price, Volume, PercentChange, MovingAverage, RSI, MACD conditions
- [x] CrossesAbove/CrossesBelow operators — in-memory previous-value tracking for crossover detection
- [x] Trade execution service — resolves Shares/DollarAmount/PercentOfPortfolio quantities, places orders
- [x] Alert actions — recorded as filled trades with descriptive notes
- [x] Strategy evaluation engine — BackgroundService polling every 60s during US market hours (9:30–16:00 ET, Mon–Fri)
- [x] AND/OR logic group evaluation — flat condition groups with LogicGroup model
- [x] Order status tracker — BackgroundService polling open orders every 30s
- [x] "Test Now" — manual evaluation against live data with per-condition pass/fail
- [x] Execution history — LastEvaluatedUtc and LastTriggeredUtc tracked per strategy
- [x] Dashboard engine status indicator

### Phase 4: Backtesting ✅

- [x] Historical data ingestion — daily OHLCV bars via Alpaca free data API
- [x] Backtesting runtime — simulate strategy execution over any historical date range
- [x] Performance metrics — total return, Sharpe ratio, max drawdown, win rate, profit factor, avg trade duration, best/worst trade
- [x] Backtesting UI — date range picker, strategy selector, metric cards
- [x] Equity curve visualization and drawdown chart (MudBlazor line charts)
- [x] Trade-by-trade breakdown table with P&L, return %, duration
- [x] Backtest persistence — Cosmos DB storage for later review
- [x] SDK backtest client — full CRUD via MacroSutra SDK

### Phase 5: Community & Marketplace ✅

- [x] Strategy visibility — Private, Public, SubscribersOnly
- [x] Strategy marketplace — browse public strategies with pagination and sorting at `/marketplace`
- [x] Strategy detail page — conditions, actions, community stats, reviews at `/marketplace/{id}`
- [x] Fork strategy — deep copy with attribution tracking
- [x] Ratings and reviews — 1–5 stars, one per account, denormalized community stats
- [x] Leaderboard — ranked by Sharpe, return, or win rate at `/leaderboard` (5-min cache)
- [x] Community Cosmos container — reviews and stats partitioned by StrategyId
- [x] SDK community client and public API endpoints

### Phase 6: Subscriptions & Alerts ✅

- [x] Subscription model — Mirror, ScaledMirror, Email, Webhook action types
- [x] Daisi Credits billing — MarketplaceClientFactory integration, daily renewal via ORC
- [x] Email alerts — SendGrid v3 API
- [x] Webhook dispatch — POST JSON with 10s timeout
- [x] Trade mimic engine — SubscriptionDispatchService with quantity scaling
- [x] Subscription management UI — My Subscriptions / My Subscribers tabs, expandable action history
- [x] Subscribe dialog — action type selector, conditional fields, credit price display
- [x] Subscription pricing — publisher sets SubscriptionCreditPrice and SubscriptionPeriodDays
- [x] SDK subscription client and API endpoints

### Phase 7: Finish What We Started ✅

Completed the gaps left in Phases 2–6.

**Strategy engine gaps (Phase 3):**
- [x] Strategy templates — 5 pre-built templates: RSI Oversold Bounce, MA Crossover, Price Breakout, Mean Reversion, MACD Momentum
- [x] Time-based conditions — TimeOfDay and DayOfWeek condition types
- [x] Compound trigger logic — nested ConditionGroup tree with recursive AND/OR evaluation

**Backtesting gaps (Phase 4):**
- [x] Backtest comparison mode — run multiple strategies side-by-side against the same data
- [x] Walk-forward analysis — rolling in-sample/out-of-sample windows with consistency scoring
- [x] Slippage and commission modeling — configurable SlippageBps and CommissionPerTrade
- [x] Intraday bars — Day, Hour, 15min, 5min, 1min time frames via BarTimeFrame enum

**MAUI app gaps:**
- [x] SDK strategy activate/deactivate endpoints
- [x] SDK brokerage account CRUD endpoints (create, update, deactivate, validate+link)
- [x] SdkDataProvider implementations for all stubbed methods

**Subscription gaps (Phase 6):**
- [x] Push notifications — FCM for Android/iOS MAUI trade alerts, token registration, LastUsedUtc tracking

### Phase 8: Production-Ready + Real-Time ✅

- [x] SignalR infrastructure — MacroSutraHub with account-scoped groups, IStrategyEventPublisher interface in Core
- [x] Real-time strategy alerts — StrategyTriggered events pushed to connected Blazor clients on trigger
- [x] Real-time portfolio updates — PortfolioUpdated events pushed after position sync
- [x] AlertBell component — header notification bell with badge count and dropdown of recent alerts
- [x] Live dashboard — SignalR subscriptions on Dashboard, Portfolio, and Strategies pages with auto-refresh fallback
- [x] "Just Triggered" indicator on strategies list when SignalR event arrives
- [x] Strategy performance tracking — StrategyTriggerRecord Cosmos container, win/loss/open outcome tracking
- [x] Performance summary — TotalTriggers, WinRate, TotalPnL, MonthlyReturns computed from trigger records
- [x] StrategyPerformancePage — summary cards, monthly returns table, trigger history with P&L
- [x] OrderStatusTracker integration — updates trigger outcomes when trades fill
- [x] API endpoints — GET /api/strategies/{id}/performance, GET /api/strategies/{id}/triggers
- [x] SDK client methods — GetPerformanceAsync, GetTriggersAsync
- [x] Test coverage — WalkForwardService (10), PushNotificationService (8), StrategyTemplateService (5), StrategyPerformanceService (8)
- [x] Landing page fix — removed false AI claims, now "Rule-Based Trading Automation"
- [x] Learn pages — Backtesting guide (/learn/backtesting), Conditions reference (/learn/conditions)
- [x] SDK reference expanded — 8 client sections (was 3): added Community, Subscription, Backtest, Push, WalkForward

### Phase 9: Additional Brokerages

- [ ] Charles Schwab provider
- [ ] Interactive Brokers provider (TWS API)
- [ ] Tradier provider
- [ ] TradeStation provider
- [ ] Tastytrade provider
- [ ] Public.com provider
- [ ] moomoo / Futu OpenAPI provider
- [ ] Robinhood provider (crypto only, pending stock API availability)
- [ ] Provider health monitoring and automatic failover

### Phase 10: Bot Tools & AI Features

- [ ] Daisinet bot tools (`MacroSutra.Tools`) — query portfolio, check triggers, get performance, execute trades via bot
- [ ] AI strategy suggestions — analyze portfolio and market conditions to recommend triggers
- [ ] Natural language strategy builder — describe strategy in English, AI generates rules
- [ ] AI risk assessment — evaluate strategy risk profile and suggest adjustments
- [ ] Market sentiment analysis — news and social sentiment as trigger inputs
- [ ] Update landing page with accurate AI feature descriptions

### Phase 11: Advanced Strategy Builder

- [ ] Visual drag-and-drop strategy builder UI with live preview
- [ ] Strategy templates gallery — browse, preview, and fork starter strategies
- [ ] Condition builder wizard — step-by-step guided condition creation
- [ ] Live strategy preview — show simulated triggers against recent market data

### Phase 12: Advanced Trading Features

- [ ] Multi-leg options support (spreads, straddles, iron condors)
- [ ] Fractional share support where brokerage allows
- [ ] Dollar-cost averaging automation
- [ ] Portfolio rebalancing triggers
- [ ] Tax-loss harvesting suggestions
- [ ] Advanced charting with TradingView integration
- [ ] Export trades to CSV/PDF for tax reporting
