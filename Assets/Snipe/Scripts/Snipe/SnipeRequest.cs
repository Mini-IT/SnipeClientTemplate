using System;
using MiniIT;

namespace MiniIT.Snipe
{
	public class SnipeRequest : IDisposable
	{
		public const string ERROR_NO_CONNECTION = "noConnection";
		public const string ERROR_NOT_LOGGED_IN = "notLoggedIn";
		public const string ERROR_INVALIND_DATA = "invalidData";

		protected SnipeClient mClient;
		protected Action<ExpandoObject> mCallback;

		public string MessageType { get; protected set; }
		public ExpandoObject Data { get; set; }

		protected int mRequestId;

		public SnipeRequest(SnipeClient client, string message_type = null)
		{
			mClient = client;
			MessageType = message_type;
		}

		public void Request(ExpandoObject data, Action<ExpandoObject> callback = null)
		{
			Data = data;
			Request(callback);
		}

		public virtual void Request(Action<ExpandoObject> callback = null)
		{
			if (mClient == null || !mClient.Connected)
			{
				if (callback != null)
					callback.Invoke(new ExpandoObject() { { "errorCode", ERROR_NO_CONNECTION } });
				return;
			}

			if (!mClient.LoggedIn)
			{
				if (callback != null)
					callback.Invoke(new ExpandoObject() { { "errorCode", ERROR_NOT_LOGGED_IN } });
				return;
			}

			if (string.IsNullOrEmpty(MessageType))
				MessageType = Data?.SafeGetString("messageType");

			if (!CheckMessageType())
			{
				if (callback != null)
					callback.Invoke(new ExpandoObject() { { "errorCode", ERROR_INVALIND_DATA } });
				return;
			}

			SetCallback(callback);
			SendRequest();
		}

		protected bool CheckMessageType()
		{
			if (string.IsNullOrEmpty(MessageType))
				MessageType = Data?.SafeGetString("messageType");

			return !string.IsNullOrEmpty(MessageType);
		}

		protected virtual void SetCallback(Action<ExpandoObject> callback)
		{
			mCallback = callback;
			if (mCallback != null && mClient != null)
			{
				mClient.ConnectionLost -= OnConnectionLost;
				mClient.MessageReceived -= OnMessageReceived;
				mClient.ConnectionLost += OnConnectionLost;
				mClient.MessageReceived += OnMessageReceived;
			}
		}

		protected virtual void SendRequest()
		{
			mRequestId = mClient.SendRequest(MessageType, Data);
		}

		private void OnConnectionLost(ExpandoObject data)
		{
			if (mCallback != null)
				mCallback.Invoke(new ExpandoObject() { { "errorCode", ERROR_NO_CONNECTION } });
		}

		protected void OnMessageReceived(ExpandoObject response_data)
		{
			if (CheckResponse(response_data))
			{
				if (mCallback != null)
					mCallback.Invoke(response_data);

				Dispose();
			}
		}

		protected virtual bool CheckResponse(ExpandoObject response_data)
		{
			return (response_data.SafeGetValue<int>("_requestID") == mRequestId && response_data.SafeGetString("type") == MessageType);
		}

		public virtual void Dispose()
		{
			if (mClient != null)
			{
				mClient.ConnectionLost -= OnConnectionLost;
				mClient.MessageReceived -= OnMessageReceived;
				mClient = null;
			}
		}
	}
}