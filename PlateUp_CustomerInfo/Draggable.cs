using SkripOrderUp;
using UnityEngine;
using UnityEngine.EventSystems;

public class Draggable : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private RectTransform dragRectTransform;
    private Canvas parentCanvas;
    private Vector2 originalLocalPointerPosition;
    private Vector3 originalPanelLocalPosition;

    private void Awake()
    {
        dragRectTransform = GetComponent<RectTransform>();
        parentCanvas = GetComponentInParent<Canvas>();

        if (parentCanvas == null)
        {
            Debug.LogError("Draggable requires a Canvas parent.");
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        originalPanelLocalPosition = dragRectTransform.localPosition;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentCanvas.transform as RectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out originalLocalPointerPosition
        );
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (dragRectTransform == null || parentCanvas == null)
            return;

        Vector2 localPointerPosition;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentCanvas.transform as RectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out localPointerPosition))
        {
            Vector3 offsetToOriginal = localPointerPosition - originalLocalPointerPosition;
            dragRectTransform.localPosition = originalPanelLocalPosition + offsetToOriginal;
            
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (dragRectTransform == null || parentCanvas == null)
            return;

        PreferencesManager.Set<float>("PosX", dragRectTransform.anchoredPosition.x);
        PreferencesManager.Set<float>("PosY", dragRectTransform.anchoredPosition.y);
    }
}
