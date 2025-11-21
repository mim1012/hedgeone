# Hedgeone PRD (Product Requirements Document)

**버전**: 1.0
**작성일**: 2025-11-21
**작성자**: Claude Code Orchestrator
**적용 대상 거래소**: Binance Futures (USDT-M)
**대상 자산**: DOGEUSDT, ALGOUSDT (확장 가능)
**전략 유형**: 단방향 대세 + 손익 기반 헷지 + RSI2/MACD 중심선 신호 기반 단기 운용

---

## 1. 목적 (Purpose)

본 시스템의 목적은 다음 세 가지 원칙에 따라 대세 한 방향 운용을 수행하면서, 손실을 최소화하기 위한 동일 수량 반대 포지션 헷지를 자동으로 처리하는 것입니다:

1. **일봉 대세 방향으로만 신규 포지션을 잡는다**
2. **손실이 발생하면 동일 수량으로 반대 포지션을 헷지한다**
3. **대세와 같은 신호가 재발하면 반대 포지션(헷지)은 손익 불문 즉시 제거한다**

이를 통해 **단방향 추세 수익 극대화 + 손실 구간 방어**를 결합한 구조를 구현합니다.

---

## 2. 전략 개요 (Strategy Concept)

### 2.1 대세 (Regime)

- **상승 대세 (UP, 콜장)**: 일봉 기준 `RSI(2) > MACD(1,1,1)`
- **하락 대세 (DOWN, 풋장)**: 일봉 기준 `RSI(2) < MACD(1,1,1)`

**특징**:
- 대세는 **하루에 한 번만 갱신**
- 대세가 바뀌면 **반대 포지션은 즉시 전부 청산**하여 단방향 프레임 유지

### 2.2 5분봉 단기 신호 (Small Trend)

- **상승 신호 (소세 상승)**: `RSI(2) > MACD(1,1,1)`
- **하락 신호 (소세 하락)**: `RSI(2) < MACD(1,1,1)`

**활용**:
- 대세 방향에 맞는 **신규 진입**에만 활용
- **포지션 관리** 및 **헤지 해제**에도 사용

---

## 3. 주요 거래 규칙 (Trading Rules)

### 3.1 상승 대세 (UP: 콜장)

#### 신규 콜 진입 조건
- 5분봉 상승 신호 발생
- `pos_call == 0` (콜 포지션 없음)

#### 콜 보유 관리
- **손익 ≥ 0**: `exit_rule_hit()` 충족 시 콜 청산
- **손익 < 0**: 풋 헤지 `CALL_SIZE` 추가 진입 (`pos_put == 0`일 때)

#### 새 상승 신호 발생 시
- `pos_put > 0` → **모든 풋 헤지 즉시 청산** (손익 무관)

#### 하락 신호 발생 시 (풋 신호)
- **콜 손익 ≥ 0** → 즉시 콜 청산 (이익 잠금)
- **콜 손익 < 0** → 풋 헤지 `CALL_SIZE` 진입

---

### 3.2 하락 대세 (DOWN: 풋장)

#### 신규 풋 진입 조건
- 5분봉 하락 신호 발생
- `pos_put == 0` (풋 포지션 없음)

#### 풋 보유 관리
- **손익 ≥ 0**: `exit_rule_hit()` 충족 시 풋 청산
- **손익 < 0**: 콜 헤지 `CALL_SIZE` 추가 진입 (`pos_call == 0`일 때)

#### 새 하락 신호 발생 시
- `pos_call > 0` → **콜 헤지 즉시 청산** (손익 불문)

#### 상승 신호 발생 시 (콜 신호)
- **풋 손익 ≥ 0** → 풋 청산 (이익 잠금)
- **풋 손익 < 0** → 콜 헤지 `CALL_SIZE` 진입

---

## 4. 필수 기능 요구사항 (Requirements)

### 4.1 가격 및 지표

| 함수 | 설명 |
|------|------|
| `GetLastPrice(symbol)` | 현재가 조회 |
| `GetIndicator("RSI", symbol, tf, length=2)` | RSI(2) 계산 |
| `GetIndicator("MACD_line", symbol, tf, fast=1, slow=1, signal=1)` | MACD(1,1,1) 계산 |

