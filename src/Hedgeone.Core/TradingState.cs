namespace Hedgeone.Core;

/// <summary>
/// 트레이딩 상태를 저장하는 모델
/// </summary>
public class TradingState
{
    /// <summary>
    /// 거래 심볼 (예: DOGEUSDT)
    /// </summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>
    /// 현재 대세 방향 ("UP" or "DOWN")
    /// </summary>
    public string Regime { get; set; } = "UP";

    // ===== Long (Call) 포지션 =====

    /// <summary>
    /// Long 포지션 수량
    /// </summary>
    public decimal PosCall { get; set; } = 0m;

    /// <summary>
    /// Long 포지션 진입 가격
    /// </summary>
    public decimal? EntryPriceCall { get; set; }

    /// <summary>
    /// Long 포지션 진입 시간
    /// </summary>
    public DateTime? EntryTimeCall { get; set; }

    /// <summary>
    /// Long 포지션 최대 유리 가격 (트레일링 스탑용)
    /// </summary>
    public decimal? MaxFavorablePriceCall { get; set; }

    // ===== Short (Put) 포지션 =====

    /// <summary>
    /// Short 포지션 수량
    /// </summary>
    public decimal PosPut { get; set; } = 0m;

    /// <summary>
    /// Short 포지션 진입 가격
    /// </summary>
    public decimal? EntryPricePut { get; set; }

    /// <summary>
    /// Short 포지션 진입 시간
    /// </summary>
    public DateTime? EntryTimePut { get; set; }

    /// <summary>
    /// Short 포지션 최대 유리 가격 (트레일링 스탑용)
    /// </summary>
    public decimal? MaxFavorablePricePut { get; set; }

    /// <summary>
    /// Long 포지션 손익 계산
    /// </summary>
    /// <param name="currentPrice">현재 가격</param>
    /// <returns>손익 금액 (양수=이익, 음수=손실)</returns>
    public decimal PnlCall(decimal currentPrice)
    {
        if (PosCall == 0 || !EntryPriceCall.HasValue)
            return 0m;

        return (currentPrice - EntryPriceCall.Value) * PosCall;
    }

    /// <summary>
    /// Short 포지션 손익 계산
    /// </summary>
    /// <param name="currentPrice">현재 가격</param>
    /// <returns>손익 금액 (양수=이익, 음수=손실)</returns>
    public decimal PnlPut(decimal currentPrice)
    {
        if (PosPut == 0 || !EntryPricePut.HasValue)
            return 0m;

        return (EntryPricePut.Value - currentPrice) * PosPut;
    }

    /// <summary>
    /// 총 손익 계산
    /// </summary>
    /// <param name="currentPrice">현재 가격</param>
    /// <returns>Long + Short 합산 손익</returns>
    public decimal TotalPnl(decimal currentPrice)
    {
        return PnlCall(currentPrice) + PnlPut(currentPrice);
    }

    /// <summary>
    /// Long 포지션 손익률 계산
    /// </summary>
    /// <param name="currentPrice">현재 가격</param>
    /// <returns>손익률 (0.01 = 1%)</returns>
    public decimal PnlPctCall(decimal currentPrice)
    {
        if (PosCall == 0 || !EntryPriceCall.HasValue || EntryPriceCall.Value == 0)
            return 0m;

        return (currentPrice - EntryPriceCall.Value) / EntryPriceCall.Value;
    }

    /// <summary>
    /// Short 포지션 손익률 계산
    /// </summary>
    /// <param name="currentPrice">현재 가격</param>
    /// <returns>손익률 (0.01 = 1%)</returns>
    public decimal PnlPctPut(decimal currentPrice)
    {
        if (PosPut == 0 || !EntryPricePut.HasValue || EntryPricePut.Value == 0)
            return 0m;

        return (EntryPricePut.Value - currentPrice) / EntryPricePut.Value;
    }
}
