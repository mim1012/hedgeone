using Hedgeone.Exchange;
using Hedgeone.Indicators;

namespace Hedgeone.Core;

/// <summary>
/// 헷지 트레이딩 전략 구현
/// PRD 문서의 로직을 따르는 메인 전략 엔진
/// </summary>
public class HedgeStrategy : IHedgeStrategy
{
    private readonly IExchangeAdapter _exchange;
    private readonly IIndicatorService _indicators;
    private readonly IExitRuleEvaluator _exitRules;
    private readonly StrategyConfig _config;
    private readonly Dictionary<string, TradingState> _states;
    private readonly IStateRepository _stateRepo;

    public HedgeStrategy(
        IExchangeAdapter exchange,
        IIndicatorService indicators,
        IExitRuleEvaluator exitRules,
        StrategyConfig config,
        IStateRepository stateRepo)
    {
        _exchange = exchange ?? throw new ArgumentNullException(nameof(exchange));
        _indicators = indicators ?? throw new ArgumentNullException(nameof(indicators));
        _exitRules = exitRules ?? throw new ArgumentNullException(nameof(exitRules));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _stateRepo = stateRepo ?? throw new ArgumentNullException(nameof(stateRepo));

        _states = new Dictionary<string, TradingState>();
    }

    /// <summary>
    /// 전략 시작
    /// </summary>
    public async Task<bool> StartAsync(CancellationToken ct)
    {
        try
        {
            Console.WriteLine("[STRATEGY] Starting Hedge Strategy...");

            // 설정 검증
            _config.Validate();

            // 상태 로드
            var loadedStates = await _stateRepo.LoadAllAsync();
            foreach (var symbol in _config.Symbols)
            {
                if (loadedStates.ContainsKey(symbol))
                {
                    _states[symbol] = loadedStates[symbol];
                    Console.WriteLine($"[STRATEGY] Loaded state for {symbol}");
                }
                else
                {
                    _states[symbol] = new TradingState { Symbol = symbol };
                    Console.WriteLine($"[STRATEGY] Created new state for {symbol}");
                }
            }

            Console.WriteLine("[STRATEGY] Strategy started successfully");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[STRATEGY] Failed to start: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 전략 중지
    /// </summary>
    public async Task StopAsync()
    {
        Console.WriteLine("[STRATEGY] Stopping strategy...");

        // 최종 상태 저장
        await _stateRepo.SaveAllAsync(_states);

        Console.WriteLine("[STRATEGY] Strategy stopped");
    }

    /// <summary>
    /// 일봉 업데이트 (대세 판단)
    /// </summary>
    public void OnNewDaily(string symbol, List<Candle> dailyCandles)
    {
        if (!_states.ContainsKey(symbol))
        {
            Console.WriteLine($"[DAILY-{symbol}] Symbol not in state dictionary");
            return;
        }

        var state = _states[symbol];

        // RSI vs MACD 계산
        var rsi = _indicators.CalculateRSI(dailyCandles, _config.RsiLength);
        var macd = _indicators.CalculateMACDLine(dailyCandles, 1, 1, 1);

        // 대세 판단
        var newRegime = rsi > macd ? "UP" : "DOWN";

        Console.WriteLine($"[DAILY-{symbol}] RSI={rsi:F2}, MACD={macd:F2}, Regime={newRegime}");

        // 대세 변경 시 반대 포지션 청산
        if (state.Regime != newRegime)
        {
            Console.WriteLine($"[DAILY-{symbol}] Regime changed: {state.Regime} -> {newRegime}");

            if (newRegime == "UP" && state.PosPut > 0)
            {
                Console.WriteLine($"[DAILY-{symbol}] Closing all SHORT positions (regime turned UP)");
                _ = CloseAllPutsAsync(symbol);
            }

            if (newRegime == "DOWN" && state.PosCall > 0)
            {
                Console.WriteLine($"[DAILY-{symbol}] Closing all LONG positions (regime turned DOWN)");
                _ = CloseAllCallsAsync(symbol);
            }

            state.Regime = newRegime;
        }
    }

    /// <summary>
    /// 5분봉 업데이트 (신호 생성 및 포지션 관리)
    /// 고객 메일 로직 기반 구현
    /// </summary>
    public void OnNew5m(string symbol, List<Candle> candles5m, decimal currentPrice, DateTime timestamp)
    {
        if (!_states.ContainsKey(symbol))
        {
            Console.WriteLine($"[5M-{symbol}] Symbol not in state dictionary");
            return;
        }

        var state = _states[symbol];

        // MaxFavorablePrice 업데이트
        UpdateMaxFavorablePrice(state, currentPrice);

        // 신호 계산
        var longSignal = _indicators.SignalLong(candles5m, _config.RsiLength);
        var shortSignal = _indicators.SignalShort(candles5m, _config.RsiLength);

        // UP 대세 로직
        if (state.Regime == "UP")
        {
            HandleUpRegime(symbol, state, currentPrice, longSignal, shortSignal, candles5m);
        }
        // DOWN 대세 로직
        else if (state.Regime == "DOWN")
        {
            HandleDownRegime(symbol, state, currentPrice, longSignal, shortSignal, candles5m);
        }

        // 상태 저장
        _ = _stateRepo.SaveAsync(symbol, state);
    }

    /// <summary>
    /// UP 대세 처리 로직 (고객 메일 기반)
    /// - 콜만 신규 진입, 풋은 헷지용
    /// </summary>
    private void HandleUpRegime(string symbol, TradingState state, decimal currentPrice, bool longSignal, bool shortSignal, List<Candle> candles5m)
    {
        var pnlCall = state.PnlCall(currentPrice);

        // 1. 콜 신호 + 콜 포지션 없음 → 콜 진입
        if (longSignal && state.PosCall == 0)
        {
            Console.WriteLine($"[UP-{symbol}] Long signal, entering LONG");
            _ = EnterLongAsync(symbol, currentPrice);

            // 기존 풋 헷지가 있으면 무조건 청산
            if (state.PosPut > 0)
            {
                Console.WriteLine($"[UP-{symbol}] Clearing PUT hedge unconditionally");
                _ = CloseAllPutsAsync(symbol);
            }
            return;
        }

        // 2. 콜 보유 중 관리
        if (state.PosCall > 0)
        {
            if (pnlCall >= 0)
            {
                // 이익/본전: exit_rule_hit 체크
                if (_exitRules.ExitRuleHit(state, currentPrice, candles5m))
                {
                    Console.WriteLine($"[UP-{symbol}] Exit rule hit (profit), closing all");
                    _ = CloseAllPositionsAsync(symbol);
                    return;
                }
            }
            else
            {
                // 손실: 풋 헷지 진입 (없으면)
                if (state.PosPut == 0)
                {
                    Console.WriteLine($"[UP-{symbol}] LONG in loss, entering PUT hedge");
                    _ = EnterShortAsync(symbol, currentPrice);
                }
            }
        }

        // 3. 콜 신호 재발 + 풋 헷지 보유 → 풋 헷지 즉시 청산 (손익 불문)
        if (longSignal && state.PosPut > 0)
        {
            Console.WriteLine($"[UP-{symbol}] Long signal reappeared, closing PUT hedge (regardless of PnL)");
            _ = CloseAllPutsAsync(symbol);
        }

        // 4. 풋 신호 발생 + 콜 보유 중 → 손익에 따라 처리
        if (shortSignal && state.PosCall > 0)
        {
            if (pnlCall >= 0)
            {
                // 이익이면 콜 청산 (이익 확정)
                Console.WriteLine($"[UP-{symbol}] Short signal + LONG profit, closing LONG to lock profit");
                _ = CloseAllCallsAsync(symbol);
            }
            else
            {
                // 손실이면 풋 헷지 진입
                if (state.PosPut == 0)
                {
                    Console.WriteLine($"[UP-{symbol}] Short signal + LONG loss, entering PUT hedge");
                    _ = EnterShortAsync(symbol, currentPrice);
                }
            }
        }
    }

    /// <summary>
    /// DOWN 대세 처리 로직 (고객 메일 기반)
    /// - 풋만 신규 진입, 콜은 헷지용
    /// </summary>
    private void HandleDownRegime(string symbol, TradingState state, decimal currentPrice, bool longSignal, bool shortSignal, List<Candle> candles5m)
    {
        var pnlPut = state.PnlPut(currentPrice);

        // 1. 풋 신호 + 풋 포지션 없음 → 풋 진입
        if (shortSignal && state.PosPut == 0)
        {
            Console.WriteLine($"[DOWN-{symbol}] Short signal, entering SHORT");
            _ = EnterShortAsync(symbol, currentPrice);

            // 기존 콜 헷지가 있으면 무조건 청산
            if (state.PosCall > 0)
            {
                Console.WriteLine($"[DOWN-{symbol}] Clearing CALL hedge unconditionally");
                _ = CloseAllCallsAsync(symbol);
            }
            return;
        }

        // 2. 풋 보유 중 관리
        if (state.PosPut > 0)
        {
            if (pnlPut >= 0)
            {
                // 이익/본전: exit_rule_hit 체크
                if (_exitRules.ExitRuleHit(state, currentPrice, candles5m))
                {
                    Console.WriteLine($"[DOWN-{symbol}] Exit rule hit (profit), closing all");
                    _ = CloseAllPositionsAsync(symbol);
                    return;
                }
            }
            else
            {
                // 손실: 콜 헷지 진입 (없으면)
                if (state.PosCall == 0)
                {
                    Console.WriteLine($"[DOWN-{symbol}] SHORT in loss, entering CALL hedge");
                    _ = EnterLongAsync(symbol, currentPrice);
                }
            }
        }

        // 3. 풋 신호 재발 + 콜 헷지 보유 → 콜 헷지 즉시 청산 (손익 불문)
        if (shortSignal && state.PosCall > 0)
        {
            Console.WriteLine($"[DOWN-{symbol}] Short signal reappeared, closing CALL hedge (regardless of PnL)");
            _ = CloseAllCallsAsync(symbol);
        }

        // 4. 콜 신호 발생 + 풋 보유 중 → 손익에 따라 처리
        if (longSignal && state.PosPut > 0)
        {
            if (pnlPut >= 0)
            {
                // 이익이면 풋 청산 (이익 확정)
                Console.WriteLine($"[DOWN-{symbol}] Long signal + SHORT profit, closing SHORT to lock profit");
                _ = CloseAllPutsAsync(symbol);
            }
            else
            {
                // 손실이면 콜 헷지 진입
                if (state.PosCall == 0)
                {
                    Console.WriteLine($"[DOWN-{symbol}] Long signal + SHORT loss, entering CALL hedge");
                    _ = EnterLongAsync(symbol, currentPrice);
                }
            }
        }
    }

    /// <summary>
    /// MaxFavorablePrice 업데이트
    /// </summary>
    private void UpdateMaxFavorablePrice(TradingState state, decimal currentPrice)
    {
        // Long 포지션
        if (state.PosCall > 0)
        {
            if (!state.MaxFavorablePriceCall.HasValue || currentPrice > state.MaxFavorablePriceCall.Value)
            {
                state.MaxFavorablePriceCall = currentPrice;
            }
        }

        // Short 포지션
        if (state.PosPut > 0)
        {
            if (!state.MaxFavorablePricePut.HasValue || currentPrice < state.MaxFavorablePricePut.Value)
            {
                state.MaxFavorablePricePut = currentPrice;
            }
        }
    }

    // ===== 진입/청산 메서드 =====

    private async Task EnterLongAsync(string symbol, decimal currentPrice)
    {
        var state = _states[symbol];

        var result = await _exchange.BuyLongAsync(symbol, _config.CallSize);
        if (result.Success)
        {
            state.PosCall = result.FilledQuantity;
            state.EntryPriceCall = result.AvgPrice;
            state.EntryTimeCall = DateTime.UtcNow;
            state.MaxFavorablePriceCall = currentPrice;

            Console.WriteLine($"[ENTER-LONG-{symbol}] Qty={result.FilledQuantity}, Price={result.AvgPrice}");
        }
        else
        {
            Console.WriteLine($"[ENTER-LONG-{symbol}] FAILED: {result.Error}");
        }
    }

    private async Task EnterShortAsync(string symbol, decimal currentPrice)
    {
        var state = _states[symbol];

        var result = await _exchange.BuyShortAsync(symbol, _config.CallSize);
        if (result.Success)
        {
            state.PosPut = result.FilledQuantity;
            state.EntryPricePut = result.AvgPrice;
            state.EntryTimePut = DateTime.UtcNow;
            state.MaxFavorablePricePut = currentPrice;

            Console.WriteLine($"[ENTER-SHORT-{symbol}] Qty={result.FilledQuantity}, Price={result.AvgPrice}");
        }
        else
        {
            Console.WriteLine($"[ENTER-SHORT-{symbol}] FAILED: {result.Error}");
        }
    }

    private async Task CloseAllCallsAsync(string symbol)
    {
        var state = _states[symbol];
        if (state.PosCall == 0) return;

        var result = await _exchange.SellLongAsync(symbol, state.PosCall);
        if (result.Success)
        {
            Console.WriteLine($"[CLOSE-LONG-{symbol}] Qty={result.FilledQuantity}, Price={result.AvgPrice}");

            state.PosCall = 0;
            state.EntryPriceCall = null;
            state.EntryTimeCall = null;
            state.MaxFavorablePriceCall = null;
        }
        else
        {
            Console.WriteLine($"[CLOSE-LONG-{symbol}] FAILED: {result.Error}");
        }
    }

    private async Task CloseAllPutsAsync(string symbol)
    {
        var state = _states[symbol];
        if (state.PosPut == 0) return;

        var result = await _exchange.SellShortAsync(symbol, state.PosPut);
        if (result.Success)
        {
            Console.WriteLine($"[CLOSE-SHORT-{symbol}] Qty={result.FilledQuantity}, Price={result.AvgPrice}");

            state.PosPut = 0;
            state.EntryPricePut = null;
            state.EntryTimePut = null;
            state.MaxFavorablePricePut = null;
        }
        else
        {
            Console.WriteLine($"[CLOSE-SHORT-{symbol}] FAILED: {result.Error}");
        }
    }

    private async Task CloseAllPositionsAsync(string symbol)
    {
        await CloseAllCallsAsync(symbol);
        await CloseAllPutsAsync(symbol);
    }

    /// <summary>
    /// 현재 상태 조회
    /// </summary>
    public Dictionary<string, TradingState> GetCurrentStates()
    {
        return new Dictionary<string, TradingState>(_states);
    }
}
