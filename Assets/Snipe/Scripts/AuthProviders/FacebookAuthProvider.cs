using System;
using UnityEngine;
using MiniIT;
using MiniIT.Snipe;
using MiniIT.Social;
using Facebook.Unity;

public class FacebookAuthProvider : BindProvider
{
	public const string PROVIDER_ID = "fb";
	public override string ProviderId { get { return PROVIDER_ID; } }

	public override void RequestAuth(Action<int, string> success_callback, Action<string> fail_callback, bool reset_auth = false)
	{
		mAuthSucceesCallback = success_callback;
		mAuthFailCallback = fail_callback;

		if (FB.IsLoggedIn && AccessToken.CurrentAccessToken != null) // FacebookProvider.InstanceInitialized)
		{
			RequestLogin(ProviderId, AccessToken.CurrentAccessToken.UserId, AccessToken.CurrentAccessToken.TokenString, reset_auth);
			return;
		}

		InvokeAuthFailCallback(AuthProvider.ERROR_NOT_INITIALIZED);
	}

	public override void RequestBind(Action<BindProvider, string> bind_callback = null)
	{
		mBindResultCallback = bind_callback;

		if (PlayerPrefs.HasKey(SnipePrefs.AUTH_UID) && PlayerPrefs.HasKey(SnipePrefs.AUTH_KEY))
		{
			if (FB.IsLoggedIn && AccessToken.CurrentAccessToken != null) // FacebookProvider.InstanceInitialized)
			{
				ExpandoObject data = new ExpandoObject()
				{
					["messageType"] = REQUEST_USER_BIND,
					["provider"] = ProviderId,
					["login"] = AccessToken.CurrentAccessToken.UserId,
					["auth"] = AccessToken.CurrentAccessToken.TokenString,
					["loginInt"] = PlayerPrefs.GetString(SnipePrefs.AUTH_UID),
					["authInt"] = PlayerPrefs.GetString(SnipePrefs.AUTH_KEY),
				};

				Debug.Log("[FacebookAuthProvider] send user.bind " + data.ToJSONString());
				SingleRequestClient.Request(SnipeConfig.Instance.auth, data, OnBindResponse);

				return;
			}
		}

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
		if (FB.IsLoggedIn && AccessToken.CurrentAccessToken != null)
			return AccessToken.CurrentAccessToken.UserId;

		return "";
	}

	public override bool CheckAuthExists(Action<BindProvider, bool, bool> callback)
	{
		if (FB.IsLoggedIn && AccessToken.CurrentAccessToken != null)
		{
			CheckAuthExists(GetUserId(), callback);
			return true;
		}

		return false;
	}
}
