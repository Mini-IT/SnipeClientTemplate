using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using Ionic.Zlib;
using MiniIT;

//
// Client to Snipe server
// http://snipeserver.com
// https://github.com/Mini-IT/SnipeWiki/wiki


// Docs on how to use TCP Client:
// http://sunildube.blogspot.ru/2011/12/asynchronous-tcp-client-easy-example.html

namespace MiniIT.Snipe
{
	public class SnipeClient : MonoBehaviour, IDisposable
	{
		private const float HEARTBEAT_INTERVAL = 30.0f; // seconds
		private const float CHECK_CONNECTION_TIMEOUT = 3.0f; // seconds

		private string mClientKey;
		public string ClientKey
		{
			get { return mClientKey; }
			set
			{
				if (mClientKey != value)
				{
					mClientKey = value;
					mClientKeySent = false;
				}
			}
		}
		private bool mClientKeySent;

		private string mAppInfo;
		public string AppInfo
		{
			get { return mAppInfo; }
			set
			{
				if (mAppInfo != value)
				{
					mAppInfo = value;
					mClientKeySent = false;
				}
			}
		}

		public bool DebugEnabled = false;

		public string ConnectionId { get; private set; }

		protected bool mConnected = false;
		protected bool mHeartbeatEnabled = true;
		protected bool mLoggedIn = false;

		private string mConnectionHost;
		private int mConnectionPort;
		private string mConnectionWebSocketURL;

		private float mHeartbeatTriggerTime = 0.0f;
		private float mCheckConnectionTriggerTime = 0.0f;

		private float mRealtimeSinceStartup;  // local variable for thread safety
		private bool mApplicationFocusLost = false;
		private bool mApplicationFocusGained = true;

		private int mRequestId = 0;

		public static SnipeClient CreateInstance(string client_key, string name = "SnipeClient", bool heartbeat_enabled = true)
		{
			SnipeClient instance = new GameObject(name).AddComponent<SnipeClient>();
			instance.ClientKey = client_key;
			instance.mHeartbeatEnabled = heartbeat_enabled;
			DontDestroyOnLoad(instance.gameObject);
			return instance;
		}

		public static SnipeClient CreateInstance(string client_key, GameObject game_object = null, bool heartbeat_enabled = true)
		{
			SnipeClient instance;

			if (game_object == null)
			{
				instance = CreateInstance(client_key, "SnipeClient", heartbeat_enabled);
			}
			else
			{
				instance = game_object.AddComponent<SnipeClient>();
				instance.ClientKey = client_key;
				instance.mHeartbeatEnabled = heartbeat_enabled;
			}

			return instance;
		}

		internal class QueuedEvent
		{
			internal Action<ExpandoObject> handler;
			internal ExpandoObject data;

			internal QueuedEvent(Action<ExpandoObject> handler, ExpandoObject data)
			{
				this.handler = handler;
				this.data = data;
			}
		}
		private Queue<QueuedEvent> mDispatchEventQueue = new Queue<QueuedEvent>();

		#pragma warning disable 0067

		public event Action<ExpandoObject> ConnectionSucceeded;
		public event Action<ExpandoObject> ConnectionFailed;
		public event Action<ExpandoObject> ConnectionLost;
		//public event Action<ExpandoObject> ErrorHappened;
		public event Action<ExpandoObject> MessageReceived;

		#pragma warning restore 0067

		private SnipeTCPClient mTCPClient = null;
		private SnipeWebSocketClient mWebSocketClient = null;

		// DEBUG
		public string DisconnectReason { get; private set; }

		public SnipeClient()
		{
		}

		private void Awake()
		{
			mRealtimeSinceStartup = Time.realtimeSinceStartup;
		}
		
		public void Init(string tcp_host, int tcp_port, string web_socket_url = "")
		{
			mConnectionHost = tcp_host;
			mConnectionPort = tcp_port;
			mConnectionWebSocketURL = web_socket_url;
		}

		private void DispatchEvent(Action<ExpandoObject> handler, ExpandoObject data = null)
		{
			mDispatchEventQueue.Enqueue(new QueuedEvent(handler, data));
		}

		private void DoDispatchEvent(Action<ExpandoObject> handler, ExpandoObject data)
		{
			Action<ExpandoObject> event_handler = handler;  // local variable for thread safety
			if (event_handler != null)
			{
				try
				{
					event_handler.Invoke(data);
				}
				catch (Exception e)
				{
					Debug.Log("[SnipeClient] DispatchEvent error: " + e.ToString() + e.Message);
					Debug.Log("[SnipeClient] ErrorData: " + (data != null ? data.ToJSONString() : "null"));
				}
			}
		}

