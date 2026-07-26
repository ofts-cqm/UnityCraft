using UnityEngine;
using UnityEngine.InputSystem;

namespace player
{
    public class Player : MonoBehaviour
    {
        public Transform cameraTransform;
        public Camera camera;
        public CharacterController characterController;
        private InputAction _moveAction;
        private InputAction _lookAction;
        
        private const float Sensitivity = 0.3f;
        private const float MoveSpeed = 4.317f;
        private const float Gravity = -9.8f;
        private const float JumpForce = 5f;
        private const float DoubleClickDelay = 0.3f;
        
        private bool _sprint;
        private float _sprintLastClickTime;
        private float _verticalMomentum;
        private bool _jumping;

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
                if (characterController.isGrounded) _jumping = true;
            };
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Update()
        {
            UpdateRotation();
        }

        private void FixedUpdate()
        {
            UpdateInput();
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
    }
}