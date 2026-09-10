using System.IO;
using JetBrains.Annotations;
using render;
using render.screens;
using render.ui;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using settings;
using world.items;
using world.persistence;
using World.blocks;

namespace player
{
    public class Player : MonoBehaviour
    {
        public Transform cameraTransform;
        public World.World world;
        public GameObject targetOutline;
        public Camera camera;
        public CharacterController characterController;
        public Texture2DArray atlas;

        private InputAction _moveAction;
        private InputAction _lookAction;
        private InputAction _attackAction;
        private InputAction _interactAction;
        private InputAction _jumpAction;
        private InputAction _sneakAction;
        private InputAction _sprintAction;
        private InputAction _inventoryAction;
        private InputAction _sprintPendingAction;
        private InputAction _pauseAction;
        
        private int _blockLayer;
        
        private const float MoveSpeed = 4.317f;
        private const float FlyingSpeed = 5f;
        private const float Gravity = -20f;
        private const float WaterGravity = -2f;
        private const float WaterTerminalFallSpeed = -3f;
        private const float WaterSwimForce = 3.5f;
        private const float WaterMovementMultiplier = 0.4f;
        private const float WaterCurrentSpeed = 1.5f;
        private const float JumpForce = 5f;
        private const float DoubleClickDelay = 0.3f;
        private const int MinimumInteractionDelay = 10;
        private const float MaxDistance = 5f;
        
        private bool _sprinting;
        private bool _jumping;
        private bool _flying;
        private bool _gameplayReady;
        
        private float _sprintLastClickTime;
        private float _flyLastClickTime;
        private float _verticalMomentum;
        
        private int _tick;
        private int _lastInteractionTick;
        
        private Vector3 _velocity;
        private RawImage _underwaterOverlay;
        private Material _underwaterOverlayMaterial;
        private int _underwaterOverlayFrame = -1;

        private const float UnderwaterOverlayAlpha = 0.60f;
        // Atlas.json reserves slots 32-63 for water; this overlay plays the first 16 frames.
        private const int WaterAtlasFirstFrame = 32;
        private const int UnderwaterOverlayFrameCount = 16;
        private const float UnderwaterOverlayFramesPerSecond = 4f;
        private const float UnderwaterSurfaceHysteresis = 0.06f;

        private Vector3Int TargetLocation { get; set; }
        private int TargetFace { get; set; }
        public Vector3 ImpactPoint { get; private set; }
        private bool HasTargetLocation { get; set; }
        public static bool Paused { get; private set; }
        [CanBeNull] public static BaseScreen CurrentScreen { get; set; }

        public Hotbar hotbar;
        public readonly ItemStack[] inventory = new ItemStack[36];
        public static Player Instance;
        private static readonly int Frame = Shader.PropertyToID("_Frame");
        private static readonly int WaterAtlas = Shader.PropertyToID("_WaterAtlas");

        private void Awake()
        {
            GameSettings.EnsureLoaded();
            Instance = this;
            Paused = false;
            CurrentScreen = null;
            Time.timeScale = 1f;
            InitializeEmptyInventory();
            CreateUnderwaterOverlay();
        }

        private void Start()
        {
            _moveAction = InputSystem.actions.FindAction("Move");
            _lookAction = InputSystem.actions.FindAction("Look");
            _attackAction = InputSystem.actions.FindAction("Attack");
            _interactAction = InputSystem.actions.FindAction("Interact");
            _jumpAction = InputSystem.actions.FindAction("Jump");
            _sneakAction = InputSystem.actions.FindAction("Sneak");
            _sprintAction = InputSystem.actions.FindAction("Sprint");
            _inventoryAction = InputSystem.actions.FindAction("Inventory");
            _sprintPendingAction = InputSystem.actions.FindAction("SprintPending");
            _pauseAction = InputSystem.actions.FindAction("Pause");
            _sprintAction.performed += OnSprintPerformed;
            _inventoryAction.performed += OnInventoryPerformed;
            _sprintPendingAction.started += OnSprintPendingStarted;
            _jumpAction.started += OnJumpStarted;
            _pauseAction.started += OnPauseStarted;
            Cursor.lockState = _gameplayReady ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !_gameplayReady;
            _blockLayer = LayerMask.GetMask("Blocks");
            
            hotbar.LoadFromPlayer(this);
        }

