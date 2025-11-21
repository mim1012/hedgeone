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
    /// </summary>
    public void OnNew5m(string symbol, List<Candle> candles5m, decimal currentPrice, DateTime timestamp)
    {
        if (!_states.ContainsKey(symbol))
        {
            Console.WriteLine($"[5M-{symbol}] Symbol not in state dictionary");
            return;
        }

        var state = _states[symbol];

        // 1. 청산 규칙 체크
        if (_exitRules.ExitRuleHit(state, currentPrice, candles5m))
        {
            Console.WriteLine($"[5M-{symbol}] Exit rule triggered");
            _ = CloseAllPositionsAsync(symbol);
            return;
        }

        // 2. MaxFavorablePrice 업데이트
        UpdateMaxFavorablePrice(state, currentPrice);

        // 3. 신호 계산
        var longSignal = _indicators.SignalLong(candles5m, _config.RsiLength);
        var shortSignal = _indicators.SignalShort(candles5m, _config.RsiLength);

        // 4. UP 대세 로직
        if (state.Regime == "UP")
        {
            HandleUpRegime(symbol, state, currentPrice, longSignal);
        }
        // 5. DOWN 대세 로직
        else if (state.Regime == "DOWN")
        {
            HandleDownRegime(symbol, state, currentPrice, shortSignal);
        }

        // 6. 상태 저장
        _ = _stateRepo.SaveAsync(symbol, state);
    }

    /// <summary>
    /// UP 대세 처리 로직
    /// </summary>
    private void HandleUpRegime(string symbol, TradingState state, decimal currentPrice, bool longSignal)
    {
        // Long 신호 발생 시 진입
        if (longSignal && state.PosCall == 0)
        {
            Console.WriteLine($"[UP-{symbol}] Long signal detected, entering LONG");
            _ = EnterLongAsync(symbol, currentPrice);
            return;
        }

        // Long 포지션 손실 시 Short 헷지
        if (state.PosCall > 0)
        {
            var pnlPct = state.PnlPctCall(currentPrice);
            if (pnlPct <= _config.HedgeLossPct && state.PosPut == 0)
            {
                Console.WriteLine($"[UP-{symbol}] LONG loss={pnlPct:P2}, entering SHORT hedge");
                _ = EnterShortAsync(symbol, currentPrice);
            }
        }

        // Long 신호 재발생 시 Short 헷지 청산
        if (longSignal && state.PosPut > 0)
        {
            Console.WriteLine($"[UP-{symbol}] Long signal reappeared, closing SHORT hedge");
            _ = CloseAllPutsAsync(symbol);
        }
    }

    /// <summary>
    /// DOWN 대세 처리 로직
    /// </summary>
    private void HandleDownRegime(string symbol, TradingState state, decimal currentPrice, bool shortSignal)
    {
        // Short 신호 발생 시 진입
        if (shortSignal && state.PosPut == 0)
        {
            Console.WriteLine($"[DOWN-{symbol}] Short signal detected, entering SHORT");
            _ = EnterShortAsync(symbol, currentPrice);
            return;
        }

        // Short 포지션 손실 시 Long 헷지
        if (state.PosPut > 0)
        {
            var pnlPct = state.PnlPctPut(currentPrice);
            if (pnlPct <= _config.HedgeLossPct && state.PosCall == 0)
            {
                Console.WriteLine($"[DOWN-{symbol}] SHORT loss={pnlPct:P2}, entering LONG hedge");
                _ = EnterLongAsync(symbol, currentPrice);
            }
        }

        // Short 신호 재발생 시 Long 헷지 청산
        if (shortSignal && state.PosCall > 0)
        {
            Console.WriteLine($"[DOWN-{symbol}] Short signal reappeared, closing LONG hedge");
            _ = CloseAllCallsAsync(symbol);
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
