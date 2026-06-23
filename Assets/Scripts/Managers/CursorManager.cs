using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;


[RequireComponent(typeof(Image))]
public class CursorManager : MonoBehaviour
{
    public static CursorManager Instance { get; private set; }

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

            bool isDragging = _player != null && _player.Current is St_Pl_Dragging;

            if (isDragging)
            {
                _screenPos    = _cam.WorldToScreenPoint(_player.transform.position);
                _image.sprite = DragSprite != null ? DragSprite : NormalSprite;
            }
            else
            {
                if (_wasDragging)
                    Mouse.current.WarpCursorPosition(_screenPos);
                else
                    _screenPos = mouseScreen;

                _image.sprite = NormalSprite;
            }

            _wasDragging = isDragging;
        }

        Cursor.visible = false;

        float hw = _rect.rect.width  * 0.5f;
        float hh = _rect.rect.height * 0.5f;
        _screenPos.x = Mathf.Clamp(_screenPos.x, hw, Screen.width  - hw);
        _screenPos.y = Mathf.Clamp(_screenPos.y, hh, Screen.height - hh);

        _rect.position = new Vector3(_screenPos.x, _screenPos.y, 0);
    }
}
