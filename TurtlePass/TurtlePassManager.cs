using System;
using System.Collections;
using System.Collections.Generic;
using FishNet;
using FishNet.Connection;
using UnityEngine;

public interface ITurtlePassReceiver
{
    public void ReceiveTurtlePassMessage(byte[] data,int packedSize, int senderId, TurtlePassDataType dataType);
}

public class TurtlePassManager : MonoBehaviour
{
    public static bool hasGet = false;
    public static TurtlePassManager get;
    public static TurtlePassMessagePool pool = new TurtlePassMessagePool();
    public const int maxSize = 32000;
    public const int maxSendPerAttempt = 1;
    public const float sendInterval = 0.33f; 

    public static List<ITurtlePassReceiver> Receivers = new List<ITurtlePassReceiver>();
    private Dictionary<int,TurtlePassMessage> TurtlePassSenderDict = new Dictionary<int, TurtlePassMessage>();
    private static Queue<TurtlePassMessage> sendQueue = new Queue<TurtlePassMessage>();
    private static TurtlePassMessage currentlyProcessing;
    public static bool hasCurrentlyProcessing = false;
    
    byte[] sendBuffer;

    public bool debug = false;
    public static bool DoDebug => hasGet && get.debug;

    private void Awake()
    {
        Receivers.Clear();
        hasGet = true;
        get = this;
    }

    private void OnDestroy()
    {
        hasGet = false;
        hasCurrentlyProcessing = false;
        currentlyProcessing = null;
    }

    private void OnEnable()
    {
        //Begins listening for any ChatBroadcast from the server.
        //When one is received the OnChatBroadcast method will be
        //called with the broadcast data.
        InstanceFinder.ClientManager.RegisterBroadcast<TurtlePassPieceBroadcast>(OnTurtlePassClientBroadcast);
        InstanceFinder.ServerManager.RegisterBroadcast<TurtlePassPieceBroadcast>(OnTurtlePassServerBroadcast);
    }
    
    private void OnDisable()
    {
        InstanceFinder.ClientManager.UnregisterBroadcast<TurtlePassPieceBroadcast>(OnTurtlePassClientBroadcast);
        InstanceFinder.ServerManager.UnregisterBroadcast<TurtlePassPieceBroadcast>(OnTurtlePassServerBroadcast);
    }
    
    private void OnTurtlePassServerBroadcast(NetworkConnection conn, TurtlePassPieceBroadcast msg) {
        ReceiveTurtlePass(msg);
    }

    private void OnTurtlePassClientBroadcast(TurtlePassPieceBroadcast msg)
    {
        ReceiveTurtlePass(msg);
    }

    public void ReceiveTurtlePass(TurtlePassPieceBroadcast msg)
    {
        int ConnectionID = msg.SenderID;
        if (!TurtlePassSenderDict.ContainsKey(ConnectionID)) {
            TurtlePassSenderDict[ConnectionID] = pool.Allocate();
            TurtlePassSenderDict[ConnectionID].Initialize(msg.TotalSize,maxSize);
        }

        if (!TurtlePassSenderDict.TryGetValue(ConnectionID, out TurtlePassMessage _dataPart))
            return;
        _dataPart.DataType = msg.DataType;
        _dataPart.packedSize = msg.PackedSize;
        _dataPart.SenderID = msg.SenderID;
        _dataPart.WriteChunk(msg.Data, msg.SlowSenderIndex,msg.TotalSize);

        if (msg.Complete) {
            byte[] data = _dataPart.FullArray;
            ReceiveCompletedMessage(data, _dataPart.packedSize,_dataPart.SenderID, _dataPart.DataType);
            pool.Release(_dataPart);
            TurtlePassSenderDict.Remove(ConnectionID);
        }
    }

