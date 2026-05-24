using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class HoldButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Hold Settings")]
    public float holdTime = 2f;
    public bool canHold = true;

    [Header("UI")]
    public Image fillImage;

    [Header("Action")]
    public UnityEvent onHoldComplete;

    private bool isHolding = false;
    private float timer = 0f;
    private bool triggered = false;

    private void Start()
    {
        if (fillImage != null)
        {
            fillImage.fillAmount = 0f;
        }
    }

    private void Update()
    {
        if (!canHold || !isHolding || triggered)
            return;

        timer += Time.deltaTime;

        if (fillImage != null)
        {
            fillImage.fillAmount = timer / holdTime;
        }

        if (timer >= holdTime)
        {
            triggered = true;
            onHoldComplete.Invoke();
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!canHold)
            return;

        isHolding = true;
        timer = 0f;
        triggered = false;

        if (fillImage != null)
        {
            fillImage.fillAmount = 0f;
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        ResetHold();
    }

    public void ResetHold()
    {
        isHolding = false;
        timer = 0f;
        triggered = false;

        if (fillImage != null)
        {
            fillImage.fillAmount = 0f;
        }
    }

    public void SetCanHold(bool value)
    {
        canHold = value;
        ResetHold();
    }
}