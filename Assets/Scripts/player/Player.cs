using UnityEngine;
using UnityEngine.InputSystem;

namespace player
{
    public class Player : MonoBehaviour
    {
        public Transform cameraTransform;
        public Camera camera;
        public World.World world;
        private InputAction _moveAction;
        private InputAction _lookAction;
        
        private const float Sensitivity = 0.3f;
        private const float MoveSpeed = 4.317f;
        private const float Gravity = -0.8f;
        private const float JumpForce = 1f;
        private const float DoubleClickDelay = 0.3f;
        private const float PlayerWidth = 0.3f;
        
        private bool _sprint;
        private float _sprintLastClickTime;
        private float _verticalMomentum;
        private bool _jumping;
        private bool _grounded;

        private Vector3 _velocity;

        private void Awake()
        {
            _moveAction = InputSystem.actions.FindAction("Move");
            _lookAction = InputSystem.actions.FindAction("Look");
            InputSystem.actions.FindAction("Sprint").performed += _ => _sprint = true;
            InputSystem.actions.FindAction("SprintPending").started += _ =>
            {
                float timeSinceLastClick = Time.time - _sprintLastClickTime;
                if (timeSinceLastClick <= DoubleClickDelay) _sprint = true;
                _sprintLastClickTime = Time.time;
            };
            InputSystem.actions.FindAction("Jump").started += _ =>
            {
                if (_grounded) _jumping = true;
            };
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Update()
        {
            UpdateInput();
        }

        private void UpdateInput()
        {
            Vector2 look = _lookAction.ReadValue<Vector2>() * Sensitivity;
            transform.Rotate(Vector3.up * look.x);
            
            float xRotation = cameraTransform.rotation.eulerAngles.x - look.y;
            if (xRotation > 180) xRotation -= 360;
            xRotation = Mathf.Clamp(xRotation, -90, 90);
            cameraTransform.rotation = Quaternion.Euler(xRotation, transform.localRotation.eulerAngles.y, 0f);
            
            Vector2 move = _moveAction.ReadValue<Vector2>().normalized;
            if (_sprint && Vector2.Dot(move, Vector2.up) <= 0.1) _sprint = false;
            if (_sprint) move *= 1.4f;
            
            //if (move == Vector2.zero && _grounded && !_jumping) return;
            
            camera.fieldOfView = Mathf.MoveTowards(camera.fieldOfView, _sprint ? 100 : 80, 80 * Time.deltaTime);
            _velocity = (transform.forward * move.y + transform.right * move.x) * (Time.deltaTime * MoveSpeed);
            _velocity.y += Time.fixedDeltaTime * Gravity;

            if (_jumping)
            {
                _velocity.y = JumpForce;
                _grounded = false;
                _jumping = false;
            }

            if ((_velocity.z > 0 && Front) || (_velocity.z < 0 && Back))
                _velocity.z = 0;
            if ((_velocity.x > 0 && Right) || (_velocity.x < 0 && Left))
                _velocity.x = 0;

            if (_velocity.y < 0)
                _velocity.y = CheckDownSpeed(_velocity.y);
            else if (_velocity.y > 0)
                _velocity.y = CheckUpSpeed(_velocity.y);
            
            transform.Translate(_velocity, Space.World);
        }
        
        private float CheckDownSpeed (float downSpeed)
        {

            if (
                world.IsInBlock(transform.position.x - PlayerWidth, transform.position.y + downSpeed, transform.position.z - PlayerWidth) ||
                world.IsInBlock(transform.position.x + PlayerWidth, transform.position.y + downSpeed, transform.position.z - PlayerWidth) ||
                world.IsInBlock(transform.position.x + PlayerWidth, transform.position.y + downSpeed, transform.position.z + PlayerWidth) ||
                world.IsInBlock(transform.position.x - PlayerWidth, transform.position.y + downSpeed, transform.position.z + PlayerWidth)
               ) {

                _grounded = true;
                return 0;

            }

            _grounded = false;
            return downSpeed;

        }

        private float CheckUpSpeed (float upSpeed) {

            if (
                world.IsInBlock(transform.position.x - PlayerWidth, transform.position.y + 2f + upSpeed, transform.position.z - PlayerWidth) ||
                world.IsInBlock(transform.position.x + PlayerWidth, transform.position.y + 2f + upSpeed, transform.position.z - PlayerWidth) ||
                world.IsInBlock(transform.position.x + PlayerWidth, transform.position.y + 2f + upSpeed, transform.position.z + PlayerWidth) ||
                world.IsInBlock(transform.position.x - PlayerWidth, transform.position.y + 2f + upSpeed, transform.position.z + PlayerWidth)
               ) {

                return 0;

            } else {

                return upSpeed;

            }

        }

        private bool Front {

            get
            {
                if (
                    world.IsInBlock(transform.position.x, transform.position.y, transform.position.z + PlayerWidth) ||
                    world.IsInBlock(transform.position.x, transform.position.y + 1f, transform.position.z + PlayerWidth)
                    )
                    return true;
                return false;
            }

        }

        private bool Back {

            get
            {
                if (
                    world.IsInBlock(transform.position.x, transform.position.y, transform.position.z - PlayerWidth) ||
                    world.IsInBlock(transform.position.x, transform.position.y + 1f, transform.position.z - PlayerWidth)
                    )
                    return true;
                return false;
            }

        }

        private bool Left {

            get
            {
                if (
                    world.IsInBlock(transform.position.x - PlayerWidth, transform.position.y, transform.position.z) ||
                    world.IsInBlock(transform.position.x - PlayerWidth, transform.position.y + 1f, transform.position.z)
                    )
                    return true;
                return false;
            }

        }

        private bool Right {

            get
            {
                if (
                    world.IsInBlock(transform.position.x + PlayerWidth, transform.position.y, transform.position.z) ||
                    world.IsInBlock(transform.position.x + PlayerWidth, transform.position.y + 1f, transform.position.z)
                    )
                    return true;
                return false;
            }

        }
    }
}