# 3D Map Editor Tool

## 개요

Project S용 3D 타일셋 맵 조립 툴은 Unity Editor에서 사용하는 정사각 격자 기반 맵 제작 도구다.

메뉴 위치:

```text
Tools/Project S/Map Editor
```

기본 타일셋 생성:

```text
Tools/Project S/Create Default Map Tool Assets
```

## 기본 사용 흐름

1. `Tools/Project S/Create Default Map Tool Assets`를 실행한다.
2. 생성된 `DefaultTileSet` 에셋을 선택하거나 Map Editor 창에 할당한다.
3. `Tools/Project S/Map Editor`를 연다.
4. `Create Map Asset`으로 새 `MapDefinition` 에셋을 만든다.
5. Layer와 Brush Mode를 선택해 Scene View에서 타일을 배치한다.
6. `Rebuild Preview`로 현재 맵 프리뷰를 재생성한다.
7. `Validate Map`으로 시작 위치, 자원 접근성, 램프 연결, 연결성, 오브젝트 겹침을 확인한다.

Scene View에서 타일을 배치하기 전에는 마우스가 올라간 칸에 선택한 프리팹이 반투명 프리뷰로 표시된다. 배치 가능한 위치는 푸른색, 배치 불가능한 위치는 붉은색으로 보인다.

타일, 자원, 장식물, 스폰 지점은 모두 격자 사각형의 중심을 기준으로 배치된다. 격자선과 셀 오버레이만 사각형 경계 기준으로 표시된다.

이미 `DefaultTileSet.asset`이 있는 상태에서 기본 타일셋 생성 메뉴를 다시 실행하면, 기존 기본 프리팹을 삭제 후 다시 만들고 기본 팔레트 내용을 최신 구성으로 갱신한다.

## 주요 데이터

- `MapDefinition`: 맵 크기, 타일 크기, 셀 데이터, 배치 오브젝트, 시작 위치, 자원 위치를 저장한다.
- `TileSetDefinition`: 지형, 램프, 절벽, 장식물, 자원, 시작 위치 프리팹 팔레트를 저장한다.
- `MapRuntimeBuilder`: 런타임에서 `MapDefinition`을 읽어 맵 오브젝트를 생성한다.

## 지원 브러시

- `SinglePaint`: 단일 셀 배치
- `RectangleFill`: 사각형 영역 채우기
- `Erase`: 셀/오브젝트 삭제
- `HeightSelect`: 셀 높이값 변경
- `WalkablePaint`: 이동 가능 여부 칠하기
- `BuildablePaint`: 건설 가능 여부 칠하기

## Scene View 단축키

- `R`: 배치할 타일을 시계 방향으로 90도 회전
- `Shift + R`: 배치할 타일을 반시계 방향으로 90도 회전

## 기본 지형 팔레트

기본 생성 메뉴는 다음 지형 프리팹을 만든다.

- `High Ground - Straight End`: 높은 땅 일직선 끝
- `High Ground - Corner Edge`: 높은 땅 가장자리
- `High Ground`: 높은 땅
- `Base Ground`: 기본 땅
- `Base To High Ramp`: 기본 땅에서 높은 땅으로 올라가는 경사진 땅
- `Base To High Ramp - 2 Cells`: 기본 땅에서 높은 땅으로 올라가는 2칸짜리 경사진 땅
- `Base To High Ramp - 3 Cells`: 기본 땅에서 높은 땅으로 올라가는 3칸짜리 경사진 땅
- `Lower Blocked Ground`: 더 낮아서 갈 수 없는 땅
- `Low Ground - Straight End`: 낮은 땅 일직선 끝
- `Low Ground - Corner Edge`: 낮은 땅 가장자리

일직선 끝과 가장자리 프리팹은 한쪽 면 기준으로 만들어지며, Map Editor의 `Rotate 90` 버튼으로 회전해서 다른 방향에 배치한다.

2칸/3칸 오르막길은 배치 시작 셀에서 선택한 회전 방향으로 긴 footprint를 가진다. 배치 전 프리뷰와 배치 가능 판정도 회전된 footprint를 기준으로 계산한다.

`High Ground`, `Base Ground`, `Lower Blocked Ground`는 인접한 비-오르막 지형과 높이 차이가 있을 때 프리뷰/런타임 생성 과정에서 자동 벽면을 만든다. 오르막길과 맞닿은 면은 진입로가 막히지 않도록 자동 벽면 생성 대상에서 제외한다.

Palette는 `Ground`, `Ramps`, `Cliffs` 그룹으로 나뉘며, 각 항목에는 footprint 크기, 높이, 이동 가능 여부, 건설 가능 여부가 표시된다.
