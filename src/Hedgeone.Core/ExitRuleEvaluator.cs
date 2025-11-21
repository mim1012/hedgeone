using Hedgeone.Indicators;

namespace Hedgeone.Core;

/// <summary>
/// 청산 규칙 평가 구현
/// PRD 문서의 exit_rule_hit() 로직 구현
/// </summary>
public class ExitRuleEvaluator : IExitRuleEvaluator
{
    private readonly StrategyConfig _config;
    private readonly IIndicatorService _indicators;

    public ExitRuleEvaluator(StrategyConfig config, IIndicatorService indicators)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _indicators = indicators ?? throw new ArgumentNullException(nameof(indicators));
    }

    /// <summary>
    /// 청산 조건 충족 여부 판단
    /// </summary>
    public bool ExitRuleHit(TradingState state, decimal currentPrice, List<Candle> candles)
    {
        // Long 포지션 체크
        if (state.PosCall > 0 && state.EntryPriceCall.HasValue)
        {
            if (CheckCallExit(state, currentPrice, candles))
                return true;
        }

        // Short 포지션 체크
        if (state.PosPut > 0 && state.EntryPricePut.HasValue)
        {
            if (CheckPutExit(state, currentPrice, candles))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Long 포지션 청산 조건 체크
    /// </summary>
    private bool CheckCallExit(TradingState state, decimal currentPrice, List<Candle> candles)
    {
        var entry = state.EntryPriceCall!.Value;
        var pnlPct = (currentPrice - entry) / entry;

        // 1. 고정 익절 (TP%)
        if (pnlPct >= _config.TakeProfitPct)
        {
            Console.WriteLine($"[EXIT-CALL] TP hit: {pnlPct:P2} >= {_config.TakeProfitPct:P2}");
            return true;
        }

        // 2. 시간 초과 (MaxHoldBars * 5분)
        if (state.EntryTimeCall.HasValue)
        {
            var holdMinutes = (DateTime.UtcNow - state.EntryTimeCall.Value).TotalMinutes;
            if (holdMinutes >= _config.MaxHoldBars * 5)
            {
                Console.WriteLine($"[EXIT-CALL] Time exceeded: {holdMinutes:F0}min >= {_config.MaxHoldBars * 5}min");
                return true;
            }
        }

        // 3. 트레일링 스탑
        if (pnlPct > _config.TrailingPct && state.MaxFavorablePriceCall.HasValue)
        {
            var trailingStop = state.MaxFavorablePriceCall.Value * (1 - _config.TrailingPct);
            if (currentPrice <= trailingStop)
            {
                Console.WriteLine($"[EXIT-CALL] Trailing stop: {currentPrice} <= {trailingStop}");
                return true;
            }
        }

        // 4. RSI2 롤오버 (70 이상 → 50 이하)
        if (candles.Count >= 3)
        {
            var rsiPrev = _indicators.CalculateRSI(candles.SkipLast(1).ToList(), _config.RsiLength);
            var rsiNow = _indicators.CalculateRSI(candles, _config.RsiLength);

            if (rsiPrev >= 70m && rsiNow <= 50m)
            {
                Console.WriteLine($"[EXIT-CALL] RSI rollover: {rsiPrev:F1} -> {rsiNow:F1}");
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Short 포지션 청산 조건 체크
    /// </summary>
    private bool CheckPutExit(TradingState state, decimal currentPrice, List<Candle> candles)
    {
        var entry = state.EntryPricePut!.Value;
        var pnlPct = (entry - currentPrice) / entry;

        // 1. 고정 익절 (TP%)
        if (pnlPct >= _config.TakeProfitPct)
        {
            Console.WriteLine($"[EXIT-PUT] TP hit: {pnlPct:P2} >= {_config.TakeProfitPct:P2}");
            return true;
        }

        // 2. 시간 초과
        if (state.EntryTimePut.HasValue)
        {
            var holdMinutes = (DateTime.UtcNow - state.EntryTimePut.Value).TotalMinutes;
            if (holdMinutes >= _config.MaxHoldBars * 5)
            {
                Console.WriteLine($"[EXIT-PUT] Time exceeded: {holdMinutes:F0}min >= {_config.MaxHoldBars * 5}min");
                return true;
            }
        }

        // 3. 트레일링 스탑
        if (pnlPct > _config.TrailingPct && state.MaxFavorablePricePut.HasValue)
        {
            var trailingStop = state.MaxFavorablePricePut.Value * (1 + _config.TrailingPct);
            if (currentPrice >= trailingStop)
            {
                Console.WriteLine($"[EXIT-PUT] Trailing stop: {currentPrice} >= {trailingStop}");
                return true;
            }
        }

        // 4. RSI2 롤오버 (30 이하 → 50 이상)
        if (candles.Count >= 3)
        {
            var rsiPrev = _indicators.CalculateRSI(candles.SkipLast(1).ToList(), _config.RsiLength);
            var rsiNow = _indicators.CalculateRSI(candles, _config.RsiLength);

            if (rsiPrev <= 30m && rsiNow >= 50m)
            {
                Console.WriteLine($"[EXIT-PUT] RSI rollover: {rsiPrev:F1} -> {rsiNow:F1}");
                return true;
            }
        }

        return false;
    }
}
