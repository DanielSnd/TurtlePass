# TurtlePass
Turtle Pass is an addon for [Fishnet](https://github.com/FirstGearGames/FishNet) that allows you to send **large byte arrays** over **several frames** so it doesn't overwhelm more limiting transports like FishySteamworks and FishyUtp

## Usage

### Setup

Add **Turtle Pass Manager** component to the NetworkManager. The Debug bool can log relevant Turtle Pass messages to console when toggled.

![image](https://user-images.githubusercontent.com/9072324/206881512-71eada63-8549-4f03-8e5c-dad35b8688b0.png)

### Sending a message
To send a large byte array call:
```csharp
TurtlePassManager.QueueSendBytes(int senderID, TurtlePassDataType dataType , byte[] data, int packedSize, bool sendToServer = false, int ToSpecificID = -1)
```

- *int* senderID - Use the id of the sender, -1 for server.
- *enum* TurtlePassDataType - It's an enum. You can add your own message types to TurtlePassDataType by editing the enum. It identifies the type of byte array so the receiver knows what to do with it.
- *byte[]* data - This is your large byte[]
- *int* packedSize - The length of your byte[] or another int that might help the receiver handle your byte[].
- *optional bool* sendToServer - Set to true if you're sending this to the server as a client, it's optional and if it's set to false it'll assume this is a server sending to clients.
- *optional int* ToSpecificID - If it's set to a number different than -1 it'll assume you're trying to send to a specific client and it'll only send the message to the client with the corresponding connecion id. (Only works if you're sending as a server).

### Receiving a message

On the script you want to receive the message implement the interface **ITurtlePassReceiver**
```csharp
public interface ITurtlePassReceiver {
    public void ReceiveTurtlePassMessage(byte[] data,int packedSize, int senderId, TurtlePassDataType dataType);
}
```
Then add your receiver to the **Receivers** list in the **Turtle Pass Manager**
```csharp
        TurtlePassManager.Receivers.Add(this);
```

It'll then call the **ReceiveTurtlePassMessage** method in your receiver when the message has been fully received.

## Change sending rates

Sending rates are controlled by 3 const values in the beginning of **Turtle Pass Manager**
```csharp
    public const int maxSize = 86000; //Maximum size of the message in bytes
    public const int maxSendPerAttempt = 1; //How many messages to send in a single pass.
    public const float sendInterval = 0.33f; //Time interval between passes.
```
These values worked well for me but feel free to play around with them.
