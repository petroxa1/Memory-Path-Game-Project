using UnityEngine;
using TMPro;
using UnityEngine.UI; // <-- Add this for UI Images

public class Tile : MonoBehaviour
{
    public int arrayX;
    public int arrayY;

    public int displayX;
    public int displayY;

    public bool isPathTile = false;
    public bool isPlayerProgress = false;

    private Image tileImage; // <-- Change from SpriteRenderer
    private TMP_Text coordText;
    private GridManager gridManager;

    private void Awake()
    {
        tileImage = GetComponent<Image>(); // <-- Get the UI Image component
        coordText = GetComponentInChildren<TMP_Text>();
    }

    public void SetGridManager(GridManager manager)
    {
        gridManager = manager;
    }

    // This stays the same for your main game levels
    public void SetCoordinates(int ax, int ay, int dx, int dy)
    {
        arrayX = ax;
        arrayY = ay;
        displayX = dx;
        displayY = dy;

        gameObject.name = "Tile_" + arrayX + "_" + arrayY; // Keep naming consistent with tracking

        if (coordText != null)
        {
            coordText.text = arrayX + ", " + arrayY;
        }
    }

    public void SetColor(Color color)
    {
        // 1. Try to find the SpriteRenderer on this object
        SpriteRenderer renderer = GetComponent<SpriteRenderer>();

        if (renderer != null)
        {
            renderer.color = color;
        }
        else
        {
            // 2. Fallback: Check if it's a UI Image
            UnityEngine.UI.Image img = GetComponent<UnityEngine.UI.Image>();
            if (img != null) img.color = color;
        }
    }

    [Header("Giriş Modu")]
    public static bool mouseInputEnabled = false; // Inspector'dan veya koddan değiştirilebilir

    private void OnMouseEnter()
    {
        // Fare girişi sadece mouseInputEnabled true ise çalışır
        if (mouseInputEnabled && gridManager != null)
        {
            gridManager.OnTileHovered(this);
        }
    }
}