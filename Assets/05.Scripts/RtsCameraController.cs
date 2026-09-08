using UnityEngine;
using UnityEngine.InputSystem;
using ProjectS.Tilemaps;
using ProjectS.UI;

namespace ProjectS
{
    public sealed class RtsCameraController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera targetCamera;

        [Header("Edge Movement")]
        [SerializeField] private bool enableEdgeMovement = true;
        [SerializeField] private float edgeMoveSpeed = 24f;
        [SerializeField] private float edgeThickness = 1f;

        [Header("Keyboard Movement")]
        [SerializeField] private bool enableKeyboardMovement = true;
        [SerializeField] private float keyboardMoveSpeed = 24f;

        [Header("Mouse Drag Movement")]
        [SerializeField] private bool enableMiddleMouseDrag = true;

        [Header("Rotation")]
        [SerializeField] private bool enableKeyboardRotation;
        [SerializeField] private float keyboardRotationSpeed = 90f;

        [Header("Zoom")]
        [SerializeField] private float minOrthographicSize = 8f;
        [SerializeField] private float maxOrthographicSize = 36f;
        [SerializeField] private float zoomSpeed = 4f;
        [SerializeField] private float zoomSmoothing = 12f;

        [Header("Movement Bounds")]
        [SerializeField] private bool useMovementBounds = true;
        [SerializeField] private bool autoResolveMapBounds = true;
        [SerializeField] private ProjectSTilemapWorld tilemapWorld;
        [SerializeField] private float boundsPadding;
        [SerializeField] private float mapPlaneZ;

        private float targetOrthographicSize;
        private bool isMiddleMouseDragging;
        private Vector3 previousMouseDragGroundPoint;

        public Camera TargetCamera
        {
            get
            {
                ResolveCamera();
                return targetCamera;
            }
        }

        private void Awake()
        {
            ResolveCamera();
            ResolveMapRoot();
            targetOrthographicSize = targetCamera != null ? targetCamera.orthographicSize : maxOrthographicSize;
            if (targetCamera != null)
            {
                targetCamera.orthographicSize = Mathf.Min(targetCamera.orthographicSize, GetMaximumOrthographicSizeForMap());
            }

            ClampPositionToBounds();
        }

        private void OnValidate()
        {
            edgeMoveSpeed = Mathf.Max(0f, edgeMoveSpeed);
            keyboardMoveSpeed = Mathf.Max(0f, keyboardMoveSpeed);
            keyboardRotationSpeed = Mathf.Max(0f, keyboardRotationSpeed);
            edgeThickness = Mathf.Max(1f, edgeThickness);
            minOrthographicSize = Mathf.Max(0.1f, minOrthographicSize);
            maxOrthographicSize = Mathf.Max(minOrthographicSize, maxOrthographicSize);
            zoomSpeed = Mathf.Max(0f, zoomSpeed);
            zoomSmoothing = Mathf.Max(0f, zoomSmoothing);
            boundsPadding = Mathf.Max(0f, boundsPadding);
        }

        private void Update()
        {
            ResolveCamera();
            ResolveMapRoot();

            if (targetCamera == null)
            {
                return;
            }

            RotateCamera();
            MoveCamera();
            UpdateMiddleMouseDrag();
            UpdateZoomTarget();
            ApplyZoom();
            ClampPositionToBounds();
        }

