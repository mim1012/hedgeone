using Hedgeone.Indicators;
using Xunit;

namespace Hedgeone.Tests;

public class IndicatorTests
{
    private readonly IIndicatorService _indicators;

    public IndicatorTests()
    {
        _indicators = new TechnicalIndicators();
    }

    [Fact]
    public void CalculateRSI_WithValidData_ReturnsCorrectValue()
    {
        // Arrange
        var prices = new List<decimal> { 44m, 44.34m, 44.09m, 43.61m, 44.33m, 44.83m };

        // Act
        var rsi = _indicators.CalculateRSI(prices, period: 2);

        // Assert
        Assert.True(rsi >= 0 && rsi <= 100);
    }

    [Fact]
    public void CalculateRSI_WithUptrend_ReturnsHighValue()
    {
        // Arrange - 상승 추세
        var prices = new List<decimal> { 100m, 101m, 102m, 103m, 104m, 105m };

        // Act
        var rsi = _indicators.CalculateRSI(prices, period: 2);

        // Assert
        Assert.True(rsi > 50, $"상승 추세에서 RSI는 50 이상이어야 합니다. 실제값: {rsi}");
    }

    [Fact]
    public void CalculateRSI_WithDowntrend_ReturnsLowValue()
    {
        // Arrange - 하락 추세
        var prices = new List<decimal> { 105m, 104m, 103m, 102m, 101m, 100m };

        // Act
        var rsi = _indicators.CalculateRSI(prices, period: 2);

        // Assert
        Assert.True(rsi < 50, $"하락 추세에서 RSI는 50 이하여야 합니다. 실제값: {rsi}");
    }

    [Fact]
    public void CalculateMACDLine_WithSamePeriods_ReturnsZero()
    {
        // Arrange - MACD(1,1,1)은 fast=slow이므로 0을 반환해야 함
        var prices = new List<decimal> { 44m, 44.34m, 44.09m, 43.61m, 44.33m, 44.83m };

        // Act
        var macd = _indicators.CalculateMACDLine(prices, fast: 1, slow: 1, signal: 1);

        // Assert
        Assert.Equal(0m, macd);
    }

    [Fact]
    public void CalculateMACDLine_WithDifferentPeriods_ReturnsNonZero()
    {
        // Arrange
        var prices = new List<decimal> { 44m, 44.34m, 44.09m, 43.61m, 44.33m, 44.83m };

        // Act
        var macd = _indicators.CalculateMACDLine(prices, fast: 3, slow: 6, signal: 1);

        // Assert
        Assert.NotEqual(0m, macd);
    }

    [Fact]
    public void SignalLong_WithMACDOneOne_ComparesRSIAgainstZero()
    {
        // Arrange - RSI > 50인 상승 추세
        // MACD(1,1,1) = 0이므로 RSI > 0을 테스트
        var candles = new List<Candle>();
        for (int i = 0; i < 10; i++)
        {
            candles.Add(new Candle
            {
                OpenTime = DateTime.UtcNow.AddMinutes(-10 + i),
                Close = 100m + i * 2m,  // 강한 상승
                Open = 100m + i * 2m - 0.5m,
                High = 100m + i * 2m + 0.5m,
                Low = 100m + i * 2m - 0.5m,
                Volume = 1000m
            });
        }

        // Act
        var rsi = _indicators.CalculateRSI(candles, 2);
        var macd = _indicators.CalculateMACDLine(candles, 1, 1, 1);
        var isLong = _indicators.SignalLong(candles, rsiLen: 2);

        // Assert
        Assert.Equal(0m, macd);  // MACD(1,1,1) = 0
        Assert.True(rsi > 0, $"상승 추세에서 RSI > 0이어야 합니다. RSI={rsi}");
        Assert.True(isLong, "RSI > MACD(=0)이므로 Long 신호가 발생해야 합니다.");
    }

    [Fact]
    public void SignalShort_WithMACDOneOne_IsAlwaysFalse()
    {
        // Arrange - MACD(1,1,1) = 0이므로
        // Short 신호는 RSI < 0을 의미하는데, RSI는 항상 0~100이므로 불가능
        var candles = new List<Candle>();
        for (int i = 0; i < 10; i++)
        {
            candles.Add(new Candle
            {
                OpenTime = DateTime.UtcNow.AddMinutes(-10 + i),
                Close = 100m - i * 2m,  // 강한 하락
                Open = 100m - i * 2m + 0.5m,
                High = 100m - i * 2m + 0.5m,
                Low = 100m - i * 2m - 0.5m,
                Volume = 1000m
            });
        }

        // Act
        var rsi = _indicators.CalculateRSI(candles, 2);
        var macd = _indicators.CalculateMACDLine(candles, 1, 1, 1);
        var isShort = _indicators.SignalShort(candles, rsiLen: 2);

        // Assert
        Assert.Equal(0m, macd);  // MACD(1,1,1) = 0
        Assert.True(rsi >= 0, "RSI는 항상 0 이상이어야 합니다.");
        Assert.False(isShort, "MACD(1,1,1)=0인 경우 RSI < 0이 불가능하므로 Short 신호는 발생하지 않습니다.");
    }

    [Fact]
    public void CalculateRSI_WithInsufficientData_ThrowsException()
    {
        // Arrange
        var prices = new List<decimal> { 100m };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _indicators.CalculateRSI(prices, period: 2));
    }

    [Fact]
    public void CalculateMACDLine_WithInsufficientData_ThrowsException()
    {
        // Arrange
        var prices = new List<decimal> { 100m };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _indicators.CalculateMACDLine(prices, fast: 2, slow: 2));
    }
}
