using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;


[RequireComponent(typeof(Image))]
public class CursorManager : MonoBehaviour
{
    [Header("References")]
    public PlayerController Player;

    [Header("Sprites")]
    public Sprite NormalSprite;
    public Sprite DragSprite;

    [Header("Pivot")]
    public Vector2 NormalPivot = new Vector2(0.5f, 0.5f);
    public Vector2 DragPivot = new Vector2(0.5f, 0.5f);

    private Image _image;
    private RectTransform _rect;
    private Camera _cam;
    private Vector2 _screenPos;
    private bool _wasDragging;

    private void Awake()
    {
        _image = GetComponent<Image>();
        _rect = GetComponent<RectTransform>();
        _cam = Camera.main;
    }

    private void Start()
    {
        _screenPos = Mouse.current.position.ReadValue();
    }

    private void OnEnable() => Cursor.visible = false;
    private void OnDisable() => Cursor.visible = true;

    private void Update()
    {
        bool isDragging = Player.Current is St_Pl_Dragging;
        Vector2 mouseScreen = Mouse.current.position.ReadValue();

        if (isDragging)
        {
            _screenPos    = _cam.WorldToScreenPoint(Player.transform.position);
            _image.sprite = DragSprite;
            _rect.pivot   = DragPivot;
        }
        else
        {
            if (_wasDragging)
            {
                // Warp the OS mouse to the sprite's current position.
                // Don't read mouse this frame — the warp won't propagate until next frame.
                Mouse.current.WarpCursorPosition(_screenPos);
            }
            else
            {
                _screenPos = mouseScreen;
            }

            _image.sprite = NormalSprite;
            _rect.pivot = NormalPivot;
        }

        _wasDragging = isDragging;

        // Clamp to screen bounds.
        float hw = _rect.rect.width * 0.5f;
        float hh = _rect.rect.height * 0.5f;
        _screenPos.x = Mathf.Clamp(_screenPos.x, hw, Screen.width - hw);
        _screenPos.y = Mathf.Clamp(_screenPos.y, hh, Screen.height - hh);

        _rect.position = _screenPos;
    }
}
