using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;


[RequireComponent(typeof(Image))]
public class CursorManager : MonoBehaviour
{
    public static CursorManager Instance { get; private set; }

    /// <summary>True while the real OS cursor is inside the game window. Gameplay ignores mouse
    /// input when this is false, so clicks made off-screen don't reach the game.</summary>
    public static bool PointerInWindow { get; private set; } = true;

    [Header("Sprites")]
    public Sprite NormalSprite;
    public Sprite DragSprite;

    private Image            _image;
    private RectTransform    _rect;
    private Camera           _cam;
    private Vector2          _screenPos;
    private bool             _wasDragging;
    private PlayerController _player;

    private bool _initialized;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(transform.root.gameObject); return; }
        Instance = this;
        _initialized = true;
        DontDestroyOnLoad(transform.root.gameObject);

        _image = GetComponent<Image>();
        _image.raycastTarget = false;
        _rect  = GetComponent<RectTransform>();

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null) canvas.sortingOrder = 32767;

        // Always mirror the cursor sprite on X.
        Vector3 s = _rect.localScale;
        s.x = -Mathf.Abs(s.x);
        _rect.localScale = s;
    }

    private void OnEnable()
    {
        if (!_initialized) return;
        Cursor.visible = false;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        if (!_initialized) return;
        Cursor.visible = true;
        PointerInWindow = true; // don't leave gameplay input blocked if we're disabled
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        _screenPos = Mouse.current.position.ReadValue();
        RefreshReferences();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => RefreshReferences();

    private void RefreshReferences()
    {
        _cam    = Camera.main;
        _player = Object.FindAnyObjectByType<PlayerController>();
    }

    private void Update()
    {
        if (!_initialized) return;
        if (_cam == null) _cam = Camera.main;

        Vector2 mouseScreen = Mouse.current.position.ReadValue();
        bool    isClicking  = Mouse.current.leftButton.isPressed;

        // The real cursor is free to leave the window; track whether it's inside so gameplay can
        // ignore clicks made off-screen.
        PointerInWindow = mouseScreen.x >= 0f && mouseScreen.x <= Screen.width
                       && mouseScreen.y >= 0f && mouseScreen.y <= Screen.height;

        var  state      = GameManager.Instance?.CurrentSceneState;
        bool isUIScene  = state == GameManager.SceneState.MainMenu
                       || state == GameManager.SceneState.Cutscene;

        if (isUIScene)
        {
            _screenPos    = mouseScreen;
            _wasDragging  = false;
            _image.sprite = isClicking ? DragSprite ?? NormalSprite : NormalSprite;
        }
        else
        {
            if (_player == null) _player = Object.FindAnyObjectByType<PlayerController>();
            if (_cam    == null) _cam    = Camera.main;

            bool isDragging = _player != null && _cam != null && _player.Current is St_Pl_Dragging;

            if (isDragging)
            {
                // Follow the goblin where it's DRAWN (the Sprite child), not the root — during a drag the
                // root stays at the origin while only the sprite is pulled back, so transform.position
                // would leave the cursor stuck at the origin. Position is the sprite's world position.
                _screenPos    = _cam.WorldToScreenPoint(_player.Position);
                _image.sprite = DragSprite != null ? DragSprite : NormalSprite;
            }
            else
            {
                // Snap the OS cursor to where the drag left it. WebGL can't warp the cursor, so skip
                // it there (the browser owns the cursor anyway) to avoid the unsupported call.
                if (_wasDragging)
                {
                    if (Application.platform != RuntimePlatform.WebGLPlayer)
                        Mouse.current.WarpCursorPosition(_screenPos);
                }
                else
                    _screenPos = mouseScreen;

                _image.sprite = NormalSprite;
            }

            _wasDragging = isDragging;
        }

        Cursor.visible = false;

        // Keep the cursor sprite on-screen and always visible. The clamp is in SCREEN PIXELS — the
        // rect's local size scaled by lossyScale — so it's correct under any CanvasScaler / build
        // resolution. (The old clamp mixed canvas-unit rect sizes with pixel Screen dimensions, so
        // it inverted off the reference resolution and made the cursor vanish at the top/bottom.)
        // The real OS cursor still roams freely off-screen; PointerInWindow gates input, not this.
        _image.enabled = true;

        float hw = _rect.rect.width  * _rect.lossyScale.x * 0.5f;
        float hh = _rect.rect.height * _rect.lossyScale.y * 0.5f;
        _screenPos.x = Mathf.Clamp(_screenPos.x, hw, Screen.width  - hw);
        _screenPos.y = Mathf.Clamp(_screenPos.y, hh, Screen.height - hh);

        _rect.position = new Vector3(_screenPos.x, _screenPos.y, 0);
    }
}
