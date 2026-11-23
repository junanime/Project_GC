using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface ISpatialHashGridClient
{
    Vector2 Position { get; }
    Vector2 Size { get; }
    Dictionary<int, int> ListIndexByCellIndex { get; set; }
    int QueryID { get; set; }
}

public class SpatialHashGrid
{
    protected List<ISpatialHashGridClient>[] cells;
    protected Vector2[] bounds;
    protected Vector2Int dimensions;
    protected int queryIds;
    private readonly HashSet<ISpatialHashGridClient> rebuildClients = new HashSet<ISpatialHashGridClient>();

    public SpatialHashGrid(Vector2[] bounds, Vector2Int dimensions)
    {
        this.bounds = bounds;
        this.dimensions = dimensions;
        this.queryIds = 0;
        this.cells = new List<ISpatialHashGridClient>[dimensions.x * dimensions.y];
        for (int i = 0; i < cells.Length; i++)
            cells[i] = new List<ISpatialHashGridClient>();
    }

    public void Rebuild(Vector2 position)
    {
        Vector2 size = bounds[1] - bounds[0];
        Rebuild(new Vector2[] { position - size/2, position + size/2 }, dimensions);
    }

    public void Rebuild(Vector2[] bounds, Vector2Int dimensions)
    {
        // Extract all old clients
        rebuildClients.Clear();
        for (int i = 0; i < cells.Length; i++)
        {
            foreach (ISpatialHashGridClient client in cells[i])
            {
                rebuildClients.Add(client);
            }
        }

        this.bounds = bounds;
        this.dimensions = dimensions;
        if (cells.Length != dimensions.x * dimensions.y)
        {
            cells = new List<ISpatialHashGridClient>[dimensions.x * dimensions.y];
            for (int i = 0; i < cells.Length; i++) cells[i] = new List<ISpatialHashGridClient>();
        }
        else
        {
            foreach (var cell in cells) cell.Clear();
        }

        foreach (var client in rebuildClients) InsertClient(client);
        rebuildClients.Clear();
    }

    public List<ISpatialHashGridClient> FindNearby(Vector2 position, Vector2 size)
    {
        Vector2Int i1 = GetCellIndex(position.x - size.x/2.0f, position.y - size.y/2.0f);
        Vector2Int i2 = GetCellIndex(position.x + size.x/2.0f, position.y + size.y/2.0f);

        List<ISpatialHashGridClient> nearbyClients = new List<ISpatialHashGridClient>();
        int queryId = queryIds++;

        for (int x = i1.x, xn = i2.x; x <= xn; x++)
        {
            for (int y = i1.y, yn = i2.y; y <= yn; y++)
            {
                int cellIndex = CellToIndex(x, y);
    
                foreach (ISpatialHashGridClient client in cells[cellIndex])
                {
                    if (client.QueryID != queryId)
                    {
                        client.QueryID = queryId;
                        nearbyClients.Add(client);
                    }
                }
            }
        }

        return nearbyClients;
    }

    public List<ISpatialHashGridClient> FindNearbyInRadius(Vector2 position, float radius)
    {
        // Find indices of points on four sides of the circle
        int xMin = GetCellXIndex(position.x-radius);
        int xMax = GetCellXIndex(position.x+radius);
        int yMin = GetCellYIndex(position.y-radius);
        int yMax = GetCellYIndex(position.y+radius);

        List<ISpatialHashGridClient> nearbyClients = new List<ISpatialHashGridClient>();
        int queryId = queryIds++;

        for (int x = xMin; x <= xMax; x++)
        {
            for (int y = yMin; y <= yMax; y++)
            {
                int cellIndex = CellToIndex(x, y);
    
                foreach (ISpatialHashGridClient client in cells[cellIndex])
                {
                    if (client.QueryID != queryId && Vector2.Distance(client.Position, position) < radius)
                    {
                        client.QueryID = queryId;
                        nearbyClients.Add(client);
                    }
                }
            }
        }

        return nearbyClients;
    }

