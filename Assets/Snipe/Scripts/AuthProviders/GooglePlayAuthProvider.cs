using System;
using UnityEngine;
using MiniIT;
using MiniIT.Snipe;
using MiniIT.Social;

public class GooglePlayAuthProvider : BindProvider
{
	public const string PROVIDER_ID = "goog";
	public override string ProviderId { get { return PROVIDER_ID; } }

	//public static new bool IsBindDone
	//{
	//	get { return PlayerPrefs.GetInt(SnipePrefs.AUTH_BIND_DONE + PROVIDER_ID, 0) == 1; }
	//}

	public override void RequestAuth(Action<int, string> success_callback, Action<string> fail_callback, bool reset_auth = false)
	{
		mAuthSucceesCallback = success_callback;
		mAuthFailCallback = fail_callback;

#if UNITY_ANDROID
		if (GooglePlayProvider.InstanceInitialized)
		{
			string google_login = GooglePlayProvider.Instance.PlayerProfile.Id;
			if (!string.IsNullOrEmpty(google_login))
			{
				GooglePlayProvider.Instance.GetServerAuthToken((google_token) =>
				{
					Debug.Log("[GooglePlayAuthProvider] google_token : " + (string.IsNullOrEmpty(google_token) ? "empty" : "ok"));

					if (string.IsNullOrEmpty(google_token))
						InvokeAuthFailCallback(AuthProvider.ERROR_NOT_INITIALIZED);
					else
						RequestLogin(ProviderId, google_login, google_token, reset_auth);
				});

				return;
			}
		}
#endif

		InvokeAuthFailCallback(AuthProvider.ERROR_NOT_INITIALIZED);
	}

	public override void RequestBind(Action<BindProvider, string> bind_callback = null)
	{
		mBindResultCallback = bind_callback;

#if UNITY_ANDROID
		if (PlayerPrefs.HasKey(SnipePrefs.AUTH_UID) && PlayerPrefs.HasKey(SnipePrefs.AUTH_KEY))
		{
			if (GooglePlayProvider.InstanceInitialized)
			{
				Debug.Log("[GooglePlayAuthProvider] GetServerAuthToken");

				GooglePlayProvider.Instance.GetServerAuthToken((google_token) =>
				{
					if (string.IsNullOrEmpty(google_token))
					{
						Debug.Log("[GooglePlayAuthProvider] google_token is empty");
						return;
					}

					ExpandoObject data = new ExpandoObject();
					data["messageType"] = REQUEST_USER_BIND;
					data["provider"] = ProviderId;
					data["login"] = GooglePlayProvider.Instance.PlayerProfile.Id;
					data["auth"] = google_token;
					data["loginInt"] = PlayerPrefs.GetString(SnipePrefs.AUTH_UID);
					data["authInt"] = PlayerPrefs.GetString(SnipePrefs.AUTH_KEY);

					Debug.Log("[GooglePlayAuthProvider] send user.bind " + data.ToJSONString());
					SingleRequestClient.Request(SnipeConfig.Instance.auth, data, OnBindResponse);
				});

				return;
			}
		}
#endif

		InvokeBindResultCallback(ERROR_NOT_INITIALIZED);
	}

	protected override void OnAuthLoginResponse(ExpandoObject data)
	{
		base.OnAuthLoginResponse(data);

		string error_code = data.SafeGetString("errorCode");

		if (error_code == ERROR_OK)
		{
			int user_id = data.SafeGetValue<int>("id");
			string login_token = data.SafeGetString("token");

			IsBindDone = true;

			InvokeAuthSuccessCallback(user_id, login_token);
		}
		else
		{
			InvokeAuthFailCallback(error_code);
		}
	}

	public override string GetUserId()
	{
		return GooglePlayProvider.InstanceInitialized ? GooglePlayProvider.Instance.PlayerProfile.Id : "";
	}

	public override bool CheckAuthExists(Action<BindProvider, bool, bool> callback)
	{
		if (!GooglePlayProvider.InstanceInitialized)
			return false;

		CheckAuthExists(GetUserId(), callback);
		return true;
	}
}
