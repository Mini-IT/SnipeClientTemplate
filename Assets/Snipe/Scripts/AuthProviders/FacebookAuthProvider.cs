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

	public override void RequestAuth(AuthSuccessCallback success_callback, AuthFailCallback fail_callback, bool reset_auth = false)
	{
		mAuthSuccessCallback = success_callback;
		mAuthFailCallback = fail_callback;

		if (FB.IsLoggedIn && AccessToken.CurrentAccessToken != null) // FacebookProvider.InstanceInitialized)
		{
			RequestLogin(ProviderId, AccessToken.CurrentAccessToken.UserId, AccessToken.CurrentAccessToken.TokenString, reset_auth);
			return;
		}

		InvokeAuthFailCallback(AuthProvider.ERROR_NOT_INITIALIZED);
	}

	public override void RequestBind(BindResultCallback bind_callback = null)
	{
		Debug.Log("[FacebookAuthProvider] RequestBind");

		mBindResultCallback = bind_callback;

		string auth_login = PlayerPrefs.GetString(SnipePrefs.AUTH_UID);
		string auth_token = PlayerPrefs.GetString(SnipePrefs.AUTH_KEY);

		if (!string.IsNullOrEmpty(auth_login) && !string.IsNullOrEmpty(auth_token))
		{
			if (FB.IsLoggedIn && AccessToken.CurrentAccessToken != null) // FacebookProvider.InstanceInitialized)
			{
				ExpandoObject data = new ExpandoObject()
				{
					["messageType"] = REQUEST_USER_BIND,
					["provider"] = ProviderId,
					["login"] = AccessToken.CurrentAccessToken.UserId,
					["auth"] = AccessToken.CurrentAccessToken.TokenString,
					["loginInt"] = auth_login,
					["authInt"] = auth_token,
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

	public override bool CheckAuthExists(CheckAuthExistsCallback callback)
	{
		if (FB.IsLoggedIn && AccessToken.CurrentAccessToken != null)
		{
			CheckAuthExists(GetUserId(), callback);
			return true;
		}

		return false;
	}

	protected override void OnBindDone()
	{
		base.OnBindDone();

		FacebookProvider.Instance.LoggedOut += () =>
		{
			IsBindDone = false;
		};
	}
}
