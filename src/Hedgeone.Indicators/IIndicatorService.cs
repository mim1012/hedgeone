namespace Hedgeone.Indicators;

/// <summary>
/// 기술 지표 계산 서비스 인터페이스
/// </summary>
public interface IIndicatorService
{
    /// <summary>
    /// RSI (Relative Strength Index) 계산
    /// </summary>
    /// <param name="prices">가격 데이터</param>
    /// <param name="period">RSI 기간 (기본=2)</param>
    /// <returns>RSI 값 (0~100)</returns>
    decimal CalculateRSI(List<decimal> prices, int period = 2);

    /// <summary>
    /// RSI를 캔들 데이터로부터 계산
    /// </summary>
    decimal CalculateRSI(List<Candle> candles, int period = 2);

    /// <summary>
    /// MACD Line 계산 (Signal Line이 아닌 MACD Line만)
    /// </summary>
    /// <param name="prices">가격 데이터</param>
    /// <param name="fast">Fast EMA 기간</param>
    /// <param name="slow">Slow EMA 기간</param>
    /// <param name="signal">Signal EMA 기간 (사용 안 함, 호환성 유지)</param>
    /// <returns>MACD Line 값</returns>
    decimal CalculateMACDLine(List<decimal> prices, int fast = 1, int slow = 1, int signal = 1);

    /// <summary>
    /// MACD Line을 캔들 데이터로부터 계산
    /// </summary>
    decimal CalculateMACDLine(List<Candle> candles, int fast = 1, int slow = 1, int signal = 1);

    /// <summary>
    /// 상승 신호 판단 (RSI > MACD)
    /// </summary>
    bool SignalLong(List<Candle> candles, int rsiLen = 2);

    /// <summary>
    /// 하락 신호 판단 (RSI < MACD)
    /// </summary>
    bool SignalShort(List<Candle> candles, int rsiLen = 2);
}
