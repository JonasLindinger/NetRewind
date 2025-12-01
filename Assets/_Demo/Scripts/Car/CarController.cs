using System;
using System.Collections.Generic;
using _Demo.Scripts.Game;
using _Demo.Scripts.Player;
using NetRewind.Utils.Input.Data;
using NetRewind.Utils.Simulation;
using NetRewind.Utils.Simulation.State;
using UnityEngine;
using UnityEngine.Serialization;

namespace _Demo.Scripts.Car
{
    [RequireComponent(typeof(Rigidbody))]
    public class CarController : NetObject, ITick, IInputListener, IStateHolder, IInteractable
    {
        private static Dictionary<ulong, CarController> _cars = new Dictionary<ulong, CarController>();
        
        [Header("References")]
        [SerializeField] private GameObject seat1;
        [SerializeField] private GameObject seat2;
        
        private ulong _playerOnSeat1 = ulong.MaxValue;
        private ulong _playerOnSeat2 = ulong.MaxValue;
        
        
        public byte[] InputData { get; set; }
        public IData Data { get; set; }
        
        private Rigidbody _rb;

        private void Start()
        {
            _rb = GetComponent<Rigidbody>();
        }

        protected override void NetSpawn()
        {
            _cars.Add(NetworkObjectId, this);
        }

        protected override void NetDespawn()
        {
            _cars.Remove(NetworkObjectId);
        }

        public void NetOwnerUpdate()
        {
            
        }
        
        public void Tick(uint tick)
        {
            
        }
        
        public void Interact(PlayerController player)
        {
            if (player.IsInCar)
            {
                // Leave car
                if (_playerOnSeat1 == player.OwnerClientId)
                {
                    // Player sat in seat 1
                    _playerOnSeat1 = ulong.MaxValue;
                }
                else if (_playerOnSeat2 == player.OwnerClientId)
                {
                    // Player sat in seat 1
                    _playerOnSeat2 = ulong.MaxValue;
                }
                
                player.HopOutCar();
            }
            else
            {
                // Hop in car
                if (_playerOnSeat1 == ulong.MaxValue) // Check if seat 1 is free
                {
                    // Seat in Seat 1
                    player.transform.position = seat1.transform.position;
                    _playerOnSeat1 = player.OwnerClientId;
                    player.HopInCar(this, seat1.transform);
                }
                else if (_playerOnSeat2 == ulong.MaxValue) // Check if seat 2 is free
                {
                    // Seat in Seat 2
                    player.transform.position = seat2.transform.position;
                    _playerOnSeat2 = player.OwnerClientId;
                    player.HopInCar(this, seat2.transform);
                }
                else
                {
                    // No space
                }
            }
        }
        
        #region State
        public IState GetCurrentState()
        {
            return new CarState()
            {
                Position = transform.position,
                Rotation = transform.eulerAngles,
                Velocity = _rb.linearVelocity,
                AngularVelocity = _rb.angularVelocity,
                Seat1 = _playerOnSeat1,
                Seat2 = _playerOnSeat2
            };
        }

        public void UpdateState(IState state)
        {
            CarState carState = (CarState) state;
            transform.position = carState.Position;
            transform.eulerAngles = carState.Rotation;
            _rb.linearVelocity = carState.Velocity;
            _rb.angularVelocity = carState.AngularVelocity;
            _playerOnSeat1 = carState.Seat1;
            _playerOnSeat2 = carState.Seat2;
        }

        public void ApplyState(IState state)
        {
            CarState carState = (CarState) state;
            transform.position = carState.Position;
            transform.eulerAngles = carState.Rotation;
            _rb.linearVelocity = carState.Velocity;
            _rb.angularVelocity = carState.AngularVelocity;
            _playerOnSeat1 = carState.Seat1;
            _playerOnSeat2 = carState.Seat2;
        }

        public void ApplyPartialState(IState state, uint part)
        {
            throw new System.NotImplementedException();
        }
        #endregion
        
        protected override bool IsPredicted() => true;
        
        public static CarController GetCar(ulong clientId) => _cars[clientId];
        public Transform GetSeatByOwner(ulong ownerClientId)
        {
            if (_playerOnSeat1 == ownerClientId)
                return seat1.transform;
            if (_playerOnSeat2 == ownerClientId)
                return seat2.transform;
            
            return null;
        }
    }
}