using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ZoomToMouse : MonoBehaviour, IScrollHandler
{
    [SerializeField] RectTransform content;
    [SerializeField] RectTransform viewport;
    [SerializeField] float zoomSpeed = 0.1f;
    [SerializeField] float minZoom = 0.5f;
    [SerializeField] float maxZoom = 2f;

    public void OnScroll(PointerEventData eventData)
    {
        float scrollDelta = eventData.scrollDelta.y;
        if (Mathf.Abs(scrollDelta) < 0.01f) return;

        float currentZoom = content.localScale.x;
        float targetZoom = Mathf.Clamp(currentZoom + scrollDelta * zoomSpeed, minZoom, maxZoom);

        if (Mathf.Approximately(currentZoom, targetZoom)) return;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, eventData.position, null, out Vector2 localCursor))
            return;

        float scaleFactor = targetZoom / currentZoom;

        Vector2 pivotOffset = localCursor - content.anchoredPosition;
        Vector2 newPos = content.anchoredPosition - pivotOffset * (scaleFactor - 1f);

        content.localScale = Vector3.one * targetZoom;
        content.anchoredPosition = newPos;

    }
}