        private bool _inventoryInitialized;

        private void InitializeEmptyInventory()
        {
            if (_inventoryInitialized) return;
            for (int i = 0; i < inventory.Length; i++) inventory[i] = ItemStack.EmptyStack();
            _inventoryInitialized = true;
        }

        public void ApplyPersistenceSnapshot(PlayerSnapshot snapshot)
        {
            if (snapshot == null) throw new System.ArgumentNullException(nameof(snapshot));
            for (int i = 0; i < inventory.Length; i++)
            {
                InventorySlotSnapshot saved = snapshot.Inventory[i];
                if (!Items.TryGetById(saved.ItemId, out Item item))
                {
                    inventory[i] = ItemStack.EmptyStack();
                    continue;
                }
                if (!saved.Infinite && saved.Count > item.MaxStack)
                    throw new InvalidDataException($"Inventory slot {i} exceeds item {saved.ItemId}'s maximum stack size.");
                inventory[i] = item.ItemId == Items.Air.ItemId
                    ? ItemStack.EmptyStack(saved.Infinite)
                    : new ItemStack(item, saved.Count, saved.Infinite);
            }
            _inventoryInitialized = true;

            bool controllerWasEnabled = characterController != null && characterController.enabled;
            if (controllerWasEnabled) characterController.enabled = false;
            transform.position = new Vector3(snapshot.X, snapshot.Y, snapshot.Z);
            if (controllerWasEnabled) characterController.enabled = true;
        }

        public PlayerSnapshot CreatePersistenceSnapshot()
        {
            InitializeEmptyInventory();
            InventorySlotSnapshot[] slots = new InventorySlotSnapshot[inventory.Length];
            for (int i = 0; i < inventory.Length; i++)
            {
                ItemStack stack = inventory[i];
                slots[i] = new InventorySlotSnapshot(stack.Item.ItemId, stack.Stack, stack.Infinite);
            }
            Vector3 position = transform.position;
            return new PlayerSnapshot(position.x, position.y, position.z, slots);
        }

        public static void PauseGame()
        {
            if (Paused) return;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            Paused = true;
            Time.timeScale = 0f;
            World.World.Instance?.RequestSave();
        }

        public static void ResumeGame()
        {
            Paused = false;
            Time.timeScale = 1f;
            bool ready = Instance != null && Instance._gameplayReady;
            Cursor.visible = !ready;
            Cursor.lockState = ready ? CursorLockMode.Locked : CursorLockMode.None;
        }

        public void SetGameplayReady(bool ready)
        {
            _gameplayReady = ready;
            if (ready) ResumeGame();
            else
            {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }
        }

        private void Update()
        {
            if (!_gameplayReady || Paused) return;
            UpdateRotation();
            UpdateTargetBlock();
        }

        private void LateUpdate()
        {
            if (_underwaterOverlay == null) return;
            bool submerged = IsCameraSubmerged();
            _underwaterOverlay.enabled = submerged;
            if (submerged) UpdateUnderwaterOverlayAnimation();
        }

        private void FixedUpdate()
        {
            if (!_gameplayReady || Paused) return;
            _tick++;
            UpdateInput();
            UpdateInteraction();
        }

        private void UpdateRotation()
        {
            Vector2 look = _lookAction.ReadValue<Vector2>() * GameSettings.Sensitivity;
            transform.Rotate(Vector3.up * look.x);
            
            float xRotation = cameraTransform.rotation.eulerAngles.x - look.y;
            if (xRotation > 180) xRotation -= 360;
            xRotation = Mathf.Clamp(xRotation, -90, 90);
            cameraTransform.rotation = Quaternion.Euler(xRotation, transform.localRotation.eulerAngles.y, 0f);
        }

