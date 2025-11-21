# Hedgeone System Architecture

**버전**: 1.0
**작성일**: 2025-11-21
**언어**: C# .NET 6/7
**UI 프레임워크**: WPF (MVVM)

---

## 1. 시스템 개요

Hedgeone은 **5개의 독립적인 C# 프로젝트**로 구성된 **모듈형 아키텍처**를 채택합니다.

### 1.1 프로젝트 구조

```
Hedgeone.sln
├── Hedgeone.Core              (Class Library) - 전략 엔진
├── Hedgeone.Indicators        (Class Library) - 기술 지표 계산
├── Hedgeone.Exchange          (Class Library) - Binance API 어댑터
├── Hedgeone.UI                (WPF Application) - 사용자 인터페이스
└── Hedgeone.Tests             (xUnit Test Project) - 단위 테스트
```

---

## 2. 계층별 설계

### 2.1 의존성 다이어그램

```
┌─────────────────┐
│  Hedgeone.UI    │ (WPF - Presentation Layer)
└────────┬────────┘
         │ depends on
         ▼
┌─────────────────┐
│  Hedgeone.Core  │ (Business Logic Layer)
└────┬────────┬───┘
     │        │
     │        │ depends on
     ▼        ▼
┌─────────────────┐    ┌──────────────────────┐
│ Hedgeone.       │    │  Hedgeone.Exchange   │
│ Indicators      │    │  (Data Access Layer) │
└─────────────────┘    └──────────────────────┘
```

### 2.2 패키지 관리

| 프로젝트 | 외부 의존성 |
|----------|-------------|
| Hedgeone.Core | `Newtonsoft.Json` (상태 저장) |
| Hedgeone.Indicators | 없음 (순수 계산) |
| Hedgeone.Exchange | `Binance.Net`, `CryptoExchange.Net` |
| Hedgeone.UI | `Microsoft.Xaml.Behaviors.Wpf` |
| Hedgeone.Tests | `xUnit`, `Moq` |

---

## 3. Hedgeone.Core (전략 엔진)

### 3.1 클래스 다이어그램

```csharp
namespace Hedgeone.Core
{
    // ===== 인터페이스 =====
    public interface IHedgeStrategy
    {
        void OnNewDaily(string symbol, List<Candle> dailyCandles);
        void OnNew5m(string symbol, decimal currentPrice, DateTime timestamp);
        Task<bool> StartAsync(CancellationToken ct);
        Task StopAsync();
    }

    public interface IExitRuleEvaluator
    {
        bool ExitRuleHit(TradingState state, decimal currentPrice,
                         IIndicatorService indicators);
    }

    // ===== 모델 =====
    public class TradingState
    {
        public string Symbol { get; set; }
        public string Regime { get; set; }  // "UP" or "DOWN"

        // Long (Call) 포지션
        public decimal PosCall { get; set; }
        public decimal? EntryPriceCall { get; set; }
        public DateTime? EntryTimeCall { get; set; }
        public decimal? MaxFavorablePriceCall { get; set; }

        // Short (Put) 포지션
        public decimal PosPut { get; set; }
        public decimal? EntryPricePut { get; set; }
        public DateTime? EntryTimePut { get; set; }
        public decimal? MaxFavorablePricePut { get; set; }

        // 계산 메서드
        public decimal PnlCall(decimal currentPrice);
        public decimal PnlPut(decimal currentPrice);
    }

    public class StrategyConfig
    {
        public decimal CallSize { get; set; } = 10m;
        public int RsiLength { get; set; } = 2;
        public decimal TakeProfitPct { get; set; } = 0.01m;  // 1%
        public decimal TrailingPct { get; set; } = 0.005m;   // 0.5%
        public int MaxHoldBars { get; set; } = 24;           // 2시간
    }

    // ===== 구현 =====
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
            // DI 주입
        }

        public void OnNewDaily(string symbol, List<Candle> dailyCandles)
        {
            // 대세 판단 로직
            var rsi = _indicators.CalculateRSI(dailyCandles, _config.RsiLength);
            var macd = _indicators.CalculateMACDLine(dailyCandles, 1, 1, 1);

            var newRegime = rsi > macd ? "UP" : "DOWN";
            var state = _states[symbol];

            if (state.Regime != newRegime)
            {
                // 대세 변경 시 반대 포지션 청산
                if (newRegime == "UP" && state.PosPut > 0)
                    CloseAllPuts(symbol);

                if (newRegime == "DOWN" && state.PosCall > 0)
                    CloseAllCalls(symbol);

                state.Regime = newRegime;
            }
        }

        public void OnNew5m(string symbol, decimal currentPrice, DateTime timestamp)
        {
            // 5분봉 로직
            // 1. 신호 계산
            // 2. 신규 진입 또는 헷지 로직
            // 3. exit_rule_hit 체크
        }
    }
}
```

