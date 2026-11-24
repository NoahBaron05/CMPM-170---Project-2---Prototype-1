using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class Prototype2Manager : MonoBehaviour
{
    [Header("Centipede")]
    [Min(2)] public int segmentCount = 5;
    [Min(0.1f)] public float headSize = 0.42f;
    [Min(0.1f)] public float segmentSize = 0.34f;
    [Min(0.1f)] public float segmentSpacing = 0.55f;
    public float climbSpeed = 5.5f;
    [Min(0.1f)] public float windowDamageRadius = 0.9f;

    [Header("City Visuals")]
    [Min(1)] public int buildingCount = 8;
    [Min(0.1f)] public float buildingSpacing = 2.45f;
    [Min(0.1f)] public float buildingWidthMin = 1.25f;
    [Min(0.1f)] public float buildingWidthMax = 2.15f;
    [Min(0.1f)] public float buildingHeightMin = 1.5f;
    [Min(0.1f)] public float buildingHeightMax = 5.4f;
    [Min(0.1f)] public float windowColumnSpacing = 0.42f;
    [Min(0.1f)] public float windowRowSpacing = 0.55f;

    private readonly List<Transform> segments = new List<Transform>();
    private readonly List<LineRenderer> joints = new List<LineRenderer>();
    private readonly List<Vector3> path = new List<Vector3>();
    private Camera gameCamera;
    private float groundY = -3.2f;
    private Sprite squareSprite;
    private Rigidbody2D centipedeBody;
    private CircleCollider2D headCollider;
    private readonly RaycastHit2D[] movementHits = new RaycastHit2D[8];

    [Header("Movement")]
    public float movementInterval = 1.5f;
    public float moveDistancePerAction = 0.5f;
    public float climbDistancePerAction = 0.25f;

    [Header("Audio")]
    public AudioClip backgroundMusic;
    public AudioClip centipedeMovingSound;
    public AudioClip windowBreakingSound;
    public AudioClip buildingFallSound;
    [Range(0f, 1f)] public float musicVolume = 0.5f;
    [Range(0f, 1f)] public float effectsVolume = 0.8f;
    [Min(0.1f)] public float windowSoundDuration = 2f;
    [Min(0.1f)] public float buildingSoundDuration = 4f;

    private float movementTimer;
    private AudioSource musicSource;
    private AudioSource movementSource;

    private void Start()
    {
        SetupAudio();
        BuildCamera();
        BuildCityVisuals();
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
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
                direction.x -= 1f;

            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
                direction.x += 1f;

            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)
                direction.y -= 1f;

            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)
                direction.y += 1f;
        }

        direction = Vector2.ClampMagnitude(direction, 1f);

        if (movementSource != null)
        {
            if (direction.sqrMagnitude > 0f && !movementSource.isPlaying)
                movementSource.Play();
            else if (direction.sqrMagnitude == 0f && movementSource.isPlaying)
                movementSource.Stop();
        }

        movementTimer += Time.deltaTime;

        if (direction.sqrMagnitude > 0f && movementTimer >= movementInterval)
        {
            movementTimer = 0f;
            PerformMovementAction(direction);
        }
    }

    private void SetupAudio()
    {
        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.clip = backgroundMusic;
        musicSource.loop = true;
        musicSource.volume = musicVolume;
        musicSource.spatialBlend = 0f;
        if (backgroundMusic != null) musicSource.Play();

        movementSource = gameObject.AddComponent<AudioSource>();
        movementSource.clip = centipedeMovingSound;
        movementSource.loop = true;
        movementSource.volume = effectsVolume;
        movementSource.spatialBlend = 0f;
    }

    private void PlayLimitedSound(AudioClip clip, float duration)
    {
        if (clip == null) return;
        GameObject soundObject = new GameObject("One Shot Audio");
        AudioSource source = soundObject.AddComponent<AudioSource>();
        source.clip = clip;
        source.volume = effectsVolume;
        source.spatialBlend = 0f;
        source.Play();
        Destroy(soundObject, Mathf.Min(duration, clip.length));
    }

    private void PerformMovementAction(Vector2 direction)
    {
        // Move head, resolving each axis independently
        Vector2 current = centipedeBody.position;
        Vector2 next = current;

        // Move horizontally first
        next.x = MoveAxis(
            current,
            Vector2.right,
            direction.x * moveDistancePerAction
        );

        // Temporarily update the physics body so the next collision cast
        // starts from the new horizontal position.
        centipedeBody.position = new Vector2(next.x, current.y);
        Physics2D.SyncTransforms();

        // Move vertically from the updated position
        current = centipedeBody.position;

        next.y = MoveAxis(
            current,
            Vector2.up,
            direction.y * climbDistancePerAction
        );

        // Apply movement boundaries
        next.x = Mathf.Clamp(next.x, -11f, 11f);
        next.y = Mathf.Clamp(next.y, groundY - 0.05f, 5.7f);

        // Finalize movement
        centipedeBody.position = next;
        Physics2D.SyncTransforms();

        centipedeBody.position = next;

        // Update follower path
        path.Insert(0, segments[0].position);

        float requiredPath = segmentSpacing * (segments.Count + 1);

        while (path.Count > 2 &&
               Vector3.Distance(path[path.Count - 1], segments[0].position) > requiredPath)
        {
            path.RemoveAt(path.Count - 1);
        }

        // Update follower segments
        float distance = 0f;
        int pathIndex = 1;

        for (int i = 1; i < segments.Count; i++)
        {
            float targetDistance = segmentSpacing * i;

            while (pathIndex < path.Count && distance < targetDistance)
            {
                distance += Vector3.Distance(
                    path[pathIndex - 1],
                    path[pathIndex]
                );

                pathIndex++;
            }

            if (pathIndex < path.Count)
            {
                segments[i].position = Vector3.MoveTowards(
                    path[pathIndex - 1],
                    path[pathIndex],
                    distance - targetDistance
                );
            }
            else
            {
                segments[i].position = segments[i - 1].position;
            }
        }

        // Update connecting lines
        for (int i = 0; i < joints.Count; i++)
        {
            joints[i].SetPosition(0, segments[i].position);
            joints[i].SetPosition(1, segments[i + 1].position);
        }
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

    private void BuildCentipede()
    {
        Vector3 start = new Vector3(0f, groundY + 6f, 0f);
        for (int i = 0; i < segmentCount; i++)
        {
            GameObject segment = new GameObject(i == 0 ? "Centipede Head" : "Centipede Segment " + i);
            segment.transform.position = start + Vector3.left * (segmentSpacing * i);
            segment.transform.localScale = Vector3.one * (i == 0 ? headSize : segmentSize);
            SpriteRenderer renderer = segment.AddComponent<SpriteRenderer>();
            renderer.sprite = CreateCircleSprite();
            renderer.color = i == 0 ? new Color(0.9f, 0.32f, 0.25f) : new Color(0.32f, 0.82f, 0.43f);
            renderer.sortingOrder = 2;
            if (i > 0)
            {
                CircleCollider2D collider = segment.AddComponent<CircleCollider2D>();
                collider.isTrigger = true;
            }

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

    private void BuildCityVisuals()
    {
        squareSprite = CreateSquareSprite();

        CreateVisualBlock(
            "Street",
            new Vector2(0f, groundY - 0.35f),
            new Vector2(24f, 0.7f),
            new Color(0.12f, 0.15f, 0.2f),
            -2
        );

        for (int i = 0; i < buildingCount; i++)
        {
            float width = Random.Range(buildingWidthMin, buildingWidthMax);
            float height = Random.Range(buildingHeightMin, buildingHeightMax);
            float x = -6.4f + i * buildingSpacing;

            CreateVisualBlock(
                "Building " + (i + 1),
                new Vector2(x, groundY + height * 0.5f),
                new Vector2(width, height),
                new Color(
                    Random.Range(0.18f, 0.34f),
                    Random.Range(0.27f, 0.43f),
                    Random.Range(0.4f, 0.62f)
                ),
                -1
            );

            for (float wx = x - width * 0.28f;
                 wx <= x + width * 0.28f;
                 wx += windowColumnSpacing)
            {
                for (float wy = groundY + 0.38f;
                     wy < groundY + height - 0.3f;
                     wy += windowRowSpacing)
                {
                    CreateVisualBlock(
                        "Window",
                        new Vector2(wx, wy),
                        new Vector2(0.16f, 0.22f),
                        new Color(0.95f, 0.72f, 0.35f),
                        0
                    );
                }
            }
        }
    }

    private void CreateVisualBlock(string objectName, Vector2 position, Vector2 size, Color color, int sortingOrder)
    {
        GameObject block = new GameObject(objectName);
        block.transform.position = position;
        block.transform.localScale = size;

        SpriteRenderer renderer = block.AddComponent<SpriteRenderer>();
        renderer.sprite = squareSprite;
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
        // City art stays decorative: no colliders or damage behavior are added.
    }

    private static Sprite CreateSquareSprite()
    {
        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
    }

    public bool RemoveTailSegment()
    {
        // Keep the head alive because it owns the Rigidbody2D used by movement.
        if (segments.Count <= 1) return false;

        int tailIndex = segments.Count - 1;
        Transform tail = segments[tailIndex];
        segments.RemoveAt(tailIndex);
        Destroy(tail.gameObject);

        int lastJointIndex = joints.Count - 1;
        if (lastJointIndex >= 0)
        {
            Destroy(joints[lastJointIndex].gameObject);
            joints.RemoveAt(lastJointIndex);
        }

        return true;
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