        private void UpdateInteraction()
        {
            if (_tick - _lastInteractionTick < MinimumInteractionDelay) return;
            
            if (HasTargetLocation && _attackAction.IsPressed())
            {
                if(hotbar.HoldingItem.OnDestroy(world, TargetLocation, TargetFace))
                    _lastInteractionTick = _tick;
            }

            if (_interactAction.IsPressed())
            {
                ItemUseContext context = new(
                    new Ray(cameraTransform.position, cameraTransform.forward),
                    MaxDistance,
                    HasTargetLocation ? TargetLocation : null,
                    TargetFace);
                if(hotbar.HoldingItem.OnUse(world, context))
                    _lastInteractionTick = _tick;
            }
        }
        private void UpdateInput()
        {
            Vector2 move = _moveAction.ReadValue<Vector2>().normalized;
            Vector3 bodyPosition = transform.position + characterController.center;
            bool inWater = IsInWater();
            if (_sprinting && Vector2.Dot(move, Vector2.up) <= 0.1) _sprinting = false;
            if (_sprinting) move *= 1.4f;
            if (_flying) move *= 1.5f;
            if (inWater) move *= WaterMovementMultiplier;
            
            camera.fieldOfView = Mathf.MoveTowards(camera.fieldOfView, _sprinting ? 100 : 80, 180 * Time.deltaTime);
            _velocity = (transform.forward * move.y + transform.right * move.x) * MoveSpeed;

            if (_flying)
            {
                if (_jumpAction.IsPressed()) _velocity.y = FlyingSpeed;
                else if (_sneakAction.IsPressed()) _velocity.y = -FlyingSpeed;
                else _velocity.y = 0;
            }
            else
            {
                _velocity.y = _verticalMomentum + Time.deltaTime * (inWater ? WaterGravity : Gravity);

                if (_jumping)
                {
                    _velocity.y = inWater ? WaterSwimForce : JumpForce;
                    _jumping = false;
                }
                
            }

            if (inWater) _velocity += world.GetFluidFlow(Vector3Int.FloorToInt(bodyPosition)) * WaterCurrentSpeed;
            if (inWater && !_flying) _velocity.y = Mathf.Max(_velocity.y, WaterTerminalFallSpeed);
            
            characterController.Move(_velocity * Time.fixedDeltaTime);
            _verticalMomentum = characterController.isGrounded ? 0 : _velocity.y;
        }

        private bool IsInWater()
        {
            Vector3 bodyPosition = transform.position + characterController.center;
            return !world.GetFluid(Vector3Int.FloorToInt(bodyPosition)).IsEmpty;
        }

        private bool IsCameraSubmerged()
        {
            Vector3 cameraPosition = cameraTransform.position;
            Vector3Int fluidPosition = Vector3Int.FloorToInt(cameraPosition);
            FluidState fluid = world.GetFluid(fluidPosition);
            if (fluid.IsEmpty) return false;

            float surface = fluidPosition.y + fluid.OwnHeight;
            float threshold = _underwaterOverlay.enabled ? UnderwaterSurfaceHysteresis : -UnderwaterSurfaceHysteresis;
            return cameraPosition.y < surface + threshold;
        }

        private void UpdateUnderwaterOverlayAnimation()
        {
            int frame = Mathf.FloorToInt(Time.unscaledTime * UnderwaterOverlayFramesPerSecond) % UnderwaterOverlayFrameCount;
            if (frame == _underwaterOverlayFrame) return;

            _underwaterOverlayMaterial.SetFloat(Frame, WaterAtlasFirstFrame + frame);
            _underwaterOverlayFrame = frame;
        }

        private void CreateUnderwaterOverlay()
        {
            Material overlayMaterialTemplate = Resources.Load<Material>("UnderwaterOverlayMaterial");
            if (overlayMaterialTemplate == null)
            {
                Debug.LogError("Underwater overlay could not load its material.");
                return;
            }

            _underwaterOverlayMaterial = new Material(overlayMaterialTemplate);
            _underwaterOverlayMaterial.SetTexture(WaterAtlas, atlas);
            _underwaterOverlayMaterial.SetFloat(Frame, WaterAtlasFirstFrame);

            GameObject canvasObject = new("Underwater Overlay", typeof(RectTransform), typeof(Canvas));
            Canvas overlayCanvas = canvasObject.GetComponent<Canvas>();
            overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            overlayCanvas.overrideSorting = true;
            overlayCanvas.sortingOrder = -1;

            GameObject imageObject = new("Water Texture", typeof(RectTransform), typeof(RawImage));
            imageObject.transform.SetParent(canvasObject.transform, false);
            RectTransform imageTransform = imageObject.GetComponent<RectTransform>();
            imageTransform.anchorMin = Vector2.zero;
            imageTransform.anchorMax = Vector2.one;
            imageTransform.sizeDelta = Vector2.zero;

            _underwaterOverlay = imageObject.GetComponent<RawImage>();
            _underwaterOverlay.texture = Texture2D.whiteTexture;
            _underwaterOverlay.material = _underwaterOverlayMaterial;
            _underwaterOverlay.color = new Color(1f, 1f, 1f, UnderwaterOverlayAlpha);
            _underwaterOverlay.raycastTarget = false;
            _underwaterOverlay.enabled = false;
        }

