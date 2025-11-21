using Binance.Net.Clients;
using Binance.Net.Enums;
using CryptoExchange.Net.Authentication;
using Hedgeone.Indicators;

namespace Hedgeone.Exchange;

/// <summary>
/// Binance Futures API 어댑터 구현
/// </summary>
public class BinanceFuturesAdapter : IExchangeAdapter
{
    private readonly BinanceRestClient _restClient;
    private readonly BinanceSocketClient _socketClient;

    /// <summary>
    /// BinanceFuturesAdapter 생성자
    /// </summary>
    /// <param name="apiKey">Binance API Key</param>
    /// <param name="apiSecret">Binance API Secret</param>
    /// <param name="testnet">Testnet 사용 여부 (기본: true)</param>
    public BinanceFuturesAdapter(string apiKey, string apiSecret, bool testnet = true)
    {
        _restClient = new BinanceRestClient(options =>
        {
            options.ApiCredentials = new ApiCredentials(apiKey, apiSecret);
            // TODO: Testnet 설정 (Binance.Net v11에서는 별도 설정 필요)
        });

        _socketClient = new BinanceSocketClient();

        Console.WriteLine($"[EXCHANGE] Initialized BinanceFuturesAdapter");
    }

    // ===== 시장 데이터 =====

    /// <summary>
    /// 최신 가격 조회
    /// </summary>
    public async Task<decimal> GetLastPriceAsync(string symbol)
    {
        try
        {
            var result = await _restClient.UsdFuturesApi.ExchangeData.GetPriceAsync(symbol);

            if (!result.Success)
            {
                throw new Exception($"Failed to get price for {symbol}: {result.Error?.Message}");
            }

            return result.Data.Price;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EXCHANGE] GetLastPrice error: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 캔들 데이터 조회
    /// </summary>
    public async Task<List<Candle>> GetCandlesAsync(string symbol, string interval, int limit)
    {
        try
        {
            // interval 파싱 (예: "1d", "5m")
            var klineInterval = ParseInterval(interval);

            var result = await _restClient.UsdFuturesApi.ExchangeData.GetKlinesAsync(
                symbol,
                klineInterval,
                limit: limit
            );

            if (!result.Success)
            {
                throw new Exception($"Failed to get candles for {symbol}: {result.Error?.Message}");
            }

            var candles = result.Data.Select(k => new Candle
            {
                OpenTime = k.OpenTime,
                Open = k.OpenPrice,
                High = k.HighPrice,
                Low = k.LowPrice,
                Close = k.ClosePrice,
                Volume = k.Volume
            }).ToList();

            return candles;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EXCHANGE] GetCandles error: {ex.Message}");
            throw;
        }
    }

    // ===== 주문 (Hedge Mode) =====

    /// <summary>
    /// Long 포지션 진입 (Buy)
    /// </summary>
    public async Task<OrderResult> BuyLongAsync(string symbol, decimal quantity)
    {
        return await PlaceOrderAsync(symbol, OrderSide.Buy, PositionSide.Long, quantity);
    }

    /// <summary>
    /// Long 포지션 청산 (Sell)
    /// </summary>
    public async Task<OrderResult> SellLongAsync(string symbol, decimal quantity)
    {
        return await PlaceOrderAsync(symbol, OrderSide.Sell, PositionSide.Long, quantity);
    }

    /// <summary>
    /// Short 포지션 진입 (Sell)
    /// </summary>
    public async Task<OrderResult> BuyShortAsync(string symbol, decimal quantity)
    {
        return await PlaceOrderAsync(symbol, OrderSide.Sell, PositionSide.Short, quantity);
    }

    /// <summary>
    /// Short 포지션 청산 (Buy)
    /// </summary>
    public async Task<OrderResult> SellShortAsync(string symbol, decimal quantity)
    {
        return await PlaceOrderAsync(symbol, OrderSide.Buy, PositionSide.Short, quantity);
    }

