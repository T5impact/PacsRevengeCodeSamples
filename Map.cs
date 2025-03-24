using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Map : MonoBehaviour
{
    //Defines a 2D grid map based on 3D positions of walls
    //Used extensively as the foundation for the Enemy AI
    //Capable of procedurally corrupting the map by changing the materials of the wall
    
    public enum GridType
    {
        Air,
        Wall,
        Barrier,
        CorruptedWall
    }

    public GridType[,] map { get; private set; }

    public Vector2Int[] openMapLocations { get; private set; }

    public Dictionary<Vector2Int, MeshRenderer> wallDictionary;

    [SerializeField] Transform player;
    [SerializeField] Transform teleporter1;
    [SerializeField] Transform teleporter2;

    [Header("Map Settings")]
    [SerializeField] float size = 1f;
    [SerializeField] int mapWidth = 50;
    [SerializeField] int mapHeight = 50;
    [SerializeField] Vector3 centerOffset;
    [SerializeField] bool startGridFromOrigin;

    public float Size { get => size; }
    public int MapWidth { get => mapWidth; }
    public int MapHeight { get => mapHeight; }

    [Header("Map Corruption Settings")]
    [SerializeField] bool gradualCorruption;
    [SerializeField] int corruptionRangePerLevel = 2;
    [SerializeField] bool startWithRandCorruption;
    [SerializeField] int startCorruptionCount = 1;

    int corruptedWallCount = 0;

    [Header("Wall Materials")]
    [SerializeField] Material endPieceMat;
    [SerializeField] Material straightPieceMat;
    [SerializeField] Material tPieceMat;
    [SerializeField] Material lPieceMat;
    [SerializeField] Material corruptedEndPieceMat;
    [SerializeField] Material corruptedStraightPieceMat;
    [SerializeField] Material corruptedTPieceMat;
    [SerializeField] Material corruptedLPieceMat;
    [SerializeField] Material transLeftPieceMat;
    [SerializeField] Material transRightPieceMat;

    [Header("Debug Settings")]
    [SerializeField] bool visualizePlayerGridLocation;
    [SerializeField] bool visualizeMap;
    [SerializeField] bool visualizePlayerNextLocation;

    void Awake()
    {
        //Initialize map array
        map = new GridType[mapWidth, mapHeight];
        for (int y = 0; y < mapHeight; y++)
        {
            for (int x = 0; x < mapWidth; x++)
            {
                map[x, y] = GridType.Air;
            }
        }

        //Initialize wall dictionary
        wallDictionary = new Dictionary<Vector2Int, MeshRenderer>();

        //Find the teleporters in the world
        if (teleporter1 == null)
        {
            GameObject obj = GameObject.FindGameObjectWithTag("LeftEdge");
            if(obj) teleporter1 = obj.transform;
        }
        if (teleporter2 == null)
        {
            GameObject obj = GameObject.FindGameObjectWithTag("RightEdge");
            if (obj) teleporter2 = obj.transform;
        }

        corruptedWallCount = 0;

        //Gather all end walls in the scene
        GameObject[] walls = GameObject.FindGameObjectsWithTag("Wall");

        //Use the walls position on the map to mark grid spaces with walls
        foreach (GameObject wall in walls)
        {
            Vector2Int index = GetGridLocation(wall.transform.position);
            map[index.x, index.y] = GridType.Wall;

            MeshRenderer meshRend = wall.GetComponent<MeshRenderer>();
            //print(meshRend.gameObject);
            wallDictionary.Add(index, meshRend);

            if (meshRend.sharedMaterial == corruptedEndPieceMat ||
                meshRend.sharedMaterial == corruptedStraightPieceMat ||
                meshRend.sharedMaterial == corruptedTPieceMat ||
                meshRend.sharedMaterial == corruptedLPieceMat)
            {
                map[index.x, index.y] = GridType.CorruptedWall;
                corruptedWallCount++;
            }
        }
        walls = GameObject.FindGameObjectsWithTag("Straight");
        foreach (GameObject wall in walls)
        {
            Vector2Int index = GetGridLocation(wall.transform.position);
            map[index.x, index.y] = GridType.Wall;

            MeshRenderer meshRend = wall.GetComponent<MeshRenderer>();
            //print(meshRend.gameObject);
            wallDictionary.Add(index, meshRend);

            if (meshRend.sharedMaterial.name == corruptedEndPieceMat.name ||
                meshRend.sharedMaterial.name == corruptedStraightPieceMat.name /*||
                meshRend.material.name == transLeftPieceMat.name + " (Instance)" ||
                meshRend.material.name == transRightPieceMat.name + " (Instance)"*/)
            {
                map[index.x, index.y] = GridType.CorruptedWall;
                corruptedWallCount++;
            }
        }
        walls = GameObject.FindGameObjectsWithTag("Corner");
        foreach (GameObject wall in walls)
        {
            Vector2Int index = GetGridLocation(wall.transform.position);
            map[index.x, index.y] = GridType.Wall;

            MeshRenderer meshRend = wall.GetComponent<MeshRenderer>();
            wallDictionary.Add(index, meshRend);

            if(meshRend.sharedMaterial.name == corruptedLPieceMat.name)
            {
                map[index.x, index.y] = GridType.CorruptedWall;
                corruptedWallCount++;
            }
        }
        walls = GameObject.FindGameObjectsWithTag("T Wall");
        foreach (GameObject wall in walls)
        {
            Vector2Int index = GetGridLocation(wall.transform.position);
            map[index.x, index.y] = GridType.Wall;

            MeshRenderer meshRend = wall.GetComponent<MeshRenderer>();
            wallDictionary.Add(index, meshRend);

            if (meshRend.sharedMaterial.name == corruptedTPieceMat.name)
            {
                map[index.x, index.y] = GridType.CorruptedWall;
                corruptedWallCount++;
            }
        }

        //Gather all barriers in the scene
        GameObject[] barriers = GameObject.FindGameObjectsWithTag("Barrier");

        //Use the barriers position on the map to mark grid spaces with barriers
        foreach (GameObject barrier in barriers)
        {
            Vector2Int index = GetGridLocation(barrier.transform.position);
            map[index.x, index.y] = GridType.Barrier;
        }

        //Create a list of all open locations on the map
        List<Vector2Int> tempList = new List<Vector2Int>();
        for (int y = 0; y < mapHeight; y++)
        {
            for (int x = 0; x < mapWidth; x++)
            {
                if (map[x, y] == GridType.Air)
                {
                    tempList.Add(new Vector2Int(x, y));
                    //map[x, y] = GridType.Air;
                }
            }
        }
        openMapLocations = tempList.ToArray();
    }

    //Procedurally corrupt the map using a recursive function
    //Clean up the map by accounting for transition walls
    #region Map Corruption
    public void CorruptMap(int tilesPerRound, int rounds)
    {
        for (int iterations = 0; iterations < rounds; iterations++)
        {
            List<Vector2Int> corruptWallLocs = new List<Vector2Int>();
            for (int y = 0; y < mapHeight; y++)
            {
                for (int x = 0; x < mapWidth; x++)
                {
                    if (map[x, y] == GridType.CorruptedWall)
                    {
                        corruptWallLocs.Add(new Vector2Int(x, y));
                    }
                }
            }

            for (int i = 0; i < corruptWallLocs.Count; i++)
            {
                CorruptNeighbors(corruptWallLocs[i], tilesPerRound);
            }
        }

        CleanUpMap();
    }
    void CorruptNeighbors(Vector2Int position, int depth)
    {
        if(depth > 0)
        {
            Vector2Int neighbor = position;
            for (int i = 0; i < 4; i++)
            {
                switch(i)
                {
                    case 0: //Check West (Left)
                        neighbor = new Vector2Int(position.x - 1, position.y);
                        break;
                    case 1: //Check East (Right)
                        neighbor = new Vector2Int(position.x + 1, position.y);
                        break;
                    case 2: //Check North (Up)
                        neighbor = new Vector2Int(position.x, position.y + 1);
                        break;
                    case 3: //Check South (Down)
                        neighbor = new Vector2Int(position.x, position.y - 1);
                        break;
                }

                if(neighbor.x >= 0 && neighbor.x < mapWidth && neighbor.y >= 0 && neighbor.y < mapHeight && SampleGrid(neighbor) != GridType.CorruptedWall)
                {
                    if (SampleGrid(neighbor) == GridType.Wall)
                    {
                        CorruptWall(wallDictionary[neighbor]);
                        SetGridAtPosition(neighbor, GridType.CorruptedWall);
                        corruptedWallCount++;
                    }
                    CorruptNeighbors(neighbor, depth - Random.Range(1, 3));
                }
            }
        }
        else
        {
            return;
        }
    }
    void CorruptWall(MeshRenderer wallRenderer)
    {
        if(wallRenderer.gameObject.CompareTag("Wall"))
        {
            wallRenderer.material = corruptedEndPieceMat;
        }
        else if (wallRenderer.gameObject.CompareTag("Straight"))
        {
            wallRenderer.material = corruptedStraightPieceMat;
        }
        else if (wallRenderer.gameObject.CompareTag("Corner"))
        {
            wallRenderer.material = corruptedLPieceMat;
        }
        else if (wallRenderer.gameObject.CompareTag("T Wall"))
        {
            wallRenderer.material = corruptedTPieceMat;
        }
    }
    void CleanUpMap()
    {
        List<Vector2Int> transitionWalls = new List<Vector2Int>();
        List<Vector2Int> transitionLefts = new List<Vector2Int>();
        List<Vector2Int> transitionRights = new List<Vector2Int>();
        for (int y = 0; y < mapHeight; y++)
        {
            for (int x = 0; x < mapWidth; x++)
            {
                Vector2Int position = new Vector2Int(x, y);
                if (SampleGrid(position) == GridType.CorruptedWall)
                {
                    MeshRenderer meshRenderer = wallDictionary[position];
                    Vector2Int leftNeighbor = new Vector2Int(position.x - 1, position.y);
                    Vector2Int rightNeighbor = new Vector2Int(position.x + 1, position.y);
                    Vector2Int upNeighbor = new Vector2Int(position.x, position.y + 1);
                    Vector2Int downNeighbor = new Vector2Int(position.x, position.y - 1);

                    if (meshRenderer.gameObject.CompareTag("Straight"))
                    {
                        if (upNeighbor.y < mapHeight && SampleGrid(upNeighbor) == GridType.Wall)
                        {
                            transitionWalls.Add(position);
                            if(downNeighbor.y >= 0 && SampleGrid(downNeighbor) == GridType.CorruptedWall)
                                meshRenderer.material = transRightPieceMat;
                            else
                                meshRenderer.material = straightPieceMat;
                        }
                        else if (rightNeighbor.x < mapWidth && SampleGrid(rightNeighbor) == GridType.Wall)
                        {
                            transitionWalls.Add(position);
                            if (leftNeighbor.x >= 0 && SampleGrid(leftNeighbor) == GridType.CorruptedWall)
                                meshRenderer.material = transRightPieceMat;
                            else
                                meshRenderer.material = straightPieceMat;
                        }
                        else if (leftNeighbor.x >= 0 && SampleGrid(leftNeighbor) == GridType.Wall)
                        {
                            transitionWalls.Add(position);
                            if (rightNeighbor.x < mapWidth && SampleGrid(rightNeighbor) == GridType.CorruptedWall)
                                meshRenderer.material = transLeftPieceMat;
                            else
                                meshRenderer.material = straightPieceMat;
                        }
                        else if (downNeighbor.y >= 0 && SampleGrid(downNeighbor) == GridType.Wall)
                        {
                            transitionWalls.Add(position);
                            if (upNeighbor.y < mapHeight && SampleGrid(upNeighbor) == GridType.CorruptedWall)
                                meshRenderer.material = transLeftPieceMat;
                            else
                                meshRenderer.material = straightPieceMat;
                        }
                    }
                    else if (meshRenderer.gameObject.CompareTag("Wall") ||
                        meshRenderer.gameObject.CompareTag("Corner") ||
                        meshRenderer.gameObject.CompareTag("T Wall"))
                    {
                        leftNeighbor = new Vector2Int(position.x - 1, position.y);
                        rightNeighbor = new Vector2Int(position.x + 1, position.y);
                        upNeighbor = new Vector2Int(position.x, position.y + 1);
                        downNeighbor = new Vector2Int(position.x, position.y - 1);

                        if(upNeighbor.y < mapHeight && SampleGrid(upNeighbor) == GridType.Wall)
                        {
                            wallDictionary[upNeighbor].material = transRightPieceMat;
                        }
                        if (rightNeighbor.x < mapWidth && SampleGrid(rightNeighbor) == GridType.Wall)
                        {
                            wallDictionary[rightNeighbor].material = transRightPieceMat;
                        }
                        if (leftNeighbor.x >= 0 && SampleGrid(leftNeighbor) == GridType.Wall)
                        {
                            wallDictionary[leftNeighbor].material = transLeftPieceMat;
                        }
                        if (downNeighbor.y >= 0 && SampleGrid(downNeighbor) == GridType.Wall)
                        {
                            wallDictionary[downNeighbor].material = transLeftPieceMat;
                        }
                    }
                }
            }
        }
        for (int i = 0; i < transitionWalls.Count; i++)
        {
            SetGridAtPosition(transitionWalls[i], GridType.Wall);
        }
    }
    #endregion

    /// <summary>
    /// Set the pos on map to provided grid type
    /// </summary>
    public void SetGridAtPosition(Vector3 pos, GridType gridType)
    {
        Vector2Int gridLoc = GetGridLocation(pos);
        if (gridLoc.x < mapWidth && gridLoc.y < mapHeight)
        {
            map[gridLoc.x, gridLoc.y] = gridType;
        }
    }
    /// <summary>
    /// Set the pos on map to provided grid type
    /// </summary>
    public void SetGridAtPosition(Vector2Int pos, GridType gridType)
    {
        if (pos.x < mapWidth && pos.y < mapHeight)
        {
            map[pos.x, pos.y] = gridType;
        }
    }

    /// <summary>
    /// Takes in a map grid location and returns the grid type [air, wall, etc.]
    /// </summary>
    public GridType SampleGrid(Vector2Int pos)
    {
        if (pos.x >= mapWidth || pos.y >= mapHeight)
            return GridType.Wall;
        return map[Mathf.Clamp(pos.x, 0, mapWidth - 1), Mathf.Clamp(pos.y, 0, mapHeight - 1)];
    }
    /// <summary>
    /// Takes in a location on the map and returns the grid type [air, wall, etc.] at that location
    /// </summary>
    public GridType SampleGrid(Vector3 pos)
    {
        Vector2Int gridLoc = GetGridLocation(pos);
        if (gridLoc.x >= mapWidth || gridLoc.y >= mapHeight)
            return GridType.Wall;
        return map[Mathf.Clamp(gridLoc.x, 0, mapWidth - 1), Mathf.Clamp(gridLoc.y, 0, mapHeight - 1)];
    }

    /// <summary>
    /// Takes in a position on the map and returns the integered location on the map grid
    /// </summary>
    public Vector2Int GetGridLocation(Vector3 pos)
    {
        Vector3 offset = Vector3.zero;
        if (!startGridFromOrigin)
            offset = -new Vector3(mapWidth, 0, mapHeight) / 2f * size;

        Vector3 adjustedPos = pos - offset - transform.position - centerOffset + Vector3.one * size / 2;

        return new Vector2Int(Mathf.Clamp(Mathf.FloorToInt(adjustedPos.x / size), 0, mapWidth - 1), Mathf.Clamp(Mathf.FloorToInt(adjustedPos.z / size), 0, mapHeight - 1));
    }

    /// <summary>
    /// Takes in a world-space dir and returns the grid space direction [0,1], [0,-1], [1,0], or [-1,0]
    /// </summary>
    public Vector2Int GetGridSpaceDirection(Vector3 dir)
    {
        Vector2Int integeredDir = Vector2Int.zero;
        dir = dir.normalized;

        float dirAngleToForward = Vector3.Dot(dir, Vector3.forward);
        if (dirAngleToForward >= 0.7f)
        {
            integeredDir = Vector2Int.up;
        }

        float dirAngleToBack = Vector3.Dot(dir, Vector3.back);
        if (dirAngleToBack >= 0.7f)
        {
            integeredDir = Vector2Int.down;
        }

        float dirAngleToLeft = Vector3.Dot(dir, Vector3.left);
        if (dirAngleToLeft >= 0.7f)
        {
            integeredDir = Vector2Int.left;
        }

        float dirAngleToRight = Vector3.Dot(dir, Vector3.right);
        if (dirAngleToRight >= 0.7f)
        {
            integeredDir = Vector2Int.right;
        }

        return integeredDir;
    }

    /// <summary>
    /// Takes in a grid position, integered grid-space direction, and priorities and finds the next grid position along that direction taking into account any walls
    /// </summary>
    public Vector2Int GetNextGridPosition(Vector2Int currentGridPos, Vector2Int dir, bool prioritizeUp, bool prioritizeRight)
    {
        if (map == null)
            return currentGridPos + dir;

        Vector2Int nextGridPos = currentGridPos + dir;
        nextGridPos = new Vector2Int(Mathf.Clamp(nextGridPos.x, 0, mapWidth - 1), Mathf.Clamp(nextGridPos.y, 0, mapHeight - 1));

        if (map[nextGridPos.x, nextGridPos.y] != GridType.Air)
        {
            float dirAngleToUp = Vector2.Dot(dir, Vector2.up);
            float dirAngleToLeft = Vector2.Dot(dir, Vector2.left);

            if(dirAngleToUp <= 0.1f && dirAngleToUp >= -0.1f) //given direction is perpendicular to the up and down dir
            {
                if(prioritizeUp && currentGridPos.y < mapHeight - 1 &&  map[currentGridPos.x, currentGridPos.y + 1] == GridType.Air)
                {
                    nextGridPos = currentGridPos + Vector2Int.up;
                } else
                {
                    nextGridPos = currentGridPos + Vector2Int.down;
                }

            } else if (dirAngleToLeft <= 0.1f && dirAngleToLeft >= -0.1f) //given direction is perpendicular to the left and right dir
            {
                if (prioritizeRight && currentGridPos.x < mapWidth - 1 && map[currentGridPos.x + 1, currentGridPos.y] == GridType.Air)
                {
                    nextGridPos = currentGridPos + Vector2Int.right;
                }
                else
                {
                    nextGridPos = currentGridPos + Vector2Int.left;
                }
            }
        }

        return nextGridPos;
    }

    /// <summary>
    /// Takes in a grid position, integered grid-space direction, and priorities and finds the next grid position along that direction taking into account any walls
    /// </summary>
    public Vector2Int GetNextGridPosition(Vector2Int currentGridPos, Vector2Int dir, bool prioritizeUp, bool prioritizeRight, out Vector2Int usedDir)
    {
        usedDir = dir;
        if (map == null)
            return currentGridPos + dir;

        Vector2Int nextGridPos = currentGridPos + dir;
        nextGridPos = new Vector2Int(Mathf.Clamp(nextGridPos.x, 0, mapWidth - 1), Mathf.Clamp(nextGridPos.y, 0, mapHeight - 1));

        if (map[nextGridPos.x, nextGridPos.y] != GridType.Air)
        {
            float dirAngleToUp = Vector2.Dot(dir, Vector2.up);
            float dirAngleToLeft = Vector2.Dot(dir, Vector2.left);

            if (dirAngleToUp <= 0.1f && dirAngleToUp >= -0.1f) //given direction is perpendicular to the up and down dir
            {
                if (prioritizeUp && currentGridPos.y < mapHeight - 1 && map[currentGridPos.x, currentGridPos.y + 1] == GridType.Air)
                {
                    nextGridPos = currentGridPos + Vector2Int.up;
                    usedDir = Vector2Int.up;
                }
                else if(currentGridPos.y > 0 && map[currentGridPos.x, currentGridPos.y - 1] == GridType.Air)
                {
                    nextGridPos = currentGridPos + Vector2Int.down;
                    usedDir = Vector2Int.down;
                }

            }
            else if (dirAngleToLeft <= 0.1f && dirAngleToLeft >= -0.1f) //given direction is perpendicular to the left and right dir
            {
                if (prioritizeRight && currentGridPos.x < mapWidth - 1 && map[currentGridPos.x + 1, currentGridPos.y] == GridType.Air)
                {
                    nextGridPos = currentGridPos + Vector2Int.right;
                    usedDir = Vector2Int.right;
                }
                else if (currentGridPos.x > 0 && map[currentGridPos.x - 1, currentGridPos.y] == GridType.Air)
                {
                    nextGridPos = currentGridPos + Vector2Int.left;
                    usedDir = Vector2Int.left;
                }
            }
        }

        return nextGridPos;
    }

    /// <summary>
    /// Takes in a grid position, world-space direction, and priorities and finds the next grid position along that direction taking into account any walls
    /// </summary>
    public Vector2Int GetNextGridPosition(Vector2Int currentGridPos, Vector3 dir, bool prioritizeUp, bool prioritizeRight)
    {
        Vector2Int integeredDir = GetGridSpaceDirection(dir);

        return GetNextGridPosition(currentGridPos, integeredDir, prioritizeUp, prioritizeRight);
    }

    /// <summary>
    /// Takes in a grid position, integered grid-space direction, number of spaces ahead, and priorities and returns the grid position in the direction 'spacesAhead' of current grid position
    /// </summary>
    public Vector2Int GetGridPositionAhead(Vector2Int currentGridPos, Vector2Int dir, int spacesAhead, bool prioritizeUp, bool prioritizeRight)
    {
        Vector2Int nextGridPos = currentGridPos;
        for (int i = 0; i < spacesAhead; i++)
        {
            nextGridPos = GetNextGridPosition(nextGridPos, dir, prioritizeUp, prioritizeRight);
        }

        return nextGridPos;
    }

    /// <summary>
    /// Takes in a grid position, world-space direction, number of spaces ahead, and priorities and returns the grid position in the direction 'spacesAhead' of current grid position
    /// </summary>
    public Vector2Int GetGridPositionAhead(Vector2Int currentGridPos, Vector3 dir, int spacesAhead, bool prioritizeUp, bool prioritizeRight)
    {
        Vector2Int integeredDir = GetGridSpaceDirection(dir);

        Vector2Int nextGridPos = currentGridPos;
        Vector2Int currentDir = integeredDir;
        for (int i = 0; i < spacesAhead; i++)
        {
            nextGridPos = GetNextGridPosition(nextGridPos, integeredDir, prioritizeUp, prioritizeRight);
        }

        return nextGridPos;
    }

    /// <summary>
    /// (Experimental) Takes in a grid position, any grid-space direction (not just left, right, up and down), and priorities and returns the grid position in that direction from current grid position
    /// </summary>
    public Vector2Int GetGridPositionAhead(Vector2Int currentGridPos, Vector2Int dir, bool prioritizeUp, bool prioritizeRight)
    {
        if(map == null)
            return currentGridPos + dir;

        //Vector2Int clampedDir = new Vector2Int(Mathf.Clamp(dir.x, -1, 1), Mathf.Clamp(dir.y, -1, 1));
        Vector2Int nextGridPos = currentGridPos + dir;
        nextGridPos = new Vector2Int(Mathf.Clamp(nextGridPos.x, 0, mapWidth - 1), Mathf.Clamp(nextGridPos.y, 0, mapHeight - 1));

        if(map[nextGridPos.x, nextGridPos.y] != GridType.Air)
        {
            if(prioritizeUp && nextGridPos.y < mapHeight - 1 && map[nextGridPos.x, nextGridPos.y + 1] == GridType.Air)
            {
                nextGridPos = nextGridPos + Vector2Int.up;
            } 
            else if (nextGridPos.y > 0 && map[nextGridPos.x, nextGridPos.y - 1] == GridType.Air) 
            {
                nextGridPos = nextGridPos + Vector2Int.down;
            } 
            else if (prioritizeRight && nextGridPos.x < mapWidth - 1 && map[nextGridPos.x + 1, nextGridPos.y] == GridType.Air)
            {
                nextGridPos = nextGridPos + Vector2Int.right;
            }
            else if (nextGridPos.x > 0 && map[nextGridPos.x - 1, nextGridPos.y] == GridType.Air)
            {
                nextGridPos = nextGridPos + Vector2Int.left;
            }
        }

        return nextGridPos;
    }

    /// <summary>
    /// Returns the world position of the given grid position
    /// </summary>
    public Vector3 GetWorldFromGrid(Vector2Int gridPosition)
    {
        Vector3 offset = Vector3.zero;
        if (!startGridFromOrigin)
            offset = -new Vector3(mapWidth, 0, mapHeight) / 2f * size;

        Vector3 worldPos = new Vector3(gridPosition.x, 0, gridPosition.y) * size + offset + transform.position + centerOffset;

        return worldPos;
    }

    /// <summary>
    /// Return the player position intgered on the map grid
    /// </summary>
    public Vector2Int GetPlayerPosition()
    {
        return GetGridLocation(player.position);
    }

    /// <summary>
    /// Takes in the ghost position on the map and returns position to get to the player the quickest
    /// </summary>
    public Vector2Int CheckEdgePositions(Vector3 ghostPosition)
    {
        Vector2Int playerGridPos = GetGridLocation(player.position);
        Vector2Int ghostGridPos = GetGridLocation(ghostPosition);
        Vector2Int teleporter1GridPos = GetGridLocation(teleporter1.position);
        Vector2Int teleporter2GridPos = GetGridLocation(teleporter2.position);

        float distBtwGhostToPlayer = Vector2Int.Distance(playerGridPos, ghostGridPos);

        float distBtwGhostToLeftEdge = Vector2Int.Distance(ghostGridPos, teleporter1GridPos);
        float distBtwPlayerToLeftEdge = Vector2Int.Distance(playerGridPos, teleporter1GridPos);

        float distBtwGhostToRightEdge = Vector2Int.Distance(ghostGridPos, teleporter2GridPos);
        float distBtwPlayerToRightEdge = Vector2Int.Distance(playerGridPos, teleporter2GridPos);

        if(distBtwGhostToRightEdge < distBtwGhostToLeftEdge)
        {
            if(distBtwGhostToPlayer < distBtwGhostToRightEdge)
            {
                return playerGridPos;
            } 
            else if(distBtwGhostToPlayer < distBtwPlayerToLeftEdge)
            {
                return playerGridPos;
            } 
            else
            {
                return teleporter2GridPos;
            }
        } 
        else
        {
            if (distBtwGhostToPlayer < distBtwGhostToLeftEdge)
            {
                return playerGridPos;
            }
            else if (distBtwGhostToPlayer < distBtwPlayerToRightEdge)
            {
                return playerGridPos;
            }
            else
            {
                return teleporter1GridPos;
            }
        }
    }

    /// <summary>
    /// Takes in the ghost position and a target position and returns the grid position to get to the target the quickest
    /// (Used for pinky because target is not player position)
    /// </summary>
    public Vector2Int CheckEdgePositions(Vector3 ghostPosition, Vector2Int targetGridPos)
    {
        Vector2Int ghostGridPos = GetGridLocation(ghostPosition);
        Vector2Int teleporter1GridPos = GetGridLocation(teleporter1.position);
        Vector2Int teleporter2GridPos = GetGridLocation(teleporter2.position);

        float distBtwGhostToTarget = Vector2Int.Distance(targetGridPos, ghostGridPos);

        float distBtwGhostToLeftEdge = Vector2Int.Distance(ghostGridPos, teleporter1GridPos);
        float distBtwTargetToLeftEdge = Vector2Int.Distance(targetGridPos, teleporter1GridPos);

        float distBtwGhostToRightEdge = Vector2Int.Distance(ghostGridPos, teleporter2GridPos);
        float distBtwTargetToRightEdge = Vector2Int.Distance(targetGridPos, teleporter2GridPos);

        if (distBtwGhostToRightEdge < distBtwGhostToLeftEdge)
        {
            if (distBtwGhostToTarget < distBtwGhostToRightEdge)
            {
                return targetGridPos;
            }
            else if (distBtwGhostToTarget < distBtwTargetToLeftEdge)
            {
                return targetGridPos;
            }
            else
            {
                return teleporter2GridPos;
            }
        }
        else
        {
            if (distBtwGhostToTarget < distBtwGhostToLeftEdge)
            {
                return targetGridPos;
            }
            else if (distBtwGhostToTarget < distBtwTargetToRightEdge)
            {
                return targetGridPos;
            }
            else
            {
                return teleporter1GridPos;
            }
        }
    }

    //Debug Function for visualizing grid map, player location, and the player's next position
    private void OnDrawGizmos()
    {
        if(visualizePlayerGridLocation && player)
        {
            Vector3 offset = Vector3.zero;
            if (!startGridFromOrigin)
                offset = -new Vector3(mapWidth, 0, mapHeight) / 2f * size;

            Vector2Int gridLocation = GetGridLocation(player.position);

            Vector3 worldPos = new Vector3(gridLocation.x, 0, gridLocation.y) * size + offset + transform.position + centerOffset;

            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(worldPos, Vector3.one * size / 2);
        }

        if (visualizePlayerNextLocation && player)
        {
            Vector3 offset = Vector3.zero;
            if (!startGridFromOrigin)
                offset = -new Vector3(mapWidth, 0, mapHeight) / 2f * size;

            Vector2Int gridLocation = GetGridLocation(player.position);

            Vector2Int nextGridLocation = GetGridPositionAhead(gridLocation, new Vector2Int(2,2), true, true);

            Vector3 worldPos = new Vector3(nextGridLocation.x, 0, nextGridLocation.y) * size + offset + transform.position + centerOffset;

            Gizmos.color = Color.blue;
            Gizmos.DrawWireCube(worldPos, Vector3.one * size / 2);
        }

        if (visualizeMap && map != null)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                for (int x = 0; x < mapWidth; x++)
                {
                    Vector3 offset = Vector3.zero;
                    if (!startGridFromOrigin)
                        offset = -new Vector3(mapWidth, 0, mapHeight) / 2f * size;

                    Vector3 worldPos = new Vector3(x, 0, y) * size + offset + transform.position + centerOffset;

                    if (SampleGrid(new Vector2Int(x, y)) == GridType.Air)
                    {
                        Gizmos.color = Color.white;
                    }
                    else
                    {
                        if(SampleGrid(new Vector2Int(x, y)) == GridType.CorruptedWall)
                            Gizmos.color = Color.black;
                        else
                            Gizmos.color = Color.red;
                    }

                    Gizmos.DrawWireCube(worldPos, Vector3.one * size);

                }
            }
        }
    }
}
