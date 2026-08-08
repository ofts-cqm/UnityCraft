using render;
using Render;
using UnityEngine;
using UnityEngine.InputSystem;
using World;

namespace player
{
    public class Player : MonoBehaviour
    {
        public Transform cameraTransform;
        public World.World world;
        public GameObject targetOutline;
        public Camera camera;
        public CharacterController characterController;
        private InputAction _moveAction;
        private InputAction _lookAction;
        private InputAction _attackAction;
        private InputAction _interactAction;
        private InputAction _jumpAction;
        private InputAction _sneakAction;
        
        private int _defaultLayer;
        private int _allLayer;
        
        private const float Sensitivity = 0.3f;
        private const float MoveSpeed = 4.317f;
        private const float FlyingSpeed = 5f;
        private const float Gravity = -20f;
        private const float JumpForce = 5f;
        private const float DoubleClickDelay = 0.3f;
        private const int MinimumInteractionDelay = 10;
        
        private bool _sprinting;
        private bool _jumping;
        private bool _flying;
        
        private float _sprintLastClickTime;
        private float _flyLastClickTime;
        private float _verticalMomentum;
        
        private int _tick;
        private int _lastInteractionTick;

        private Vector3Int TargetLocation { get; set; }
        private int TargetFace { get; set; }
        private bool HasTargetLocation { get; set; }
        private bool Paused { get; set; }
        private int HoldingBlock { get; set; } = 2;

        private Vector3 _velocity;

        private const float MaxDistance = 5f;

        private void Awake()
        {
            _moveAction = InputSystem.actions.FindAction("Move");
            _lookAction = InputSystem.actions.FindAction("Look");
            _attackAction = InputSystem.actions.FindAction("Attack");
            _interactAction = InputSystem.actions.FindAction("Interact");
            _jumpAction = InputSystem.actions.FindAction("Jump");
            _sneakAction = InputSystem.actions.FindAction("Sneak");
            InputSystem.actions.FindAction("Sprint").performed += _ => _sprinting = true;
            InputSystem.actions.FindAction("SprintPending").started += _ =>
            {
                float timeSinceLastClick = Time.time - _sprintLastClickTime;
                if (timeSinceLastClick <= DoubleClickDelay) _sprinting = true;
                _sprintLastClickTime = Time.time;
            };
            InputSystem.actions.FindAction("Jump").started += _ =>
            {
                // jump
                if (!_flying && characterController.isGrounded) _jumping = true;
                
                // fly check
                float timeSinceLastClick = Time.time - _flyLastClickTime;
                if (timeSinceLastClick <= DoubleClickDelay) _flying = !_flying;

                if (!_flying) _verticalMomentum = 0;
                else _jumping = false;
                
                _flyLastClickTime = Time.time;
            };
            InputSystem.actions.FindAction("Pause").started += _ => Paused = !Paused;
            InputSystem.actions.FindAction("Scroll").performed += context =>
            {
                int delta = (int)context.ReadValue<float>();
                HoldingBlock += delta;
                if (HoldingBlock >= Blocks.BlockList.Count) HoldingBlock = 2;
                if (HoldingBlock < 2) HoldingBlock = Blocks.BlockList.Count - 1;
            };
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            _defaultLayer = LayerMask.GetMask("Default");
            _allLayer = LayerMask.GetMask("Default", "Ignore Raycast");
        }

        private void Update()
        {
            if (Paused) return;
            UpdateRotation();
            UpdateTargetBlock();
        }

        private void FixedUpdate()
        {
            if (Paused) return;
            _tick++;
            UpdateInput();
            UpdateInteraction();
        }

        private void UpdateRotation()
        {
            Vector2 look = _lookAction.ReadValue<Vector2>() * Sensitivity;
            transform.Rotate(Vector3.up * look.x);
            
            float xRotation = cameraTransform.rotation.eulerAngles.x - look.y;
            if (xRotation > 180) xRotation -= 360;
            xRotation = Mathf.Clamp(xRotation, -90, 90);
            cameraTransform.rotation = Quaternion.Euler(xRotation, transform.localRotation.eulerAngles.y, 0f);
        }

        private void UpdateInteraction()
        {
            if (!HasTargetLocation) return;
            if (_tick - _lastInteractionTick < MinimumInteractionDelay) return;
            
            if (_attackAction.IsPressed())
            {
                world.SetBlock(TargetLocation, Blocks.Air);
                _lastInteractionTick = _tick;
            }

            if (_interactAction.IsPressed())
            {
                Vector3Int rawPosition = TargetLocation;
                int face = TargetFace;
                Vector3Int finalPosition = face switch
                {
                    ChunkRenderObject.TopFace => rawPosition + Vector3Int.up,
                    ChunkRenderObject.BottomFace => rawPosition + Vector3Int.down,
                    ChunkRenderObject.LeftFace => rawPosition + Vector3Int.left,
                    ChunkRenderObject.RightFace => rawPosition + Vector3Int.right,
                    ChunkRenderObject.FrontFace => rawPosition + Vector3Int.forward,
                    ChunkRenderObject.BackFace => rawPosition + Vector3Int.back,
                    _ => rawPosition
                };

                if (world.GetBlock(finalPosition)?.IsAir ?? false)
                {

                    Vector3 half = new Vector3(0.5f, 0.5f, 0.5f);
                    Vector3 center = finalPosition + half;
                    if (!Physics.CheckBox(center, half * 0.9f, new Quaternion(), _allLayer))
                    {
                        world.SetBlock(finalPosition, Blocks.BlockList[HoldingBlock]);
                        _lastInteractionTick = _tick;
                    }
                }
            }
        }
        private void UpdateInput()
        {
            Vector2 move = _moveAction.ReadValue<Vector2>().normalized;
            if (_sprinting && Vector2.Dot(move, Vector2.up) <= 0.1) _sprinting = false;
            if (_sprinting) move *= 1.4f;
            if (_flying) move *= 1.5f;
            
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
                _velocity.y = _verticalMomentum + Time.deltaTime * Gravity;

                if (_jumping)
                {
                    _velocity.y = JumpForce;
                    _jumping = false;
                }
                
            }
            
            characterController.Move(_velocity * Time.fixedDeltaTime);
            _verticalMomentum = characterController.isGrounded ? 0 : _velocity.y;
        }

        private GameObject _lastHitObject;
        private int _lastHitFace;
        
        private void UpdateTargetBlock()
        {
            // Define origin point and direction vector
            Vector3 origin = cameraTransform.position;
            Vector3 direction = cameraTransform.forward;
            
            // Perform the standard physics operation
            if (Physics.Raycast(origin, direction, out RaycastHit hitInfo, MaxDistance, _defaultLayer))
            {
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