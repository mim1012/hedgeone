using Newtonsoft.Json;

namespace Hedgeone.Core;

/// <summary>
/// JSON 파일 기반 상태 영속화 구현
/// </summary>
public class JsonStateRepository : IStateRepository
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);

    /// <summary>
    /// JsonStateRepository 생성자
    /// </summary>
    /// <param name="filePath">상태 파일 경로 (기본: state.json)</param>
    public JsonStateRepository(string filePath = "state.json")
    {
        _filePath = filePath;
    }

    /// <summary>
    /// 모든 심볼의 상태 로드
    /// </summary>
    public async Task<Dictionary<string, TradingState>> LoadAllAsync()
    {
        await _lock.WaitAsync();
        try
        {
            if (!File.Exists(_filePath))
                return new Dictionary<string, TradingState>();

            var json = await File.ReadAllTextAsync(_filePath);

            if (string.IsNullOrWhiteSpace(json))
                return new Dictionary<string, TradingState>();

            var states = JsonConvert.DeserializeObject<Dictionary<string, TradingState>>(json);
            return states ?? new Dictionary<string, TradingState>();
        }
        catch (Exception ex)
        {
            // 로깅 (나중에 추가)
            Console.WriteLine($"Error loading state from {_filePath}: {ex.Message}");
            return new Dictionary<string, TradingState>();
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// 특정 심볼의 상태 저장
    /// </summary>
    public async Task SaveAsync(string symbol, TradingState state)
    {
        await _lock.WaitAsync();
        try
        {
            // 전체 상태 로드
            var allStates = await LoadAllInternalAsync();

            // 해당 심볼 상태 업데이트
            allStates[symbol] = state;

            // 저장
            await SaveAllInternalAsync(allStates);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// 모든 상태 일괄 저장
    /// </summary>
    public async Task SaveAllAsync(Dictionary<string, TradingState> states)
    {
        await _lock.WaitAsync();
        try
        {
            await SaveAllInternalAsync(states);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// 내부용 로드 (lock 없음)
    /// </summary>
    private async Task<Dictionary<string, TradingState>> LoadAllInternalAsync()
    {
        if (!File.Exists(_filePath))
            return new Dictionary<string, TradingState>();

        var json = await File.ReadAllTextAsync(_filePath);

        if (string.IsNullOrWhiteSpace(json))
            return new Dictionary<string, TradingState>();

        var states = JsonConvert.DeserializeObject<Dictionary<string, TradingState>>(json);
        return states ?? new Dictionary<string, TradingState>();
    }

    /// <summary>
    /// 내부용 저장 (lock 없음)
    /// </summary>
    private async Task SaveAllInternalAsync(Dictionary<string, TradingState> states)
    {
        var json = JsonConvert.SerializeObject(states, Formatting.Indented);

        // 디렉토리 생성
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(_filePath, json);
    }
}