### 3.2 Exit Rule Evaluator

```csharp
public class ExitRuleEvaluator : IExitRuleEvaluator
{
    private readonly StrategyConfig _config;

    public bool ExitRuleHit(TradingState state, decimal currentPrice,
                            IIndicatorService indicators)
    {
        // 콜 포지션 체크
        if (state.PosCall > 0 && state.EntryPriceCall.HasValue)
        {
            var entry = state.EntryPriceCall.Value;
            var pnlPct = (currentPrice - entry) / entry;

            // 1. 고정 TP
            if (pnlPct >= _config.TakeProfitPct)
                return true;

            // 2. 시간 초과
            if (state.EntryTimeCall.HasValue)
            {
                var holdMinutes = (DateTime.UtcNow - state.EntryTimeCall.Value).TotalMinutes;
                if (holdMinutes >= _config.MaxHoldBars * 5)
                    return true;
            }

            // 3. 트레일링 스탑
            if (pnlPct > _config.TrailingPct && state.MaxFavorablePriceCall.HasValue)
            {
                if (currentPrice <= state.MaxFavorablePriceCall.Value * (1 - _config.TrailingPct))
                    return true;
            }

            // 4. RSI2 롤오버 (별도 체크 필요)
        }

        // 풋 포지션도 동일 로직
        return false;
    }
}
```

---

## 4. Hedgeone.Indicators (기술 지표)

### 4.1 인터페이스

```csharp
namespace Hedgeone.Indicators
{
    public interface IIndicatorService
    {
        decimal CalculateRSI(List<Candle> candles, int period);
        decimal CalculateRSI(List<decimal> prices, int period);

        decimal CalculateMACDLine(List<Candle> candles, int fast, int slow, int signal);
        decimal CalculateMACDLine(List<decimal> prices, int fast, int slow, int signal);

        bool SignalLong(List<Candle> candles, int rsiLen);
        bool SignalShort(List<Candle> candles, int rsiLen);
    }

    public class Candle
    {
        public DateTime OpenTime { get; set; }
        public decimal Open { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public decimal Close { get; set; }
        public decimal Volume { get; set; }
    }
}
```

### 4.2 구현

```csharp
public class TechnicalIndicators : IIndicatorService
{
    public decimal CalculateRSI(List<decimal> prices, int period)
    {
        // RSI = 100 - (100 / (1 + RS))
        // RS = Average Gain / Average Loss

        var changes = new List<decimal>();
        for (int i = 1; i < prices.Count; i++)
            changes.Add(prices[i] - prices[i-1]);

        var gains = changes.Where(x => x > 0).ToList();
        var losses = changes.Where(x => x < 0).Select(Math.Abs).ToList();

        var avgGain = gains.Count > 0 ? gains.Average() : 0;
        var avgLoss = losses.Count > 0 ? losses.Average() : 0;

        if (avgLoss == 0) return 100m;

        var rs = avgGain / avgLoss;
        return 100m - (100m / (1m + rs));
    }

    public decimal CalculateMACDLine(List<decimal> prices, int fast, int slow, int signal)
    {
        // MACD Line = EMA(fast) - EMA(slow)
        var emaFast = CalculateEMA(prices, fast);
        var emaSlow = CalculateEMA(prices, slow);
        return emaFast - emaSlow;
    }

    private decimal CalculateEMA(List<decimal> prices, int period)
    {
        // EMA 계산 로직
        decimal multiplier = 2m / (period + 1);
        decimal ema = prices[0];

        for (int i = 1; i < prices.Count; i++)
            ema = (prices[i] * multiplier) + (ema * (1 - multiplier));

        return ema;
    }
}
```

---

