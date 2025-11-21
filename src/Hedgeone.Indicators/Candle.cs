namespace Hedgeone.Indicators;

/// <summary>
/// 캔들 데이터 모델
/// </summary>
public class Candle
{
    public DateTime OpenTime { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public decimal Volume { get; set; }

    public Candle() { }

    public Candle(DateTime openTime, decimal open, decimal high, decimal low, decimal close, decimal volume)
    {
        OpenTime = openTime;
        Open = open;
        High = high;
        Low = low;
        Close = close;
        Volume = volume;
    }
}
