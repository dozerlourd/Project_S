# 작업 일시정지 기록

## 대상 작업

- 우선순위 3: 건물별 생산 역할 분리
- 범위: 생산 정의의 허용 생산 건물 검증, 생산 큐와 HUD 노출 제한, 초기 맵 및 에디터 셋업 역할 설정

## 현재 완료된 구현

- `UnitProductionDefinition`에 허용 생산 건물 목록을 추가했다. 목록이 비어 있는 기존 정의는 호환성을 위해 제한 없이 생산할 수 있다.
- `UnitProductionQueue`의 Spliter 전용 하드코딩을 일반화해, 모든 생산 정의가 허용 건물 목록을 기준으로 큐 등록을 검증하도록 변경했다.
- 역할이 맞지 않는 정의는 생산 목록에서 제거되어 HUD 버튼과 단축키 슬롯에 나타나지 않으며, 정의를 직접 큐에 넣으려는 요청도 비용 또는 보급 예약 전에 거부된다.
- 초기 맵 부트스트랩과 맵 에디터 셋업 빌더에 다음 역할을 설정했다.
  - `MainBase`: `Worker`
  - `Production`: `Soldier`, `Ranger`, `Tank`, `Striker`, `Swarm`
  - `SpliterProduction`: `Spliter`
- 역할 필터와 직접 큐 요청 거부를 다루는 `UnitProductionQueue_FiltersDefinitionsByConfiguredBuildingRole` PlayMode 테스트를 추가했다.

## 변경 파일

- `Assets/05.Scripts/Buildings/UnitProductionDefinition.cs`
- `Assets/05.Scripts/Buildings/UnitProductionQueue.cs`
- `Assets/05.Scripts/MapCreateSceneAutoBootstrap.cs`
- `Assets/05.Scripts/Editor/MapCreateSceneSetupBuilder.cs`
- `Assets/05.Scripts/Tests/PlayMode/UnitPathAgentMovementTests.cs`
- `Docs/Project_S_Verification_Backlog.md`

## 검증 상태

- `dotnet build Project_S.sln --no-restore -m:1` 통과
- 빌드 결과: 경고 0개, 오류 0개
- Unity Test Runner에서 PlayMode 테스트를 실제 실행하는 확인은 아직 남아 있으며, `Docs/Project_S_Verification_Backlog.md`에 기록했다.

## 작업 범위 외 항목

- 우선순위 4, 6, 7은 구현하지 않았다.
- 기존 생산 비용, 보급, 랠리 흐름은 변경하지 않았다.
- 커밋 및 푸시는 수행하지 않았다.