    /// <summary>
    /// 주문 실행 (내부 메서드)
    /// </summary>
    private async Task<OrderResult> PlaceOrderAsync(string symbol, OrderSide side, PositionSide positionSide, decimal quantity)
    {
        try
        {
            var result = await _restClient.UsdFuturesApi.Trading.PlaceOrderAsync(
                symbol: symbol,
                side: side,
                type: FuturesOrderType.Market,
                quantity: quantity,
                positionSide: positionSide
            );

            if (!result.Success)
            {
                return new OrderResult
                {
                    Success = false,
                    Error = result.Error?.Message ?? "Unknown error"
                };
            }

            return new OrderResult
            {
                Success = true,
                OrderId = result.Data.Id.ToString(),
                FilledQuantity = result.Data.Quantity,
                AvgPrice = result.Data.AveragePrice,
                Error = null
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EXCHANGE] PlaceOrder error: {ex.Message}");
            return new OrderResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    // ===== 포지션 조회 =====

    /// <summary>
    /// 포지션 정보 조회
    /// </summary>
    public async Task<PositionInfo> GetPositionAsync(string symbol, string positionSide)
    {
        try
        {
            var result = await _restClient.UsdFuturesApi.Account.GetPositionInformationAsync(symbol);

            if (!result.Success)
            {
                throw new Exception($"Failed to get position for {symbol}: {result.Error?.Message}");
            }

            var position = result.Data.FirstOrDefault(p =>
                p.Symbol == symbol &&
                p.PositionSide.ToString().Equals(positionSide, StringComparison.OrdinalIgnoreCase)
            );

            if (position == null)
            {
                return new PositionInfo
                {
                    Symbol = symbol,
                    PositionSide = positionSide,
                    Quantity = 0,
                    EntryPrice = 0,
                    UnrealizedPnl = 0
                };
            }

            return new PositionInfo
            {
                Symbol = position.Symbol,
                PositionSide = position.PositionSide.ToString(),
                Quantity = Math.Abs(position.Quantity),
                EntryPrice = position.EntryPrice,
                UnrealizedPnl = position.UnrealizedPnl
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EXCHANGE] GetPosition error: {ex.Message}");
            throw;
        }
    }

    // ===== WebSocket =====

    /// <summary>
    /// 캔들 업데이트 구독
    /// TODO: Binance.Net v11 WebSocket API 업데이트 필요
    /// </summary>
    public async Task SubscribeCandleUpdatesAsync(string symbol, string interval, Action<Candle> onCandle)
    {
        await Task.CompletedTask;  // Async signature 유지

        Console.WriteLine($"[EXCHANGE] WebSocket subscription for {symbol} {interval} - TODO: Implement Binance.Net v11 API");

        // TODO: Binance.Net v11에서는 WebSocket API가 변경됨
        // 참고: https://jkorf.github.io/Binance.Net/
        // 현재는 REST API로만 구현하고, WebSocket은 향후 업데이트 예정

        throw new NotImplementedException("WebSocket subscription will be implemented in future version");
    }

    // ===== Helper Methods =====

    /// <summary>
    /// interval 문자열을 KlineInterval로 변환
    /// </summary>
    private KlineInterval ParseInterval(string interval)
    {
        return interval.ToLower() switch
        {
            "1m" => KlineInterval.OneMinute,
            "3m" => KlineInterval.ThreeMinutes,
            "5m" => KlineInterval.FiveMinutes,
            "15m" => KlineInterval.FifteenMinutes,
            "30m" => KlineInterval.ThirtyMinutes,
            "1h" => KlineInterval.OneHour,
            "2h" => KlineInterval.TwoHour,
            "4h" => KlineInterval.FourHour,
            "6h" => KlineInterval.SixHour,
            "8h" => KlineInterval.EightHour,
            "12h" => KlineInterval.TwelveHour,
            "1d" => KlineInterval.OneDay,
            "3d" => KlineInterval.ThreeDay,
            "1w" => KlineInterval.OneWeek,
            "1M" => KlineInterval.OneMonth,
            _ => throw new ArgumentException($"Unknown interval: {interval}")
        };
    }

    /// <summary>
    /// 리소스 정리
    /// </summary>
    public void Dispose()
    {
        _restClient?.Dispose();
        _socketClient?.Dispose();
    }
}
