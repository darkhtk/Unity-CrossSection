# 📐 Unity-CrossSection — BIM 횡단면 자동 생성 알고리즘

> 도로 선형(Alignment) 기준 3D 메쉬를 평면으로 절단하여 2D 횡단면을 자동 생성하는 Unity 모듈

## 📋 프로젝트 개요

고속도로 BIM 3D 시각화 플랫폼의 핵심 모듈. 도로 선형의 StationPoint 기준으로 3D 메쉬를 평면으로 절단하고, 정점 분석으로 교차 라인을 추출하여 2D 횡단면을 자동 렌더링합니다. AI 없이 직접 설계·구현한 도메인 알고리즘.

## 🔧 기술 스택

`Unity C#` `Job System` `Burst Compile` `Partial Class` `Coroutine`

## ✨ 핵심 기능

### 횡단면 자동 생성
- **자유 모드** — 마우스 클릭 지점에서 횡단면 즉시 생성
- **측점 모드** — 도로 선형(Alignment) 기준 시작~끝 측점 범위 지정, 간격별 자동 생성
- **다중 도로** — 복수 도로의 교차 지점 자동 감지

### 2D 횡단면 뷰어
- 2D 뷰 (줌/팬/스크롤)
- **모델 타입별 레이어 분리** 및 On/Off
- 모델 타입별 색상 구분
- 그리드 자동 생성

### 경사도 자동 계산
- **절토(Cut Slope)** — `1:N` 표기
- **성토(Fill Slope)** — `1:N` 표기
- **노면(Road Surface)** — `N%` 표기
- 모델 타입별 epsilon 자동 적용

### 치수선(Dimension) 시스템
- 스냅핑 (Snap to nearest point)
- 멀티 치수 (다중 치수선 동시 표시)
- 모델 정보 표시 (마우스 오버 시)

### 성능
- **Coroutine 기반 비동기 처리** — 진행률 표시, 취소 지원
- **Clipping 연동** — 횡단면 기준으로 3D 모델 자동 클리핑

## 📁 코드 구조

```
CrossSectionManager.cs          — 메인 컨트롤러 (Partial Class)
CrossSectionManager.Classes.cs  — 내부 클래스 정의
  ├─ SlopeLabel        — 경사도 라벨 (3D 텍스트)
  ├─ MeshLine2D        — 메쉬 교차선 2D 데이터
  ├─ CrossSection2D    — 2D 뷰어 렌더링·상호작용
  ├─ Entity            — 모델 타입별 GameObject
  └─ AlignmentInfoIntersection — 도로 교차 정보
```

## 🔗 관련 링크

| 구분 | 링크 |
|---|---|
| BIM 전체 프로젝트 | [darkhtk/WatchBIM](https://github.com/darkhtk/WatchBIM) |
| BIM 시각화 시연 | [YouTube](https://youtu.be/ajJKPro7008) |

## 📝 비고

이 모듈은 **AI를 사용하지 않고 직접 설계·구현**한 도메인 알고리즘입니다.
BIM 실무 도메인 지식(도로 선형, 측점, 절토/성토, 경사도 표기 규격)을 기반으로 개발.
