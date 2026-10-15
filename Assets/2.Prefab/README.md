# 프리팹 분류

- Characters: Cookie — 달리기, 2단 점프, 고정 슬라이드, 탈진 프레임은 PlayerCookie에서 교체합니다.
- Items: Jelly, BigJelly, Potion, Magnet, Boost, Dash.
- Obstacles: ObstacleLow, ObstacleTall, ObstacleHang.
- Environment: Ground, Cloud, Hill. Themes에는 기본·숲·돌산·용암·동굴·타이틀 배경이 있습니다.
- Systems: GameAudio.
- UI: 씬에 연결된 TitleUI, GameHUD, SettingsPanel, RankingPanel.
- UI/Controls: VolumeSlider, JumpButton, SlideButton, PauseButton, StartButton, ScorePanel.
- UI/Popups: GameOverPanel, ConfirmTitlePanel.
- UI/Title: CookieAdventureLogo, StorybookBackdrop.

원본 18개 프리팹은 GUID를 유지한 채 이동했으므로 씬과 연결이 유지됩니다.
Controls/Popups/Title의 개별 프리팹은 독립적으로 재사용할 수 있는 사본입니다. 이 사본을 수정해도 기존 전체 UI에 자동으로 전파되지 않습니다. 현재 게임을 변경하려면 UI 폴더의 전체 프리팹을 편집합니다. 개별 사본을 새 UI에 사용할 경우 GameHud/SettingsPanel의 버튼·슬라이더 필드를 연결합니다.

볼륨 바: Background(갈색) → Fill Area/Fill(주황색) → VolumeFrame(테두리) → Handle Slide Area/Handle(54×54 원형 손잡이).
손잡이의 가로·세로 크기를 같게 유지합니다.
