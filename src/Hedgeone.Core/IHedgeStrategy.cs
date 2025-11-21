using Hedgeone.Indicators;

namespace Hedgeone.Core;

/// <summary>
/// 헷지 트레이딩 전략 인터페이스
/// </summary>
public interface IHedgeStrategy
{
    /// <summary>
    /// 일봉 업데이트 이벤트 (대세 판단)
    /// </summary>
    /// <param name="symbol">심볼</param>
    /// <param name="dailyCandles">일봉 캔들 데이터</param>
    void OnNewDaily(string symbol, List<Candle> dailyCandles);

    /// <summary>
    /// 5분봉 업데이트 이벤트 (신호 생성 및 포지션 관리)
    /// </summary>
    /// <param name="symbol">심볼</param>
    /// <param name="candles5m">5분봉 캔들 데이터</param>
    /// <param name="currentPrice">현재 가격</param>
    /// <param name="timestamp">타임스탬프</param>
    void OnNew5m(string symbol, List<Candle> candles5m, decimal currentPrice, DateTime timestamp);

    /// <summary>
    /// 전략 시작
    /// </summary>
    /// <param name="ct">취소 토큰</param>
    Task<bool> StartAsync(CancellationToken ct);

    /// <summary>
    /// 전략 중지
    /// </summary>
    Task StopAsync();

    /// <summary>
    /// 현재 상태 조회
    /// </summary>
    Dictionary<string, TradingState> GetCurrentStates();
}