        private void ResolveCamera()
        {
            if (targetCamera != null)
            {
                return;
            }

            targetCamera = GetComponentInChildren<Camera>();
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }
        }

        public bool TryMoveToWorldPoint(Vector3 worldPoint)
        {
            ResolveCamera();
            if (targetCamera == null)
            {
                return false;
            }

            var currentLookPoint = GetCameraCenterGroundPoint();
            var correction = worldPoint - currentLookPoint;
            transform.position += new Vector3(correction.x, correction.y, 0f);
            ClampPositionToBounds();
            return true;
        }

        private void MoveCamera()
        {
            var moveInput = Vector2.zero;
            moveInput += GetEdgeMoveInput();
            moveInput += GetKeyboardMoveInput();

            if (moveInput.sqrMagnitude <= 0f)
            {
                return;
            }

            moveInput = Vector2.ClampMagnitude(moveInput, 1f);

            var moveDirection = new Vector3(moveInput.x, moveInput.y, 0f);
            transform.position += moveDirection * GetMoveSpeed() * Time.deltaTime;
        }

        private void UpdateMiddleMouseDrag()
        {
            if (!enableMiddleMouseDrag || Mouse.current == null)
            {
                isMiddleMouseDragging = false;
                return;
            }

            var mouse = Mouse.current;
            var screenPosition = mouse.position.ReadValue();
            if (mouse.middleButton.wasPressedThisFrame)
            {
                isMiddleMouseDragging = !RtsMinimap.IsScreenPointOverMinimap(screenPosition)
                    && TryGetGroundPointFromScreenPosition(screenPosition, out previousMouseDragGroundPoint);
            }

            if (!isMiddleMouseDragging || !mouse.middleButton.isPressed)
            {
                if (mouse.middleButton.wasReleasedThisFrame)
                {
                    isMiddleMouseDragging = false;
                }

                return;
            }

            if (!TryGetGroundPointFromScreenPosition(screenPosition, out var currentGroundPoint))
            {
                return;
            }

            var correction = previousMouseDragGroundPoint - currentGroundPoint;
            transform.position += new Vector3(correction.x, correction.y, 0f);
            ClampPositionToBounds();
            TryGetGroundPointFromScreenPosition(screenPosition, out previousMouseDragGroundPoint);
        }

        private void RotateCamera()
        {
            var rotationInput = GetKeyboardRotationInput();
            if (Mathf.Approximately(rotationInput, 0f))
            {
                return;
            }

            var lookPointBeforeRotation = GetCameraCenterGroundPoint();
            transform.Rotate(Vector3.forward, rotationInput * keyboardRotationSpeed * Time.deltaTime, Space.World);
            var lookPointAfterRotation = GetCameraCenterGroundPoint();
            var correction = lookPointBeforeRotation - lookPointAfterRotation;
            transform.position += new Vector3(correction.x, correction.y, 0f);
        }

        private void ResolveMapRoot()
        {
            if (!autoResolveMapBounds)
            {
                return;
            }

            if (tilemapWorld == null)
            {
                tilemapWorld = ProjectSTilemapWorld.ActiveInstance;
            }

            if (tilemapWorld == null)
            {
                tilemapWorld = FindFirstObjectByType<ProjectSTilemapWorld>();
            }

            tilemapWorld?.ResolveReferences();
        }

        private Vector2 GetEdgeMoveInput()
        {
            if (!enableEdgeMovement || Mouse.current == null)
            {
                return Vector2.zero;
            }

            var mousePosition = Mouse.current.position.ReadValue();
            var input = Vector2.zero;

            if (mousePosition.x <= edgeThickness)
            {
                input.x -= 1f;
            }
            else if (mousePosition.x >= Screen.width - edgeThickness)
            {
                input.x += 1f;
            }

            if (mousePosition.y <= edgeThickness)
            {
                input.y -= 1f;
            }
            else if (mousePosition.y >= Screen.height - edgeThickness)
            {
                input.y += 1f;
            }

            return input;
        }

        private Vector2 GetKeyboardMoveInput()
        {
            if (!enableKeyboardMovement || Keyboard.current == null)
            {
                return Vector2.zero;
            }

            var keyboard = Keyboard.current;
            var input = Vector2.zero;

            if (keyboard.leftArrowKey.isPressed)
            {
                input.x -= 1f;
            }

            if (keyboard.rightArrowKey.isPressed)
            {
                input.x += 1f;
            }

            if (keyboard.downArrowKey.isPressed)
            {
                input.y -= 1f;
            }

            if (keyboard.upArrowKey.isPressed)
            {
                input.y += 1f;
            }

            return input;
        }

        private float GetKeyboardRotationInput()
        {
            if (!enableKeyboardRotation || Keyboard.current == null)
            {
                return 0f;
            }

            var keyboard = Keyboard.current;
            var input = 0f;

            if (keyboard.qKey.isPressed)
            {
                input -= 1f;
            }

            if (keyboard.eKey.isPressed)
            {
                input += 1f;
            }

            return input;
        }

        private float GetMoveSpeed()
        {
            if (enableKeyboardMovement && Keyboard.current != null)
            {
                var keyboard = Keyboard.current;
                if (keyboard.upArrowKey.isPressed || keyboard.leftArrowKey.isPressed || keyboard.downArrowKey.isPressed || keyboard.rightArrowKey.isPressed)
                {
                    return keyboardMoveSpeed;
                }
            }

            return edgeMoveSpeed;
        }

        private void UpdateZoomTarget()
        {
            if (Mouse.current == null)
            {
                return;
            }

            var scrollY = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Approximately(scrollY, 0f))
            {
                return;
            }

            targetOrthographicSize = Mathf.Clamp(
                targetOrthographicSize - (scrollY * zoomSpeed * 0.01f),
                minOrthographicSize,
                maxOrthographicSize);
        }

        private void ApplyZoom()
        {
            var requestedSize = zoomSmoothing <= 0f
                ? targetOrthographicSize
                : Mathf.Lerp(targetCamera.orthographicSize, targetOrthographicSize, 1f - Mathf.Exp(-zoomSmoothing * Time.deltaTime));

            targetCamera.orthographicSize = Mathf.Min(requestedSize, GetMaximumOrthographicSizeForMap());
        }

        private void ClampPositionToBounds()
        {
            if (!useMovementBounds)
            {
                return;
            }

            if (!TryGetTilemapWorldBounds(out var boundsMin, out var boundsMax))
            {
                return;
            }

            ApplyBoundsPadding(ref boundsMin, ref boundsMax);

            var lookPoint = GetCameraCenterGroundPoint();
            if (!TryGetViewportGroundOffsets(lookPoint, out var viewportMin, out var viewportMax))
            {
                return;
            }

            var clampedLookPoint = new Vector3(
                ClampToViewportBounds(lookPoint.x, viewportMin.x, viewportMax.x, boundsMin.x, boundsMax.x),
                ClampToViewportBounds(lookPoint.y, viewportMin.y, viewportMax.y, boundsMin.y, boundsMax.y),
                lookPoint.z);
            var correction = clampedLookPoint - lookPoint;

            if (correction.sqrMagnitude > 0f)
            {
                transform.position += new Vector3(correction.x, correction.y, 0f);
            }
        }

        private float GetMaximumOrthographicSizeForMap()
        {
            if (!useMovementBounds || targetCamera == null || !targetCamera.orthographic ||
                !TryGetTilemapWorldBounds(out var boundsMin, out var boundsMax))
            {
                return maxOrthographicSize;
            }

            ApplyBoundsPadding(ref boundsMin, ref boundsMax);

            var lookPoint = GetCameraCenterGroundPoint();
            if (!TryGetViewportGroundOffsets(lookPoint, out var viewportMin, out var viewportMax))
            {
                return maxOrthographicSize;
            }

            var viewportSize = viewportMax - viewportMin;
            var currentSize = Mathf.Max(targetCamera.orthographicSize, 0.01f);
            var maxSize = maxOrthographicSize;

            if (viewportSize.x > 0.001f)
            {
                maxSize = Mathf.Min(maxSize, currentSize * ((boundsMax.x - boundsMin.x) / viewportSize.x));
            }

            if (viewportSize.y > 0.001f)
            {
                maxSize = Mathf.Min(maxSize, currentSize * ((boundsMax.y - boundsMin.y) / viewportSize.y));
            }

            return Mathf.Max(0.01f, maxSize);
        }

        private void ApplyBoundsPadding(ref Vector2 boundsMin, ref Vector2 boundsMax)
        {
            var maximumPadding = Mathf.Min((boundsMax.x - boundsMin.x) * 0.5f, (boundsMax.y - boundsMin.y) * 0.5f);
            var padding = Mathf.Min(boundsPadding, Mathf.Max(0f, maximumPadding));
            boundsMin += Vector2.one * padding;
            boundsMax -= Vector2.one * padding;
        }

        private bool TryGetViewportGroundOffsets(Vector3 lookPoint, out Vector2 viewportMin, out Vector2 viewportMax)
        {
            viewportMin = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            viewportMax = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            var groundPlane = new Plane(Vector3.forward, new Vector3(0f, 0f, mapPlaneZ));

            foreach (var viewportCorner in ViewportCorners)
            {
                var ray = targetCamera.ViewportPointToRay(viewportCorner);
                if (!groundPlane.Raycast(ray, out var enter))
                {
                    return false;
                }

                var groundPoint = ray.GetPoint(enter);
                var offset = new Vector2(groundPoint.x - lookPoint.x, groundPoint.y - lookPoint.y);
                viewportMin = Vector2.Min(viewportMin, offset);
                viewportMax = Vector2.Max(viewportMax, offset);
            }

            return true;
        }

        private static float ClampToViewportBounds(float lookCoordinate, float viewportMin, float viewportMax, float boundsMin, float boundsMax)
        {
            var minimumLookCoordinate = boundsMin - viewportMin;
            var maximumLookCoordinate = boundsMax - viewportMax;
            if (minimumLookCoordinate > maximumLookCoordinate)
            {
                return (minimumLookCoordinate + maximumLookCoordinate) * 0.5f;
            }

            return Mathf.Clamp(lookCoordinate, minimumLookCoordinate, maximumLookCoordinate);
        }

        private Vector3 GetCameraCenterGroundPoint()
        {
            if (targetCamera == null)
            {
                return transform.position;
            }

            var ray = targetCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            var groundPlane = new Plane(Vector3.forward, new Vector3(0f, 0f, mapPlaneZ));

            return groundPlane.Raycast(ray, out var enter)
                ? ray.GetPoint(enter)
                : transform.position;
        }

        private bool TryGetGroundPointFromScreenPosition(Vector2 screenPosition, out Vector3 groundPoint)
        {
            groundPoint = default;
            if (targetCamera == null)
            {
                return false;
            }

            var ray = targetCamera.ScreenPointToRay(screenPosition);
            var groundPlane = new Plane(Vector3.forward, new Vector3(0f, 0f, mapPlaneZ));
            if (!groundPlane.Raycast(ray, out var enter))
            {
                return false;
            }

            groundPoint = ray.GetPoint(enter);
            return true;
        }

        private static readonly Vector3[] ViewportCorners =
        {
            new(0f, 0f, 0f),
            new(0f, 1f, 0f),
            new(1f, 0f, 0f),
            new(1f, 1f, 0f)
        };

        private bool TryGetTilemapWorldBounds(out Vector2 boundsMin, out Vector2 boundsMax)
        {
            boundsMin = default;
            boundsMax = default;

            if (tilemapWorld == null)
            {
                return false;
            }

            var cellBounds = tilemapWorld.CellBounds;
            var worldMin = CellToWorldCorner(new Vector3Int(cellBounds.xMin, cellBounds.yMin, 0));
            var worldMax = CellToWorldCorner(new Vector3Int(cellBounds.xMax, cellBounds.yMax, 0));
            boundsMin = new Vector2(Mathf.Min(worldMin.x, worldMax.x), Mathf.Min(worldMin.y, worldMax.y));
            boundsMax = new Vector2(Mathf.Max(worldMin.x, worldMax.x), Mathf.Max(worldMin.y, worldMax.y));

            return boundsMax.x > boundsMin.x && boundsMax.y > boundsMin.y;
        }

        private Vector3 CellToWorldCorner(Vector3Int cell)
        {
            if (tilemapWorld.GroundTilemap != null)
            {
                return tilemapWorld.GroundTilemap.CellToWorld(cell);
            }

            return tilemapWorld.Grid != null ? tilemapWorld.Grid.CellToWorld(cell) : cell;
        }

    }
}
