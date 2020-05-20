using System;
using System.Collections.Generic;
using MiniIT;
using UnityEngine;

namespace MiniIT.Snipe
{
	public class SnipeServiceCommunicator : MonoBehaviour
	{
		public event Action<ExpandoObject> MessageReceived;

		public bool Ready { get { return mClient != null && mClient.LoggedIn; } }

		private SnipeServiceClient mClient;
		internal SnipeServiceClient Client
		{
			get { return mClient; }
			private set { mClient = value; }
		}

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

			if (mClient == null)
			{
				mClient = new SnipeServiceClient();
				mClient.LoginSucceeded += OnLoginSucceeded;
				mClient.LoginFailed += OnLoginFailed;
			}
			if (!mClient.Connected)
			{
				mClient.Connect();
			}
		}

		public void DisposeClient()
		{
			mReceivedMessages = null;

			if (mClient != null)
			{
				mClient.LoginSucceeded -= OnLoginSucceeded;
				mClient.LoginFailed -= OnLoginFailed;
				mClient.MessageReceived -= OnMessageReceived;
				mClient.Disconnect();
				mClient = null;
			}
		}

		private void OnLoginSucceeded()
		{
			mClient.LoginSucceeded -= OnLoginSucceeded;
			mClient.LoginFailed -= OnLoginFailed;
			mClient.MessageReceived += OnMessageReceived;
			mReceivedMessages = new Queue<ExpandoObject>();
		}

		private void OnLoginFailed(string obj)
		{
			mClient.LoginSucceeded -= OnLoginSucceeded;
			mClient.LoginFailed -= OnLoginFailed;

			// TODO: process error
		}

		protected void OnDestroy()
		{
			DisposeClient();
		}

		private void OnMessageReceived(ExpandoObject data)
		{
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