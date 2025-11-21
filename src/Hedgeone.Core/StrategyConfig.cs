namespace Hedgeone.Core;

/// <summary>
/// 전략 설정 파라미터
/// </summary>
public class StrategyConfig
{
    /// <summary>
    /// 1회 포지션 크기 (USDT)
    /// </summary>
    public decimal CallSize { get; set; } = 10m;

    /// <summary>
    /// RSI 계산 기간
    /// </summary>
    public int RsiLength { get; set; } = 2;

    /// <summary>
    /// 고정 익절 수익률 (기본 1%)
    /// </summary>
    public decimal TakeProfitPct { get; set; } = 0.01m;

    /// <summary>
    /// 트레일링 스탑 기준 (기본 0.5%)
    /// </summary>
    public decimal TrailingPct { get; set; } = 0.005m;

    /// <summary>
    /// 최대 보유 시간 (5분봉 개수, 24 = 2시간)
    /// </summary>
    public int MaxHoldBars { get; set; } = 24;

    /// <summary>
    /// 헷지 진입 손실 기준 (기본 -1%)
    /// </summary>
    public decimal HedgeLossPct { get; set; } = -0.01m;

    /// <summary>
    /// 모니터링할 심볼 리스트
    /// </summary>
    public List<string> Symbols { get; set; } = new() { "DOGEUSDT", "ALGOUSDT" };

    /// <summary>
    /// Testnet 사용 여부
    /// </summary>
    public bool UseTestnet { get; set; } = true;

    /// <summary>
    /// Binance API Key
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Binance API Secret
    /// </summary>
    public string ApiSecret { get; set; } = string.Empty;

    /// <summary>
    /// 상태 파일 저장 경로
    /// </summary>
    public string StateFilePath { get; set; } = "state.json";

    /// <summary>
    /// 설정 검증
    /// </summary>
    /// <exception cref="ArgumentException">잘못된 설정 값이 있을 때</exception>
    public void Validate()
    {
        if (CallSize <= 0)
            throw new ArgumentException("CallSize must be greater than 0");

        if (RsiLength <= 0)
            throw new ArgumentException("RsiLength must be greater than 0");

        if (TakeProfitPct <= 0)
            throw new ArgumentException("TakeProfitPct must be greater than 0");

        if (TrailingPct <= 0)
            throw new ArgumentException("TrailingPct must be greater than 0");

        if (MaxHoldBars <= 0)
            throw new ArgumentException("MaxHoldBars must be greater than 0");

        if (HedgeLossPct >= 0)
            throw new ArgumentException("HedgeLossPct must be negative");

        if (Symbols == null || Symbols.Count == 0)
            throw new ArgumentException("Symbols list cannot be empty");

        if (string.IsNullOrWhiteSpace(ApiKey))
            throw new ArgumentException("ApiKey cannot be empty");

        if (string.IsNullOrWhiteSpace(ApiSecret))
            throw new ArgumentException("ApiSecret cannot be empty");
    }
}