## 5. Hedgeone.Exchange (Binance API)

### 5.1 인터페이스

```csharp
namespace Hedgeone.Exchange
{
    public interface IExchangeAdapter
    {
        // 시장 데이터
        Task<decimal> GetLastPriceAsync(string symbol);
        Task<List<Candle>> GetCandlesAsync(string symbol, string interval, int limit);

        // 주문 (Hedge Mode)
        Task<OrderResult> BuyLongAsync(string symbol, decimal quantity);
        Task<OrderResult> SellLongAsync(string symbol, decimal quantity);
        Task<OrderResult> BuyShortAsync(string symbol, decimal quantity);
        Task<OrderResult> SellShortAsync(string symbol, decimal quantity);

        // 포지션 조회
        Task<PositionInfo> GetPositionAsync(string symbol, string positionSide);

        // WebSocket
        Task SubscribeCandleUpdatesAsync(string symbol, string interval,
                                          Action<Candle> onCandle);
    }

    public class OrderResult
    {
        public bool Success { get; set; }
        public string OrderId { get; set; }
        public decimal FilledQuantity { get; set; }
        public decimal AvgPrice { get; set; }
        public string Error { get; set; }
    }

    public class PositionInfo
    {
        public string Symbol { get; set; }
        public string PositionSide { get; set; }  // "LONG" or "SHORT"
        public decimal Quantity { get; set; }
        public decimal EntryPrice { get; set; }
        public decimal UnrealizedPnl { get; set; }
    }
}
```

### 5.2 Binance 구현

```csharp
using Binance.Net.Clients;
using Binance.Net.Objects.Models.Futures;

public class BinanceFuturesAdapter : IExchangeAdapter
{
    private readonly BinanceClient _client;
    private readonly BinanceSocketClient _socketClient;

    public BinanceFuturesAdapter(string apiKey, string apiSecret, bool testnet = false)
    {
        var options = new BinanceClientOptions();
        if (testnet)
        {
            options.UsdFuturesApiOptions.BaseAddress =
                "https://testnet.binancefuture.com";
        }

        _client = new BinanceClient(options);
        _socketClient = new BinanceSocketClient();
    }

    public async Task<decimal> GetLastPriceAsync(string symbol)
    {
        var result = await _client.UsdFuturesApi.ExchangeData
            .GetPriceAsync(symbol);

        if (!result.Success)
            throw new Exception($"Failed to get price: {result.Error.Message}");

        return result.Data.Price;
    }

    public async Task<OrderResult> BuyLongAsync(string symbol, decimal quantity)
    {
        var result = await _client.UsdFuturesApi.Trading.PlaceOrderAsync(
            symbol: symbol,
            side: Binance.Net.Enums.OrderSide.Buy,
            type: Binance.Net.Enums.FuturesOrderType.Market,
            quantity: quantity,
            positionSide: Binance.Net.Enums.PositionSide.Long
        );

        return new OrderResult
        {
            Success = result.Success,
            OrderId = result.Data?.Id.ToString(),
            FilledQuantity = result.Data?.QuantityFilled ?? 0,
            AvgPrice = result.Data?.AveragePrice ?? 0,
            Error = result.Error?.Message
        };
    }

    // SellLong, BuyShort, SellShort도 유사하게 구현
}
```

---

## 6. Hedgeone.UI (WPF MVVM)

### 6.1 MVVM 구조

```
Hedgeone.UI/
├── App.xaml
├── App.xaml.cs
├── MainWindow.xaml
├── MainWindow.xaml.cs
├── ViewModels/
│   ├── MainViewModel.cs
│   ├── PositionViewModel.cs
│   └── StrategyConfigViewModel.cs
├── Views/
│   ├── PositionTableView.xaml
│   └── LogView.xaml
├── Models/
│   └── UIModels.cs
└── Services/
    └── ServiceRunner.cs
```

### 6.2 MainViewModel

