using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MiniIT.Snipe
{
	public class SnipeCommunicator : MonoBehaviour, IDisposable
	{
		public delegate void MessageReceivedHandler(ExpandoObject data, bool original = false);

		public event Action ConnectionSucceeded;
		public event Action ConnectionFailed;
		public event Action LoginSucceeded;
		public event MessageReceivedHandler MessageReceived;

		public string LoginName { get; private set; }

		protected SnipeClient Client { get; set; }
		public SnipeServiceCommunicator ServiceCommunicator { get; private set; }

		public int RestoreConnectionAttempts = 3;
		private int mRestoreConnectionAttempt;

		protected bool mDebugEnabled = false;
		public bool DebugEnabled
		{
			get
			{
				return mDebugEnabled;
			}
			set
			{
				mDebugEnabled = value;

				if (Client != null)
					Client.DebugEnabled = value;
			}
		}

		public bool Connected
		{
			get
			{
				return Client != null && Client.Connected;
			}
		}

		public bool LoggedIn
		{
			get { return Client != null && Client.LoggedIn; }
		}

		private bool mDisconnecting = false;

		public virtual void StartCommunicator()
		{
			DontDestroyOnLoad(this.gameObject);

			if (ServiceCommunicator == null)
				ServiceCommunicator = this.gameObject.AddComponent<SnipeServiceCommunicator>();
			else
				ServiceCommunicator.DisposeClient();

			if (CheckLoginParams())
			{
				InitClient();
			}
			else
			{
				Authorize();
			}
		}

		private bool CheckLoginParams()
		{
			if (SnipeAuthCommunicator.UserID != 0 && !string.IsNullOrEmpty(SnipeAuthCommunicator.LoginToken))
			{
				// TODO: check token expiry
				return true;
			}

			return false;
		}

		private void Authorize()
		{
			Debug.Log("[SnipeCommunicator] Authorize");

			SnipeAuthCommunicator.Authorize(OnAuthSucceeded, OnAuthFailed);
		}

		protected void OnAuthSucceeded()
		{
			InitClient();
		}

		protected void OnAuthFailed()
		{
			Debug.Log("[SnipeCommunicator] OnAuthFailed");

			if (ConnectionFailed != null)
				ConnectionFailed.Invoke();
		}

		protected virtual void InitClient()
		{
			InitClient(SnipeConfig.Instance.server.host, SnipeConfig.Instance.server.port, SnipeConfig.Instance.server.websocket);
		}

		protected virtual void InitClient(string tcp_host, int tcp_port, string web_socket_url = "")
		{
			if (LoggedIn)
			{
				Debug.LogWarning("[SnipeCommunicator] InitClient - already logged in");
				return;
			}
			
			if (Client == null)
			{
				Client = SnipeClient.CreateInstance(SnipeConfig.Instance.snipe_client_key, this.gameObject);
				Client.AppInfo = SnipeConfig.Instance.snipe_app_info;
				Client.Init(tcp_host, tcp_port, web_socket_url);
				Client.ConnectionSucceeded += OnConnectionSucceeded;
				Client.ConnectionFailed += OnConnectionFailed;
				Client.ConnectionLost += OnConnectionFailed;
				Client.DebugEnabled = this.DebugEnabled;
			}

			mDisconnecting = false;

			if (Client.Connected)
				RequestLogin();
			else
				Client.Connect();
		}

		public virtual void Reconnect()
		{
			if (Client == null)
				return;

			Client.Reconnect();
		}

		public virtual void Disconnect()
		{
			Debug.Log($"[SnipeCommunicator] {this.name} Disconnect");

			mDisconnecting = true;
			LoginName = "";

			if (Client != null)
				Client.Disconnect();
		}

		protected virtual void OnDestroy()
		{
			Debug.Log("[SnipeCommunicator] OnDestroy");

			if (Client != null)
			{
				Client.ConnectionSucceeded -= OnConnectionSucceeded;
				Client.ConnectionFailed -= OnConnectionFailed;
				Client.ConnectionLost -= OnConnectionFailed;
				Client.MessageReceived -= OnSnipeResponse;
				Client.Disconnect();
				Client = null;
			}
		}

		protected virtual void OnConnectionSucceeded(ExpandoObject data)
		{
			Debug.Log($"[SnipeCommunicator] {this.name} Connection succeeded");

			mRestoreConnectionAttempt = 0;
			mDisconnecting = false;

			if (ConnectionSucceeded != null)
				ConnectionSucceeded.Invoke();

			Client.MessageReceived += OnSnipeResponse;
			
			RequestLogin();
		}

		protected virtual void OnConnectionFailed(ExpandoObject data = null)
		{
			Debug.Log($"[SnipeCommunicator] {this.name} [{Client?.ConnectionId}] Game Connection failed. Reason: {Client?.DisconnectReason}");

			if (Client != null)
				Client.MessageReceived -= OnSnipeResponse;

			if (mRestoreConnectionAttempt < RestoreConnectionAttempts && !mDisconnecting)
			{
				mRestoreConnectionAttempt++;
				Debug.Log($"[SnipeCommunicator] Attempt to restore connection {mRestoreConnectionAttempt}");
				StartCoroutine(WaitAndInitClient());
			}
			else
			{
				if (ConnectionFailed != null)
					ConnectionFailed.Invoke();
			}
		}

		private void OnSnipeResponse(ExpandoObject data)
		{
			Debug.Log($"{DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss")} [SnipeCommunicator] [{Client?.ConnectionId}] OnSnipeResponse " + (data != null ? data.ToJSONString() : "null"));

			ProcessSnipeMessage(data, true);

			if (data["serverNotify"] is IList notification_messages)
			{
				foreach (ExpandoObject notification_data in notification_messages)
				{
					if (notification_data.ContainsKey("_type"))
						notification_data["type"] = notification_data["_type"];

					ProcessSnipeMessage(notification_data, false);
				}
			}
		}

		protected virtual void ProcessSnipeMessage(ExpandoObject data, bool original = false)
		{
			string message_type = data.SafeGetString("type");
			string error_code = data.SafeGetString("errorCode");

			switch (message_type)
			{
				case "user.login":
					if (error_code == "ok")
					{
						LoginName = data.SafeGetString("name");

						if (LoginSucceeded != null)
							LoginSucceeded.Invoke();
					}
					else if (error_code == "wrongToken" || error_code == "userNotFound")
					{
						Authorize();
					}
					else if (error_code == "userDisconnecting")
					{
						StartCoroutine(WaitAndRequestLogin());
					}
					else if (error_code == "userOnline")
					{
						RequestLogout();
						StartCoroutine(WaitAndRequestLogin());
					}
					break;
			}

			MessageReceived?.Invoke(data, original);
		}

		private IEnumerator WaitAndInitClient()
		{
			yield return new WaitForSeconds(0.5f);
			InitClient();
		}

		private IEnumerator WaitAndRequestLogin()
		{
			yield return new WaitForSeconds(1.0f);
			RequestLogin();
		}

		protected void RequestLogin()
		{
			if (ServiceCommunicator != null)
				ServiceCommunicator.DisposeClient();

			ExpandoObject data = new ExpandoObject();
			data["id"] = SnipeAuthCommunicator.UserID;
			data["token"] = SnipeAuthCommunicator.LoginToken;
			//data["lang"] = "ru";

			Client.SendRequest("user.login", data);
		}

		protected void RequestLogout()
		{
			Client.SendRequest("kit/user.logout");
		}

		public void Request(string message_type, ExpandoObject parameters = null)
		{
			if (Client == null || !Client.LoggedIn)
				return;

			Client.SendRequest(message_type, parameters);
			// else add to queue???
		}

		public SnipeRequest CreateRequest(string message_type = null, ExpandoObject parameters = null)
		{
			SnipeRequest request = new SnipeRequest(this.Client, message_type);
			request.Data = parameters;
			return request;
		}

		#region Kit Requests

		public void RequestKitActionSelf(string action_id, ExpandoObject parameters = null)
		{
			if (Client == null || !Client.LoggedIn)
				return;

			if (parameters == null)
				parameters = new ExpandoObject();

			parameters["messageType"] = "kit/action.self";
			parameters["actionID"] = action_id;
			Client.SendRequest(parameters);
		}

		public void RequestKitAttrSet(string key, object value)
		{
			ExpandoObject parameters = new ExpandoObject();
			parameters["messageType"] = "kit/attr.set";
			parameters["key"] = key;
			parameters["val"] = value;
			Client.SendRequest(parameters);
		}

		public void RequestKitAttrGetAll()
		{
			Client.SendRequest("kit/attr.getAll");
		}

		public SnipeRequest CreateKitActionSelfRequest(string action_id, ExpandoObject parameters = null)
		{
			SnipeKitActionSelfRequest request = new SnipeKitActionSelfRequest(this.Client, action_id);
			request.Data = parameters;
			return request;
		}

		#endregion // Kit Requests

		public virtual void Dispose()
		{
			Disconnect();

			if (this.gameObject != null)
			{
				GameObject.DestroyImmediate(this.gameObject);
			}
		}
	}
}