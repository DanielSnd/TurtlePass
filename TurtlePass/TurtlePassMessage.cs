using System;
using FishNet.Broadcast;
using UnityEngine;

public enum TurtlePassDataType
{
    GameState = 0
}
/// <summary>
/// Helper class to split and recombine a byte array into chunks
/// </summary>
public class TurtlePassMessage
{
    public bool isAllocated = false;
    public int poolIndex = -1;
    public int ChunkSize { get { return _maxSize; } }
    public int Size { get { return _array.Length; } }
    public byte[] FullArray { get { return _array; } }

    private byte[] _array;
    private int _maxSize => TurtlePassManager.maxSize;
    private int _byteIndex, _bytesLeft;

    public int _totalSize = 0,packedSize=0, SenderID = 0, CurrentSlowSenderIndex = 0, ToSpecificPlayer = 0;
    public TurtlePassDataType DataType = TurtlePassDataType.GameState;
    public bool sendingToServer = false;
    
    public void Initialize(int totalSize, int sendSize) => Initialize(new byte[totalSize], sendSize);
    public void Initialize(byte[] array, int sendSize)
    {
        _array = array;
        _totalSize = sendSize;
        CurrentSlowSenderIndex = 0;  _byteIndex = 0;
        _bytesLeft = _array.Length;
    }

    public TurtlePassMessage() {
        CurrentSlowSenderIndex = 0;
        _byteIndex = 0;
        _totalSize = _maxSize;
    }

    public bool ReadChunk(out byte[] buffer)
    {
        buffer = null;
        if (!HasBytesLeft()) return false; // end of stream

        int bytesToSend = Mathf.Min(_bytesLeft, _maxSize);
        buffer = new byte[bytesToSend];
        Array.Copy(_array, _byteIndex, buffer, 0, bytesToSend);

        _byteIndex += bytesToSend;
        _bytesLeft -= bytesToSend;
        //Debug.Log(_bytesLeft);
        return true;
    }

    public bool HasBytesLeft() => _bytesLeft > 0;
    public int BytesLeft() => _bytesLeft;
    public void WriteChunk(byte[] buffer, int index, int desiredtotalsize) {
        int offset = index * ChunkSize;
        if (_array.Length < desiredtotalsize)
            _array = new byte[desiredtotalsize];
        Array.Copy(buffer, 0, _array, offset, buffer.Length);
    }
}


public struct TurtlePassPieceBroadcast : IBroadcast
{
    public int SenderID;
    public int SlowSenderIndex;
    public int PackedSize;
    public int TotalSize;
    public TurtlePassDataType DataType;
    public byte[] Data;
    public bool Complete;

    public TurtlePassPieceBroadcast(int senderID,TurtlePassDataType dataType, int slowSenderIndex, int totalSize, byte[] data, int dataPackedSize, bool complete)
    {
        DataType = dataType;
        SenderID = senderID;
        SlowSenderIndex = slowSenderIndex;
        TotalSize = totalSize;
        Data = data;
        PackedSize = dataPackedSize;
        Complete = complete;
    }
}