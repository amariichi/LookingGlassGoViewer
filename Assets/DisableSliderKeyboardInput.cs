using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// スライダーにキーボードフォーカスを残さないようにし、ナビゲーションも無効化する補助コンポーネント。
/// </summary>
[RequireComponent(typeof(Slider))]
public class DisableSliderKeyboardInput : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IEndDragHandler
{
    private Slider slider;

    private void Awake()
    {
        slider = GetComponent<Slider>();
        DisableNavigation();
    }

    private void OnEnable()
    {
        DisableNavigation();
        ClearSelectionIfNeeded();
    }

    private void DisableNavigation()
    {
        if (slider == null)
        {
            return;
        }

        Navigation nav = slider.navigation;
        nav.mode = Navigation.Mode.None;
        slider.navigation = nav;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        ClearSelectionIfNeeded();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        ClearSelectionIfNeeded();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        ClearSelectionIfNeeded();
    }

    private void ClearSelectionIfNeeded()
    {
        if (EventSystem.current == null)
        {
            return;
        }

        if (EventSystem.current.currentSelectedGameObject == gameObject)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }
}
