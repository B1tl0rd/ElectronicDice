﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Animations;

public class Die
	: MonoBehaviour
{
	public const float SCALE_2G = 2.0f;
	public const float SCALE_4G = 4.0f;
	public const float SCALE_8G = 8.0f;
	float scale = SCALE_8G;

	public enum State
	{
		Disconnected = -1,
		Unknown = 0,
		Face1 = 1,
		Face2,
		Face3,
		Face4,
		Face5,
		Face6,
		Handling,
		Falling,
		Rolling,
		Jerking,
		Crooked,
		Count
	}

    public State state { get; private set; } = State.Disconnected;

	// Name is already a part of Monobehaviour
	public string address
	{
		get;
		set;
	}

	public bool connected
	{
		get
		{
			return state != State.Disconnected;
		}
	}

	public int face
	{
		get
		{
			int ret = (int)state;
			if (ret < 1 || ret > 6)
			{
				ret = -1;
			}
			return ret;
		}
	}

	public delegate void TelemetryEvent(Vector3 acc, int millis);
	public TelemetryEvent OnTelemetry;

	public delegate void StateChangedEvent(State newState);
	public StateChangedEvent OnStateChanged;

	// For telemetry
	int lastSampleTime; // ms
	ISendBytes _sendBytes;

    // Internal delegate per message type
    delegate void MessageReceivedDelegate(DieMessage msg);
    Dictionary<DieMessageType, MessageReceivedDelegate> messageDelegates;

    void Awake()
	{
        messageDelegates = new Dictionary<DieMessageType, MessageReceivedDelegate>();

        // Setup delegates for face and telemetry
        messageDelegates.Add(DieMessageType.State, OnStateMessage);
        messageDelegates.Add(DieMessageType.Telemetry, OnTelemetryMessage);
    }

	// Use this for initialization
	void Start ()
	{
	}
	
	// Update is called once per frame
	void Update ()
	{
		
	}

	public void PlayAnimation(int animationIndex)
	{
		if (connected)
		{
			byte[] data = new byte[] { (byte)'A', (byte)'N', (byte)'I', (byte)'M', (byte)animationIndex };
			_sendBytes.SendBytes(this, data, 5, null);
		}
	}


	public void Ping()
	{
		if (connected)
		{
			byte[] data = new byte[] { (byte)'R', (byte)'D', (byte)'G', (byte)'T' };
			_sendBytes.SendBytes(this, data, 4, null);
		}
	}

	public void ForceState(State forcedState)
	{
		Debug.Log("Forcing state of die " + name + " to " + forcedState);
		state = forcedState;
	}

	public void Connect(ISendBytes sb)
	{
		_sendBytes = sb;
		state = State.Unknown;

		// Ping the die so we know its initial state
		Ping();
	}

	public void Disconnect()
	{
		_sendBytes = null;
		state = State.Disconnected;
	}

	public void DataReceived(byte[] data)
	{
		if (!connected)
		{
			Debug.LogError("Die " + name + " received data while disconnected!");
			return;
		}
		
		// Process the message coming from the actual die!
		var message = DieMessages.FromByteArray(data);
        MessageReceivedDelegate del;
        if (messageDelegates.TryGetValue(message.type, out del))
        {
            del.Invoke(message);
        }
	}

    void AddMessageHandler(DieMessageType msgType, MessageReceivedDelegate newDel)
    {
        MessageReceivedDelegate del;
        if (messageDelegates.TryGetValue(msgType, out del))
        {
            del += newDel;
        }
        else
        {
            messageDelegates.Add(msgType, newDel);
        }
    }

    void RemoveMessageHandler(DieMessageType msgType, MessageReceivedDelegate newDel)
    {
        MessageReceivedDelegate del;
        if (messageDelegates.TryGetValue(msgType, out del))
        {
            del -= newDel;
        }
    }

    IEnumerator SendMessage<T>(T message)
        where T : DieMessage
    {
        bool msgReceived = false;
        byte[] msgBytes = DieMessages.ToByteArray(message);
        _sendBytes.SendBytes(this, msgBytes, msgBytes.Length, () => msgReceived = true);
        yield return new WaitUntil(() => msgReceived);
    }

    IEnumerator WaitForMessage(DieMessageType msgType, System.Action<DieMessage> msgReceivedCallback)
    {
        bool msgReceived = false;
        DieMessage msg = default(DieMessage);
        MessageReceivedDelegate callback = (ackMsg) =>
        {
            msgReceived = true;
            msg = ackMsg;
        };

        AddMessageHandler(msgType, callback);
        yield return new WaitUntil(() => msgReceived);
        RemoveMessageHandler(msgType, callback);
        if (msgReceivedCallback != null)
        {
            msgReceivedCallback.Invoke(msg);
        }
    }

    IEnumerator SendMessageWithAck<T>(T message, DieMessageType ackType)
        where T : DieMessage
    {
        bool msgReceived = false;
        byte[] msgBytes = DieMessages.ToByteArray(message);
        _sendBytes.SendBytes(this, msgBytes, msgBytes.Length, null);

        MessageReceivedDelegate callback = (ackMsg) =>
        {
            msgReceived = true;
        };

        AddMessageHandler(ackType, callback);
        yield return new WaitUntil(() => msgReceived);
        RemoveMessageHandler(ackType, callback);
    }

    void OnStateMessage(DieMessage message)
    {
        // Handle the message
        var stateMsg = (DieMessageState)message;
        state = (State)stateMsg.face;

        // Notify anyone who cares
        if (OnStateChanged != null)
        {
            OnStateChanged.Invoke(state);
        }
    }

    void OnTelemetryMessage(DieMessage message)
    {
        // Don't bother doing anything with the message if we don't have
        // anybody interested in telemetry data.
        if (OnTelemetry != null)
        {
            var telem = (DieMessageAcc)message;

            for (int i = 0; i < 2; ++i)
            {
                // Compute actual accelerometer readings (in Gs)
                float cx = (float)telem.data[i].X / (float)(1 << 11) * (float)(scale);
                float cy = (float)telem.data[i].Y / (float)(1 << 11) * (float)(scale);
                float cz = (float)telem.data[i].Z / (float)(1 << 11) * (float)(scale);
                Vector3 acc = new Vector3(cx, cy, cz);

                lastSampleTime += telem.data[i].DeltaTime;

                // Notify anyone who cares
                OnTelemetry.Invoke(acc, lastSampleTime);
            }
        }
    }

    public IEnumerator UploadBulkData(byte[] bytes)
    {
        short remainingSize = (short)bytes.Length;

        // Send setup message
        var setup = new DieMessageBulkSetup();
        setup.size = remainingSize;
        yield return StartCoroutine(SendMessageWithAck(setup, DieMessageType.BulkSetupAck));

        // Then transfer data
        short offset = 0;
        while (remainingSize > 0)
        {
            var data = new DieMessageBulkData();
            data.offset = offset;
            data.size = (byte)Mathf.Min(remainingSize, 16);
            System.Array.Copy(bytes, offset, data.data, 0, data.size);
            yield return StartCoroutine(SendMessageWithAck(data, DieMessageType.BulkDataAck));
        }
    }

    public IEnumerator DownloadBulkData(System.Action<byte[]> onBufferReady)
    {
        // Wait for setup message
        short size = 0;
        yield return StartCoroutine(WaitForMessage(DieMessageType.BulkSetup, (msg) =>
        {
            var setupMsg = (DieMessageBulkSetup)msg;
            size = setupMsg.size;
        }));

        // Allocate a byte buffer
        byte[] buffer = new byte[size];
        short totalDataReceived = 0;

        // Setup bulk receive handler
        MessageReceivedDelegate bulkReceived = (msg) =>
        {
            var bulkMsg = (DieMessageBulkData)msg;
            System.Array.Copy(bulkMsg.data, 0, buffer, bulkMsg.offset, bulkMsg.size);
            totalDataReceived += bulkMsg.size;

            // Send acknowledgment (no need to do it synchronously)
            StartCoroutine(SendMessage(new DieMessageBulkDataAck()));
        };
        AddMessageHandler(DieMessageType.BulkData, bulkReceived);

        // Send acknowledgement to the die, so it may transfer bulk data immediately
        StartCoroutine(SendMessage(new DieMessageBulkSetupAck()));

        // Wait for all the bulk data to be received
        yield return new WaitUntil(() => totalDataReceived == size);

        // We're done
        RemoveMessageHandler(DieMessageType.BulkData, bulkReceived);
        onBufferReady.Invoke(buffer);
    }

    public IEnumerator UploadAnimationSet(AnimationSet set)
    {
        // Prepare the die
        var prepareDie = new DieMessageTransferAnimSet();
        prepareDie.count = (byte)set.animations.Length;
        prepareDie.totalAnimationByteSize = (short)set.GetTotalByteSize();
        yield return StartCoroutine(SendMessageWithAck(prepareDie, DieMessageType.TransferAnimSetAck));

        // Die is ready, perform bulk transfer for each of the animations
        foreach (var anim in set.animations)
        {
            byte[] animBytes = RGBAnimation.ToByteArray(anim);
            yield return StartCoroutine(UploadBulkData(animBytes));

            // Then wait until the die is ready for the next anim bulk transfer
            yield return StartCoroutine(WaitForMessage(DieMessageType.TransferAnimReadyForNextAnim, null));
        }

        // We're done!
    }

    public IEnumerator DownloadAnimationSet(AnimationSet outSet)
    {
        // Request the anim set from the die
        SendMessage(new DieMessageRequestAnimSet());

        // Now wait for the setup message back
        int animCount = 0;
        yield return StartCoroutine(WaitForMessage(DieMessageType.TransferAnimSet, (msg) =>
        {
            var setupMsg = (DieMessageTransferAnimSet)msg;
            animCount = setupMsg.count;
        }));

        // Got the message, acknowledge it
        StartCoroutine(SendMessage(new DieMessageTransferAnimSetAck()));

        outSet.animations = new RGBAnimation[animCount];
        for (int i = 0; i < animCount; ++i)
        {
            byte[] animData = null;
            yield return StartCoroutine(DownloadBulkData((buf) => animData = buf));
            outSet.animations[i] = RGBAnimation.FromByteArray(animData);

            // Tell die we're ready for next anim
            StartCoroutine(SendMessage(new DieMessageTransferAnimReadyForNextAnim()));
        }

        // We've read all the anims!
    }

    public IEnumerator UploadSettings(DieSettings settings)
    {
        // Prepare the die
        var prepareDie = new DieMessageTransferSettings();
        yield return StartCoroutine(SendMessageWithAck(prepareDie, DieMessageType.TransferSettingsAck));

        // Die is ready, perform bulk transfer of the settings
        byte[] settingsBytes = DieSettings.ToByteArray(settings);
        yield return StartCoroutine(UploadBulkData(settingsBytes));

        // We're done!
    }

    public IEnumerator DownloadSettings(System.Action<DieSettings> settingsReadCallback)
    {
        // Request the settings from the die
        SendMessage(new DieMessageRequestSettings());

        // Now wait for the setup message back
        yield return StartCoroutine(WaitForMessage(DieMessageType.TransferSettings, null));

        // Got the message, acknowledge it
        StartCoroutine(SendMessage(new DieMessageTransferSettingsAck()));

        byte[] settingsBytes = null;
        yield return StartCoroutine(DownloadBulkData((buf) => settingsBytes = buf));
        var newSettings = DieSettings.FromByteArray(settingsBytes);

        // We've read the settings
        settingsReadCallback.Invoke(newSettings);
    }
}