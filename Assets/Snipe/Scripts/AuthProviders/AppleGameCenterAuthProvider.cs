using System;
#if UNITY_IOS
using System.Runtime.InteropServices;
using AOT;
#endif
using UnityEngine;
using MiniIT;
using MiniIT.Snipe;
using MiniIT.Social;

public class AppleGameCenterAuthProvider : BindProvider
{
	public const string PROVIDER_ID = "appl";
	public override string ProviderId { get { return PROVIDER_ID; } }

	private static Action<ExpandoObject> mLoginSignatureCallback;

	public override void RequestAuth(AuthSuccessCallback success_callback, AuthFailCallback fail_callback, bool reset_auth = false)
	{
		mAuthSuccessCallback = success_callback;
		mAuthFailCallback = fail_callback;

#if UNITY_IOS
		if (AppleGameCenterProvider.InstanceInitialized)
		{
			string gc_login = AppleGameCenterProvider.Instance.PlayerProfile.Id;
			if (!string.IsNullOrEmpty(gc_login))
			{
				mLoginSignatureCallback = (data) =>
				{
					Debug.Log("[AppleGameCenterAuthProvider] RequestAuth - LoginSignatureCallback");
					
					data["messageType"] = REQUEST_USER_LOGIN;
					data["login"] = gc_login;
					if (reset_auth)
						data["resetInternalAuth"] = reset_auth;
					
					SingleRequestClient.Request(SnipeConfig.Instance.auth, data, OnAuthLoginResponse);
				};
				generateIdentityVerificationSignature(VerificationSignatureGeneratorCallback);
				return;
			}
		}
#endif

		InvokeAuthFailCallback(AuthProvider.ERROR_NOT_INITIALIZED);
	}

	public override void RequestBind(BindResultCallback bind_callback = null)
	{
		Debug.Log("[AppleGameCenterAuthProvider] RequestBind");

		mBindResultCallback = bind_callback;

#if UNITY_IOS
		if (PlayerPrefs.HasKey(SnipePrefs.AUTH_UID) && PlayerPrefs.HasKey(SnipePrefs.AUTH_KEY))
		{
			if (AppleGameCenterProvider.InstanceInitialized)
			{
				string gc_login = AppleGameCenterProvider.Instance.PlayerProfile.Id;
				if (!string.IsNullOrEmpty(gc_login))
				{
					mLoginSignatureCallback = (data) =>
					{
						Debug.Log("[AppleGameCenterAuthProvider] RequestBind - LoginSignatureCallback");

						string auth_login = PlayerPrefs.GetString(SnipePrefs.AUTH_UID);
						string auth_token = PlayerPrefs.GetString(SnipePrefs.AUTH_KEY);

						if (string.IsNullOrEmpty(auth_login) || string.IsNullOrEmpty(auth_token))
						{
							Debug.Log("[AppleGameCenterAuthProvider] internal uid or token is invalid");
							InvokeBindResultCallback(AuthProvider.ERROR_PARAMS_WRONG);
							return;
						}

						data["messageType"] = REQUEST_USER_BIND;
						data["provider"] = ProviderId;
						data["login"] = gc_login;
						//data["auth"] = login_token;
						data["loginInt"] = auth_login;
						data["authInt"] = auth_token;

						Debug.Log("[AppleGameCenterAuthProvider] send user.bind " + data.ToJSONString());
						SingleRequestClient.Request(SnipeConfig.Instance.auth, data, OnBindResponse);
					};
					generateIdentityVerificationSignature(VerificationSignatureGeneratorCallback);
					return;
				}

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
		return AppleGameCenterProvider.InstanceInitialized ? AppleGameCenterProvider.Instance.PlayerProfile.Id : "";
	}

	public override bool CheckAuthExists(CheckAuthExistsCallback callback)
	{
		if (!AppleGameCenterProvider.InstanceInitialized)
			return false;

		CheckAuthExists(GetUserId(), callback);
		return true;
	}

	protected override void OnBindDone()
	{
		base.OnBindDone();

		AppleGameCenterProvider.Instance.LoggedOut += () =>
		{
			IsBindDone = false;
		};
	}

	#region GenerateIdentityVerificationSignature
	// https://gist.github.com/BastianBlokland/bbc02a407b05beaf3f55ead3dd10f808

#if UNITY_IOS
	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	private delegate void IdentityVerificationSignatureCallback(
		string publicKeyUrl,
		IntPtr signaturePointer, int signatureLength,
		IntPtr saltPointer, int saltLength,
		ulong timestamp,
		string error);

	[DllImport("__Internal")]
	private static extern void generateIdentityVerificationSignature(
		[MarshalAs(UnmanagedType.FunctionPtr)]IdentityVerificationSignatureCallback callback);

	// Note: This callback has to be static because Unity's il2Cpp doesn't support marshalling instance methods.
	[MonoPInvokeCallback(typeof(IdentityVerificationSignatureCallback))]
	private static void VerificationSignatureGeneratorCallback(
		string publicKeyUrl,
		IntPtr signaturePointer, int signatureLength,
		IntPtr saltPointer, int saltLength,
		ulong timestamp,
		string error)
	{
		// Create a managed array for the signature
		var signature = new byte[signatureLength];
		Marshal.Copy(signaturePointer, signature, 0, signatureLength);

		// Create a managed array for the salt
		var salt = new byte[saltLength];
		Marshal.Copy(saltPointer, salt, 0, saltLength);

		UnityEngine.Debug.Log($"publicKeyUrl: {publicKeyUrl}");
		UnityEngine.Debug.Log($"signature length: {signature?.Length}");
		UnityEngine.Debug.Log($"salt length: {salt?.Length}");
		UnityEngine.Debug.Log($"timestamp: {timestamp}");
		UnityEngine.Debug.Log($"error: {error}");

		if (mLoginSignatureCallback != null)
		{
			ExpandoObject data = new ExpandoObject();
			data["provider"] = PROVIDER_ID;
			data["publicKeyUrl"] = publicKeyUrl;
			data["signature"] = Convert.ToBase64String(signature);
			data["salt"] = Convert.ToBase64String(salt);
			data["timestamp"] = Convert.ToString(timestamp);

			mLoginSignatureCallback.Invoke(data);
		}
	}
#endif
	#endregion // GenerateIdentityVerificationSignature
}
