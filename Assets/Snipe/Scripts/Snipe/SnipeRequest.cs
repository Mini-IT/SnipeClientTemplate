using System;
using MiniIT;

namespace MiniIT.Snipe
{
	public class SnipeRequest : IDisposable
	{
		public const string ERROR_NO_CONNECTION = "noConnection";
		public const string ERROR_INVALIND_DATA = "invalidData";

		protected SnipeClient mClient;
		protected Action<ExpandoObject> mCallback;
		protected string mMessageType;

		public ExpandoObject Data { get; set; }

		public SnipeRequest(SnipeClient client, string message_type = null)
		{
			mClient = client;
			mMessageType = message_type;
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

			if (string.IsNullOrEmpty(mMessageType))
				mMessageType = (Data != null) ? Data.SafeGetString("messageType") : null;

			if (string.IsNullOrEmpty(mMessageType))
			{
				if (callback != null)
					callback.Invoke(new ExpandoObject() { { "errorCode", ERROR_INVALIND_DATA } });
				return;
			}

			mCallback = callback;
			if (mCallback != null)
			{
				mClient.ConnectionLost += OnConnectionLost;
				mClient.DataReceived += OnDataReceived;
			}
			mClient.SendRequest(mMessageType, Data);
		}

		private void OnConnectionLost(ExpandoObject data)
		{
			if (mCallback != null)
				mCallback.Invoke(new ExpandoObject() { { "errorCode", ERROR_NO_CONNECTION } });
		}

		protected void OnDataReceived(ExpandoObject response_data)
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
			return (response_data.SafeGetString("type") == mMessageType);
		}

		public void Dispose()
		{
			if (mClient != null)
			{
				mClient.ConnectionLost -= OnConnectionLost;
				mClient.DataReceived -= OnDataReceived;
				mClient = null;
			}
		}
	}
}