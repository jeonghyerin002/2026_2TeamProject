# 배틀 SO 적용 및 흐름 안내

기준: 2026-09-23 원격 origin/main fetch 확인, 커밋 6801563.
기존 BattleSystem, BattleState, BattleTurnSystem, BattleResolver, BattleFlow를 확장했다.
기존 씬/프리팹의 스크립트 GUID, 선발 Character/Aether 필드, SelectPlayerSkill(int) 연결을 유지한다.

## 사진의 12단계와 코드 연결

| 순서 | 동작 | 담당 |
|---|---|---|
| 1 | 스킬 슬롯과 PP 확인, 양측 선택 | BattleFlow.SelectPlayerSkill → BattleSystem |
| 2 | 우선도 → 스피드 → 동률 50:50 | BattleTurnSystem |
| 3 | 기절, 수면, 마비, 혼란 판정 | BattleState.CanAct |
| 4 | 실제 사용 시 PP 1 소비, 기술 사용 문구 | BattleResolver.TryBegin / BattleFlow |
| 5 | Accuracy 명중 판정, 실패 시 문구 후 행동 종료 | BattleResolver.IsHit |
| 6 | 사용자/스킬별 애니메이션 이벤트, 지정 시간 대기 | BattleFlow.AnimationRequested 및 Inspector 이벤트 |
| 7 | 데미지 계산 후 HP 변경 | BattleResolver.ApplyDamage |
| 8 | 효과가 굉장함/약함/없음, 급소 문구 | BattleFlow.ResolveAction |
| 9 | 효과 ID, 회복, 흡수, 반동 | BattleResolver.ApplyEffects |
| 10 | 양측 HP 0 확인, 기절 문구 | BattleSystem.CompleteAction |
| 11 | 경험치, 교체, 승패 판단 | BattleSystem |
| 12 | 두 행동 완료 시 지속 피해 처리 후 다음 턴 | BattleTurnSystem |

행동 불가 시 사용 문구·PP 소비·명중·애니메이션·기술 효과를 생략한다.
빗나가면 PP는 소비하지만 애니메이션·데미지·부가효과는 적용하지 않는다.
기절한 멤버의 예약 행동은 취소되며, 교체 멤버가 대신 공격하지 않는다.
전투 종료가 확정되면 남은 행동과 턴 종료 피해는 실행하지 않는다.
양측이 턴 종료 피해 또는 반동 등으로 모두 전멸하면 무승부다.

## 데미지

기본 피해:
    (((2 × 레벨 / 5 + 2) × 위력 × 공격 / 방어) / 50) + 2

계산 순서:
1. 기본 피해: 각 정수 나눗셈에서 소수점 버림
2. 자속: 기술 타입 = 사용자의 에테르 타입이면 ×1.5, 소수점 버림
3. 타입 상성: ×2 / ×1 / ×0.5, 소수점 버림
4. 급소: 성공 시 ×2

랜덤 데미지 보정과 기타 최종 배율은 없다.
물리는 공격/방어, 특수는 특수공격/특수방어를 사용한다.
능력치는 무기 배율, 랭크, 상태이상을 반영한 전투 능력치다.
위력 0은 피해 0으로 유지하고, 공격 기술은 상성이 0이 아닐 때 최소 1이다.
BattleResolveResult.Damage는 실제 줄어든 HP이며 흡수·반동도 이를 기준으로 한다.

사진 예시: 레벨 50, 위력 40, 공격 100, 방어 80 → 기본 24.
자속 + 유리 상성 + 급소면 24 → 36 → 72 → 144.

현재 ElementType의 6개 타입만 처리한다. 방어 타입은 에테르의 단일 타입이다.

| 공격 타입 | 2배 | 0.5배 |
|---|---|---|
| Normal | 없음 | 없음 |
| Fire | Grass | Fire, Water |
| Water | Fire | Water, Grass |
| Grass | Water | Fire, Grass |
| Electric | Water | Electric, Grass |
| Fighting | Normal | 없음 |

나머지는 1배다. 현재 6개 타입 안에는 0배 조합이 없다.
효과 없음 분기는 준비되어 있으며 새 타입 도입 시 GetEffectiveness에 상성을 추가한다.

## 네 종류 SO의 역할

- CharacterData: 기본 HP, 물리/특수 능력치, 스피드.
- AetherData: 자속/방어 타입과 기본 스킬 슬롯.
- WeaponData: 6개 능력치 배율과 추가 스킬. 미장착은 1배.
- SkillData: 위력, PP, 명중, 우선도, 타입, 효과 ID와 회복/흡수/반동 비율.

BattleState가 HP, PP, 랭크, 상태이상, 경험치를 소유한다.
SO 원본이나 SO의 스킬 배열은 전투 중 수정하지 않는다.
스킬 슬롯은 에테르 배열 순서 뒤에 무기 배열 순서로 이어진다. 빈 슬롯도 인덱스를 유지한다.
같은 스킬 SO를 여러 슬롯에 넣으면 PP를 공유한다. 서로 다른 SO는 ID가 같아도 PP를 공유하지 않는다.
잘못된 무기 배율(0 이하, NaN, Infinity)은 1배로 처리한다.

SkillData에 추가된 Inspector 필드:
- Critical Chance: 기본 0.04(4%). 사진에 확률이 없어 둔 기본값이며 변경 가능.
- Heal Ratio: 자신의 최대 HP 대비 회복 비율(기본 0).
- Drain Ratio: 실제 가한 피해 대비 흡수 비율(기본 0).
- Recoil Ratio: 실제 가한 피해 대비 반동 비율(기본 0).

기존 CSV 가져오기는 그대로 동작한다.
이 추가 필드들은 Inspector에서 설정하거나 같은 필드명의 CSV 열을 추가할 수 있다.

