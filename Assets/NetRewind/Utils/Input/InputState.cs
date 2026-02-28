using System;
using NetRewind.Utils.Input.Data;
using Unity.Netcode;
using UnityEngine;

namespace NetRewind.Utils.Input
{
    public struct InputState : INetworkSerializable
    {
        public uint Tick;
        public byte[] Input;
        public IData Data;

        public InputState(uint tick, byte[] input, IData data)
        {
            Tick = tick;
            Input = input;
            Data = data;
        }
        
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Tick);
            serializer.SerializeValue(ref Input);
            
            #region Serialize Data
            if (serializer.IsReader)
            {
                // Reader
                ushort stateType = 0;
                serializer.SerializeValue(ref stateType); // Read the data type
                
                Data = DataTypeRegistry.Create(stateType);
                if (Data != null)
                    Data.NetworkSerialize(serializer);
            }
            else if (serializer.IsWriter)
            {
                // Writer
                ushort stateType = DataTypeRegistry.GetId(Data.GetType());
                serializer.SerializeValue(ref stateType);
                
                Data.NetworkSerialize(serializer);
            }
            #endregion
        }
    }
}