using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Hedgeone.UI.ViewModels;

/// <summary>
/// 메인 윈도우 ViewModel
/// </summary>
public class MainViewModel : ViewModelBase
{
    private string _logText = "";
    private bool _isRunning;

    public MainViewModel()
    {
        Config = new StrategyConfigViewModel();
        Positions = new ObservableCollection<PositionViewModel>();

        // 명령 초기화
        TestConnectionCommand = new RelayCommand(TestConnection);
        StartStrategyCommand = new RelayCommand(StartStrategy, CanStartStrategy);
        PauseStrategyCommand = new RelayCommand(PauseStrategy, CanPauseStrategy);
        StopStrategyCommand = new RelayCommand(StopStrategy, CanStopStrategy);
    }

    public StrategyConfigViewModel Config { get; }
    public ObservableCollection<PositionViewModel> Positions { get; }

    public string LogText
    {
        get => _logText;
        set => SetProperty(ref _logText, value);
    }

    public bool IsRunning
    {
        get => _isRunning;
        set
        {
            if (SetProperty(ref _isRunning, value))
            {
                ((RelayCommand)StartStrategyCommand).RaiseCanExecuteChanged();
                ((RelayCommand)PauseStrategyCommand).RaiseCanExecuteChanged();
                ((RelayCommand)StopStrategyCommand).RaiseCanExecuteChanged();
            }
        }
    }

    // Commands
    public ICommand TestConnectionCommand { get; }
    public ICommand StartStrategyCommand { get; }
    public ICommand PauseStrategyCommand { get; }
    public ICommand StopStrategyCommand { get; }

    private void TestConnection()
    {
        AddLog("API 연결 테스트 중...");
        // TODO: Binance API 연결 테스트 구현
        AddLog("API 연결 성공!");
    }

    private bool CanStartStrategy()
    {
        return !IsRunning;
    }

    private void StartStrategy()
    {
        IsRunning = true;
        AddLog($"전략 시작 - {Config.SelectedSymbol}");
        // TODO: HedgeStrategy 실행
    }

    private bool CanPauseStrategy()
    {
        return IsRunning;
    }

    private void PauseStrategy()
    {
        IsRunning = false;
        AddLog("전략 일시정지");
        // TODO: 전략 일시정지 구현
    }

    private bool CanStopStrategy()
    {
        return IsRunning;
    }

    private void StopStrategy()
    {
        IsRunning = false;
        AddLog("전략 즉시 종료");
        // TODO: 모든 포지션 청산 및 종료
    }

    private void AddLog(string message)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        LogText += $"[{timestamp}] {message}\n";
    }
}
