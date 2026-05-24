using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GridManager : MonoBehaviour
{
    [Header("References")]
    public GameObject tilePrefab;
    public GameplayManager gameplayManager;
    public MasterCameraSystem cameraSystem; // Debounce sıfırlamak için

    [Header("Grid Settings")]
    public float tileSpacing = 1.1f;
    [Tooltip("Karelerin kendi büyüklüğünü (görselini) ayarlamak için kullanılır.")]
    public float tileScale = 0.95f; 
    public int width = 7;
    public int height = 5;

    [Header("Colors")]
    public Color normalColor = Color.gray;
    public Color pathVisibleColor = Color.yellow;
    public Color playerProgressColor = Color.cyan;
    public Color wrongColor = Color.red;

    [Header("Green Band Settings")]
    public Color bandColor = new Color(0.75f, 1f, 0.45f);
    public float bandHeight = 1.6f;

    [Header("Reveal Settings")]
    public float showDuration = 3f;
    public float hideDuration = 10f;

    private Tile[,] tiles;
    private List<Tile> pathTiles = new List<Tile>();

    private int currentPathIndex = 0;
    private bool inputEnabled = false;
    private bool wrongCooldown = false; // Wrong sonrası bekleme

    // Grid boyutlarına dışarıdan erişim
    public int Width => width;
    public int Height => height;
    public Tile[,] Tiles => tiles;

    private int minPathLength;
    private int maxPathLength;

    // --- CAMERA BRIDGE FUNCTION ---
    // This allows the MasterCameraSystem to "step" on tiles remotely
    public void OnTileStepped(int x, int y)
    {
        if (tiles != null && IsInsideGrid(x, y))
        {
            OnTileHovered(tiles[x, y]);
        }
    }

    public void GenerateGrid(int gridWidth, int gridHeight)
    {
        width = gridWidth;
        height = gridHeight;
        ClearGrid();
        tiles = new Tile[width, height];

        float startX = -(width - 1) * tileSpacing * 0.5f;
        float startY = -(height - 1) * tileSpacing * 0.5f;
        int xOffset = width / 2;

        CreateBands(startY);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // Objeyi direkt transform altına (GridManager içine) üretiyoruz
                GameObject tileObj = Instantiate(tilePrefab, transform);
                
                // Artık World Position yerine Local Position kullanıyoruz ki GridManager Scale edilince araları da açılsın!
                tileObj.transform.localPosition = new Vector3(startX + x * tileSpacing, startY + y * tileSpacing, 0f);
                
                // Kullanıcının ayarladığı Scale değerini uyguluyoruz
                tileObj.transform.localScale = new Vector3(tileScale, tileScale, 1f);

                Tile tile = tileObj.GetComponent<Tile>();

                int displayX = x - xOffset;
                int displayY = y;

                tile.SetCoordinates(x, y, displayX, displayY);
                tile.SetColor(normalColor);
                tile.SetGridManager(this);

                tiles[x, y] = tile;
            }
        }
        // StartLevel() artık burada çağrılmıyor.
        // Kalibrasyon tamamlandıktan sonra GameFlowManager tarafından çağrılacak.
    }

    public void StartLevel()
    {
        StopAllCoroutines();
        ResetAllTiles();
        pathTiles.Clear();
        currentPathIndex = 0;

        int chapter = GameData.selectedChapter;
        int level = GameData.selectedLevel;

        Vector2Int range = gameplayManager.GetPathLengthRange(chapter, level);
        minPathLength = range.x;
        maxPathLength = range.y;

        bool pathCreated = false;
        for (int attempt = 0; attempt < 100; attempt++)
        {
            pathTiles.Clear();
            if (GenerateSnakePath()) { pathCreated = true; break; }
        }

        if (pathCreated) StartCoroutine(PathLoopCoroutine());
        else Debug.LogError("Failed to generate snake path.");
    }

    private bool GenerateSnakePath()
    {
        int targetLength = Random.Range(minPathLength, maxPathLength + 1);
        int startX = Random.Range(0, width);

        List<Tile> candidatePath = new List<Tile>();
        HashSet<Tile> visited = new HashSet<Tile>();

        Tile startTile = tiles[startX, 0];
        candidatePath.Add(startTile);
        visited.Add(startTile);

        if (!ExtendPath(startTile, candidatePath, visited, targetLength)) return false;

        pathTiles = new List<Tile>(candidatePath);
        foreach (Tile tile in pathTiles) tile.isPathTile = true;
        return true;
    }

    private bool ExtendPath(Tile currentTile, List<Tile> candidatePath, HashSet<Tile> visited, int targetLength)
    {
        if (candidatePath.Count >= targetLength && currentTile.arrayY == height - 1) return true;

        List<Tile> nextOptions = GetShuffledValidNeighbors(currentTile, visited, candidatePath);
        foreach (Tile nextTile in nextOptions)
        {
            visited.Add(nextTile);
            candidatePath.Add(nextTile);
            if (CanStillReachTop(nextTile, targetLength - candidatePath.Count))
            {
                if (ExtendPath(nextTile, candidatePath, visited, targetLength)) return true;
            }
            candidatePath.RemoveAt(candidatePath.Count - 1);
            visited.Remove(nextTile);
        }
        return false;
    }

    private List<Tile> GetShuffledValidNeighbors(Tile currentTile, HashSet<Tile> visited, List<Tile> candidatePath)
    {
        List<Tile> result = new List<Tile>();
        Vector2Int[] dirs = { new Vector2Int(0, 1), new Vector2Int(0, -1), new Vector2Int(-1, 0), new Vector2Int(1, 0) };
        List<Vector2Int> dirList = new List<Vector2Int>(dirs);
        Shuffle(dirList);

        foreach (Vector2Int dir in dirList)
        {
            int nx = currentTile.arrayX + dir.x;
            int ny = currentTile.arrayY + dir.y;
            if (!IsInsideGrid(nx, ny)) continue;

            Tile nextTile = tiles[nx, ny];
            if (visited.Contains(nextTile)) continue;
            if (!IsReadableMove(nextTile, currentTile, candidatePath)) continue;
            result.Add(nextTile);
        }
        return result;
    }

    private bool IsReadableMove(Tile candidate, Tile currentTile, List<Tile> candidatePath)
    {
        foreach (Tile pathTile in candidatePath)
        {
            if (pathTile == currentTile) continue;
            if (Mathf.Abs(pathTile.arrayX - candidate.arrayX) + Mathf.Abs(pathTile.arrayY - candidate.arrayY) == 1) return false;
        }
        return !Creates2x2Block(candidate, candidatePath);
    }

    private bool Creates2x2Block(Tile candidate, List<Tile> candidatePath)
    {
        for (int ox = -1; ox <= 0; ox++)
        {
            for (int oy = -1; oy <= 0; oy++)
            {
                int bx = candidate.arrayX + ox; int by = candidate.arrayY + oy;
                if (!IsInsideGrid(bx, by) || !IsInsideGrid(bx + 1, by) || !IsInsideGrid(bx, by + 1) || !IsInsideGrid(bx + 1, by + 1)) continue;
                int count = 0;
                if (IsInSet(bx, by, candidate, candidatePath)) count++;
                if (IsInSet(bx + 1, by, candidate, candidatePath)) count++;
                if (IsInSet(bx, by + 1, candidate, candidatePath)) count++;
                if (IsInSet(bx + 1, by + 1, candidate, candidatePath)) count++;
                if (count >= 4) return true;
            }
        }
        return false;
    }

    private bool IsInSet(int x, int y, Tile c, List<Tile> p)
    {
        if (c.arrayX == x && c.arrayY == y) return true;
        foreach (Tile t in p) if (t.arrayX == x && t.arrayY == y) return true;
        return false;
    }

    private bool CanStillReachTop(Tile tile, int remainingSteps) => remainingSteps >= (height - 1) - tile.arrayY;

    private IEnumerator PathLoopCoroutine()
    {
        while (true)
        {
            ShowPath();
            gameplayManager.SetStateText("MEMORIZE");
            gameplayManager.SetTimerRunning(false);
            inputEnabled = false;
            yield return new WaitForSeconds(showDuration);

            HidePathButKeepProgress();
            gameplayManager.SetStateText("MOVE");
            gameplayManager.SetTimerRunning(true);
            inputEnabled = true;
            yield return new WaitForSeconds(hideDuration);
        }
    }

    private void ShowPath()
    {
        foreach (Tile tile in pathTiles)
            tile.SetColor(tile.isPlayerProgress ? playerProgressColor : pathVisibleColor);
    }

    private void HidePathButKeepProgress()
    {
        foreach (Tile tile in pathTiles)
            tile.SetColor(tile.isPlayerProgress ? playerProgressColor : normalColor);
    }

    public void OnTileHovered(Tile hoveredTile)
    {
        if (!inputEnabled || wrongCooldown || currentPathIndex >= pathTiles.Count) return;

        // Zaten doğru basılmış bir tile'a tekrar basıldıysa → sessizce atla
        if (hoveredTile.isPlayerProgress) return;

        if (hoveredTile == pathTiles[currentPathIndex])
        {
            hoveredTile.isPlayerProgress = true;
            hoveredTile.SetColor(playerProgressColor);
            currentPathIndex++;

            // Kamera debounce'ını sıfırla — bir sonraki tile'a geçebilsin
            if (cameraSystem != null)
                cameraSystem.ResetLastConfirmedTile();

            if (currentPathIndex >= pathTiles.Count)
            {
                inputEnabled = false;
                gameplayManager.SetTimerRunning(false);
                gameplayManager.OnLevelComplete();
            }
        }
        else
        {
            StartCoroutine(HandleWrongTile(hoveredTile));
        }
    }

    private IEnumerator HandleWrongTile(Tile wrongTile)
    {
        inputEnabled = false;
        wrongCooldown = true;
        gameplayManager.SetTimerRunning(false);
        wrongTile.SetColor(wrongColor);
        gameplayManager.SetStateText("WRONG");
        yield return new WaitForSeconds(1.0f); // Cooldown süresi artırıldı
        ResetPlayerProgress();
        HidePathButKeepProgress();

        // Kamera debounce'ını sıfırla
        if (cameraSystem != null)
            cameraSystem.ResetLastConfirmedTile();

        gameplayManager.SetStateText("MOVE");
        gameplayManager.SetTimerRunning(true);
        wrongCooldown = false;
        inputEnabled = true;
    }

    private void ResetPlayerProgress()
    {
        currentPathIndex = 0;
        foreach (Tile tile in pathTiles)
        {
            tile.isPlayerProgress = false;
            tile.SetColor(normalColor);
        }
    }

    private void ResetAllTiles()
    {
        if (tiles == null) return;
        foreach (Tile tile in tiles)
        {
            if (tile == null) continue;
            tile.isPathTile = false;
            tile.isPlayerProgress = false;
            tile.SetColor(normalColor);
        }
    }

    private bool IsInsideGrid(int x, int y) => x >= 0 && x < width && y >= 0 && y < height;

    private void Shuffle<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            T temp = list[i];
            int r = Random.Range(i, list.Count);
            list[i] = list[r];
            list[r] = temp;
        }
    }

    private void CreateBands(float startY)
    {
        // Ekranı boydan boya kaplaması için genişliği çok büyük bir değer yapıyoruz
        float boardWidth = 100f; 
        
        CreateBand("BottomBand", 0f, startY - tileSpacing, boardWidth, bandHeight);
        CreateBand("TopBand", 0f, startY + (height - 1) * tileSpacing + tileSpacing, boardWidth, bandHeight);
    }

    private void CreateBand(string bandName, float x, float y, float w, float h)
    {
        GameObject band = GameObject.CreatePrimitive(PrimitiveType.Quad);
        band.name = bandName;
        band.transform.SetParent(transform);
        band.transform.localPosition = new Vector3(x, y, 1f); // Local position'a çevirdik ki GridManager ile beraber hareket etsin
        band.transform.localScale = new Vector3(w, h, 1f);
        Renderer r = band.GetComponent<Renderer>();
        if (r != null)
        {
            r.material = new Material(Shader.Find("Sprites/Default"));
            r.material.color = bandColor;
        }
        Destroy(band.GetComponent<Collider>());
    }

    public void ClearGrid()
    {
        for (int i = transform.childCount - 1; i >= 0; i--) Destroy(transform.GetChild(i).gameObject);
        tiles = null;
    }

    // ===== OTOMATİK KALİBRASYON İÇİN YARDIMCI METOTLAR =====

    /// <summary>
    /// 4 köşe tile'ını döndürür. Sıra: Sol-Alt, Sağ-Alt, Sol-Üst, Sağ-Üst
    /// </summary>
    public Tile[] GetCornerTiles()
    {
        if (tiles == null) return null;
        return new Tile[]
        {
            tiles[0, 0],                     // Sol-Alt (Bottom-Left)
            tiles[width - 1, 0],             // Sağ-Alt (Bottom-Right)
            tiles[0, height - 1],            // Sol-Üst (Top-Left)
            tiles[width - 1, height - 1]     // Sağ-Üst (Top-Right)
        };
    }

    /// <summary>
    /// Ortadaki tile'ı döndürür (doğrulama testi için).
    /// </summary>
    public Tile GetCenterTile()
    {
        if (tiles == null) return null;
        return tiles[width / 2, height / 2];
    }

    /// <summary>
    /// Tüm tile'ları koyu/karanlık bir renge çevirir (kalibrasyon flash için).
    /// </summary>
    public void DarkenAllTiles()
    {
        if (tiles == null) return;
        Color dark = new Color(0.1f, 0.1f, 0.1f);
        foreach (Tile tile in tiles)
        {
            if (tile != null) tile.SetColor(dark);
        }
    }

    /// <summary>
    /// Tüm tile'ları normal rengine döndürür.
    /// </summary>
    public void RestoreAllTiles()
    {
        if (tiles == null) return;
        foreach (Tile tile in tiles)
        {
            if (tile != null) tile.SetColor(normalColor);
        }
    }

    /// <summary>
    /// Alt başlangıç bandını parlak şekilde vurgular (yanıp sönme efekti için).
    /// </summary>
    public void HighlightStartBand(bool highlight)
    {
        Transform band = transform.Find("BottomBand");
        if (band != null)
        {
            Renderer r = band.GetComponent<Renderer>();
            if (r != null)
            {
                r.material.color = highlight ? Color.green : bandColor;
            }
        }
    }
}