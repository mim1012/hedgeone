namespace Hedgeone.UI.ViewModels;

/// <summary>
/// 실시간 포지션 상태를 표시하는 ViewModel
/// </summary>
public class PositionViewModel : ViewModelBase
{
    private string _symbol = "";
    private string _regime = "";
    private string _position = "";
    private decimal _quantity;
    private decimal _entryPrice;
    private decimal _currentPrice;
    private string _pnl = "";
    private string _hedge = "";

    public string Symbol
    {
        get => _symbol;
        set => SetProperty(ref _symbol, value);
    }

    public string Regime
    {
        get => _regime;
        set => SetProperty(ref _regime, value);
    }

    public string Position
    {
        get => _position;
        set => SetProperty(ref _position, value);
    }

    public decimal Quantity
    {
        get => _quantity;
        set => SetProperty(ref _quantity, value);
    }

    public decimal EntryPrice
    {
        get => _entryPrice;
        set => SetProperty(ref _entryPrice, value);
    }

    public decimal CurrentPrice
    {
        get => _currentPrice;
        set => SetProperty(ref _currentPrice, value);
    }

    public string Pnl
    {
        get => _pnl;
        set => SetProperty(ref _pnl, value);
    }

    public string Hedge
    {
        get => _hedge;
        set => SetProperty(ref _hedge, value);
    }
}
