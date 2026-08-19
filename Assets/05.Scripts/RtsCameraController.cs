using UnityEngine;
using UnityEngine.InputSystem;
using ProjectS.Tilemaps;

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
        [SerializeField] private Transform mapRoot;
        [SerializeField] private Vector2 mapWorldOrigin;
        [SerializeField] private float boundsPadding;
        [SerializeField] private Vector2 minBounds = new Vector2(0f, 0f);
        [SerializeField] private Vector2 maxBounds = new Vector2(120f, 120f);
        [SerializeField] private float mapPlaneZ;

        private float targetOrthographicSize;

        private void Awake()
        {
            ResolveCamera();
            ResolveMapRoot();
            targetOrthographicSize = targetCamera != null ? targetCamera.orthographicSize : maxOrthographicSize;
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

            if (maxBounds.x < minBounds.x)
            {
                maxBounds.x = minBounds.x;
            }

            if (maxBounds.y < minBounds.y)
            {
                maxBounds.y = minBounds.y;
            }
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
            if (!autoResolveMapBounds || mapRoot != null)
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

            if (tilemapWorld != null)
            {
                mapRoot = tilemapWorld.transform;
            }
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

            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
            {
                input.x -= 1f;
            }

            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
            {
                input.x += 1f;
            }

            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
            {
                input.y -= 1f;
            }

            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
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
                if (keyboard.wKey.isPressed || keyboard.aKey.isPressed || keyboard.sKey.isPressed || keyboard.dKey.isPressed ||
                    keyboard.upArrowKey.isPressed || keyboard.leftArrowKey.isPressed || keyboard.downArrowKey.isPressed || keyboard.rightArrowKey.isPressed)
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
            targetCamera.orthographicSize = zoomSmoothing <= 0f
                ? targetOrthographicSize
                : Mathf.Lerp(targetCamera.orthographicSize, targetOrthographicSize, 1f - Mathf.Exp(-zoomSmoothing * Time.deltaTime));
        }

        private void ClampPositionToBounds()
        {
            if (!useMovementBounds)
            {
                return;
            }

            var boundsMin = minBounds;
            var boundsMax = maxBounds;

            if (tilemapWorld != null)
            {
                var bounds = tilemapWorld.CellBounds;
                var worldMin = tilemapWorld.GetCellCenterWorld(new Vector3Int(bounds.xMin, bounds.yMin, 0));
                var worldMax = tilemapWorld.GetCellCenterWorld(new Vector3Int(bounds.xMax - 1, bounds.yMax - 1, 0));
                boundsMin = new Vector2(Mathf.Min(worldMin.x, worldMax.x), Mathf.Min(worldMin.y, worldMax.y)) - Vector2.one * boundsPadding;
                boundsMax = new Vector2(Mathf.Max(worldMin.x, worldMax.x), Mathf.Max(worldMin.y, worldMax.y)) + Vector2.one * boundsPadding;
            }
            else if (boundsPadding > 0f)
            {
                boundsMin -= Vector2.one * boundsPadding;
                boundsMax += Vector2.one * boundsPadding;
            }

            var lookPoint = GetCameraCenterGroundPoint();
            var clampedLookPoint = new Vector3(
                Mathf.Clamp(lookPoint.x, boundsMin.x, boundsMax.x),
                Mathf.Clamp(lookPoint.y, boundsMin.y, boundsMax.y),
                lookPoint.z);
            var correction = clampedLookPoint - lookPoint;

            if (correction.sqrMagnitude > 0f)
            {
                transform.position += new Vector3(correction.x, correction.y, 0f);
            }
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

        private Vector2 GetMapWorldOrigin()
        {
            if (mapRoot != null)
            {
                var position = mapRoot.position;
                return new Vector2(position.x, position.y);
            }

            return mapWorldOrigin;
        }
    }
}
