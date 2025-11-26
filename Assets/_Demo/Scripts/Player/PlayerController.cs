using System.Collections.Generic;
using _Demo.Scripts.State;
using NetRewind.Utils.Input.Data;
using NetRewind.Utils.Player;
using NetRewind.Utils.Simulation;
using NetRewind.Utils.Simulation.State;
using UnityEngine;
using UnityEngine.InputSystem;

namespace _Demo.Scripts.Player
{
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerController : NetPlayer
    {
        [Header("Mouse Settings")]
        [SerializeField] private float xSensitivity = 6;
        [SerializeField] private float ySensitivity = 6;
        [Space(5)]
        [Header("Move Settings")]
        [SerializeField] private float walkSpeed;
        [SerializeField] private float sprintSpeed;
        [SerializeField] private float crouchSpeed;
        [SerializeField] private float groundDrag;
        [Space(2)]
        [SerializeField] private float jumpForce; 
        [SerializeField] private float jumpCooldown;
        [SerializeField] private float airMultiplier;
        [Space(5)] 
        [Header("Camera")] 
        [SerializeField] private Camera playerCamera;
        [Space(5)]
        [Header("References")]
        [SerializeField] private Transform orientation;
        [SerializeField] private LayerMask whatIsGround;
        [SerializeField] private float playerHeight;

        private Rigidbody _rb;
        
        private bool _grounded;
        private float _jumpCooldownTimer;
        
        private float _xRotation;
        private float _yRotation;
        
        protected override void NetSpawn()
        {
            playerCamera.enabled = IsOwner;
            if (IsOwner)
            {
                Cursor.visible = false;
                Cursor.lockState = CursorLockMode.Locked;
            }
            
            _rb = GetComponent<Rigidbody>();
            _rb.freezeRotation = true;
            // _rb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        protected override void NetUpdate()
        {
            #if Client
            Look();
            #endif
        }

        protected override void OnTick(uint tick)
        {
            // GetPlayer data
            PlayerData data = GetData<PlayerData>();
            
            // Rotate player
            Vector3 newRotation = transform.eulerAngles;
            newRotation.y = data.YRotation;
            transform.eulerAngles = newRotation;
            
            // Move player
            Move();
        }
        
        #if Client
        private void Look()
        {
            // Looking
            float mouseX = InputActions["Look"].ReadValue<Vector2>().x * Time.deltaTime * xSensitivity;
            float mouseY = InputActions["Look"].ReadValue<Vector2>().y * Time.deltaTime * ySensitivity;
            
            _yRotation += mouseX;
            
            _xRotation -= mouseY;
            _xRotation = Mathf.Clamp(_xRotation, -90f, 90f);
            
            playerCamera.transform.rotation = Quaternion.Euler(_xRotation, _yRotation, 0);
            orientation.rotation = Quaternion.Euler(0, _yRotation, 0);
            transform.rotation = Quaternion.Euler(0, _yRotation, 0);
        }
        #endif        

        private void Move()
        {
            if (_jumpCooldownTimer > 0)
                _jumpCooldownTimer -= Simulation.TimeBetweenTicks;
            
            // Applying movement
            // Setting the drag
            _grounded = Physics.Raycast(transform.position, Vector3.down, playerHeight * 0.5f + 0.2f, whatIsGround);

            if (_grounded)
                _rb.linearDamping = groundDrag;
            else
                _rb.linearDamping = 0;

            // Calculating movement
            Vector2 moveInput = GetVector2(0);

            // _orientation.rotation = Quaternion.Euler(0, input.PlayerRotation, 0);
            Vector3 moveDirection = orientation.forward * moveInput.y + orientation.right * moveInput.x;

            // Applying movement
            float moveSpeed = GetButton(4) ? sprintSpeed : GetButton(5) ? crouchSpeed : walkSpeed;

            // Grounded
            if (_grounded)
                _rb.AddForce(moveDirection.normalized * (moveSpeed * 10), ForceMode.Force);

            // In air
            else
                _rb.AddForce(moveDirection.normalized * (moveSpeed * 10 * airMultiplier), ForceMode.Force);

            // Speed Control
            Vector3 flatVel = new Vector3(_rb.linearVelocity.x, 0, _rb.linearVelocity.z);
            if (flatVel.magnitude > moveSpeed)
            {
                Vector3 limitedVel = flatVel.normalized * moveSpeed;
                _rb.linearVelocity = new Vector3(limitedVel.x, _rb.linearVelocity.y, limitedVel.z);
            }

            if (GetButton(6) && _grounded && _jumpCooldownTimer <= 0)
            {
                // Resetting Y velocity
                _rb.linearVelocity = new Vector3(_rb.linearVelocity.x, 0, _rb.linearVelocity.z);

                // Applying Force
                _rb.AddForce(transform.up * jumpForce, ForceMode.Impulse);

                // Applying Cooldown
                _jumpCooldownTimer = jumpCooldown;
            }
        }

        protected override IData GetAdditionalData()
        {
            return new PlayerData()
            {
                YRotation = transform.eulerAngles.y,
            };
        }

        protected override void UpdateState(IState state)
        {
            PlayerState playerState = (PlayerState) state;
            transform.position = playerState.Position;
            transform.localRotation = Quaternion.Euler(0, playerState.YRotation, 0);
            _rb.linearVelocity = playerState.Velocity;
            _rb.angularVelocity = playerState.AngularVelocity;
        }

        protected override void ApplyState(IState state)
        {
            PlayerState playerState = (PlayerState) state;
            transform.position = playerState.Position;
            transform.localRotation = Quaternion.Euler(0, playerState.YRotation, 0);
            _rb.linearVelocity = playerState.Velocity;
            _rb.angularVelocity = playerState.AngularVelocity;
        }
        
        protected override IState GetCurrentState()
        {
            return new PlayerState()
            {
                Position = transform.position,
                YRotation = transform.localRotation.eulerAngles.y,
                Velocity = _rb.linearVelocity,
                AngularVelocity = _rb.angularVelocity
            };
        }

        protected override void ApplyPartialState(IState state, uint part)
        {
            throw new System.NotImplementedException();
        }
    }
}