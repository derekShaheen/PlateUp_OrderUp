using SkripOrderUp;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class Draggable : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    RectTransform rect;
    Canvas canvas;
    RectTransform parentRect;
    Vector2 pointerOffset;

    void Awake()
    {
        rect = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>(true);
        parentRect = canvas != null ? (RectTransform)canvas.transform : null;

        var g = GetComponent<Graphic>();
        if (g == null)
        {
            var img = gameObject.AddComponent<Image>();
            img.color = new Color(0, 0, 0, 0);
        }

        if (PreferencesManager.HasKey("PosX") && PreferencesManager.HasKey("PosY"))
        {
            rect.anchoredPosition = new Vector2(
                PreferencesManager.Get<float>("PosX", rect.anchoredPosition.x),
                PreferencesManager.Get<float>("PosY", rect.anchoredPosition.y)
            );
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        rect.SetAsLastSibling();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRect, eventData.position, eventData.pressEventCamera, out var localPos))
        {
            pointerOffset = rect.anchoredPosition - localPos;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRect, eventData.position, eventData.pressEventCamera, out var localPos))
        {
            rect.anchoredPosition = localPos + pointerOffset;
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        PreferencesManager.Set<float>("PosX", rect.anchoredPosition.x);
        PreferencesManager.Set<float>("PosY", rect.anchoredPosition.y);
    }
}
