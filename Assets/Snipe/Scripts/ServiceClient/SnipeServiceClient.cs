using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using MiniIT;
using UnityEngine;
using CS;

namespace MiniIT.Snipe
{
	public class SnipeServiceClient
	{
		public event Action<ExpandoObject> MessageReceived;
		public event Action LoginSucceeded;
		public event Action<string> LoginFailed;

		internal const string MESSAGE_TYPE_USER_LOGIN = "user.login";
		internal const string ERROR_CODE_OK = "ok";

		private const double HEARTBEAT_INTERVAL = 30; // seconds

		protected bool mLoggedIn = false;

		public bool Connected { get { return (mWebSocket != null && mWebSocket.Connected); } }
		public bool LoggedIn { get { return mLoggedIn && Connected; } }

		protected bool mHeartbeatEnabled = true;
		public bool HeartbeatEnabled
		{
			get { return mHeartbeatEnabled; }
			set
			{
				if (mHeartbeatEnabled != value)
				{
					mHeartbeatEnabled = value;
					if (!mHeartbeatEnabled)
						StopHeartbeat();
					else if (LoggedIn)
						StartHeartbeat();
				}
			}
		}

		private int mRequestId = 0;

		protected void RequestLogin()
		{
			if (mLoggedIn || !Connected)
				return;

			SendRequest(new MPackMap()
			{
				["t"] = MESSAGE_TYPE_USER_LOGIN,
				["data"] = new MPackMap()
				{
					["ckey"] = SnipeConfig.Instance.snipe_client_key,
					["id"] = SnipeAuthCommunicator.UserID,
					["token"] = SnipeAuthCommunicator.LoginToken
				}
			});
		}

		protected MPackMap ConvertToMPackMap(Dictionary<string, object> dictionary)
		{
			var map = new MPackMap();
			foreach (var pair in dictionary)
			{
				if (pair.Value is MPack mpack_value)
					map.Add(MPack.From(pair.Key), mpack_value);
				else if (pair.Value is Dictionary<string, object> value_dictionary)
					map.Add(MPack.From(pair.Key), ConvertToMPackMap(value_dictionary));
				else
					map.Add(MPack.From(pair.Key), MPack.From(pair.Value));
			}

			return map;
		}

		protected ExpandoObject ConvertToExpandoObject(MPackMap map)
		{
			var obj = new ExpandoObject();
			foreach (string key in map.Keys)
			{
				var member = map[key];
				if (member is MPackMap member_map)
				{
					obj[key] = ConvertToExpandoObject(member_map);
				}
				else if (member is MPackArray member_array)
				{
					var list = new List<object>();
					foreach (var v in member_array)
					{
						if (v is MPackMap value_map)
							list.Add(ConvertToExpandoObject(value_map));
						else
							list.Add(v.Value);
					}
					obj[key] = list;
				}
				else
				{
					obj[key] = member.Value;
				}
			}
			return obj;
		}



		#region Web Socket

		private WebSocketWrapper mWebSocket = null;

		public void Connect()
		{
			if (mWebSocket != null)  // already connected or trying to connect
				return;

			Disconnect(); // clean up

			string url = SnipeConfig.Instance.snipe_service_websocket;

			//#if DEBUG
			Debug.Log("[SnipeServiceClient] WebSocket Connect to " + url);
			//#endif

			mWebSocket = new WebSocketWrapper();
			mWebSocket.OnConnectionOpened += OnWebSocketConnected;
			mWebSocket.OnConnectionClosed += OnWebSocketClosed;
			mWebSocket.ProcessMessage += ProcessMessage;
			mWebSocket.Connect(url);
		}

		private void OnWebSocketConnected()
		{
			//#if DEBUG
			Debug.Log("[SnipeServiceClient] OnWebSocketConnected");
			//#endif

			RequestLogin();
		}

		protected void OnWebSocketClosed()
		{
			Debug.Log("[SnipeServiceClient] OnWebSocketClosed");

			Disconnect();
		}

		public void Disconnect()
		{
			mLoggedIn = false;

			StopHeartbeat();

			if (mWebSocket != null)
			{
				mWebSocket.OnConnectionOpened -= OnWebSocketConnected;
				mWebSocket.OnConnectionClosed -= OnWebSocketClosed;
				mWebSocket.ProcessMessage -= ProcessMessage;
				mWebSocket.Disconnect();
				mWebSocket = null;
			}
		}

		public int SendRequest(MPackMap message)
		{
			if (!Connected || message == null)
				return 0;

			Debug.Log("[SnipeServiceClient] SendRequest - " + message["t"]);

			message["_requestID"] = ++mRequestId;

			var bytes = message.EncodeToBytes();
			lock (mWebSocket)
			{
				mWebSocket.SendRequest(bytes);
			}

			if (mHeartbeatEnabled)
			{
				ResetHeartbeatTimer();
			}

			return mRequestId;
		}

		public int SendRequest(Dictionary<string, object> message)
		{
			return SendRequest(ConvertToMPackMap(message));
		}

		public int SendRequest(string message_type, Dictionary<string, object> data)
		{
			return SendRequest(new MPackMap()
			{
				["t"] = message_type,
				["data"] = ConvertToMPackMap(data)
			});
		}

		protected void ProcessMessage(byte[] raw_data_buffer)
		{
			var message = MPack.ParseFromBytes(raw_data_buffer) as MPackMap;

			if (message != null)
			{
				if (!mLoggedIn)
				{
					if (message["t"] == MESSAGE_TYPE_USER_LOGIN)
					{
						string error_code = Convert.ToString(message["errorCode"]);
						if (error_code == ERROR_CODE_OK)
						{
							mLoggedIn = true;

							LoginSucceeded?.Invoke();

							if (mHeartbeatEnabled)
							{
								StartHeartbeat();
							}
						}
						else
						{
							LoginFailed?.Invoke(error_code);
						}
					}
				}

				MessageReceived?.Invoke(ConvertToExpandoObject(message));

				if (mHeartbeatEnabled)
				{
					ResetHeartbeatTimer();
				}
			}
		}

		#endregion // Web Socket

		#region Heartbeat

		private long mHeartbeatTriggerTicks = 0;

		private CancellationTokenSource mHeartbeatCancellation;

		private void StartHeartbeat()
		{
			mHeartbeatCancellation?.Cancel();

			mHeartbeatCancellation = new CancellationTokenSource();
			_ = HeartbeatTask(mHeartbeatCancellation.Token);
		}

		private void StopHeartbeat()
		{
			if (mHeartbeatCancellation != null)
			{
				mHeartbeatCancellation.Cancel();
				mHeartbeatCancellation = null;
			}
		}

		private async Task HeartbeatTask(CancellationToken cancellation)
		{
			var message = new MPackMap() { ["t"] = "user.ping" };
			var bytes = message.EncodeToBytes();

			ResetHeartbeatTimer();

			while (!cancellation.IsCancellationRequested && Connected)
			{
				await Task.Delay(5000, cancellation);

				if (DateTime.Now.Ticks >= mHeartbeatTriggerTicks)
				{
					lock (mWebSocket)
					{
						mWebSocket.Ping();
					}
					ResetHeartbeatTimer();

					//#if DEBUG
					Debug.Log("[SnipeServiceClient] Heartbeat ping");
					//#endif
				}
			}
		}

		private void ResetHeartbeatTimer()
		{
			mHeartbeatTriggerTicks = DateTime.Now.AddSeconds(HEARTBEAT_INTERVAL).Ticks;
		}

		#endregion

	}
}