using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Kamera verisini (MasterCameraSystem) alarak ekran üzerinde sanal bir imleç (Cursor) hareket ettirir.
/// İmlecin altındaki butonları (özellikle HoldButton) dwell-time (üzerinde bekleme) mantığıyla tetikler.
/// </summary>
public class VirtualCursorController : MonoBehaviour
{
    [Header("References")]
    public MasterCameraSystem cameraSystem;
    public RectTransform cursorVisual; // UI Canvas üzerindeki imleç görseli (örn: bir yuvarlak veya el ikonu)

    [Header("Cursor Settings")]
    public float cursorSmoothSpeed = 10f;
    [Tooltip("Eğer aktifse, kamera takibi olmasa bile fare ile test edebilirsiniz.")]
    public bool fallbackToMouse = true;

    private GameObject lastHoveredObject = null;
    private PointerEventData pointerEventData;

    [Header("Tolerance")]
    [Tooltip("Kamera titremelerinde butonun hemen sıfırlanmaması için tolerans süresi (saniye)")]
    public float exitDelay = 0.2f;
    private float exitTimer = 0f;
    private GameObject pendingExitObject = null;

    private void Start()
    {
        pointerEventData = new PointerEventData(EventSystem.current);
        if (cursorVisual != null)
        {
            cursorVisual.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (cameraSystem == null) return;

        Vector3 targetScreenPos = Vector3.zero;
        bool positionFound = false;

        if (cameraSystem.IsCalibrated)
        {
            Vector2 normPos = cameraSystem.GetNormalizedTrackedPosition();
            if (normPos.x >= 0f && normPos.x <= 1f && normPos.y >= 0f && normPos.y <= 1f)
            {
                targetScreenPos = new Vector3(normPos.x * Screen.width, normPos.y * Screen.height, 0f);
                positionFound = true;
            }
        }

        if (!positionFound && fallbackToMouse)
        {
            targetScreenPos = Input.mousePosition;
            positionFound = true;
        }

        if (positionFound)
        {
            if (cursorVisual != null)
            {
                cursorVisual.gameObject.SetActive(true);
                cursorVisual.position = Vector3.Lerp(cursorVisual.position, targetScreenPos, Time.deltaTime * cursorSmoothSpeed);
            }

            Vector2 currentPointerPos = cursorVisual != null ? (Vector2)cursorVisual.position : (Vector2)targetScreenPos;
            CheckUIRaycast(currentPointerPos);
        }
        else
        {
            if (cursorVisual != null)
            {
                cursorVisual.gameObject.SetActive(false);
            }
            RequestPointerExit();
        }

        // Handle delayed exit for jitter tolerance
        if (pendingExitObject != null)
        {
            exitTimer -= Time.deltaTime;
            if (exitTimer <= 0f)
            {
                ExecutePointerExit();
            }
        }
    }

    private void CheckUIRaycast(Vector2 screenPosition)
    {
        pointerEventData.position = screenPosition;
        List<RaycastResult> raycastResults = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerEventData, raycastResults);

        GameObject hoveredObj = null;

        foreach (var result in raycastResults)
        {
            HoldButton holdBtn = result.gameObject.GetComponentInParent<HoldButton>();
            if (holdBtn != null)
            {
                hoveredObj = holdBtn.gameObject;
                break;
            }
        }

        if (hoveredObj != lastHoveredObject)
        {
            // Eğer yeni bir objeye geçtiysek
            if (hoveredObj != null)
            {
                // Önceki çıkışı hemen iptal et veya uygula
                if (pendingExitObject != null)
                {
                    ExecutePointerExit();
                }
                else if (lastHoveredObject != null)
                {
                    RequestPointerExit();
                    ExecutePointerExit(); // Anında çıkış yap çünkü yeni objeye girdik
                }

                lastHoveredObject = hoveredObj;
                HoldButton holdBtn = lastHoveredObject.GetComponent<HoldButton>();
                if (holdBtn != null)
                {
                    holdBtn.OnPointerEnter(pointerEventData);
                }
            }
            else
            {
                // Boşluğa çıktıysak, tolerans süresini başlat
                RequestPointerExit();
            }
        }
        else if (hoveredObj != null)
        {
            // Aynı objede kalmaya devam ediyorsak, çıkış isteğini iptal et
            pendingExitObject = null;
        }
    }

    private void RequestPointerExit()
    {
        if (lastHoveredObject != null)
        {
            pendingExitObject = lastHoveredObject;
            exitTimer = exitDelay;
            lastHoveredObject = null;
        }
    }

    private void ExecutePointerExit()
    {
        if (pendingExitObject != null)
        {
            HoldButton holdBtn = pendingExitObject.GetComponent<HoldButton>();
            if (holdBtn != null)
            {
                holdBtn.OnPointerExit(pointerEventData);
            }
            pendingExitObject = null;
        }
    }

    private void OnDisable()
    {
        RequestPointerExit();
        ExecutePointerExit();
        if (cursorVisual != null)
        {
            cursorVisual.gameObject.SetActive(false);
        }
    }
}
