# 작업 일시정지 기록

## 2026-09-16: 건물 배치 중심 보정 및 선택 범위 UI 검증 중단

### 대상 작업

- 홀수/짝수 footprint 건설 부지와 완성 건물이 점유 셀의 기하학적 중심에 배치되는지 확인한다.
- 중심 보정 뒤에도 건설 부지, 완성 건물, 인접 배치의 점유 및 겹침 판정이 유지되는지 확인한다.
- 자동 포탑 공격 범위, 이동 속도 오라 범위, 신호 중계소 시야 범위가 해당 건물을 선택한 동안에만 표시되는지 확인한다.
- 문제가 발견되면 원인을 수정하고 컴파일 및 가능한 Unity PlayMode 검증까지 수행한다.

### 현재 워크트리에 반영된 변경

- `ConstructionSite.GetPlacementCenterWorld`가 footprint의 최소/최대 셀 중심 평균으로 실제 배치 중심을 계산한다.
- 건설 부지 생성 시 계산한 중심을 사용하고, 완성 건물은 건설 부지의 `transform.position`을 이어받는다.
- 이미 중심에 놓인 건물의 점유 셀 계산에는 짝수 footprint를 고려한 별도 중심 기준 열거가 사용된다.
- `BuildingRangeIndicator`는 반경, Sprite, 활성 상태뿐 아니라 현재 주 선택 건물인지 확인한 뒤 렌더러를 표시한다.
- 자동 포탑, 이동 속도 오라, 신호 중계소 프리팹의 `RangeIndicator` 렌더러 기본 상태가 비활성으로 변경되어 선택 전에는 표시되지 않는다.

### 검토 및 검증 결과

- 2x2 footprint는 클릭 기준 셀부터 오른쪽/위쪽 2x2를 점유하고 네 셀 중심의 평균 위치에 놓이며, 3x3 footprint는 클릭 기준 셀을 중심으로 대칭 배치되는 계산임을 코드 수준에서 확인했다.
- 선택 상태 확인은 `PlayerUnitCommandController.ActiveInstance.PrimarySelection.SelectionGameObject`와 범위 표시 컴포넌트의 GameObject를 비교한다. 관련 세 프리팹 모두 컴포넌트가 루트에 있어 현재 프리팹 구조와 일치한다.
- 범위 반경이 이전 프레임과 같아도 렌더러 활성 여부는 조기 반환 전에 매 프레임 갱신되므로 선택 전환을 반영하는 흐름임을 확인했다.
- `dotnet build Project_S.sln --no-restore -m:1`을 실행했고 경고 0개, 오류 0개로 통과했다.
- Codex 컴퓨터 제어 환경에서 Unity 네이티브 앱 연결 API를 사용할 수 없어 실제 에디터 화면 조작은 수행하지 못했다.

### 미완료 및 재개 순서

- 홀수/짝수 footprint의 중심 위치와 점유/겹침 판정을 검증하는 PlayMode 테스트는 아직 추가하지 않았다.
- 실제 자동 포탑, 이동 속도 오라, 신호 중계소 프리팹을 사용해 선택 전환 및 선택 해제 시 범위 표시를 검증하는 PlayMode 테스트도 아직 추가하지 않았다.
- 재개 시 기존 PlayMode 테스트의 Tilemap 및 리플렉션 헬퍼를 확인한 뒤 위 두 범주의 집중 테스트를 추가한다.
- 현재 열린 Unity 프로젝트와 충돌하지 않도록 `Assets`, `Packages`, `ProjectSettings`를 임시 프로젝트로 복사하고 Unity 6000.3.9f1 배치 PlayMode 테스트를 실행한다.
- 테스트 결과 XML과 Unity 로그를 함께 확인하고, 실패가 재현될 때만 생산 코드를 수정한다.

### 작업 상태 및 주의 사항

- 사용자 요청에 따라 이 기록 시점에서 모든 구현 및 검증 작업을 일시정지했다.
- 이번 검증 세션에서는 생산 코드, 프리팹, 테스트 파일을 추가로 수정하지 않았다. 본 기록 문서와 검증 백로그만 갱신했다.
- 현재 관련 미커밋 파일은 `ConstructionSite.cs`, `BuildingRangeIndicator.cs`, 자동 포탑/이동 속도 오라/신호 중계소 프리팹 3개다.
- `Assets/_Recovery/0 (3).unity`, `Assets/_Recovery/0 (4).unity` 및 각 `.meta`는 별도 사용자 변경으로 간주하며 수정하거나 삭제하지 않는다.
- 커밋과 푸시는 수행하지 않았다.

