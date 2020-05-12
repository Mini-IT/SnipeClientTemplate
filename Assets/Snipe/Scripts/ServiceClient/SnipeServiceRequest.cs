using System;
using MiniIT;

namespace MiniIT.Snipe
{
	public class SnipeServiceRequest : IDisposable
	{
		public const string ERROR_NOT_READY = "notReady";
		public const string ERROR_INVALIND_DATA = "invalidData";

		protected SnipeServiceCommunicator mCommunicator;
		protected Action<ExpandoObject> mCallback;
		protected string mMessageType;

		protected int mRequestId;

		public ExpandoObject Data { get; set; }

		public SnipeServiceRequest(SnipeServiceCommunicator client, string message_type = null)
		{
			mCommunicator = client;
			mMessageType = message_type;
		}

		public void Request(ExpandoObject data, Action<ExpandoObject> callback = null)
		{
			Data = data;
			Request(callback);
		}

		public virtual void Request(Action<ExpandoObject> callback = null)
		{
			if (mCommunicator == null || !mCommunicator.Ready)
			{
				if (callback != null)
					callback.Invoke(new ExpandoObject() { { "errorCode", ERROR_NOT_READY } });
				return;
			}

			if (string.IsNullOrEmpty(mMessageType))
				mMessageType = Data?.SafeGetString("t");

			if (string.IsNullOrEmpty(mMessageType))
			{
				if (callback != null)
					callback.Invoke(new ExpandoObject() { { "errorCode", ERROR_INVALIND_DATA } });
				return;
			}

			mCallback = callback;
			if (mCallback != null)
			{
				//mCommunicator.ConnectionLost += OnConnectionLost;
				mCommunicator.MessageReceived += OnMessageReceived;
			}
			mRequestId = mCommunicator.Client.SendRequest(mMessageType, Data);
		}

		//private void OnConnectionLost(ExpandoObject data)
		//{
		//	if (mCallback != null)
		//		mCallback.Invoke(new ExpandoObject() { { "errorCode", ERROR_NO_CONNECTION } });
		//}

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
			int request_id = response_data.SafeGetValue<int>("_requestID");
			return ((request_id == 0 || request_id == mRequestId) && response_data.SafeGetString("type") == mMessageType);
		}

		public void Dispose()
		{
			if (mCommunicator != null)
			{
				//mCommunicator.ConnectionLost -= OnConnectionLost;
				mCommunicator.MessageReceived -= OnMessageReceived;
				mCommunicator = null;
			}
		}
	}
}