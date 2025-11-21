namespace Hedgeone.Indicators;

/// <summary>
/// 기술 지표 계산 구현
/// </summary>
public class TechnicalIndicators : IIndicatorService
{
    public decimal CalculateRSI(List<decimal> prices, int period = 2)
    {
        if (prices == null || prices.Count < period + 1)
            throw new ArgumentException($"최소 {period + 1}개의 가격 데이터가 필요합니다.");

        // 가격 변화량 계산
        var changes = new List<decimal>();
        for (int i = 1; i < prices.Count; i++)
        {
            changes.Add(prices[i] - prices[i - 1]);
        }

        // 상승/하락 분리
        var gains = changes.Select(x => x > 0 ? x : 0m).ToList();
        var losses = changes.Select(x => x < 0 ? Math.Abs(x) : 0m).ToList();

        // 평균 계산 (Simple Moving Average for first period)
        var avgGain = gains.Take(period).Average();
        var avgLoss = losses.Take(period).Average();

        // Smoothed Moving Average for subsequent periods
        for (int i = period; i < gains.Count; i++)
        {
            avgGain = ((avgGain * (period - 1)) + gains[i]) / period;
            avgLoss = ((avgLoss * (period - 1)) + losses[i]) / period;
        }

        // RSI 계산
        if (avgLoss == 0)
            return 100m;

        var rs = avgGain / avgLoss;
        return 100m - (100m / (1m + rs));
    }

    public decimal CalculateRSI(List<Candle> candles, int period = 2)
    {
        var prices = candles.Select(c => c.Close).ToList();
        return CalculateRSI(prices, period);
    }

    public decimal CalculateMACDLine(List<decimal> prices, int fast = 1, int slow = 1, int signal = 1)
    {
        if (prices == null || prices.Count < Math.Max(fast, slow))
            throw new ArgumentException($"최소 {Math.Max(fast, slow)}개의 가격 데이터가 필요합니다.");

        // MACD Line = EMA(fast) - EMA(slow)
        var emaFast = CalculateEMA(prices, fast);
        var emaSlow = CalculateEMA(prices, slow);

        return emaFast - emaSlow;
    }

    public decimal CalculateMACDLine(List<Candle> candles, int fast = 1, int slow = 1, int signal = 1)
    {
        var prices = candles.Select(c => c.Close).ToList();
        return CalculateMACDLine(prices, fast, slow, signal);
    }

    public bool SignalLong(List<Candle> candles, int rsiLen = 2)
    {
        var rsi = CalculateRSI(candles, rsiLen);
        var macd = CalculateMACDLine(candles, 1, 1, 1);
        return rsi > macd;
    }

    public bool SignalShort(List<Candle> candles, int rsiLen = 2)
    {
        var rsi = CalculateRSI(candles, rsiLen);
        var macd = CalculateMACDLine(candles, 1, 1, 1);
        return rsi < macd;
    }

    /// <summary>
    /// EMA (Exponential Moving Average) 계산
    /// </summary>
    private decimal CalculateEMA(List<decimal> prices, int period)
    {
        if (prices.Count < period)
            throw new ArgumentException($"최소 {period}개의 가격 데이터가 필요합니다.");

        // Multiplier = 2 / (period + 1)
        decimal multiplier = 2m / (period + 1);

        // 첫 EMA는 SMA로 시작
        decimal ema = prices.Take(period).Average();

        // 이후 데이터에 대해 EMA 계산
        for (int i = period; i < prices.Count; i++)
        {
            ema = (prices[i] * multiplier) + (ema * (1 - multiplier));
        }

        return ema;
    }
}
