using System.Text.Json;
using MacroSutra.Core.Enums;
using MacroSutra.Core.Models;
using MacroSutra.Core.Models.Options;

namespace MacroSutra.Tests.Core;

/// <summary>
/// WU8: Options model tests — serialization and computed properties.
/// </summary>
public class OptionsModelTests
{
    [Fact]
    public void Position_IsOption_TrueWhenOptionDetailsPresent()
    {
        var position = new Position
        {
            Symbol = "AAPL 240620C200",
            OptionDetails = new OptionDetails
            {
                ContractSymbol = "AAPL 240620C200",
                UnderlyingSymbol = "AAPL",
                OptionType = OptionType.Call
            }
        };
        Assert.True(position.IsOption);
    }

    [Fact]
    public void Position_IsOption_FalseWhenNoOptionDetails()
    {
        var position = new Position { Symbol = "AAPL" };
        Assert.False(position.IsOption);
    }

    [Fact]
    public void Trade_OptionDetails_SerializationRoundTrip()
    {
        var trade = new Trade
        {
            Symbol = "AAPL 240620C200",
            Side = TradeSide.Buy,
            OrderType = TradeActionType.BuyCall,
            Quantity = 5,
            OptionDetails = new OptionDetails
            {
                ContractSymbol = "AAPL 240620C200",
                UnderlyingSymbol = "AAPL",
                OptionType = OptionType.Call,
                ExpirationDate = new DateOnly(2024, 6, 20),
                StrikePrice = 200m,
                Contracts = 5
            }
        };

        var json = JsonSerializer.Serialize(trade);
        var deserialized = JsonSerializer.Deserialize<Trade>(json);

        Assert.NotNull(deserialized?.OptionDetails);
        Assert.Equal("AAPL 240620C200", deserialized!.OptionDetails!.ContractSymbol);
        Assert.Equal(OptionType.Call, deserialized.OptionDetails.OptionType);
        Assert.Equal(200m, deserialized.OptionDetails.StrikePrice);
        Assert.Equal(5, deserialized.OptionDetails.Contracts);
    }

    [Fact]
    public void Trade_WithoutOptionDetails_SerializesNormally()
    {
        var trade = new Trade
        {
            Symbol = "AAPL",
            Side = TradeSide.Buy,
            OrderType = TradeActionType.MarketOrder,
            Quantity = 10
        };

        var json = JsonSerializer.Serialize(trade);
        var deserialized = JsonSerializer.Deserialize<Trade>(json);

        Assert.NotNull(deserialized);
        Assert.Null(deserialized!.OptionDetails);
        Assert.Equal("AAPL", deserialized.Symbol);
    }

    [Fact]
    public void OptionsChain_DefaultValues()
    {
        var chain = new OptionsChain { UnderlyingSymbol = "AAPL" };
        Assert.Equal("AAPL", chain.UnderlyingSymbol);
        Assert.Equal(0m, chain.UnderlyingPrice);
        Assert.Empty(chain.Expirations);
    }

    [Fact]
    public void OptionsChain_WithExpirations_ContainsCallsAndPuts()
    {
        var chain = new OptionsChain
        {
            UnderlyingSymbol = "AAPL",
            UnderlyingPrice = 190m,
            Expirations =
            [
                new OptionsExpiration
                {
                    ExpirationDate = new DateOnly(2024, 6, 20),
                    Calls =
                    [
                        new OptionContract { ContractSymbol = "AAPL240620C200", StrikePrice = 200, Bid = 2.50m, Ask = 2.60m }
                    ],
                    Puts =
                    [
                        new OptionContract { ContractSymbol = "AAPL240620P180", StrikePrice = 180, Bid = 1.50m, Ask = 1.60m }
                    ]
                }
            ]
        };

        Assert.Single(chain.Expirations);
        Assert.Single(chain.Expirations[0].Calls);
        Assert.Single(chain.Expirations[0].Puts);
        Assert.Equal(200m, chain.Expirations[0].Calls[0].StrikePrice);
        Assert.Equal(180m, chain.Expirations[0].Puts[0].StrikePrice);
    }

    [Fact]
    public void TradeActionType_OptionsValues_Exist()
    {
        Assert.Equal(5, (int)TradeActionType.BuyCall);
        Assert.Equal(6, (int)TradeActionType.BuyPut);
        Assert.Equal(7, (int)TradeActionType.SellCall);
        Assert.Equal(8, (int)TradeActionType.SellPut);
    }

    [Fact]
    public void OptionType_Values_Exist()
    {
        Assert.Equal(0, (int)OptionType.Call);
        Assert.Equal(1, (int)OptionType.Put);
    }

    [Fact]
    public void DcaFrequency_Values_Exist()
    {
        Assert.Equal(0, (int)DcaFrequency.Daily);
        Assert.Equal(1, (int)DcaFrequency.Weekly);
        Assert.Equal(2, (int)DcaFrequency.BiWeekly);
        Assert.Equal(3, (int)DcaFrequency.Monthly);
    }

    [Fact]
    public void DcaSchedule_DefaultValues()
    {
        var schedule = new DcaSchedule();
        Assert.Equal(nameof(DcaSchedule), schedule.Type);
        Assert.Equal(DcaFrequency.Weekly, schedule.Frequency);
        Assert.True(schedule.IsActive);
        Assert.Equal(new TimeOnly(10, 0), schedule.ExecutionTime);
        Assert.Equal(0, schedule.TotalExecutions);
        Assert.Equal(0m, schedule.TotalInvested);
    }

    [Fact]
    public void RebalanceTarget_DefaultValues()
    {
        var target = new RebalanceTarget();
        Assert.Equal(nameof(RebalanceTarget), target.Type);
        Assert.Equal(5m, target.DriftThresholdPercent);
        Assert.True(target.IsActive);
        Assert.Empty(target.Allocations);
    }

    [Fact]
    public void TaxLossHarvestingReport_DefaultValues()
    {
        var report = new TaxLossHarvestingReport();
        Assert.Equal(0m, report.TotalEstimatedLoss);
        Assert.Equal(0m, report.TotalEstimatedTaxBenefit);
        Assert.Empty(report.Candidates);
    }
}
