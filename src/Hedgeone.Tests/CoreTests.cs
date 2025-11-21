using Hedgeone.Core;
using Hedgeone.Indicators;
using Xunit;

namespace Hedgeone.Tests;

public class CoreTests
{
    [Fact]
    public void TradingState_PnlCall_WithLongPosition_CalculatesCorrectly()
    {
        // Arrange
        var state = new TradingState
        {
            Symbol = "DOGEUSDT",
            PosCall = 100m,
            EntryPriceCall = 0.10m
        };

        // Act
        var pnl = state.PnlCall(0.11m);  // 10% 상승 (0.01 price increase)

        // Assert
        Assert.Equal(1.00m, pnl);  // (0.11 - 0.10) * 100 = 0.01 * 100 = 1.00
    }

    [Fact]
    public void TradingState_PnlPut_WithShortPosition_CalculatesCorrectly()
    {
        // Arrange
        var state = new TradingState
        {
            Symbol = "DOGEUSDT",
            PosPut = 100m,
            EntryPricePut = 0.10m
        };

        // Act
        var pnl = state.PnlPut(0.09m);  // 10% 하락 (Short는 이익, 0.01 price decrease)

        // Assert
        Assert.Equal(1.00m, pnl);  // (0.10 - 0.09) * 100 = 0.01 * 100 = 1.00
    }

    [Fact]
    public void TradingState_PnlPctCall_WithProfit_ReturnsCorrectPercentage()
    {
        // Arrange
        var state = new TradingState
        {
            PosCall = 100m,
            EntryPriceCall = 100m
        };

        // Act
        var pnlPct = state.PnlPctCall(105m);  // 5% 상승

        // Assert
        Assert.Equal(0.05m, pnlPct);
    }

    [Fact]
    public void TradingState_TotalPnl_CombinesBothPositions()
    {
        // Arrange
        var state = new TradingState
        {
            PosCall = 100m,
            EntryPriceCall = 0.10m,
            PosPut = 100m,
            EntryPricePut = 0.10m
        };

        // Act - 가격이 0.11로 상승
        var totalPnl = state.TotalPnl(0.11m);

        // Assert
        // Long: (0.11 - 0.10) * 100 = +10
        // Short: (0.10 - 0.11) * 100 = -10
        // Total = 0 (헷지됨)
        Assert.Equal(0m, totalPnl);
    }

    [Fact]
    public void StrategyConfig_Validate_WithValidConfig_DoesNotThrow()
    {
        // Arrange
        var config = new StrategyConfig
        {
            CallSize = 10m,
            ApiKey = "test-key",
            ApiSecret = "test-secret",
            HedgeLossPct = -0.01m
        };

        // Act & Assert
        config.Validate();  // Should not throw
    }

    [Fact]
    public void StrategyConfig_Validate_WithInvalidCallSize_ThrowsException()
    {
        // Arrange
        var config = new StrategyConfig
        {
            CallSize = 0m,  // Invalid
            ApiKey = "test-key",
            ApiSecret = "test-secret"
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => config.Validate());
    }

    [Fact]
    public void StrategyConfig_Validate_WithPositiveHedgeLoss_ThrowsException()
    {
        // Arrange
        var config = new StrategyConfig
        {
            CallSize = 10m,
            HedgeLossPct = 0.01m,  // Should be negative
            ApiKey = "test-key",
            ApiSecret = "test-secret"
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => config.Validate());
    }

    [Fact]
    public async Task JsonStateRepository_SaveAndLoad_WorksCorrectly()
    {
        // Arrange
        var testFile = "test_state.json";
        var repo = new JsonStateRepository(testFile);

        var originalState = new TradingState
        {
            Symbol = "DOGEUSDT",
            Regime = "UP",
            PosCall = 100m,
            EntryPriceCall = 0.10m
        };

        try
        {
            // Act - Save
            await repo.SaveAsync("DOGEUSDT", originalState);

            // Act - Load
            var loadedStates = await repo.LoadAllAsync();

            // Assert
            Assert.True(loadedStates.ContainsKey("DOGEUSDT"));
            Assert.Equal("UP", loadedStates["DOGEUSDT"].Regime);
            Assert.Equal(100m, loadedStates["DOGEUSDT"].PosCall);
            Assert.Equal(0.10m, loadedStates["DOGEUSDT"].EntryPriceCall);
        }
        finally
        {
            // Cleanup
            if (File.Exists(testFile))
                File.Delete(testFile);
        }
    }

    [Fact]
    public void ExitRuleEvaluator_TakeProfitHit_ReturnsTrue()
    {
        // Arrange
        var config = new StrategyConfig
        {
            TakeProfitPct = 0.01m  // 1%
        };
        var indicators = new TechnicalIndicators();
        var evaluator = new ExitRuleEvaluator(config, indicators);

        var state = new TradingState
        {
            PosCall = 100m,
            EntryPriceCall = 100m,
            EntryTimeCall = DateTime.UtcNow
        };

        var candles = CreateDummyCandles(10);

        // Act - 현재 가격이 101 (1% 상승)
        var shouldExit = evaluator.ExitRuleHit(state, 101m, candles);

        // Assert
        Assert.True(shouldExit, "1% 익절 조건에서 청산되어야 합니다");
    }

    [Fact]
    public void ExitRuleEvaluator_TimeExceeded_ReturnsTrue()
    {
        // Arrange
        var config = new StrategyConfig
        {
            MaxHoldBars = 1  // 5분
        };
        var indicators = new TechnicalIndicators();
        var evaluator = new ExitRuleEvaluator(config, indicators);

        var state = new TradingState
        {
            PosCall = 100m,
            EntryPriceCall = 100m,
            EntryTimeCall = DateTime.UtcNow.AddMinutes(-10)  // 10분 전 진입
        };

        var candles = CreateDummyCandles(10);

        // Act
        var shouldExit = evaluator.ExitRuleHit(state, 100.5m, candles);

        // Assert
        Assert.True(shouldExit, "최대 보유 시간 초과 시 청산되어야 합니다");
    }

    [Fact]
    public void ExitRuleEvaluator_NoExitCondition_ReturnsFalse()
    {
        // Arrange
        var config = new StrategyConfig
        {
            TakeProfitPct = 0.01m,  // 1%
            MaxHoldBars = 24
        };
        var indicators = new TechnicalIndicators();
        var evaluator = new ExitRuleEvaluator(config, indicators);

        var state = new TradingState
        {
            PosCall = 100m,
            EntryPriceCall = 100m,
            EntryTimeCall = DateTime.UtcNow
        };

        var candles = CreateDummyCandles(10);

        // Act - 가격이 100.5 (0.5% 상승, TP 미도달)
        var shouldExit = evaluator.ExitRuleHit(state, 100.5m, candles);

        // Assert
        Assert.False(shouldExit, "청산 조건 미충족 시 보유해야 합니다");
    }

    // Helper method
    private List<Candle> CreateDummyCandles(int count)
    {
        var candles = new List<Candle>();
        for (int i = 0; i < count; i++)
        {
            candles.Add(new Candle
            {
                OpenTime = DateTime.UtcNow.AddMinutes(-count + i),
                Open = 100m,
                High = 100.5m,
                Low = 99.5m,
                Close = 100m + i * 0.1m,
                Volume = 1000m
            });
        }
        return candles;
    }
}
