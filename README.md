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

### Phase 1: Foundation

- [ ] Core domain models — User, BrokerageAccount, TradingStrategy, Trigger, Trade, Position
- [ ] Cosmos DB data layer with container-per-entity pattern
- [ ] Service layer scaffold — account management, strategy CRUD
- [ ] Shared Razor Class Library (`MacroSutra.UI`) with layout, navigation, and theme (MudBlazor)
- [ ] Blazor Server web app (`MacroSutra.Web`) wired to services and data layer
- [ ] MAUI Blazor Hybrid app (`MacroSutra.App`) scaffold with SDK client
- [ ] SDK project with gRPC/REST API surface for MAUI communication
- [ ] User authentication via Daisinet SSO (`AddDaisiForWeb()`)

### Phase 2: Brokerage Integration

- [x] `IBrokerageProvider` abstraction — connect, authenticate, get positions, place orders, get market data, get historical data
- [x] Alpaca provider (primary — most developer-friendly, free, paper trading for dev)
- [ ] Charles Schwab provider
- [ ] Interactive Brokers provider
- [ ] Tradier provider
- [x] Webull provider (REST via HttpClient, sandbox mode, token refresh)
- [x] Brokerage account linking UI — API key entry, credential validation, connection status
- [x] Portfolio dashboard — unified view of positions, balances, and P&L across all linked brokerages
- [x] Paper trading mode toggle per brokerage account
- [x] Position sync service — fetch remote positions, upsert/delete stale, cache balance
- [x] Credential management — provider-specific credential forms with validation

### Phase 3: Trading Triggers & Strategy Engine

- [x] Market data service — Alpaca free data API for snapshots and historical bars with 30s in-memory cache
- [x] Technical indicators — SMA, EMA, RSI, MACD calculated from historical price data
- [x] Condition evaluator — evaluates Price, Volume, PercentChange, MovingAverage, RSI, MACD conditions
- [x] CrossesAbove/CrossesBelow operators — in-memory previous-value tracking for crossover detection
- [x] Trade execution service — resolves Shares/DollarAmount/PercentOfPortfolio quantities, places orders via brokerage providers
- [x] Alert actions — recorded as filled trades with descriptive notes
- [x] Strategy evaluation engine — BackgroundService polling every 60s during US market hours (9:30-16:00 ET, Mon-Fri)
- [x] AND/OR logic group evaluation — flat condition groups matching existing LogicGroup model
- [x] Order status tracker — BackgroundService polling open orders every 30s, updates trade status from brokerage
- [x] "Test Now" feature — manually evaluate a strategy against live data, shows per-condition pass/fail
- [x] Execution history — LastEvaluatedUtc and LastTriggeredUtc tracked per strategy
- [x] Dashboard engine status indicator — shows running/stopped and last evaluation timestamp
- [ ] Strategy builder UI — visual drag-and-drop trigger composition with live preview
- [ ] Strategy templates — pre-built common strategies (stop-loss, trailing stop, mean reversion, momentum, breakout)
- [ ] Time-based conditions — time-of-day, day-of-week, market open/close relative
- [ ] Compound trigger logic — nested condition groups (currently flat AND/OR per strategy)

### Phase 4: Backtesting Engine

- [ ] Historical data ingestion from brokerage APIs and market data providers
- [ ] Backtesting runtime — simulate strategy execution over any historical date range
- [ ] Performance metrics — total return, annualized return, Sharpe ratio, max drawdown, win rate, profit factor, average trade duration
- [ ] Backtesting UI — date range picker, strategy selector, results dashboard with charts
- [ ] Equity curve visualization and drawdown chart
- [ ] Trade-by-trade breakdown table
- [ ] Comparison mode — run multiple strategies side-by-side against the same data
- [ ] Walk-forward analysis and out-of-sample testing support

### Phase 5: Strategy Sharing & Community

- [ ] Privacy controls — per-strategy and per-trade visibility toggle (private/public/subscribers-only)
- [ ] Strategy marketplace — browse, search, and filter community strategies by performance, asset class, risk level
- [ ] Strategy detail page — description, backtest results, live performance, author profile
- [ ] Fork strategy — copy a public strategy and modify it for personal use
- [ ] Strategy ratings and reviews
- [ ] Leaderboard — top-performing public strategies ranked by returns, Sharpe ratio, consistency

### Phase 6: Trade Subscriptions & Alerts

- [ ] Subscription model — subscribe to a trader, choose alert actions, set credit price
- [ ] Daisi Credits integration — subscriber payment, sharer RevShare earnings via `MarketplaceService`
- [ ] Email alerts — real-time email when followed trader executes a trade
- [ ] Push notifications — MAUI app push via platform notification services
- [ ] Trade mimic engine — automatically replicate trades on subscriber's brokerage (proportional sizing or fixed quantity)
- [ ] Webhook action — POST trade data to subscriber-configured URL
- [ ] Subscription management UI — active subscriptions, billing history, action preferences
- [ ] Sharer dashboard — subscriber count, earnings, payout history

### Phase 7: Additional Brokerages

- [x] Webull provider (moved to Phase 2)
- [ ] TradeStation provider
- [ ] Tastytrade provider
- [ ] Public.com provider
- [ ] moomoo (Futu OpenAPI) provider
- [ ] Robinhood provider (crypto only, pending stock API availability)
- [ ] Provider health monitoring and failover

### Phase 8: Bot Tools & AI Features

- [ ] Daisinet bot tools — query portfolio, check triggers, get strategy performance, execute trades via bot
- [ ] AI strategy suggestions — analyze portfolio and market conditions to recommend trigger configurations
- [ ] Natural language strategy builder — describe a trading strategy in plain English, AI generates trigger rules
- [ ] AI risk assessment — evaluate strategy risk profile and suggest adjustments
- [ ] Market sentiment analysis — AI-powered news and social sentiment signals as trigger inputs

### Phase 9: Advanced Features

- [ ] Multi-leg options strategy support (spreads, straddles, iron condors)
- [ ] Fractional share support where brokerage allows
- [ ] Dollar-cost averaging automation
- [ ] Portfolio rebalancing triggers
- [ ] Tax-loss harvesting suggestions
- [ ] Advanced charting with TradingView integration
- [ ] Export trades to CSV/PDF for tax reporting
- [ ] User management — roles (Owner/Trader/Viewer), per-account access controls