---

## 2026-09-15: Medic / Siege / Scout 유닛 확장 착수 전 중단

> 후속 상태: 같은 날 작업을 재개해 행동 기준, 타입과 기본값, 전술 프로필, 에디터 프리팹 생성기, PlayMode 테스트 구현을 완료했다. 실제 프리팹 에셋 생성과 Unity Test Runner 실행은 Unity 에디터에서 확인해야 한다.

### 요청 범위

- `Docs/Unit_Behavior_Guidelines.md`에 기존 상태 시스템을 사용하는 `Medic`, `Siege`, `Scout`의 단일 행동 기준을 먼저 추가한다.
- `Medic`은 치료 시스템을 새로 만들지 않고 이동, 생존, 정찰만 수행하는 비전투 지원 유닛으로 구성한다.
- `Siege`는 기존 공격 시스템만 사용하는 느린 장거리 대건물 포격 유닛으로 구성한다.
- `Scout`은 기존 공격 시스템만 사용하는 빠른 정찰 및 경량 원거리 유닛으로 구성한다.
- `PrototypeUnitType`, `PrototypeUnitStatus` 기본값과 공개 API, 프리팹 생성 파이프라인, Asset 에디터 팩토리를 확장한다.
- `B_Medic`, `B_Siege`, `B_Scout` 프리팹에 팀, 선택, 안개, 이동, 기존 공격 시스템을 역할에 맞게 연결한다.
- PlayMode 테스트와 `Docs/Project_S_Verification_Backlog.md` 검증 항목을 추가한다.
- `MapCreateSceneAutoBootstrap`과 생산 정의는 직접 수정하지 않는다.

### 현재까지 진행된 내용

- 기존 작업에서 유닛 확장 요청으로 전환했다.
- 구현에 앞서 행동 기준 문서, 유닛 타입과 상태 기본값, 프리팹 생성 경로, 에디터 팩토리, 기존 프리팹 및 테스트를 조사할 준비를 했다.
- 공용 일시정지 기록 문서와 현재 Git 작업 상태를 확인했다.

### 미반영 항목 및 재개 순서

- 이번 유닛 확장과 관련한 문서, 코드, 프리팹, 테스트 변경은 아직 시작하지 않았다.
- 재개 시 가장 먼저 `Docs/Unit_Behavior_Guidelines.md`를 읽고 3종의 행동 기준과 실제 기본 수치를 확정해 문서에 반영한다.
- 다음으로 현재 `PrototypeUnitType`, `PrototypeUnitStatus`, 기존 유닛 프리팹 생성기와 Asset 에디터 팩토리의 직렬화 및 생성 규칙을 확인한다.
- 기존 enum 직렬화 값을 보존하면서 3종 타입과 기본값, 필요한 공개 API를 추가한다.
- 기존 팀, 선택, 안개, 이동, 공격 컴포넌트 조합을 그대로 사용해 프리팹 3종을 생성한다. `Medic`에는 치료 또는 별도 전투 시스템을 추가하지 않는다.
- PlayMode 테스트를 추가하고 컴파일 및 Unity Test Runner 검증을 수행한 뒤, 즉시 실행하지 못한 항목은 검증 백로그에 기록한다.

### 작업 상태 및 주의 사항

- 구현/수정 작업은 이 기록 시점에서 일시정지했다.
- 이번 유닛 확장 범위로 변경된 파일은 없다. 이 항목을 기록하기 위해 본 문서만 수정했다.
- 현재 워크트리에는 건물 프리팹 카탈로그, 카메라, 안개, 자동 부트스트랩, 에디터 셋업, 테스트 및 복구 씬과 관련된 기존 미커밋 변경이 있다. 유닛 작업 재개 시 이를 사용자 또는 다른 작업의 변경으로 간주하고 수정하거나 되돌리지 않는다.
- 커밋과 푸시는 수행하지 않았다.

---

## 2026-09-15: 건물 Structure 계층 1단계

### 대상 작업

- 모든 건물이 공통 `Structure` 기반 생명주기와 API를 사용하도록 구조를 정리한다.
- 기존 `BuildingStatus` 직렬화 참조와 레지스트리, 승패, 선택, 안개, 생산, 보급 흐름의 호환성을 유지한다.
- 기존 건물 역할 7종과 신규 건물 역할 7종의 파생 컴포넌트를 추가한다.
- 신규 건물 프리팹과 공통 상태 PlayMode 테스트를 추가한다.
- `BuildingPlacementService`, 건설 UI, 해금, 동적 경로 점유는 이번 단계에서 변경하지 않는다.