### 4.2 주문

| 함수 | 설명 | Binance 매핑 |
|------|------|--------------|
| `BuyCall(size)` | 콜 매수 | Long Position (BUY) |
| `SellCall(size)` | 콜 청산 | Long Close (SELL) |
| `BuyPut(size)` | 풋 매수 | Short Position (SELL) |
| `SellPut(size)` | 풋 청산 | Short Close (BUY) |

**참고**: Binance Futures에서는 "콜/풋" 개념이 없으므로:
- **콜** = Long Position (`positionSide="LONG"`)
- **풋** = Short Position (`positionSide="SHORT"`)

### 4.3 기타

- `ExitRuleHit()`: 목표가/트레일링/RSI 롤오버 등 청산 조건
- **State 영속화**: JSON/DB 파일로 상태 저장
- **오류 처리**: 부분 체결, 주문 실패 재시도
- **재시작 복구**: 프로그램 재시작 시 마지막 포지션 상태 복원

---

## 5. 변수 설명 및 구조

### Parameters

| 변수 | 설명 |
|------|------|
| `CALL_SIZE` | 기본 진입 및 헤지 수량 |
| `RSI_LEN` | RSI 기간 (기본=2) |
| `SYMBOL` | 거래 심볼 (예: DOGEUSDT) |

### State

| 변수 | 설명 |
|------|------|
| `regime` | "UP" 또는 "DOWN" |
| `pos_call` | 콜 보유 수량 |
| `pos_put` | 풋 보유 수량 |
| `entry_price_call` | 콜 진입가 |
| `entry_price_put` | 풋 진입가 |
| `entry_time_call` | 콜 진입 시각 |
| `entry_time_put` | 풋 진입 시각 |
| `max_favorable_price_call` | 콜 진입 이후 최고가 |
| `max_favorable_price_put` | 풋 진입 이후 최저가 |

---

## 6. 예외 처리 및 시스템 요구사항

### 6.1 재시작/프로그램 종료 대비
- State를 **JSON 파일** 또는 **DB**에 자동 저장
- 재부팅 후 마지막 포지션 상태를 정확히 복구

### 6.2 비정상 주문 실패
- 주문 실패 시 **재시도 3회**
- **오더 체결 확인** 후 상태 업데이트

### 6.3 심볼 확장성
- DOGEUSDT, ALGOUSDT → **배열 구조**로 확장 가능
- 각 심볼별 독립적인 State 관리

---

## 7. exit_rule_hit() 상세 설계

### 7.1 설계 목표
1. **이익이 어느 정도 나면 욕심부리지 말고 정리**
2. **너무 오래 들고 있지 않기**
3. **단기 모멘텀이 꺾인 느낌 (RSI2 롤오버) 때 정리**

### 7.2 파라미터

| 변수 | 설명 |
|------|------|
| `TAKE_PROFIT_PCT` | +1% 이상 수익 시 청산 후보 |
| `MAX_HOLD_BARS` | 5분봉 24개 ≒ 2시간 이상 보유 시 정리 |
| `TRAILING_PCT` | +0.5% 이상 이익 구간에서 트레일링 스탑 |

### 7.3 청산 로직

#### 콜 보유 시
1. **고정 TP**: 수익률 ≥ `TAKE_PROFIT_PCT` → 청산
2. **시간 초과**: 보유 시간 ≥ `MAX_HOLD_BARS * 5분` → 청산
3. **트레일링 스탑**: 수익률 > `TRAILING_PCT`이고, 현재가가 최고가 대비 `TRAILING_PCT` 하락 → 청산
4. **RSI2 롤오버**: 상승장에서 `RSI2 < MACD` (하락 신호) → 청산

#### 풋 보유 시
- 콜과 반대 방향으로 동일 로직 적용

---

## 8. 개발자가 구현해야 하는 부분

