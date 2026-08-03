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
        
        private const float Sensitivity = 0.3f;
        private const float MoveSpeed = 4.317f;
        private const float Gravity = -20f;
        private const float JumpForce = 5f;
        private const float DoubleClickDelay = 0.3f;
        private const int MinimumInteractionDelay = 10;
        
        private bool _sprint;
        private float _sprintLastClickTime;
        private float _verticalMomentum;
        private bool _jumping;
        private int _tick = 0;
        private int _lastInteractionTick = 0;

        private Vector3Int TargetLocation { get; set; }
        private int TargetFace { get; set; }
        private bool HasTargetLocation { get; set; }
        private bool Paused { get; set; }

        private Vector3 _velocity;

        private const float MaxDistance = 5f;

        private void Awake()
        {
            _moveAction = InputSystem.actions.FindAction("Move");
            _lookAction = InputSystem.actions.FindAction("Look");
            _attackAction = InputSystem.actions.FindAction("Attack");
            _interactAction = InputSystem.actions.FindAction("Interact");
            InputSystem.actions.FindAction("Sprint").performed += _ => _sprint = true;
            InputSystem.actions.FindAction("SprintPending").started += _ =>
            {
                float timeSinceLastClick = Time.time - _sprintLastClickTime;
                if (timeSinceLastClick <= DoubleClickDelay) _sprint = true;
                _sprintLastClickTime = Time.time;
            };
            InputSystem.actions.FindAction("Jump").started += _ =>
            {
                if (characterController.isGrounded) _jumping = true;
            };
            InputSystem.actions.FindAction("Pause").started += _ => Paused = !Paused;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
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
                    if (!Physics.CheckBox(center, half * 0.9f, new Quaternion(), 0x0FFFFFFF))
                    {
                        world.SetBlock(finalPosition, Blocks.Stone);
                        _lastInteractionTick = _tick;
                    }
                }
            }
        }
        private void UpdateInput()
        {
            Vector2 move = _moveAction.ReadValue<Vector2>().normalized;
            if (_sprint && Vector2.Dot(move, Vector2.up) <= 0.1) _sprint = false;
            if (_sprint) move *= 1.4f;
            
            camera.fieldOfView = Mathf.MoveTowards(camera.fieldOfView, _sprint ? 100 : 80, 180 * Time.deltaTime);
            _velocity = (transform.forward * move.y + transform.right * move.x) * MoveSpeed;
            _velocity.y = _verticalMomentum + Time.deltaTime * Gravity;

            if (_jumping)
            {
                _velocity.y = JumpForce;
                _jumping = false;
            }
            
            characterController.Move(_velocity * Time.fixedDeltaTime);
            _verticalMomentum = characterController.isGrounded ? _verticalMomentum : _velocity.y;
        }

        private GameObject _lastHitObject;
        private int _lastHitFace;
        
        private void UpdateTargetBlock()
        {
            // Define origin point and direction vector
            Vector3 origin = cameraTransform.position;
            Vector3 direction = cameraTransform.forward;
            
            // Perform the standard physics operation
            if (Physics.Raycast(origin, direction, out RaycastHit hitInfo, MaxDistance, 0x0FFFFFFF))
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