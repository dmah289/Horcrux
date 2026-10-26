using UnityEngine;
using UnityEngine.EventSystems;

namespace Horcrux.Editor.FeelTrace
{
    /// <summary>Gõ pointer giả lên một GameObject qua EventSystem: đi đúng đường input thật, lặp lại y hệt giữa các lần quay.</summary>
    public static class FeelTraceInput
    {
        public static void Down(GameObject target) => ExecuteEvents.Execute(target, NewPointer(), ExecuteEvents.pointerDownHandler);

        public static void Up(GameObject target) => ExecuteEvents.Execute(target, NewPointer(), ExecuteEvents.pointerUpHandler);

        public static void Click(GameObject target) => ExecuteEvents.Execute(target, NewPointer(), ExecuteEvents.pointerClickHandler);

        /// <summary>Down, Up, Click trong cùng frame.</summary>
        public static void Tap(GameObject target)
        {
            Down(target);
            Up(target);
            Click(target);
        }

        private static PointerEventData NewPointer() => new(EventSystem.current) { button = PointerEventData.InputButton.Left };
    }
}
