using Hedgeone.Indicators;

namespace Hedgeone.Core;

/// <summary>
/// 청산 규칙 평가 인터페이스
/// </summary>
public interface IExitRuleEvaluator
{
    /// <summary>
    /// 청산 조건 충족 여부 판단
    /// </summary>
    /// <param name="state">현재 트레이딩 상태</param>
    /// <param name="currentPrice">현재 가격</param>
    /// <param name="candles">최근 캔들 데이터 (RSI 롤오버 체크용)</param>
    /// <returns>true: 청산 조건 충족, false: 보유 유지</returns>
    bool ExitRuleHit(TradingState state, decimal currentPrice, List<Candle> candles);
}
