# 전투 이미지와 스킬 연출 설정

## NPC 또는 플레이어 추가

`CharacterData`의 `Battle Sprite`, `Battle Launch`, `Battle Hit`를 설정한다.

- `Battle Launch`: 스킬이 출발할 위치. 이미지 바깥의 좌표도 허용한다.
- `Battle Hit`: 상대 스킬의 주요 타격 부분이 닿을 몸 부위.
- 좌표는 실제 표시 이미지의 왼쪽 아래가 `(0, 0)`, 오른쪽 위가 `(1, 1)`이다. `Image.preserveAspect`로 생기는 여백은 제외한다.
- 같은 이미지에 장비나 머리카락, 투명 여백이 있어도 피격점은 몸통 안쪽에 둔다. 피격 흔들림에 따라 살짝 이동해도 몸 안에 남도록 가장자리를 피한다.

NPC 전투에서는 `BattleFlow`가 현재 양측 `BattleState.Character`를 연출에 전달한다. `BattleSession`으로 들어온 새 NPC에도 같은 경로를 사용하므로 이름이나 ID에 따른 코드 분기는 추가하지 않는다. Battle 씬 단독 실행용 기본 데이터는 `BattleSystem` 오브젝트의 `BattleSkillPresenter`에 연결한다. 플레이어 뒤쪽 이미지는 기존 씬의 Image를 유지한다.

현재 플레이어의 `Battle Hit`는 `(0.58, 0.32)`로, 얼굴보다 낮은 가슴 높이의 몸통 안쪽을 사용한다. 준호는 `(0.43, 0.47)`, 소라는 `(0.40, 0.45)`로 설정했다. 피격 위치만 바꾸려면 이 값을 조정하면 된다.

## 스킬 추가

`Battle/Skill Presentation` 에셋을 만들고 `SkillData.Presentation`에 연결한다. 전용 설정이 없으면 Presenter의 `Default Presentation`을 사용한다.

`Effects` 항목의 주요 설정은 다음과 같다.

| 필드 | 설정 내용 |
| --- | --- |
| `Prefab` | 루트에 Simple UI Image를 가진 이펙트 |
| `Playback` | `Travel`: 발사점에서 피격점으로 이동 / `AtTarget`: 피격점 고정 / `AtActor`: 발사점 고정 / `AtCenter`: 양측 중간 고정 |
| `Time` | 이펙트 생성 시점. 이동형은 Impact Time보다 먼저 생성되도록 설정 |
| `Animation` | 루트 Image의 Sprite와 색상을 재생하는 클립. 설정하면 기존 Animator 대신 공통 시간축으로 재생 |
| `Animation Impact Time` | 도착 시 보여 줄 클립 내부의 타격 시점(초) |
| `Contact Point` | 스킬 이미지 안에서 상대에게 닿아야 하는 위치. 이미지 왼쪽 아래 `(0, 0)` 기준 |
| `Contact Frames` | 프레임마다 그림이 내부에서 이동하는 클립의 접촉점. 비어 있으면 Contact Point만 사용 |
| `Size Scale` | 프리팹 크기에 적용할 배율 |
| `Rotate To Direction` | 공격 경로에 맞춰 기울일지 선택 |
| `Mirror For Enemy` | 적 공격에서 좌우반전할지 선택 |
| `Only On Damage` | 실제 피해가 있을 때만 표시할 고정 연출. 이동형은 피해 판정 전에 표시 |

`Impact Time`은 연출 시작 후 피해를 적용할 시점이다. 이동은 그때 끝나고, 지정한 `Animation Impact Time` 프레임을 보여 준 뒤 같은 프레임에 피해 콜백을 실행한다. 이후에는 클립의 남은 부분을 재생하며 프레임별 접촉점으로 위치를 보정한다. 프레임이 늦어도 타격 프레임을 건너뛰지 않도록 처리한다.

`Offset`은 공통 Canvas 좌표의 의도적인 추가 보정이다. 몸에 정확하게 맞추는 연출에서는 `(0, 0)`으로 두고 Contact Point 또는 Contact Frames를 조정한다. `Lifetime`은 Animation을 지정하지 않은 기존 Animator 연출의 유지 시간이다. `Legacy`와 숨겨진 `travelToTarget` 값은 이전 직렬화 설정을 읽기 위해 유지한다.

## 접촉점 초안 생성과 미리보기

1. 스킬 에셋에서 Prefab과 Animation을 연결한다.
2. Inspector의 **클립에서 프레임별 접촉점 초안 생성**을 누른다. 원본 PNG/JPG를 읽어 각 프레임의 가장 큰 연결 영역에서 알파 가중 중심을 구한다. 텍스처의 Read/Write 설정이나 원본 클립은 바꾸지 않는다. Undo로 되돌릴 수 있다.
3. **스킬 위치 미리보기 열기** 또는 `Tools > Battle > Skill Preview`를 연다.
4. 플레이어 설정·이미지, NPC 설정, 스킬 연출을 선택한다. 파란 점은 발사, 빨간 점은 피격 지점이다.
5. **타격 순간**과 시간 슬라이더로 큰 타격 모양이 몸에 겹치는지 확인한다. `Animation Impact Time`, `Contact Frames`, `Size Scale`을 조정한다. 자동 생성은 초안이므로 검이나 긴 물줄기처럼 중심과 실제 접촉 부위가 다른 그림은 수동 보정한다.
6. 방향을 바꾸고 체형이 다른 NPC로도 확인한다. 도착 직후와 소멸 전에도 몸 바깥으로 주 타격 부분이 흘러가지 않는지 본다. 프리팹을 교체했다면 **설정 다시 읽기**를 누른다.
7. 실제 Battle 씬에서 피해·깜빡임·흔들림·턴 진행까지 확인한다. 미리보기는 피해나 전투 상태를 변경하지 않으며 원본 씬과 분리된 공간을 사용한다.

현재 6개 연출은 0.3초에 도착하도록 설정했다. 각 클립의 타격 시점은 불꽃·물대포 0.16666667초, 강한 펀치·마하펀치·Lunge 0.125초, 나뭇잎 칼날 0.083333336초이다. 이는 클립 내부 시각이며 실제 피해는 공통으로 연출 시작 후 0.3초에 적용된다.
