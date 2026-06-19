using UnityEngine;

public class ScreenBoundary : MonoBehaviour
{
    public PhysicsMaterial2D WallMaterial;

    private void Start()
    {
        Camera cam = Camera.main;
        Vector2 min = cam.ViewportToWorldPoint(new Vector3(0f, 0f, 0f));
        Vector2 max = cam.ViewportToWorldPoint(new Vector3(1f, 1f, 0f));

        float w = max.x - min.x;
        float h = max.y - min.y;
        float cx = min.x + w * 0.5f;
        float cy = min.y + h * 0.5f;
        const float thickness = 1f;

        CreateWall("Left",   new Vector2(min.x - thickness * 0.5f, cy), new Vector2(thickness, h + thickness * 2f));
        CreateWall("Right",  new Vector2(max.x + thickness * 0.5f, cy), new Vector2(thickness, h + thickness * 2f));
        CreateWall("Bottom", new Vector2(cx, min.y - thickness * 0.5f), new Vector2(w + thickness * 2f, thickness));
        CreateWall("Top",    new Vector2(cx, max.y + thickness * 0.5f), new Vector2(w + thickness * 2f, thickness));
    }

    private void CreateWall(string wallName, Vector2 position, Vector2 size)
    {
        GameObject wall = new GameObject(wallName);
        wall.transform.SetParent(transform);
        wall.transform.position = new Vector3(position.x, position.y, 0f);

        BoxCollider2D col = wall.AddComponent<BoxCollider2D>();
        col.size = size;
        if (WallMaterial != null) col.sharedMaterial = WallMaterial;
    }
}
