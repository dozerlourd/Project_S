# 해금 규칙 CSV 입력 안내

## 파일과 목적

- 입력 템플릿: `Assets/Resources/UnlockRuleCatalog_Template.csv`
- 가져온 카탈로그: `Assets/Resources/UnlockRuleCatalog.asset`
- CSV에서 활성화한 규칙은 런타임 유닛 생산, 건설 메뉴, AI 건설 판정에 적용된다.
- 템플릿에는 규칙 데이터가 없으므로 현재 유닛과 건물은 잠기지 않는다.

## 열 정의

| 열 | 입력 기준 |
|---|---|
| `규칙ID` | 규칙을 구분하는 고유 ID. 영문 소문자와 하이픈 사용을 권장한다. |
| `대상종류` | `유닛` 또는 `건물`. 영문 `Unit`, `Building`도 허용한다. |
| `대상ID` | 유닛은 `PrototypeUnitType`, 건물은 `BuildingKind`의 enum 이름을 입력한다. |
| `필요건물` | 필요한 `BuildingKind` 이름. 여러 값은 `|` 또는 `;`로 구분한다. |
| `필요연구` | `UnitUpgradeDefinition.ResearchId`와 일치하는 안정적인 연구 ID. 여러 값은 `|` 또는 `;`로 구분한다. 기본 연구 ID는 `attack-damage`, `movement-speed`다. |
| `선행해금` | 먼저 충족해야 할 해금 ID. 여러 값은 `|` 또는 `;`로 구분한다. |
| `활성` | `TRUE/FALSE`, `예/아니오`, `1/0`을 허용한다. 빈 값은 안전하게 비활성으로 가져온다. |
| `메모` | 기획 의도나 확인 사항을 자유롭게 기록한다. 쉼표가 있으면 셀 값을 큰따옴표로 감싼다. |

## 가져오기

1. Excel 또는 스프레드시트 앱에서 CSV를 UTF-8 형식으로 편집한다.
2. Unity Project 창에서 가져올 CSV `TextAsset`을 선택한다.
3. `Tools > Project S > Unlock Rules > Import Selected CSV`를 실행한다.
4. 생성 또는 갱신된 `UnlockRuleCatalog.asset`의 항목을 Inspector에서 확인한다.

`Import Default Template` 메뉴는 기본 템플릿을 가져온다. 현재 템플릿은 헤더만 있으므로 빈 카탈로그가 생성되며 런타임 동작은 바뀌지 않는다.

활성 규칙의 `필요건물`, `필요연구`, `선행해금`은 모두 충족해야 한다. 같은 대상에 활성 규칙이 여러 개 있으면 각 규칙을 모두 충족해야 하며, 비활성 규칙과 대상 규칙이 없는 항목은 기존처럼 사용할 수 있다.
