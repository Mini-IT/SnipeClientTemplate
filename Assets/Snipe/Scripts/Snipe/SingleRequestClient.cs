using System;
using UnityEngine;
using MiniIT;

namespace MiniIT.Snipe
{
	public class SingleRequestClient : MonoBehaviour
	{
		private SnipeClient mClient;
		private ExpandoObject mRequestData;

		private Action<ExpandoObject> mCallback;

		private SingleRequestClient()
		{
		}

		public static void Request(SnipeServerConfig config, ExpandoObject request, Action<ExpandoObject> callback)
		{
			SnipeClient client = SnipeClient.CreateInstance(SnipeConfig.Instance.snipe_client_key, "SnipeSingleRequestClient", false);
			client.AppInfo = SnipeConfig.Instance.snipe_app_info;
			SingleRequestClient instance = client.gameObject.AddComponent<SingleRequestClient>();
			instance.InitClient(client, config, request, callback);
		}

		private void InitClient(SnipeClient client, SnipeServerConfig config, ExpandoObject request, Action<ExpandoObject> callback)
		{
			mRequestData = request;
			mCallback = callback;

			mClient = client;
			mClient.Init(config.host, config.port, config.websocket);
			mClient.ConnectionSucceeded += OnConnectionSucceeded;
			mClient.ConnectionFailed += OnConnectionFailed;
			mClient.ConnectionLost += OnConnectionFailed;
			mClient.Connect();
		}

		private void OnConnectionSucceeded(ExpandoObject data)
		{
			Debug.Log($"[SingleRequestClient] ({mRequestData?.SafeGetString("messageType")}) Connection succeeded");

			mClient.MessageReceived += OnResponse;
			mClient.SendRequest(mRequestData);
		}

		private void OnConnectionFailed(ExpandoObject data)
		{
			Debug.Log($"[SingleRequestClient] ({mRequestData?.SafeGetString("messageType")}) Connection failed");

			mClient.MessageReceived -= OnResponse;

			InvokeCallback(new ExpandoObject() { ["errorCode"] = "connectionFailed" });
			DisposeClient();
		}

		private void OnResponse(ExpandoObject data)
		{
			Debug.Log($"[SingleRequestClient] ({mRequestData?.SafeGetString("messageType")}) OnResponse {data?.ToJSONString()}");

			if (data.SafeGetString("type") == mRequestData?.SafeGetString("messageType"))
			{
				InvokeCallback(data);
				DisposeClient();
			}
		}

		private void InvokeCallback(ExpandoObject data)
		{
			if (mCallback != null)
				mCallback.Invoke(data);

			mCallback = null;
		}

		private void DisposeClient()
		{
			Debug.Log($"[SingleRequestClient] ({mRequestData?.SafeGetString("messageType")}) DisposeClient");

			mCallback = null;
			mRequestData = null;

			if (mClient == null)
				return;

			mClient.MessageReceived -= OnResponse;
			mClient.ConnectionSucceeded -= OnConnectionSucceeded;
			mClient.ConnectionFailed -= OnConnectionFailed;
			mClient.ConnectionLost -= OnConnectionFailed;
			mClient.Dispose();
			mClient = null;
		}
	}
}