### 현재까지 반영된 내용

- 공통 `Structure` 기반 클래스와 `StructureFactory`를 추가했다.
- 기존 `BuildingStatus`를 `Structure` 상속 호환 계층으로 변경하고 `BuildingKind`에 신규 7종을 추가했다.
- 기존 역할용 파생 클래스 `MainBaseStructure`, `ProductionStructure`, `SpliterProductionStructure`, `ResourceDropOffStructure`, `SupplyDepotStructure`, `AutoTurretStructure`, `SpeedAuraStructure`를 추가했다.
- 신규 역할용 파생 클래스 `ResearchLabStructure`, `DefenseControlCenterStructure`, `VehicleFactoryStructure`, `SignalRelayStructure`, `TacticalCommandCenterStructure`, `MaintenanceBayStructure`, `ForwardSupplyPostStructure`를 추가했다.
- `BuildingHealth`에 최대 체력 설정 API를 추가하고 `SupplyManager`가 공통 `Structure` 타입을 받을 수 있도록 변경했다.
- 런타임 부트스트랩과 에디터 셋업 빌더에 역할 컴포넌트 및 신규 건물 템플릿/프리팹 생성 경로를 연결하는 초안이 반영됐다.
- 기존 특수 건물 프리팹 4종에 파생 역할 컴포넌트 전환 변경이 반영됐다.
- `StructurePlayModeTests` 초안이 추가됐다.

### 미완료 및 재개 지점

- 신규 건물 프리팹 7종은 아직 실제 `.prefab` 파일로 생성되지 않았다. 에디터 생성 메뉴 또는 검토된 YAML 생성 방식으로 완성해야 한다.
- `PrototypeMainBase`를 포함한 기존 건물 프리팹의 역할 컴포넌트 적용 상태가 일관적인지 전체 점검해야 한다.
- `Structure`, `StructureFactory`, 부트스트랩, 셋업 빌더의 변경을 다시 읽고 직렬화 호환성과 런타임 등록 순서를 검토해야 한다.
- `StructurePlayModeTests`는 컴파일 안정성과 의도한 검증 범위를 아직 최종 검토하지 않았다.
- 커스텀 targets를 사용한 `dotnet build`를 시작했으나 최종 종료 결과를 확인하지 못했다. 현재 빌드 성공으로 간주하면 안 된다.
- Unity Editor 프로세스가 실행 중이어서 프로젝트 잠금에 영향을 줄 수 있다. 프로세스를 임의로 종료하지 말고 사용자 상태를 먼저 확인한다.

### 주요 변경 파일

- `Assets/05.Scripts/Buildings/Structure.cs`
- `Assets/05.Scripts/Buildings/StructureFactory.cs`
- `Assets/05.Scripts/Buildings/BuildingStatus.cs`
- `Assets/05.Scripts/Buildings/BuildingHealth.cs`
- `Assets/05.Scripts/Buildings/*Structure.cs`
- `Assets/05.Scripts/Resources/SupplyManager.cs`
- `Assets/05.Scripts/MapCreateSceneAutoBootstrap.cs`
- `Assets/05.Scripts/Editor/MapCreateSceneSetupBuilder.cs`
- `Assets/05.Scripts/Tests/PlayMode/StructurePlayModeTests.cs`
- `Assets/03.Prefabs/Buildings/Core/PrototypeAutoTurretBuilding.prefab`
- `Assets/03.Prefabs/Buildings/Core/PrototypeProductionBuilding.prefab`
- `Assets/03.Prefabs/Buildings/Core/PrototypeSpeedAuraBuilding.prefab`
- `Assets/03.Prefabs/Buildings/Core/PrototypeSpliterProductionBuilding.prefab`

### 작업 상태 및 주의 사항

- 구현/수정 작업은 이 기록 시점에서 일시정지했다.
- 커밋과 푸시는 수행하지 않았다.
- 서브 에이전트 2개가 프리팹/생성기와 테스트를 나눠 작업했으나 사용량 제한으로 종료됐으며, 공유 워크트리에 남은 변경은 메인 에이전트의 최종 통합 검토를 거치지 않았다.
- 작업 전부터 존재하던 `Assets/_Recovery/0 (3).unity`, `Assets/_Recovery/0 (4).unity` 및 각 `.meta`는 사용자 변경으로 간주하며 수정하거나 삭제하지 않는다.
- 유닛 행동은 변경하지 않았으므로 `Docs/Unit_Behavior_Guidelines.md`는 갱신하지 않았다.

---

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