        private void OnSprintPerformed(InputAction.CallbackContext context)
        {
            if (_gameplayReady && !Paused) _sprinting = true;
        }

        private void OnInventoryPerformed(InputAction.CallbackContext context)
        {
            if (_gameplayReady && !Paused && InventoryScreen.Instance != null) InventoryScreen.Instance.OpenMenu();
        }

        private void OnSprintPendingStarted(InputAction.CallbackContext context)
        {
            if (!_gameplayReady || Paused) return;
            float timeSinceLastClick = Time.time - _sprintLastClickTime;
            if (timeSinceLastClick <= DoubleClickDelay) _sprinting = true;
            _sprintLastClickTime = Time.time;
        }

        private void OnJumpStarted(InputAction.CallbackContext context)
        {
            if (!_gameplayReady || Paused) return;
            if (!_flying && (characterController.isGrounded || IsInWater())) _jumping = true;
            float timeSinceLastClick = Time.time - _flyLastClickTime;
            if (timeSinceLastClick <= DoubleClickDelay) _flying = !_flying;
            if (!_flying) _verticalMomentum = 0;
            else _jumping = false;
            _flyLastClickTime = Time.time;
        }

        private void OnPauseStarted(InputAction.CallbackContext context)
        {
            if (!_gameplayReady) return;
            if (CurrentScreen != null)
            {
                CurrentScreen.CloseMenu();
                return;
            }
            if (GameplayMenuController.Instance == null) return;
            if (GameplayMenuController.Instance.PauseVisible) GameplayMenuController.Instance.Resume();
            else GameplayMenuController.Instance.ShowPause();
        }

        private void OnDestroy()
        {
            if (_sprintAction != null) _sprintAction.performed -= OnSprintPerformed;
            if (_inventoryAction != null) _inventoryAction.performed -= OnInventoryPerformed;
            if (_sprintPendingAction != null) _sprintPendingAction.started -= OnSprintPendingStarted;
            if (_jumpAction != null) _jumpAction.started -= OnJumpStarted;
            if (_pauseAction != null) _pauseAction.started -= OnPauseStarted;
            if (_underwaterOverlayMaterial != null) Destroy(_underwaterOverlayMaterial);
            if (Instance == this) Instance = null;
        }

        private GameObject _lastHitObject;
        private int _lastHitFace;
        
        private void UpdateTargetBlock()
        {
            // Define origin point and direction vector
            Vector3 origin = cameraTransform.position;
            Vector3 direction = cameraTransform.forward;
            
            // Perform the standard physics operation
            if (Physics.Raycast(origin, direction, out RaycastHit hitInfo, MaxDistance, _blockLayer,
                    QueryTriggerInteraction.Ignore))
            {
                ImpactPoint = hitInfo.point;
                if (hitInfo.collider.gameObject == _lastHitObject && hitInfo.triangleIndex == _lastHitFace) return;

                if (hitInfo.collider.gameObject.TryGetComponent(out RenderObjectProperty renderObjectProperty))
                {
                    if (!targetOutline.activeSelf) targetOutline.SetActive(true);
                    
                    TargetLocation = renderObjectProperty.RenderObject.GetBlockPositionOfTriangle(hitInfo.triangleIndex);
                    TargetFace = renderObjectProperty.RenderObject.GetTriangleFacing(hitInfo.triangleIndex);
                    HasTargetLocation = true;
                    
                    targetOutline.transform.position = TargetLocation;
                    _lastHitFace = hitInfo.triangleIndex;
                    _lastHitObject = hitInfo.transform.gameObject;
                    return;
                }
            }
            targetOutline.SetActive(false);
            HasTargetLocation = false;
            _lastHitFace = -1;
            
        }
    }
}