    public void ReceiveCompletedMessage(byte[] data, int packedSize, int senderId, TurtlePassDataType dataType) {
        if(DoDebug) Debug.Log($"Received message {dataType} with length {data.Length} from sender {senderId}");

        for (int i = 0; i < Receivers.Count; i++) {
            Receivers[i].ReceiveTurtlePassMessage(data, packedSize,senderId, dataType);
        }
    }

    private float lastTimeCheckedSendQueue;
    
    private void Update()
    {
        if (lastTimeCheckedSendQueue + sendInterval > Time.realtimeSinceStartup)
            return;
        lastTimeCheckedSendQueue = Time.realtimeSinceStartup;
        
        if (hasCurrentlyProcessing)
        {
            int SentThisFrame = 0;
            while (SentThisFrame < maxSendPerAttempt && currentlyProcessing != null && currentlyProcessing.ReadChunk(out sendBuffer))
            {
                bool Completed = !currentlyProcessing.HasBytesLeft();

                TurtlePassPieceBroadcast newBroadcast = new TurtlePassPieceBroadcast(currentlyProcessing.SenderID, currentlyProcessing.DataType,
                    currentlyProcessing.CurrentSlowSenderIndex, currentlyProcessing.FullArray.Length,  sendBuffer,currentlyProcessing.packedSize, Completed);

                if (currentlyProcessing.sendingToServer)
                {
                    if (InstanceFinder.IsServer) OnTurtlePassServerBroadcast(InstanceFinder.IsClient ? InstanceFinder.ClientManager.Connection : default, newBroadcast);
                    else InstanceFinder.ClientManager.Broadcast(newBroadcast);
                }
                else
                {
                    if (currentlyProcessing.ToSpecificPlayer != -1) {
                        if (InstanceFinder.ServerManager.Clients.TryGetValue(currentlyProcessing.ToSpecificPlayer,
                                out NetworkConnection conn)) {
                            InstanceFinder.ServerManager.Broadcast(conn, newBroadcast);
                        }
                        else
                        {
                            //Did not find specific player I should besending to
                            pool.Release(currentlyProcessing);
                            currentlyProcessing = null;
                            hasCurrentlyProcessing = false;
                        }
                    }
                    else {
                        InstanceFinder.ServerManager.Broadcast(newBroadcast);
                    }
                }
                
                if (Completed)
                {
                    if (DoDebug) Debug.Log($"Completed sending! Sent {currentlyProcessing._totalSize} total");
                    pool.Release(currentlyProcessing);
                    currentlyProcessing = null;
                    hasCurrentlyProcessing = false;
                }
                if(hasCurrentlyProcessing)
                    currentlyProcessing.CurrentSlowSenderIndex++;
                SentThisFrame++;
            }

            if (DoDebug && hasCurrentlyProcessing)
                    Debug.Log($"Done round of sending still has to send {currentlyProcessing.BytesLeft()}");
        }
        else if (sendQueue.Count > 0 && !hasCurrentlyProcessing) {
            currentlyProcessing = sendQueue.Dequeue();
            hasCurrentlyProcessing = currentlyProcessing != null;
        }
    }

    public static void QueueSendBytes(int senderID, TurtlePassDataType dataType , byte[] data,int packedSize, bool sendToServer = false, int ToSpecificID = -1) {
        if(DoDebug) Debug.Log(string.Format($"Send {data.Length} to {(sendToServer? "server" : (ToSpecificID != -1 ? $"client {ToSpecificID}" : "all clients"))}. It will take {data.Length/maxSize} messages. Should be done in {(data.Length/maxSize)/maxSendPerAttempt} frames, sending {maxSize*maxSendPerAttempt} per frame"));
        
        TurtlePassMessage slowSender = pool.Allocate();
        slowSender.Initialize(data, maxSize);
        slowSender.DataType = dataType;
        slowSender._totalSize = data.Length;
        slowSender.packedSize = packedSize;
        slowSender.SenderID = senderID;
        slowSender.sendingToServer = sendToServer;
        slowSender.ToSpecificPlayer = ToSpecificID;
        sendQueue.Enqueue(slowSender);
    }
}
