using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MiniIT.Snipe
{
	public class SnipeCommunicator : MonoBehaviour, IDisposable
	{
		public event Action ConnectionSucceeded;
		public event Action ConnectionFailed;
		public event Action LoginSucceeded;

		public int UserID { get; private set; }
		public string LoginName { get; private set; }

		protected SnipeClient Client { get; set; }

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

		public virtual void StartCommunicator()
		{
			//SnipeAuthCommunicator.Instance.AddAuthProvider(new GooglePlayAuthProvider());
			//SnipeAuthCommunicator.Instance.AddAuthProvider(new AppleGameCenterAuthProvider());

			if (PlayerPrefs.HasKey(SnipePrefs.LOGIN_USER_ID) && !string.IsNullOrEmpty(SnipeAuthCommunicator.LoginToken))
			{
				UserID = Convert.ToInt32(PlayerPrefs.GetString(SnipePrefs.LOGIN_USER_ID));
				
				if (CheckLoginParams())
				{
					InitClient();
					return;
				}
				else
				{
					UserID = 0;
				}
			}

			GotoAuth();
		}

		private bool CheckLoginParams()
		{
			if (UserID != 0 && !string.IsNullOrEmpty(SnipeAuthCommunicator.LoginToken))
			{
				// TODO: check token expiry
				return true;
			}

			return false;
		}

		private void GotoAuth()
		{
			Debug.Log("[SnipeCommunicator] GotoAuth");

			SnipeAuthCommunicator.Instance.Authorize(OnAuthSucceeded, OnAuthFailed);
		}

		protected void OnAuthSucceeded()
		{
			UserID = SnipeAuthCommunicator.UserID;

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
			if (Client == null)
			{
				Client = SnipeClient.CreateInstance(SnipeConfig.Instance.snipe_client_key, this.gameObject);
				Client.AppInfo = SnipeConfig.Instance.snipe_app_info;
				Client.Init(tcp_host, tcp_port, web_socket_url);
				Client.ConnectionSucceeded += OnConnectionSucceeded;
				Client.ConnectionFailed += OnConnectionFailed;
				Client.ConnectionLost += OnConnectionFailed;
				Client.DebugEnabled = this.DebugEnabled;
				DontDestroyOnLoad(this.gameObject);
			}
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
			Debug.Log("[SnipeCommunicator] Disconnect");

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
				Client.DataReceived -= OnSnipeResponse;
				Client.Disconnect();
				Client = null;
			}
		}

		protected virtual void OnConnectionSucceeded(ExpandoObject data)
		{
			Debug.Log("[SnipeCommunicator] Game Connection succeeded");

			if (ConnectionSucceeded != null)
				ConnectionSucceeded.Invoke();

			Client.DataReceived += OnSnipeResponse;
			
			RequestLogin();
		}

		protected virtual void OnConnectionFailed(ExpandoObject data = null)
		{
			Debug.Log("[SnipeCommunicator] Game Connection failed");

			if (Client != null)
				Client.DataReceived -= OnSnipeResponse;

			if (ConnectionFailed != null)
				ConnectionFailed.Invoke();
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
					else if (error_code == "wrongToken")
					{
						GotoAuth();
					}
					else if (error_code == "userNotFound")  // need to register new user
					{
						PlayerPrefs.DeleteKey(SnipePrefs.AUTH_UID);
						PlayerPrefs.DeleteKey(SnipePrefs.AUTH_KEY);
						GotoAuth();
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
		}

		private IEnumerator WaitAndRequestLogin()
		{
			yield return new WaitForSeconds(1.0f);
			RequestLogin();
		}

		protected void RequestLogin()
		{
			ExpandoObject data = new ExpandoObject();
			data["id"] = UserID;
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

		public SnipeRequest CreateRequest(string message_type, ExpandoObject parameters = null)
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