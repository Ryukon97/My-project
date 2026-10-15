using UnityEngine;
using UnityEngine.EventSystems;

namespace CookieRun
{
    /// <summary>
    /// 화면의 Jump / Slide 버튼. 누른 순간(ConsumePressed)과 누르고 있는 상태(IsHeld)를 게임이 읽어 간다.
    /// 레이캐스트를 받는 Graphic(Image)이 같은 오브젝트에 있어야 한다.
    /// </summary>
    public class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [Tooltip("누르는 동안의 크기 배율")] public float pressedScale = 0.92f;

        public bool IsHeld { get; private set; }

        bool pressed;

        /// <summary>마지막 호출 이후 눌린 적이 있으면 true 를 한 번만 돌려준다.</summary>
        public bool ConsumePressed()
        {
            bool result = pressed;
            pressed = false;
            return result;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            IsHeld = true;
            pressed = true;
            transform.localScale = Vector3.one * pressedScale;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            Release();
        }

        void OnDisable()
        {
            Release();
            pressed = false;
        }

        void Release()
        {
            IsHeld = false;
            transform.localScale = Vector3.one;
        }
    }
}