1. ✅ **Binance 주문 API 연결** (C# Binance.Net)
2. ✅ **지표 계산** (RSI, MACD) - 자체 구현
3. ✅ **exit_rule_hit() 구체화**
4. ✅ **포지션 상태 저장/복구** (JSON)
5. ✅ **UI** (WPF - 수량 변경, 시작/종료 버튼)
6. ✅ **오류 처리** + Partial fill 처리

---

## 9. 시스템 아키텍처 (C# 기준)

### 9.1 모듈 구조

```
Hedgeone/
├── Hedgeone.Core/         # 전략 엔진
│   ├── IHedgeStrategy.cs
│   ├── HedgeStrategy.cs
│   ├── TradingState.cs
│   └── ExitRules.cs
├── Hedgeone.Indicators/   # 기술 지표
│   ├── IIndicatorService.cs
│   └── TechnicalIndicators.cs
├── Hedgeone.Exchange/     # Binance API
│   ├── IExchangeAdapter.cs
│   └── BinanceFuturesAdapter.cs
├── Hedgeone.UI/           # WPF UI
│   ├── MainWindow.xaml
│   ├── ViewModels/
│   └── Views/
└── Hedgeone.Tests/        # 테스트
```

### 9.2 실행 흐름

1. `main.exe` 실행
2. `config/settings.json` 로드
3. UI 모드 실행 → API 키/심볼/전략 파라미터 입력
4. "시작" 버튼 클릭 → `ServiceRunner` 시작
5. `ServiceRunner`:
   - Binance 시간 동기화
   - 1D 캔들 클로즈 감지 → `OnNewDaily()`
   - 5m 캔들 클로즈 감지 → `OnNew5m()`
6. 로그 및 UI 상태 패널 업데이트

---

## 10. UI 요구사항

### 10.1 화면 구성

```
+--------------------------------------------------------------+
| [ Binance Auto Hedge Trader ]                                |
+--------------------------------------------------------------+
| API 설정                                                      |
| API Key [_____________________________] [연결 테스트]        |
+--------------------------------------------------------------+
| 전략 설정                                                     |
| 심볼 선택 [DOGEUSDT] [ALGOUSDT]                              |
| 기본 수량 CALL_SIZE: [ 10 ]                                   |
| TP% [ 1.0 ] 트레일링% [ 0.5 ] 최대보유(5m) [ 24 ]            |
+--------------------------------------------------------------+
| 실시간 상태                                                   |
| 심볼      대세  포지션  수량  진입가  현재가  PnL    헷지    |
| DOGEUSDT  UP    LONG    10    0.123  0.125  +1.3%  SHORT    |
+--------------------------------------------------------------+
| 로그                                                          |
| [2025-11-21 10:05] DOGEUSDT: 콜 10계약 진입                  |
+--------------------------------------------------------------+
| [전략 시작] [일시정지] [즉시 종료]                            |
+--------------------------------------------------------------+
```

---

## 11. 배포 요구사항

### 11.1 EXE 빌드
- **.NET 6/7** Self-contained publish
- **단일 EXE** 생성 (PublishSingleFile)
- Windows x64 타겟

### 11.2 설정 파일
- `appsettings.json` - 기본 설정
- API 키는 **UI 입력** 또는 **환경변수** 사용 (하드코딩 금지)

### 11.3 배포 패키지
- `Hedgeone.exe`
- `README.md` (사용 방법)
- `config/` (샘플 설정)

---

## 12. 성공 기준

1. ✅ 일봉 대세 판단이 정확히 작동
2. ✅ 5분봉 신호 발생 시 즉시 진입
3. ✅ 손실 발생 시 헷지 자동 진입
4. ✅ 대세 방향 신호 재발 시 헷지 즉시 제거
5. ✅ exit_rule_hit() 조건 충족 시 청산
6. ✅ 프로그램 재시작 후 상태 복구
7. ✅ UI에서 실시간 포지션 모니터링 가능
8. ✅ 백테스트로 전략 수익성 검증

---

## 13. 참고 자료

- **원본 PRD**: `hedgeone PRD.pdf`
- **Binance API 문서**: https://binance-docs.github.io/apidocs/futures/en/
- **Binance.Net 라이브러리**: https://github.com/JKorf/Binance.Net

---

**문서 버전**: 1.0
**최종 수정일**: 2025-11-21
**작성자**: Claude Code - Orchestrator Agent
