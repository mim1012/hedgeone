# Hedgeone - Binance Futures Auto Hedge Trading System

## 개요

**Hedgeone**은 Binance Futures (USDT-M) 시장에서 동작하는 자동 헷지 트레이딩 시스템입니다.

### 주요 특징

- **단방향 대세 추종**: 일봉 기준 RSI2 vs MACD(1,1,1)로 대세 판단
- **손익 기반 헷지**: 손실 발생 시 동일 수량 반대 포지션 자동 진입
- **5분봉 단기 운용**: 빠른 신호 포착 및 실행
- **멀티 심볼 지원**: DOGEUSDT, ALGOUSDT (확장 가능)

### 기술 스택

- **언어**: C# (.NET 6/7)
- **UI**: WPF (MVVM 패턴)
- **거래소**: Binance Futures API
- **지표**: RSI(2), MACD(1,1,1)

## 프로젝트 구조

```
Hedgeone/
├── docs/               # 문서
│   ├── dashboard.md    # 실시간 진행 상황
│   ├── prd/           # 요구사항 문서
│   ├── architecture/  # 설계 문서
│   ├── api_design/    # API 설계
│   └── tests/         # 테스트 계획
├── src/               # 소스 코드
│   ├── Hedgeone.Core/        # 전략 엔진
│   ├── Hedgeone.Exchange/    # Binance API 어댑터
│   ├── Hedgeone.Indicators/  # 기술 지표
│   ├── Hedgeone.UI/          # WPF UI
│   └── Hedgeone.Tests/       # 테스트
└── agents/            # 서브에이전트 작업 로그
```

## 개발 상태

현재 개발 진행 상황은 [docs/dashboard.md](docs/dashboard.md)에서 실시간으로 확인할 수 있습니다.

## 라이선스

Private Project - All Rights Reserved

## 작성자

Generated with Claude Code v1.0