```csharp
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

namespace Hedgeone.UI.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly IHedgeStrategy _strategy;
        private readonly ServiceRunner _runner;

        // Properties
        public string ApiKey { get; set; }
        public string ApiSecret { get; set; }
        public decimal CallSize { get; set; } = 10m;
        public decimal TakeProfitPct { get; set; } = 1.0m;

        public ObservableCollection<PositionViewModel> Positions { get; set; }
        public ObservableCollection<string> Logs { get; set; }

        // Commands
        public ICommand StartCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand TestConnectionCommand { get; }

        // Constructor
        public MainViewModel()
        {
            Positions = new ObservableCollection<PositionViewModel>();
            Logs = new ObservableCollection<string>();

            StartCommand = new RelayCommand(Start);
            StopCommand = new RelayCommand(Stop);
            TestConnectionCommand = new RelayCommand(TestConnection);
        }

        private async void Start()
        {
            AddLog("전략 시작...");
            await _runner.StartAsync();
        }

        private async void Stop()
        {
            AddLog("전략 중지...");
            await _runner.StopAsync();
        }

        private void AddLog(string message)
        {
            Logs.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {message}");
        }
    }
}
```

---

## 7. 상태 관리 및 영속화

### 7.1 IStateRepository

```csharp
namespace Hedgeone.Core
{
    public interface IStateRepository
    {
        Task<Dictionary<string, TradingState>> LoadAllAsync();
        Task SaveAsync(string symbol, TradingState state);
        Task SaveAllAsync(Dictionary<string, TradingState> states);
    }
}
```

### 7.2 JSON 구현

```csharp
using Newtonsoft.Json;

public class JsonStateRepository : IStateRepository
{
    private readonly string _filePath;

    public JsonStateRepository(string filePath = "state.json")
    {
        _filePath = filePath;
    }

    public async Task<Dictionary<string, TradingState>> LoadAllAsync()
    {
        if (!File.Exists(_filePath))
            return new Dictionary<string, TradingState>();

        var json = await File.ReadAllTextAsync(_filePath);
        return JsonConvert.DeserializeObject<Dictionary<string, TradingState>>(json)
               ?? new Dictionary<string, TradingState>();
    }

    public async Task SaveAllAsync(Dictionary<string, TradingState> states)
    {
        var json = JsonConvert.SerializeObject(states, Formatting.Indented);
        await File.WriteAllTextAsync(_filePath, json);
    }
}
```

---

## 8. 실행 흐름

### 8.1 ServiceRunner

```csharp
public class ServiceRunner
{
    private readonly IHedgeStrategy _strategy;
    private CancellationTokenSource _cts;
    private Task _runnerTask;

    public async Task StartAsync()
    {
        _cts = new CancellationTokenSource();
        _runnerTask = RunAsync(_cts.Token);
    }

    public async Task StopAsync()
    {
        _cts?.Cancel();
        await _runnerTask;
    }

    private async Task RunAsync(CancellationToken ct)
    {
        DateTime? lastDailyCheck = null;
        DateTime? last5mCheck = null;

        while (!ct.IsCancellationRequested)
        {
            var now = DateTime.UtcNow;

            // 일봉 체크 (UTC 00:00)
            if (lastDailyCheck == null || now.Date > lastDailyCheck.Value.Date)
            {
                await OnDailyAsync();
                lastDailyCheck = now;
            }

            // 5분봉 체크
            if (last5mCheck == null || now.Minute % 5 == 0 && now.Second < 5)
            {
                if (last5mCheck == null ||
                    now.ToString("yyyy-MM-dd HH:mm") != last5mCheck.Value.ToString("yyyy-MM-dd HH:mm"))
                {
                    await On5mAsync();
                    last5mCheck = now;
                }
            }

            await Task.Delay(1000, ct);
        }
    }
}
```

---

## 9. 배포 설정

### 9.1 .csproj (Hedgeone.UI)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net7.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <PublishSingleFile>true</PublishSingleFile>
    <SelfContained>true</SelfContained>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
  </PropertyGroup>
</Project>
```

### 9.2 빌드 명령

```bash
dotnet publish Hedgeone.UI -c Release -r win-x64 --self-contained
```

---

## 10. 보안 고려사항

1. **API 키 저장**: 평문 저장 금지 → DPAPI 암호화 또는 환경변수
2. **상태 파일**: 중요 정보 암호화
3. **로그**: 민감 정보 마스킹
4. **HTTPS 전용**: Binance API 통신

---

**문서 버전**: 1.0
**최종 수정일**: 2025-11-21
**작성자**: Claude Code - Orchestrator Agent
