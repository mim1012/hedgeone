using Hedgeone.Indicators;

namespace Hedgeone.Exchange;

/// <summary>
/// 거래소 API 어댑터 인터페이스
/// </summary>
public interface IExchangeAdapter
{
    // ===== 시장 데이터 =====

    /// <summary>
    /// 최신 가격 조회
    /// </summary>
    Task<decimal> GetLastPriceAsync(string symbol);

    /// <summary>
    /// 캔들 데이터 조회
    /// </summary>
    /// <param name="symbol">심볼 (예: DOGEUSDT)</param>
    /// <param name="interval">캔들 간격 (1m, 5m, 1d 등)</param>
    /// <param name="limit">조회 개수</param>
    Task<List<Candle>> GetCandlesAsync(string symbol, string interval, int limit);

    // ===== 주문 (Hedge Mode) =====

    /// <summary>
    /// Long 포지션 진입 (Buy)
    /// </summary>
    Task<OrderResult> BuyLongAsync(string symbol, decimal quantity);

    /// <summary>
    /// Long 포지션 청산 (Sell)
    /// </summary>
    Task<OrderResult> SellLongAsync(string symbol, decimal quantity);

    /// <summary>
    /// Short 포지션 진입 (Sell)
    /// </summary>
    Task<OrderResult> BuyShortAsync(string symbol, decimal quantity);

    /// <summary>
    /// Short 포지션 청산 (Buy)
    /// </summary>
    Task<OrderResult> SellShortAsync(string symbol, decimal quantity);

    // ===== 포지션 조회 =====

    /// <summary>
    /// 포지션 정보 조회
    /// </summary>
    /// <param name="symbol">심볼</param>
    /// <param name="positionSide">"LONG" or "SHORT"</param>
    Task<PositionInfo> GetPositionAsync(string symbol, string positionSide);

    // ===== WebSocket =====

    /// <summary>
    /// 캔들 업데이트 구독
    /// </summary>
    Task SubscribeCandleUpdatesAsync(string symbol, string interval, Action<Candle> onCandle);
}

/// <summary>
/// 주문 결과
/// </summary>
public class OrderResult
{
    public bool Success { get; set; }
    public string? OrderId { get; set; }
    public decimal FilledQuantity { get; set; }
    public decimal AvgPrice { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// 포지션 정보
/// </summary>
public class PositionInfo
{
    public string Symbol { get; set; } = string.Empty;
    public string PositionSide { get; set; } = string.Empty;  // "LONG" or "SHORT"
    public decimal Quantity { get; set; }
    public decimal EntryPrice { get; set; }
    public decimal UnrealizedPnl { get; set; }
}
