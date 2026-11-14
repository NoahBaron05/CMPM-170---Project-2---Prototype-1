using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Tiny, self-contained first prototype: builds a city and a controllable centipede at runtime.
/// Add this component to an empty GameObject and press Play.
/// </summary>
public class GameManager : MonoBehaviour
{
    [Header("Centipede")]
    [Min(2)] public int segmentCount = 5;
    [Min(0.1f)] public float headSize = 0.42f;
    [Min(0.1f)] public float segmentSize = 0.34f;
    [Min(1f)] public float moveSpeed = 20f;
    [Min(0.1f)] public float segmentSpacing = 0.55f;
    public float climbSpeed = 5.5f;
    [Min(0.1f)] public float windowDamageRadius = 0.9f;

    [Header("City")]
    public int buildingCount = 8;
    [Min(0.1f)] public float buildingSpacing = 2.45f;
    [Min(0.1f)] public float buildingWidthMin = 1.25f;
    [Min(0.1f)] public float buildingWidthMax = 2.15f;
    [Min(0.1f)] public float buildingHeightMin = 1.5f;
    [Min(0.1f)] public float buildingHeightMax = 5.4f;
    [Min(0.1f)] public float windowColumnSpacing = 0.42f;
    [Min(0.1f)] public float windowRowSpacing = 0.55f;

    private readonly List<Transform> segments = new List<Transform>();
    private readonly List<LineRenderer> joints = new List<LineRenderer>();
    private readonly List<BuildingState> buildings = new List<BuildingState>();
    private readonly List<Vector3> path = new List<Vector3>();
    private Camera gameCamera;
    private float groundY = -3.2f;
    private Rigidbody2D centipedeBody;
    private CircleCollider2D headCollider;
    private readonly RaycastHit2D[] movementHits = new RaycastHit2D[8];

    private void Start()
    {
        BuildCamera();
        BuildCity();
        BuildCentipede();
    }