## 기존 효과 CSV 연결

SkillEffectData.csv의 현재 효과 정의를 BattleResolver.ApplyEffectId에서 처리한다.
CSV를 런타임에 읽는 기능은 아니다. 효과 정의를 바꾸면 해당 매핑도 변경한다.

- 60001~60005: 30% 확률로 마비/독/화상/동상/맹독.
- 60006: 30% 혼란. 다른 주요 상태와 공존.
- 60007: 자신의 상태이상과 혼란을 해제하고 수면 적용.
- 60008: 60% 상대 수면.
- 60009: 100% 상대 수면. 명중과 상태 중복 제한은 적용.
- 60010: 현재 기술 급소 확률 25%, 급소 배율 2배.
- 60012~60016: 방어/공격/특수공격/특수방어/스피드 1랭크 상승.
- 0 및 정의되지 않은 ID: 추가 효과 없음.

랭크는 -6~+6이며 +1랭크는 1.5배다.
상승 배율은 (2+랭크)/2, 하락 배율은 2/(2-랭크)다.
스피드 변화는 다음 턴의 행동 순서부터 반영한다.

사진과 CSV에 수치가 없는 세부 규칙은 다음 기본값을 사용한다:
- 수면: 자신의 행동 기회 1~3회 차단 후 다음 기회에 행동.
- 마비: 스피드 절반, 행동 불가 확률 25%.
- 혼란: 행동 기회 2~5회, 1/3 확률로 위력 40의 자해. 자속·상성·급소 없음.
- 독: 턴 끝 최대 HP의 1/8.
- 화상/동상: 턴 끝 최대 HP의 1/16, 각각 공격/특수공격 절반.
- 맹독: 턴 끝 최대 HP의 1/16부터 단계적으로 증가(최대 15단계). 교체 시 단계 초기화.
- 주요 상태는 하나만 허용하며 독 → 맹독 교체만 허용.
- 타입별 상태이상 면역은 별도 기획이 없어 적용하지 않음.
- 교체 시 랭크/혼란 초기화. 전투 종료 시 수면도 해제.
- 전체 PP 소진: 슬롯 -1의 발버둥(위력 50, 무속성, 필중, 자신의 최대 HP 1/4 반동).
- 적 AI: 사용 가능한 첫 스킬, 없으면 발버둥.

독/마비 등 주요 상태는 종료 후 BattleState에 남는다.
StartBattle은 새 전투 상태를 생성하므로 새 전투나 세이브에 상태/경험치를 영구 보존하는 기능은 포함하지 않는다.

## Inspector 및 UI 연결

BattleSystem:
1. 기존 Player/Enemy Character와 Aether에 SO를 연결한다.
2. Player/Enemy Weapon은 선택 사항이다.
3. 양측 Level을 지정한다(기본 1, 1~100).
4. Player/Enemy Reserves에 예비 Character/Aether/Weapon/Level을 지정한다.
5. Experience Reward는 적 한 명 격파 시 현재 살아 있는 아군에게 지급할 경험치다(기본 50).

BattleFlow:
1. Battle System 참조는 기존 연결을 유지한다.
2. Battle Text에 대화창 TMP_Text를 연결한다.
3. Player Hp / Enemy Hp에 HP Slider를 연결한다.
4. 스킬 버튼에서 SelectPlayerSkill(슬롯 번호)을 호출한다.
5. 발버둥 버튼은 SelectStruggle()을 호출한다. PP가 남아 있으면 거절한다.
6. 교체 버튼은 SelectReplacement(파티 번호)를 호출한다. 0은 최초 선발, 1부터 예비 멤버다.
7. On Player Animation / On Enemy Animation에 Animator 트리거 등을 연결한다.
8. Animation Duration을 연결한 연출 길이에 맞춘다. 기술별 연출은 AnimationRequested(actor, target, skill) 이벤트에서 선택할 수 있다.
9. Text Duration은 문구 표시 시간이다.
10. On State Changed에서 스킬 PP와 버튼 상태 등을 갱신한다.

캐릭터 이미지와 애니메이션 클립 자체를 제작하는 작업은 포함하지 않는다.
이번 변경은 씬 UI를 자동 생성하지 않으므로 기존 Turn_Test의 단일 스킬 버튼 외에 문구·HP·교체 UI는 위 필드에 연결한다.
BattleFlow.IsBusy 동안 입력을 비활성화하고, Phase가 WaitingReplacement일 때 교체 목록을 표시한다.
PlayerParty, PlayerState, EnemyState에서 UI용 값을 읽는다.
연출 중 BattleFlow를 비활성화하면 중단된 행동은 재실행하지 않으며 전투가 중단된다. 재개 대신 StartBattle로 명시적으로 다시 시작한다.
BattleSystem.BattleEnded에서 승패를 받는다. null은 무승부다.
경험치는 BattleState.Experience에 누적한다. 레벨업 공식이나 세이브 시스템은 별도로 연결한다.

## 검증

Unity 메뉴 Tools > Battle > Verify Rules에서 검증한다.
검증은 임시 SO/게임 오브젝트를 사용하고 해제하며, 기존 에셋과 전역 난수 상태를 보존한다.
계산, 보정, PP, 상태이상, 장비, 순서, 교체, 보상, 발버둥, 무승부, 코루틴 연출 순서를 확인한다.

2026-09-23 검증 결과: Unity 6000.3.23f1 별도 테스트 프로젝트에서 47개 검사 통과. 원본 Assembly-CSharp 전체 빌드 오류 0, 경고 0. 실제 씬에서 그래픽/클립을 재생하는 수동 시각 검증은 수행하지 않았다.