		void Update()
		{
			mRealtimeSinceStartup = Time.realtimeSinceStartup;

			while (mDispatchEventQueue != null && mDispatchEventQueue.Count > 0)
			{
				QueuedEvent item = mDispatchEventQueue.Dequeue();
				if (item == null)
					continue;

				DoDispatchEvent(item.handler, item.data);

				// item.handler could have called Dispose
				if (!ClientIsValid)
					return;
			}

			if (!mApplicationFocusLost && mConnected && mCheckConnectionTriggerTime > 0.0f && mRealtimeSinceStartup >= mCheckConnectionTriggerTime)
			{
				// Disconnect detected
				if (DebugEnabled)
//#if DEBUG
					Debug.Log("[SnipeClient] Update - Disconnect detected");
//#endif
				mConnected = false;
				DisconnectReason = "Update - Disconnect detected";
				DisconnectAndDispatch(ConnectionLost);
			}

			if (mConnected)
			{
				if (mApplicationFocusLost && mApplicationFocusGained)
				{
					mApplicationFocusLost = false;

					if (mHeartbeatEnabled)
					{
						ResetHeartbeatTime(true);
						ResetCheckConnectionTime();
						SendPingRequest();
					}
				}
				else if (mHeartbeatTriggerTime > 0.0f && mRealtimeSinceStartup >= mHeartbeatTriggerTime)
				{
					ResetHeartbeatTime();
					if (mHeartbeatEnabled)
					{
						ResetCheckConnectionTime();
						SendPingRequest();
					}
				}
			}
		}

		private void ResetHeartbeatTime(bool force = false)
		{
			if (mConnected || force)
			{
				mHeartbeatTriggerTime =  mRealtimeSinceStartup + HEARTBEAT_INTERVAL;
			}
		}

		private void ResetCheckConnectionTime()
		{
			mCheckConnectionTriggerTime = mRealtimeSinceStartup + CHECK_CONNECTION_TIMEOUT;
		}

		public void Connect()
		{
			if (!string.IsNullOrEmpty(mConnectionHost) && mConnectionPort > 0)
			{
				ConnectTCP();
			}
			else if (!string.IsNullOrEmpty(mConnectionWebSocketURL))
			{
				ConnectWebSocket();
			}
		}
		
		private void ConnectTCP()
		{
			if (mTCPClient == null)
			{
				mTCPClient = new SnipeTCPClient();
				mTCPClient.OnConnectionSucceeded = OnTCPConnectionSucceeded;
				mTCPClient.OnConnectionFailed = OnTCPConnectionFailed;
				mTCPClient.OnConnectionLost = OnConnectionLost;
				mTCPClient.OnMessageReceived = OnMessageReceived;
			}
			mTCPClient.Connect(mConnectionHost, mConnectionPort);
		}
		
		/*
		public void ConnectWebSocket(string host, int port = 80)
		{
			string url = host.ToLower();
			if (!url.StartsWith("ws://") || !url.StartsWith("wss://"))
			{
				url = url.Replace("http://", "ws://").Replace("https://", "wss://");
				if (!url.StartsWith("ws://") || !url.StartsWith("wss://"))
					url = "ws://" + url;
			}
			if (url.EndsWith("/"))
				url = url.Substring(0, url.Length - 1);

			if (port > 0 && port != 80)
				url += ":" + port.ToString() + "/";

			ConnectWebSocket(url);
		}
		*/

		public void ConnectWebSocket()
		{
			ConnectionId = "";

			if (mWebSocketClient == null)
			{
				mWebSocketClient = new SnipeWebSocketClient();
				mWebSocketClient.OnConnectionSucceeded = OnWebSocketConnectionSucceeded;
				mWebSocketClient.OnConnectionFailed = OnWebSocketConnectionFailed;
				mWebSocketClient.OnConnectionLost = OnConnectionLost;
				mWebSocketClient.OnMessageReceived = OnMessageReceived;
			}
			mWebSocketClient.Connect(mConnectionWebSocketURL);
		}
		
		private void OnTCPConnectionSucceeded()
		{
			if (mWebSocketClient != null)
			{
				mWebSocketClient.Dispose();
				mWebSocketClient = null;
			}
			
			mConnected = true;
			mClientKeySent = false;
			mLoggedIn = false;

			mCheckConnectionTriggerTime = 0.0f;
			mHeartbeatTriggerTime = 0.0f;

			DispatchEvent(ConnectionSucceeded);
		}
		
		private void OnTCPConnectionFailed()
		{
			mConnected = false;
			mLoggedIn = false;

			if (!string.IsNullOrEmpty(mConnectionWebSocketURL))
			{
				ConnectWebSocket();
			}
			else
			{
				DisconnectReason = "OnTCPConnectionFailed";
				DispatchEvent(ConnectionFailed);

				ConnectionId = "";
			}
		}
		
		private void OnConnectionLost()
		{
			mConnected = false;
			mLoggedIn = false;
			DisconnectReason = "OnConnectionLost";
			DispatchEvent(ConnectionLost);

			ConnectionId = "";
		}
		
		private void OnMessageReceived(ExpandoObject data)
		{
			// reset check connection
			mCheckConnectionTriggerTime = 0.0f;

			if (!mLoggedIn && data != null && data.SafeGetString("type") == "user.login" && data.SafeGetString("errorCode") == "ok")
			{
				mLoggedIn = true;
				ResetHeartbeatTime();
			}

			if (data != null && data.ContainsKey("_connectionID"))
				ConnectionId = data.SafeGetString("_connectionID");

			//if (DebugEnabled)
			//Debug.Log(DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss") + " [SnipeClient] OnMessageReceived: " + data?.ToJSONString());

			DispatchEvent(MessageReceived, data);
		}
		
