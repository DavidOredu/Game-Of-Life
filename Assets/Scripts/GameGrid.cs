using Cinemachine;
using UnityEditor.ShaderGraph.Internal;
using UnityEngine;
using UnityEngine.Tilemaps;
using static UnityEngine.Rendering.DebugUI.Table;

public class GameGrid : MonoBehaviour
{
    public Grid grid;

    public CinemachineVirtualCamera virtualCamera;

    public Vector2Int gridSize;

    private TileBase tile;
    public Tilemap tilemap;

    private bool[,] nextGeneration;

    public bool stepSimulation;
    private bool startSimulation;
    private bool canRunSimulation;

    [Header("PERLIN GENERATION")]
    public bool randomConfig;
    public bool useSmoothing;

    public int smoothenCheckSize;

    public float perlinOffsetLimit;
    public float perlinScale;
    public float perlinThreshold;

    private Vector2 perlinOffset;

    // Start is called before the first frame update
    void Awake()
    {
        tilemap.size = new Vector3Int(gridSize.x, gridSize.y, 0);
        Vector3Int position = new Vector3Int();
        nextGeneration = new bool[gridSize.x, gridSize.y];

        tile = Resources.Load<TileBase>("Tiles/Cell");

        for (int x = 0; x < tilemap.size.x; x++)
        {
            for (int y = 0; y < tilemap.size.y; y++)
            {
                position.Set(x, y, 0);

                // Use line when base tile is defined

                tilemap.SetTile(position, null);
            }
        }

        Perlin();
        AlignCamera();
    }
    private void Update()
    {
        if (Input.GetMouseButtonDown(0) && !startSimulation)
        {
            ClickTile();
        }
        if (Input.GetKeyDown(KeyCode.Space))
        {
            startSimulation = true;
        }
        if (Input.GetKeyDown(KeyCode.LeftControl))
        {
            canRunSimulation = true;
        }

        if (startSimulation && canRunSimulation)
        {
            RunAutomation();
        }

        // Make drag camera script
    }
    void AlignCamera()
    {
        virtualCamera.m_Lens.OrthographicSize = 0.5f * Mathf.Max(gridSize.x, gridSize.y);

        var comp = virtualCamera.GetCinemachineComponent<CinemachineFramingTransposer>();
        comp.m_TrackedObjectOffset = new Vector3(0.5f * gridSize.x, 0.5f * gridSize.y);
    }
    void Perlin()
    {
        float width;
        float height;

        float left;
        float top;

        // The perlin map creates smooth differing values for the level bricks for a seeming randomness
        float[,] perlinMap = new float[gridSize.x, gridSize.y];

        // The smoothed map averages the perlin map, the give more of a symmetrical and organised feel
        float[,] smoothedMap = new float[gridSize.x, gridSize.y];

        // The symmetry map creates a symmetrical reflection along the y - axis to increase organisation
        float[,] symmetryMap = new float[gridSize.x, gridSize.y];

        // The binary map thresholds the above map to a binary value to determine where to spawn a brick
        bool[,] binaryMap = new bool[gridSize.x, gridSize.y];

        // determine width and height
        width = (gridSize.x - 1);
        height = gridSize.y;

        // get leftmost and topmost points to begin spawning
        left = transform.position.x - width / 2;
        top = transform.position.y + height / 2;

        // if we need a random level...
        if (randomConfig)
        {
            // ...scroll the perlin map a certain amount, determined by a random value
            perlinOffset.x = Random.Range(0, perlinOffsetLimit);
            perlinOffset.y = Random.Range(0, perlinOffsetLimit);
        }

        // generate the perlin map
        for (int x = 0; x < gridSize.x; x++)
        {
            for (int y = 0; y < gridSize.y; y++)
            {
                perlinMap[x, y] = Mathf.PerlinNoise(perlinOffset.x + (float)x / gridSize.x * perlinScale, perlinOffset.y + (float)y / gridSize.y * perlinScale);
                Debug.Log("Perlin Map: " + $"({x}, {y}): {perlinMap[x, y]}");
            }
        }

        // generate the smoothed map, if required
        if (useSmoothing)
        {
            for (int x = 0; x < gridSize.x; x++)
            {
                for (int y = 0; y < gridSize.y; y++)
                {
                    // the following for loop creates a square that averages every pixel around the current pixel in the perlin map to get an average value of a position relative to the surrounding pixels
                    float sum = 0;
                    for (int i = -smoothenCheckSize; i <= smoothenCheckSize; i++)
                    {
                        for (int j = -smoothenCheckSize; j <= smoothenCheckSize; j++)
                        {
                            //  if ((x == 0 && j < 0) || (y == 0 && i < 0) || (x == rows - 1 && j > 0) || (y == cols - 1 && i > 0)) { continue; }
                            // if the current pixel is within the perlin map bound...
                            if (x + i >= 0 && y + j >= 0 && x + i < gridSize.x && y + j < gridSize.y)
                            {
                                // get the maximum value of pixel coordinates to determine what order the pixel is at, to obtain the weight of the order of the pixel relative to the main pixel
                                sum += perlinMap[x + i, y + j] * (1 - ((float)(Mathf.Max(Mathf.Abs(i), Mathf.Abs(j))) / (smoothenCheckSize + 1)));
                            }
                        }
                    }

                    // divide the sum by the iterations to get the average. Since the check size goes from negative to positive e.g -2 to 2, we multiply by the check size by 2 and add 1 to get the full length, and since it's an array, we square it.
                    smoothedMap[x, y] = sum / (((smoothenCheckSize * 2) + 1) * ((smoothenCheckSize * 2) + 1));
                    Debug.Log("Smoothed Map: " + $"({x}, {y}): {smoothedMap[x, y]}");
                }
            }
        }

        // generate the symmetry map
        for (int x = 0; x < gridSize.x; x++)
        {
            for (int y = 0; y < gridSize.y; y++)
            {
                if (useSmoothing)
                {
                    // if we have reached the half of the column size...
                    if (y >= gridSize.y / 2)
                        // we use the position of the mirrored column position as the current y position
                        symmetryMap[x, y] = smoothedMap[x, gridSize.y - 1 - y];
                    else
                        // Use the regular position as the current y position
                        symmetryMap[x, y] = smoothedMap[x, y];
                }
                else
                {
                    if (y >= gridSize.y / 2)
                        symmetryMap[x, y] = perlinMap[x, gridSize.y - 1 - y];
                    else
                        symmetryMap[x, y] = perlinMap[x, y];
                }
            }
        }

        // generate the binary map
        for (int x = 0; x < gridSize.x; x++)
        {
            for (int y = 0; y < gridSize.y; y++)
            {
                binaryMap[x, y] = symmetryMap[x, y] > perlinThreshold;
            }
        }

        for (int x = 0; x < gridSize.x; x++)
        {
            for (int y = 0; y < gridSize.y; y++)
            {
                if (binaryMap[x,y])
                {
                    tilemap.SetTile(new Vector3Int(x, y), tile);
                }
                else
                {
                    tilemap.SetTile(new Vector3Int(x, y), null);
                }
            }
        }
     }
    public void ClickTile()
    {
        var position = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        var tilePosition = grid.LocalToCell(position);

        // If the input space is within the bounds of the grid, it is a valid tap....
        if ((Mathf.RoundToInt(tilePosition.x) >= 0 && Mathf.RoundToInt(tilePosition.x) < gridSize.x) && (Mathf.RoundToInt(tilePosition.y) >= 0 && Mathf.RoundToInt(tilePosition.y) < gridSize.y))
        {
            if (tilemap.HasTile(tilePosition))
            {
                tilemap.SetTile(tilePosition, null);
            }
            else
            {
                tile = Resources.Load<TileBase>("Tiles/Cell");
                tilemap.SetTile(new Vector3Int(Mathf.RoundToInt(tilePosition.x), Mathf.RoundToInt(tilePosition.y)), tile);
            }
            // perform some click action
            return;
        }
    }
    public bool CheckTileClicked()
    {
        var position = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        var tilePosition = grid.LocalToCellInterpolated(position);

        if ((Mathf.RoundToInt(tilePosition.x) >= 0 && Mathf.RoundToInt(tilePosition.x) < gridSize.x) && (Mathf.RoundToInt(tilePosition.y) >= 0 && Mathf.RoundToInt(tilePosition.y) < gridSize.y))
        {
            // Check if tile is already clicked
            if (true/*tiles[Mathf.RoundToInt(tilePosition.x), Mathf.RoundToInt(tilePosition.y)].image.enabled*/)
            {
                // if is, stop play action...
                return true;
            }
        }
        // if isn't, allow play action...
        return false;
    }
    private void RunAutomation()
    {
        if (stepSimulation)
            canRunSimulation = false;

        for (int x = 0; x < tilemap.size.x; x++)
        {
            for (int y = 0; y < tilemap.size.y; y++)
            {
                nextGeneration[x, y] = CheckPopulation(x, y);
            }
        }

        for (int x = 0; x < tilemap.size.x; x++)
        {
            for (int y = 0; y < tilemap.size.y; y++)
            {
                if (nextGeneration[x, y])
                {
                    tilemap.SetTile(new Vector3Int(x, y), tile);
                }
                else
                {
                    tilemap.SetTile(new Vector3Int(x, y), null);
                }
            }
        }
    }
    private bool CheckPopulation(int m, int n)
    {
        int aliveCount = 0;

        bool isLiveCell = false;
        bool shouldLive = false;

        for (int x = -1; x < 2; x++)
        {
            for (int y = -1; y < 2; y++)
            {
                if (x == 0 && y == 0)
                {
                    isLiveCell = tilemap.HasTile(new Vector3Int(m, n));
                    continue;
                }

                if (tilemap.HasTile(new Vector3Int(m + x, n + y)))
                {
                    aliveCount++;
                }
            }
        }

        if (isLiveCell)
        {
            if (aliveCount > 3)
            {
                // overpopulation
                shouldLive = false;
            }

            if (aliveCount == 2 || aliveCount == 3)
            {
                // live on
                shouldLive = true;
            }

            if (aliveCount < 2)
            {
                // underpopulation
                shouldLive = false;
            }
        }
        else
        {
            if (aliveCount == 3)
            {
                // repopulation
                shouldLive = true;
            }
        }

        return shouldLive;
    }
}
