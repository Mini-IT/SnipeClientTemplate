using System;
using System.Collections.Generic;
using MiniIT;
using UnityEngine;

namespace MiniIT.Snipe
{
	public class SnipeServiceCommunicator : MonoBehaviour
	{
		public event Action<ExpandoObject> MessageReceived;

		public bool Ready { get { return Client != null && Client.LoggedIn; } }

		internal SnipeServiceClient Client { get; private set; }

		private Queue<ExpandoObject> mReceivedMessages = null;

		private List<Action> mReadyCallbacks;

		public void StartCommunicator(Action callback = null)
		{
			if (callback != null)
			{
				if (mReadyCallbacks == null)
					mReadyCallbacks = new List<Action>();

				if (!mReadyCallbacks.Contains(callback))
					mReadyCallbacks.Add(callback);
			}

			if (Ready)
			{
				return; // callback will be invoked in Update (main thread)
			}

			if (Client == null)
			{
				Client = new SnipeServiceClient();
				Client.LoginSucceeded += OnLoginSucceeded;
				Client.LoginFailed += OnLoginFailed;
			}
			if (!Client.Connected)
			{
				Client.Connect();
			}
		}

		public void DisposeClient()
		{
			mReceivedMessages = null;

			if (Client != null)
			{
				Client.LoginSucceeded -= OnLoginSucceeded;
				Client.LoginFailed -= OnLoginFailed;
				Client.MessageReceived -= OnMessageReceived;
				Client.Disconnect();
				Client = null;
			}
		}

		private void OnLoginSucceeded()
		{
			Client.LoginSucceeded -= OnLoginSucceeded;
			Client.LoginFailed -= OnLoginFailed;
			Client.MessageReceived += OnMessageReceived;
			mReceivedMessages = new Queue<ExpandoObject>();
		}

		private void OnLoginFailed(string obj)
		{
			Client.LoginSucceeded -= OnLoginSucceeded;
			Client.LoginFailed -= OnLoginFailed;

			// TODO: process error
		}

		protected void OnDestroy()
		{
			DisposeClient();
		}

		private void OnMessageReceived(ExpandoObject data)
		{
#if UNITY_EDITOR
			Debug.Log("[SnipeServiceCommunicator] OnMessageReceived: " + data?.ToJSONString());
#endif

			if (mReceivedMessages != null)
			{
				lock (mReceivedMessages)
				{
					mReceivedMessages.Enqueue(data);
				}
			}
		}

		private void Update()
		{
			if (Ready && mReadyCallbacks != null && mReadyCallbacks.Count > 0)
			{
				for (int i = 0; i < mReadyCallbacks.Count; i++)
				{
					try
					{
						mReadyCallbacks[i]?.Invoke();
					}
					catch (Exception)
					{
						// ignore
					}
				}
				mReadyCallbacks = null;
			}

			if (mReceivedMessages != null)
			{
				lock (mReceivedMessages)
				{
					while (mReceivedMessages.Count > 0)
					{
						try
						{
							MessageReceived?.Invoke(mReceivedMessages.Dequeue());
						}
						catch (Exception ex)
						{
							Debug.Log("[SnipeServiceCommunicator] MessageReceived Invoke Error: " + ex.Message);
						}

					}
				}
			}
		}

		public SnipeServiceRequest CreateRequest(string message_type = null)
		{
			return new SnipeServiceRequest(this, message_type);
		}

		public void Request(string message_type, ExpandoObject data, Action<ExpandoObject> callback = null)
		{
			new SnipeServiceRequest(this, message_type).Request(data, callback);
		}
	}
}