    private void Update()
    {
        if (segments.Count == 0) return;

        Vector2 direction = Vector2.zero;
        if (Gamepad.current != null)
            direction = Gamepad.current.leftStick.ReadValue();
        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) direction.x -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) direction.x += 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) direction.y -= 1f;
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) direction.y += 1f;
        }
        direction = Vector2.ClampMagnitude(direction, 1f);
        if (direction.sqrMagnitude > 0f)
        {
            // Vertical input is deliberately slower: it gives the creature a readable climbing feel.
            Vector2 current = centipedeBody.position;
            Vector2 next = current;
            next.x = MoveAxis(current, Vector2.right, direction.x * moveSpeed * Time.deltaTime);
            current = new Vector2(next.x, current.y);
            next.y = MoveAxis(current, Vector2.up, direction.y * climbSpeed * Time.deltaTime);
            next.x = Mathf.Clamp(next.x, -11f, 11f);
            next.y = Mathf.Clamp(next.y, groundY - 0.05f, 5.7f);
            centipedeBody.MovePosition(next);
        }

        path.Insert(0, segments[0].position);
        float requiredPath = segmentSpacing * (segments.Count + 1);
        while (path.Count > 2 && Vector3.Distance(path[path.Count - 1], segments[0].position) > requiredPath)
            path.RemoveAt(path.Count - 1);

        float distance = 0f;
        int pathIndex = 1;
        for (int i = 1; i < segments.Count; i++)
        {
            float targetDistance = segmentSpacing * i;
            while (pathIndex < path.Count && distance < targetDistance)
            {
                distance += Vector3.Distance(path[pathIndex - 1], path[pathIndex]);
                pathIndex++;
            }
            if (pathIndex < path.Count)
                segments[i].position = Vector3.MoveTowards(path[pathIndex - 1], path[pathIndex], distance - targetDistance);
            else
                segments[i].position = segments[i - 1].position;
        }

        for (int i = 0; i < joints.Count; i++)
        {
            joints[i].SetPosition(0, segments[i].position);
            joints[i].SetPosition(1, segments[i + 1].position);
        }

        DamageWindowsUnderCentipede();
    }

    private float MoveAxis(Vector2 origin, Vector2 axis, float amount)
    {
        if (Mathf.Approximately(amount, 0f)) return axis.x != 0f ? origin.x : origin.y;
        Vector2 direction = axis * Mathf.Sign(amount);
        float distance = Mathf.Abs(amount);
        int hitCount = headCollider.Cast(direction, movementHits, distance + 0.02f);
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = movementHits[i];
            if (hit.collider == null || hit.collider.isTrigger) continue;
            distance = Mathf.Min(distance, Mathf.Max(0f, hit.distance - 0.015f));
        }
        float start = axis.x != 0f ? origin.x : origin.y;
        return start + Mathf.Sign(amount) * distance;
    }

    private void BuildCamera()
    {
        gameCamera = Camera.main;
        if (gameCamera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            gameCamera = cameraObject.AddComponent<Camera>();
        }
        gameCamera.orthographic = true;
        gameCamera.orthographicSize = 5f;
        gameCamera.transform.position = new Vector3(0f, 0f, -10f);
        gameCamera.backgroundColor = new Color(0.08f, 0.13f, 0.22f);
    }

    private void BuildCity()
    {
        MakeBlock("Street", new Vector2(0f, groundY - 0.35f), new Vector2(24f, 0.7f), new Color(0.12f, 0.15f, 0.2f), -2);
        for (int i = 0; i < buildingCount; i++)
        {
            float width = Random.Range(buildingWidthMin, buildingWidthMax);
            float height = Random.Range(buildingHeightMin, buildingHeightMax);
            // Leave a clear street-level spawn lane on the left for the centipede.
            float x = -6.4f + i * buildingSpacing;
            GameObject building = MakeBlock("Building " + (i + 1), new Vector2(x, groundY + height * 0.5f), new Vector2(width, height),
                new Color(Random.Range(0.18f, 0.34f), Random.Range(0.27f, 0.43f), Random.Range(0.4f, 0.62f)), -1);
            BuildingState buildingState = new BuildingState(building);
            buildings.Add(buildingState);

            // Simple lit windows make the skyline legible without adding asset dependencies.
            for (float wx = x - width * 0.28f; wx <= x + width * 0.28f; wx += windowColumnSpacing)
            for (float wy = groundY + 0.38f; wy < groundY + height - 0.3f; wy += windowRowSpacing)
            {
                GameObject window = MakeBlock("Window", new Vector2(wx, wy), new Vector2(0.16f, 0.22f), new Color(0.95f, 0.72f, 0.35f), 0);
                buildingState.AddWindow(window.GetComponent<SpriteRenderer>());
            }
        }
    }

    private void DamageWindowsUnderCentipede()
    {
        // Compare against rendered window bounds directly; this stays reliable as follower transforms move.
        foreach (Transform segment in segments)
        {
            // A slightly generous radius lets the creature damage inset window columns from the building edge.
            float damageRadius = segment == segments[0] ? windowDamageRadius : windowDamageRadius * 0.88f;
            foreach (BuildingState building in buildings)
                building.DamageWindowsNear(segment.position, damageRadius, this);
        }
    }

    public void DamageWindow(SpriteRenderer window)
    {
        if (window == null || window.color == Color.black) return;
        window.color = Color.black;
        foreach (BuildingState building in buildings)
        {
            if (building.Contains(window))
            {
                if (building.AllWindowsBlack()) building.Destroy();
                return;
            }
        }
    }

    private void BuildCentipede()
    {
        Vector3 start = new Vector3(-8.2f, groundY + 0.45f, 0f);
        for (int i = 0; i < segmentCount; i++)
        {
            GameObject segment = new GameObject(i == 0 ? "Centipede Head" : "Centipede Segment " + i);
            segment.transform.position = start + Vector3.left * (segmentSpacing * i);
            segment.transform.localScale = Vector3.one * (i == 0 ? headSize : segmentSize);
            SpriteRenderer renderer = segment.AddComponent<SpriteRenderer>();
            renderer.sprite = CreateCircleSprite();
            renderer.color = i == 0 ? new Color(0.9f, 0.32f, 0.25f) : new Color(0.32f, 0.82f, 0.43f);
            renderer.sortingOrder = 2;
            segments.Add(segment.transform);
            path.Add(segment.transform.position);

            if (i == 0)
            {
                // Move the head through physics so the building colliders can guide it up walls and roofs.
                centipedeBody = segment.AddComponent<Rigidbody2D>();
                centipedeBody.bodyType = RigidbodyType2D.Kinematic;
                centipedeBody.gravityScale = 0f;
                centipedeBody.useFullKinematicContacts = true;
                centipedeBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                centipedeBody.interpolation = RigidbodyInterpolation2D.Interpolate;
                headCollider = segment.AddComponent<CircleCollider2D>();
                headCollider.radius = 0.48f;
            }
        }

        for (int i = 0; i < segments.Count - 1; i++)
        {
            GameObject jointObject = new GameObject("Centipede Link");
            LineRenderer line = jointObject.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.startWidth = 0.16f;
            line.endWidth = 0.16f;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.startColor = new Color(0.2f, 0.58f, 0.3f);
            line.endColor = new Color(0.2f, 0.58f, 0.3f);
            line.sortingOrder = 1;
            joints.Add(line);
        }
    }

    private static GameObject MakeBlock(string name, Vector2 position, Vector2 size, Color color, int sortingOrder)
    {
        GameObject block = new GameObject(name);
        block.transform.position = position;
        block.transform.localScale = size;
        SpriteRenderer renderer = block.AddComponent<SpriteRenderer>();
        renderer.sprite = CreateSquareSprite();
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
        if (name.StartsWith("Building") || name == "Street")
        {
            BoxCollider2D collider = block.AddComponent<BoxCollider2D>();
            collider.size = Vector2.one;
        }
        else if (name == "Window")
        {
            BoxCollider2D collider = block.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
        }
        return block;
    }

    private class BuildingState
    {
        private readonly GameObject building;
        private readonly List<SpriteRenderer> windows = new List<SpriteRenderer>();

        public BuildingState(GameObject buildingObject) { building = buildingObject; }
        public void AddWindow(SpriteRenderer window) { windows.Add(window); }
        public bool Contains(SpriteRenderer window) { return windows.Contains(window); }
        public bool AllWindowsBlack()
        {
            foreach (SpriteRenderer window in windows)
                if (window != null && window.color != Color.black) return false;
            return true;
        }
        public void DamageWindowsNear(Vector2 position, float radius, GameManager manager)
        {
            foreach (SpriteRenderer window in windows)
            {
                if (window == null || window.color == Color.black) continue;
                if (Vector2.Distance(position, window.transform.position) <= radius)
                    manager.DamageWindow(window);
            }
        }
        public void Destroy()
        {
            foreach (SpriteRenderer window in windows)
                if (window != null) UnityEngine.Object.Destroy(window.gameObject);
            if (building != null) UnityEngine.Object.Destroy(building);
        }
    }

    private static Sprite CreateSquareSprite()
    {
        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        // One pixel should represent one world unit so transform scale matches the requested block size.
        return Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
    }

    private static Sprite CreateCircleSprite()
    {
        const int size = 32;
        Texture2D texture = new Texture2D(size, size);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = (x + 0.5f - size * 0.5f) / (size * 0.5f);
            float dy = (y + 0.5f - size * 0.5f) / (size * 0.5f);
            texture.SetPixel(x, y, dx * dx + dy * dy <= 1f ? Color.white : Color.clear);
        }
        texture.filterMode = FilterMode.Bilinear;
        texture.Apply();
        // A 32-pixel circle at 32 PPU has a one-unit diameter before its transform scale is applied.
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}
