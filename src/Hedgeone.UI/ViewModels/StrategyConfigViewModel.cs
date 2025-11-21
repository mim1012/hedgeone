namespace Hedgeone.UI.ViewModels;

/// <summary>
/// 전략 설정 ViewModel
/// </summary>
public class StrategyConfigViewModel : ViewModelBase
{
    private string _apiKey = "";
    private string _apiSecret = "";
    private string _selectedSymbol = "DOGEUSDT";
    private decimal _callSize = 10m;
    private decimal _takeProfitPct = 1.0m;
    private decimal _trailingPct = 0.5m;
    private int _maxHoldBars = 24;

    public string ApiKey
    {
        get => _apiKey;
        set => SetProperty(ref _apiKey, value);
    }

    public string ApiSecret
    {
        get => _apiSecret;
        set => SetProperty(ref _apiSecret, value);
    }

    public string SelectedSymbol
    {
        get => _selectedSymbol;
        set => SetProperty(ref _selectedSymbol, value);
    }

    public decimal CallSize
    {
        get => _callSize;
        set => SetProperty(ref _callSize, value);
    }

    public decimal TakeProfitPct
    {
        get => _takeProfitPct;
        set => SetProperty(ref _takeProfitPct, value);
    }

    public decimal TrailingPct
    {
        get => _trailingPct;
        set => SetProperty(ref _trailingPct, value);
    }

    public int MaxHoldBars
    {
        get => _maxHoldBars;
        set => SetProperty(ref _maxHoldBars, value);
    }
}
