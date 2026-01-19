using System;
using _Demo.Scripts.Car;
using _Demo.Scripts.Game;
using NetRewind.Utils.Input.Data;
using NetRewind.Utils.Simulation;
using NetRewind.Utils.Simulation.State;
using UnityEngine;

namespace _Demo.Scripts.Player
{
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerController : NetObject, ITick, IStateHolder, IInputListener, IInputDataSource
    {
        public bool IsInCar => _currentCar != null;
        
        [Header("Interaction")]
        [SerializeField] private LayerMask interactableMask = ~0;
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

        public byte[] InputData { get; set; }
        public IData Data { get; set; }
        public uint TickOfTheInput { get; set; }

        private Rigidbody _rb;
        
        private bool _grounded;
        private float _jumpCooldownTimer;
        
        private float _xRotation;
        private float _yRotation;

        private bool _canMove = true;
        private CarController _currentCar;
        private Transform _seat;
        
        private bool _isInteracting;
        
        protected override void NetSpawn()
        {
            playerCamera.enabled = IsOwner;
            if (playerCamera != null && playerCamera.TryGetComponent(out AudioListener audioListener))
                audioListener.enabled = IsOwner;
            
            if (IsOwner)
            {
                Cursor.visible = false;
                Cursor.lockState = CursorLockMode.Locked;
            }
            
            _rb = GetComponent<Rigidbody>();
            _rb.freezeRotation = true;
            
            ChangePredictionState(IsOwner); // Predict if we are the owner. If not, wait for server updates.
        }

        protected override void NetDespawn()
        {
            if (!(IsOwner || IsServer))
                return;
            
            if (IsInCar)
                _currentCar.Interact(this);
        }

        #region Ownership
        public override void OnLostOwnership()
        {
            ChangePredictionState(false);
        }
        public override void OnGainedOwnership()
        {
            ChangePredictionState(true);
        }
        #endregion

        #region Interaction
        private void OnCollisionStay(Collision other)
        {
            #if Server
            if (!IsServer) return;
            Transform obj = GetTotalParent(other.transform);
            if (obj.TryGetComponent(out NetObject netObject))
                RegisterInteraction(Simulation.CurrentTick, this, netObject);
            #endif
        }

        private Transform GetTotalParent(Transform obj)
        {
            if (obj.parent != null)
                return GetTotalParent(obj.parent);
            
            return obj;
        }
        #endregion
        
        public void Tick(uint tick)
        {
            if (IsInCar)
            {
                transform.position = _seat.position;
                
                #if Server
                if (IsServer)
                    RegisterInteraction(tick, this, _currentCar);
                #endif
            }

            if (HasInputForThisTick(tick))
            {
                // GetPlayer data
                PlayerData data = GetData<PlayerData>();
            
                // Rotate player
                Vector3 newRotation = transform.eulerAngles;
                newRotation.y = data.YRotation;
                transform.eulerAngles = newRotation;

                // Move player
                if (_canMove)
                    Move();
            
                CheckInteract();
            }
        }
        
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
            Vector2 moveInput = GetVector2(0).normalized;

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

        private void CheckInteract()
        {
            bool interacting = GetButton(7);
            
            if (interacting && !_isInteracting)
            {
                IInteractable interactable = FindAnyObjectByType<CarController>();
                if (IsInCar)
                {
                    // Hop out of car
                    _currentCar.Interact(this);
                }
                else if (interactable != null)
                {
                    // Found an interactable
                    interactable.Interact(this);
                }
                else
                {
                    // No interactable in range
                }
            }
            
            _isInteracting = interacting;
        }

        public void HopInCar(CarController car, Transform seat)
        {
            _currentCar = car;
            _seat = seat;
         
            visual.SetParent(_seat);
            visual.localPosition = Vector3.zero;
            SetVisualSyncMode(false);
            
            transform.position = visual.position;
            _rb.position = visual.position;
    
            _canMove = false;
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.useGravity = false;
        }

        public void HopOutCar()
        {
            visual.SetParent(null);
            SetVisualSyncMode(true);
            
            Vector3 newPosition = transform.position;
            newPosition.y += 3;
            transform.position = newPosition;
            _rb.position = newPosition;
            
            _currentCar = null;
            _seat = null;
            
            _canMove = true;
            _rb.useGravity = true;
        }
        
        public void NetOwnerUpdate()
        {
            #if Client
            Look();
            #endif
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

        #region State
        
        public IData OnInputData()
        {
            return new PlayerData()
            {
                YRotation = transform.eulerAngles.y,
            };
        }

        public IState GetCurrentState()
        {
            return new PlayerState()
            {
                Position = transform.position,
                YRotation = transform.localRotation.eulerAngles.y,
                Velocity = _rb.linearVelocity,
                AngularVelocity = _rb.angularVelocity,
                CanMove = _canMove,
                Car = IsInCar ? _currentCar.NetworkObjectId : ulong.MaxValue,
            };
        }

        public void UpdateState(IState state)
        {
            PlayerState playerState = (PlayerState) state;
            
            _canMove = playerState.CanMove;

            _rb.linearVelocity = playerState.Velocity;
            _rb.angularVelocity = playerState.AngularVelocity;
            
            if (playerState.Car != ulong.MaxValue)
            {
                // In car
                _currentCar = CarController.GetCar(playerState.Car);
                _seat = _currentCar.GetSeatByOwner(OwnerClientId);
                
                visual.SetParent(_seat);
                visual.localPosition = Vector3.zero;
                
                SetVisualSyncMode(false);
                
                _rb.useGravity = false;
            }
            else
            {
                // Out of the car
                _currentCar = null;
                _seat = null;
                
                visual.SetParent(null);
                transform.position = playerState.Position;
                
                SetVisualSyncMode(true);
                
                _rb.useGravity = true;
            }
            
            transform.localRotation = Quaternion.Euler(0, playerState.YRotation, 0);
        }

        public void ApplyState(IState state)
        {
            PlayerState playerState = (PlayerState) state;
            
            _canMove = playerState.CanMove;

            _rb.linearVelocity = playerState.Velocity;
            _rb.angularVelocity = playerState.AngularVelocity;
            
            if (playerState.Car != ulong.MaxValue)
            {
                // In car
                _currentCar = CarController.GetCar(playerState.Car);
                _seat = _currentCar.GetSeatByOwner(OwnerClientId);
                
                visual.SetParent(_seat);
                visual.localPosition = Vector3.zero;
                
                SetVisualSyncMode(false);
                
                _rb.useGravity = false;
            }
            else
            {
                // Out of the car
                _currentCar = null;
                _seat = null;
                
                visual.SetParent(null);
                transform.position = playerState.Position;
                
                SetVisualSyncMode(true);
                
                _rb.useGravity = true;
            }
            
            transform.localRotation = Quaternion.Euler(0, playerState.YRotation, 0);
        }

        public void ApplyPartialState(IState state, uint part)
        {
            throw new System.NotImplementedException();
        }
        
        #endregion
    }
}