		private void OnWebSocketConnectionSucceeded()
		{
			if (mTCPClient != null)
			{
				mTCPClient.Dispose();
				mTCPClient = null;
			}
			
			mConnected = true;
			mClientKeySent = false;
			mLoggedIn = false;

			mCheckConnectionTriggerTime = 0.0f;
			ResetHeartbeatTime();

			DispatchEvent(ConnectionSucceeded);
		}
		
		private void OnWebSocketConnectionFailed()
		{
			mConnected = false;
			mLoggedIn = false;
			DisconnectReason = "OnWebSocketConnectionFailed";
			DispatchEvent(ConnectionFailed);
		}
		
		public void Reconnect()
		{
			if (Connected)
				return;

			Connect();
		}
		
	
		public void Disconnect()
		{
			DisconnectReason = "Disconnect called explicitly";
			DisconnectAndDispatch(null);
		}
		
		public void DisconnectAndDispatch(Action<ExpandoObject> event_to_dispatch)
		{
			if (DebugEnabled)
//#if DEBUG
			Debug.Log("[SnipeClient] DisconnectAndDispatch");
//#endif
			if (mTCPClient != null)
			{
				mTCPClient.Dispose();
				mTCPClient = null;
			}
			
			if (mWebSocketClient != null)
			{
				mWebSocketClient.Dispose();
				mWebSocketClient = null;
			}
			
			mConnected = false;
			mLoggedIn = false;
			
			mHeartbeatTriggerTime = 0.0f;
			mCheckConnectionTriggerTime = 0.0f;

			if (event_to_dispatch != null)
				DispatchEvent(event_to_dispatch);

			ConnectionId = "";
		}

		public int SendRequest(string message_type, ExpandoObject parameters = null)
		{
			if (parameters == null)
				parameters = new ExpandoObject();
			
			parameters["messageType"] = message_type;

			return SendRequest(parameters);
		}

		public int SendRequest(ExpandoObject parameters)
		{
			if (parameters == null)
				parameters = new ExpandoObject();

			parameters["_requestID"] = ++mRequestId;

			if (DebugEnabled)
			{
//#if DEBUG
				Debug.Log($"[SnipeClient] [{ConnectionId}] SendRequest " + parameters.ToJSONString());
//#endif
			}

			// mTcpClient.Connected property gets the connection state of the Socket as of the LAST I/O operation (not current state!)
			// (http://msdn.microsoft.com/en-us/library/system.net.sockets.socket.connected.aspx)
			// So we need to check the connection availability manually, and here is where we can do it

			if (this.Connected)
			{
				if (!mClientKeySent && !string.IsNullOrEmpty(ClientKey))
				{
					parameters["clientKey"] = ClientKey;
					mClientKeySent = true;

					if (!string.IsNullOrEmpty(mAppInfo))
						parameters["appInfo"] = mAppInfo;
				}

				ResetHeartbeatTime();

				if (mTCPClient != null)
				{
					string message = HaxeSerializer.Run(parameters);
					mTCPClient.SendRequest(message);
				}
				else if (mWebSocketClient != null)
				{
					string message = HaxeSerializer.Run(parameters);
					mWebSocketClient.SendRequest(message);
				}
			}
			else
			{
				CheckConnectionLost();
			}

			return mRequestId;
		}

		protected void SendPingRequest()
		{
			if (mLoggedIn)
			{
				SendRequest("kit/user.ping");
			}
			else
			{
				ResetHeartbeatTime();
			}
		}

		protected bool CheckConnectionLost()
		{
			if (mConnected && !this.Connected)
			{
				// Disconnect detected
				mConnected = false;
				DisconnectReason = "CheckConnectionLost";
				DisconnectAndDispatch(ConnectionLost);
				return true;
			}
			return false;
		}

		private bool ClientIsValid
		{
			get
			{
				return mTCPClient != null || mWebSocketClient != null;
			}
		}

		public bool Connected
		{
			get
			{
				return mConnected && ((mTCPClient != null && mTCPClient.Connected) || (mWebSocketClient != null && mWebSocketClient.Connected));
			}
		}

		public bool LoggedIn
		{
			get
			{
				return Connected && mLoggedIn;
			}
		}

		public bool ConnectedViaWebSocket
		{
			get
			{
				return mWebSocketClient != null && mWebSocketClient.Connected;
			}
		}

		#region IDisposable implementation
		
		public void Dispose ()
		{
			Disconnect();

			if (this.gameObject != null)
			{
				GameObject.DestroyImmediate(this.gameObject);
			}
		}

		#endregion

		private void OnApplicationFocus(bool focus)
		{
			Debug.Log($"[SnipeClient] OnApplicationFocus focus = {focus}");

			if (focus)
			{
				mApplicationFocusGained = true;
			}
			else
			{
				mApplicationFocusGained = false;
				mApplicationFocusLost = true;

				// cancel connection checking
				mCheckConnectionTriggerTime = 0.0f;
			}
		}
	}

}