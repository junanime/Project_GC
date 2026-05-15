# 24시간의 사투 - Main Menu UI References

세 가지 시안 모두 Vampire Survivors의 메뉴 구조를 참고해서 큰 타이틀, 큰 버튼, 캐릭터/강화/도감 진입을 빠르게 읽히도록 잡았다.

- `main_menu_reference_01.png`: 가장 Vampire Survivors에 가까운 어두운 고딕풍 메인 메뉴. 왼쪽 세로 버튼, 오른쪽 마스코트/플레이어 배치.
- `main_menu_reference_02.png`: 원광대 공식 캐릭터 4종을 캐릭터 선택 카드처럼 보여주는 메인 메뉴 확장안.
- `main_menu_reference_03.png`: 원광대 블루/골드 톤을 앞세운 캠퍼스 브랜드형 메인 메뉴. 공식 프로젝트 느낌이 가장 강함.

사용한 방향성:
- 게임명: `24시간의 사투`
- 대학 표기: `Wonkwang University`, `원광대`
- 마스코트 키워드: `혁이`, `신이`, `아시`, `아리`
- 원광대 공식 설명 기준: 아시와 아리는 각각 봉황과 봉황의 알을 형상화한 캐릭터, 혁이와 신이는 대학혁신사업단 캐릭터

다음 단계는 선택한 시안을 Unity Canvas prefab으로 만드는 것이다.

최종 방향:
- `main_menu_final_reference.png`
- 사용자가 보낸 레퍼런스처럼 어두운 캠퍼스 전장 배경, 큰 왼쪽 타이틀, 왼쪽 세로 메뉴, 오른쪽 상단 유틸리티 아이콘, 전면 마스코트 3인 구도로 잡았다.
- 실제 프리팹 제작 시 배경 키아트와 버튼/텍스트 UI를 분리해서 Canvas 위에 재구성하는 것이 좋다.

작동 씬:
- `Assets/donggyu/Scenes/DonggyuMainMenu.unity`
- `Assets/donggyu/Scripts/DonggyuMainMenu.cs`
- `Assets/donggyu/Resources/MainMenuBackground.png`
- `main_menu_scene_background.png`는 씬에서 쓰는 배경 키아트의 참고용 복사본이다.
