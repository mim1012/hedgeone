namespace Hedgeone.Core;

/// <summary>
/// 트레이딩 상태 영속화 인터페이스
/// </summary>
public interface IStateRepository
{
    /// <summary>
    /// 모든 심볼의 상태 로드
    /// </summary>
    /// <returns>심볼별 트레이딩 상태 딕셔너리</returns>
    Task<Dictionary<string, TradingState>> LoadAllAsync();

    /// <summary>
    /// 특정 심볼의 상태 저장
    /// </summary>
    /// <param name="symbol">심볼 이름</param>
    /// <param name="state">저장할 상태</param>
    Task SaveAsync(string symbol, TradingState state);

    /// <summary>
    /// 모든 상태 일괄 저장
    /// </summary>
    /// <param name="states">저장할 상태 딕셔너리</param>
    Task SaveAllAsync(Dictionary<string, TradingState> states);
}
