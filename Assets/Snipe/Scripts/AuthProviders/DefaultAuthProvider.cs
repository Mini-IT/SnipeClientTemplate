using System;
using UnityEngine;

namespace MiniIT.Snipe
{
	public class DefaultAuthProvider : AuthProvider
	{
		public const string PROVIDER_ID = "__";
		public override string ProviderId { get { return PROVIDER_ID; } }


		public override void RequestAuth(Action<int, string> success_callback, Action<string> fail_callback)
		{
			mAuthSucceesCallback = success_callback;
			mAuthFailCallback = fail_callback;

			//Debug.Log("[AuthProvider] RequestAuth " + provider + "  " + login + " token: " + (token != null ? token.Substring(0, 4) + "..." : "null"));

			if (PlayerPrefs.HasKey(SnipePrefs.AUTH_UID) && PlayerPrefs.HasKey(SnipePrefs.AUTH_KEY))
			{
				RequestLogin(ProviderId, PlayerPrefs.GetString(SnipePrefs.AUTH_UID), PlayerPrefs.GetString(SnipePrefs.AUTH_KEY));
			}
			else
			{
				InvokeAuthFailCallback(ERROR_NOT_INITIALIZED);
			}
		}

		protected override void OnAuthLoginResponse(ExpandoObject data)
		{
			if (mAuthSucceesCallback == null)
				return;

			string error_code = data.SafeGetString("errorCode");

			if (error_code == ERROR_OK)
			{
				int user_id = data.SafeGetValue<int>("id");
				string login_token = data.SafeGetString("token");

				InvokeAuthSuccessCallback(user_id, login_token);
			}
			else
			{
				if (error_code == ERROR_NO_SUCH_USER)
				{
					PlayerPrefs.DeleteKey(SnipePrefs.AUTH_UID);
					PlayerPrefs.DeleteKey(SnipePrefs.AUTH_KEY);
				}

				//	if (error_code == ERROR_PARAMS_WRONG)
				//	{

				//	}

				InvokeAuthFailCallback(error_code);
			}
		}
	}
}