    public ISpatialHashGridClient FindClosestInRadius(Vector2 position, float radius)
    {
        int xMin = GetCellXIndex(position.x-radius), xMax = GetCellXIndex(position.x+radius);
        int yMin = GetCellYIndex(position.y-radius), yMax = GetCellYIndex(position.y+radius);
        int queryId = queryIds++;
        ISpatialHashGridClient closest = null;
        float best = radius;
        // Match FindNearbyInRadius's x/y/insertion ordering, including equal distances.
        for (int x = xMin; x <= xMax; x++)
        {
            for (int y = yMin; y <= yMax; y++)
            {
                foreach (var client in cells[CellToIndex(x, y)])
                {
                    if (client.QueryID == queryId) continue;
                    float distance = Vector2.Distance(client.Position,position);
                    if (distance >= radius) continue;
                    client.QueryID = queryId;
                    if (distance < best) { best = distance; closest = client; }
                }
            }
        }
        return closest;
    }

    public void InsertClient(ISpatialHashGridClient client)
    {
        ClientRange(client, out var i1, out var i2);
        
        InsertIntoCells(client,i1,i2);
    }

    void InsertIntoCells(ISpatialHashGridClient client, Vector2Int i1, Vector2Int i2)
    {
        if (client.ListIndexByCellIndex == null) client.ListIndexByCellIndex = new Dictionary<int,int>();
        else client.ListIndexByCellIndex.Clear();
        
        for (int x = i1.x, xn = i2.x; x <= xn; x++)
        {
            for (int y = i1.y, yn = i2.y; y <= yn; y++)
            {
                // Add the client to the list of clients in its given grid cell
                int cellIndex = CellToIndex(x, y);
                cells[cellIndex].Add(client);

                // Save cell information to client for future reference
                client.ListIndexByCellIndex[cellIndex] = cells[cellIndex].Count-1;
            }
        }
    }

    public void UpdateClient(ISpatialHashGridClient client)
    {
        ClientRange(client, out var i1, out var i2);

        int cellIndexMin = CellToIndex(i1.x, i1.y);
        int cellIndexMax = CellToIndex(i2.x, i2.y);

        if (client.ListIndexByCellIndex.ContainsKey(cellIndexMin) && client.ListIndexByCellIndex.ContainsKey(cellIndexMax))
            return;

        RemoveClient(client);
        InsertIntoCells(client,i1,i2);
    }

    public void RemoveClient(ISpatialHashGridClient client)
    {
        
        foreach (var indices in client.ListIndexByCellIndex)
        {
            int endIndex = cells[indices.Key].Count - 1;
            // Remove client by replacing it with client from end of list
            ISpatialHashGridClient endClient = cells[indices.Key][endIndex];
            if (endClient != client)
            {
                endClient.ListIndexByCellIndex[indices.Key] = indices.Value;
                cells[indices.Key][indices.Value] = endClient;
            }
            // And removing end of list
            cells[indices.Key].RemoveAt(endIndex);
        }
    }

    public bool CloseToEdge(ISpatialHashGridClient client)
    {
        ClientRange(client, out var i1, out var i2);
        return i1.x == 0 || i1.y == 0 || i2.x == dimensions.x-1 || i2.y == dimensions.y-1;
    }

    void ClientRange(ISpatialHashGridClient client, out Vector2Int min, out Vector2Int max)
    {
        // Monster position and collider bounds are Unity native reads.
        Vector2 position = client.Position, halfSize = client.Size * .5f;
        min = GetCellIndex(position.x-halfSize.x,position.y-halfSize.y);
        max = GetCellIndex(position.x+halfSize.x,position.y+halfSize.y);
    }

    private Vector2Int GetCellIndex(float xPosition, float yPosition)
    {
        float u = Mathf.Clamp01((xPosition - bounds[0][0]) / (bounds[1][0] - bounds[0][0]));
        float v = Mathf.Clamp01((yPosition - bounds[0][1]) / (bounds[1][1] - bounds[0][1]));

        int xIndex = Mathf.FloorToInt(u * (dimensions.x-1));
        int yIndex = Mathf.FloorToInt(v * (dimensions.y-1));

        return new Vector2Int(xIndex, yIndex);
    }

    private int GetCellXIndex(float xPosition)
    {
        float u = Mathf.Clamp01((xPosition - bounds[0][0]) / (bounds[1][0] - bounds[0][0]));
        int xIndex = Mathf.FloorToInt(u * (dimensions.x-1));
        return xIndex;
    }

    private int GetCellYIndex(float yPosition)
    {
        float v = Mathf.Clamp01((yPosition - bounds[0][1]) / (bounds[1][1] - bounds[0][1]));
        int yIndex = Mathf.FloorToInt(v * (dimensions.y-1));
        return yIndex;
    }

    private int CellToIndex(int x, int y)
    {
        return x + y * dimensions.x;
    }
}