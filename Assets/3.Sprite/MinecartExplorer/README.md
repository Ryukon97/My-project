# 광산 카트 모험가

`MinerCartSheet.png`는 노란 헤드램프 안전모의 광부가 철제 광산 카트에 탑승한 캐릭터입니다. 내장 ImageGen으로 레퍼런스의 카트 형태와 광부 컨셉을 참고해 새로 제작했습니다.

- Ride_0~3: 기본 탑승 모습
- Jump_0~3: 점프·2단 점프
- Duck_0~3: 카트 안으로 몸을 숨기는 회피 모습. 플레이 중에는 Duck_1을 고정해 사용합니다.
- Exhaustion_0~3: 카트 안에서 지쳐 쓰러지는 모습

PNG는 투명 배경이며 원본 픽셀을 변경하지 않고 Unity의 Multiple Sprite로 영역을 분리합니다. 프레임은 같은 Pixels Per Unit을 사용하고 바퀴 하단을 기준으로 정렬합니다.

기존 씬 연결을 유지하기 위해 `Assets/2.Prefab/Characters/Cookie.prefab`의 이미지를 교체합니다. 타이틀·MainGame·맵/아이템 편집기의 캐릭터 미리보기에 함께 반영됩니다. 프리팹의 PlayerCookie 컴포넌트에서 이미지 배열을 바꿀 수 있습니다.

**Tools > CookieRun > 광산 카트 캐릭터 이미지 적용**으로 시트를 다시 분리하고 연결할 수 있습니다. 점프 물리와 충돌 판정, 맵·아이템·UI 데이터는 변경하지 않습니다.

## 제작 프롬프트

방식: 내장 `image_gen`, 투명 배경. 입력 1은 카트 구조, 입력 2는 광부 컨셉, 입력 3은 기존 캐릭터의 동화풍 표현 참고용입니다.

> Use case: stylized-concept. Asset type: production-ready transparent 2D game character animation sprite sheet, exactly 4 columns by 4 rows, 16 equally spaced separate sprites on a square canvas. Input images: image 1 is a reference ONLY for the shape of an industrial mine cart with two visible side wheels; image 2 is a reference ONLY for a friendly chibi miner wearing a yellow helmet and brown overalls; image 3 is a reference ONLY for the existing game's cute rounded 2D painted sprite art style. Draw an original new human miner riding inside a small mine cart, consistently facing RIGHT in all 16 cells. The childlike adult adventurer has a round peach-skinned face, brown hair, black expressive eyes, a bright yellow miner helmet with a round cream headlamp, tan shirt and brown overalls. Their lower body stays INSIDE the cart. The cart is compact warm dark gray steel with bronze edge trim, rivets, two clearly visible circular dark wheels and a small pickaxe fastened to its rear side. Use crisp dark warm-brown outlines, polished soft painted cel shading, friendly cheerful mobile adventure game aesthetics, readable at small size. Preserve IDENTICAL character identity, cart design, cart width, wheel size, lighting, viewing angle and scale throughout the sheet. Show the whole cart with wheels in every cell; leave generous transparent padding and never crop a helmet or wheel. No rail track or ground is included. Row 1, 4 cells: normal riding animation, smiling and looking right, hands resting on the front rim, only subtle body bob and different wheel spoke angles. Row 2, 4 cells: jumping in the cart, first lifting upward with excited face, then a small backward tilt at peak, then a small forward tilt, then preparing to land; miner remains in the cart, no full somersault. Row 3, 4 cells: a LOW ducking/dodging pose, miner crouched deep inside the cart so only the helmet and eyes peek just above the cart rim; all four cells retain the same low pose with very subtle expressive changes, NEVER stretch or squash the cart. Row 4, 4 cells: exhaustion/death sequence, tired eyes, drooping head, then head resting on the cart's front rim, final resting slump; cart remains upright and intact with wheels resting at the SAME bottom baseline as row 1. These are animation states of ONE miner-in-cart character, never a standing miner outside the cart. True transparent alpha background, including between cells. NO text, numbers, labels, grid lines, scenery, ground plane, solid backdrop, checkerboard pattern, watermark, logos or UI.
