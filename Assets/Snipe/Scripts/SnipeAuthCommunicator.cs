using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using MiniIT;

namespace MiniIT.Snipe
{
	public class SnipeAuthCommunicator : MonoBehaviour
	{
		protected const string REQUEST_USER_REGISTER = "auth/user.register";
		protected const string REQUEST_USER_EXISTS = "auth/user.exists";

		private const float LOGING_TOKEN_REFRESH_TIMEOUT = 1800.0f; // = 30 min

		private static SnipeAuthCommunicator mInstance;
		private static void InitInstance()
		{
			if (mInstance == null)
			{
				mInstance = new GameObject("SnipeAuthCommunicator").AddComponent<SnipeAuthCommunicator>();
				GameObject.DontDestroyOnLoad(mInstance.gameObject);
			}
		}

		private static int mUserID = 0;
		public static int UserID
		{
			get
			{
				if (mUserID <= 0)
				{
					mUserID = Convert.ToInt32(PlayerPrefs.GetString(SnipePrefs.LOGIN_USER_ID, "0"));
				}
				return mUserID;
			}
			private set
			{
				mUserID = value;
				PlayerPrefs.SetString(SnipePrefs.LOGIN_USER_ID, mUserID.ToString());
			}
		}
		public static string LoginToken { get; private set; }

		private static float mLoginTokenExpiry = -1;
		private Coroutine mCheckLoginTokenExpiryCoroutine;

		public static bool JustRegistered { get; private set; } = false;

		private List<AuthProvider> mAuthProviders;
		private AuthProvider mCurrentProvider;

		private Action mAuthSucceededCallback;
		private Action mAuthFailedCallback;

		// private constructor
		//private SnipeAuthCommunicator()
		//{
		//}

		public static void ClearLoginToken()
		{
			LoginToken = "";
		}

		private void Awake()
		{
			if (mInstance != null && mInstance != this)
			{
				Destroy(this.gameObject);
				return;
			}
		}

		public static ProviderType AddAuthProvider<ProviderType>() where ProviderType : AuthProvider, new()
		{
			if (mInstance == null)
				InitInstance();

			if (mInstance.mAuthProviders == null)
				mInstance.mAuthProviders = new List<AuthProvider>();

			ProviderType auth_provider = null;
			foreach (AuthProvider provider in mInstance.mAuthProviders)
			{
				if (provider.GetType().Equals(typeof(ProviderType)))
				{
					auth_provider = provider as ProviderType;
					break;
				}
			}
			if (auth_provider == null)
			{
				auth_provider = new ProviderType();
				mInstance.mAuthProviders.Add(auth_provider);
			}

			return auth_provider;
		}

		public static List<AuthProvider> GetAuthProviders()
		{
			return mInstance?.mAuthProviders;
		}

		public static ProviderType GetAuthProvider<ProviderType>() where ProviderType : AuthProvider
		{
			if (mInstance?.mAuthProviders != null)
			{
				foreach (AuthProvider provider in mInstance.mAuthProviders)
				{
					if (provider != null && provider is ProviderType)
					{
						return provider as ProviderType;
					}
				}
			}

			return null;
		}

		public static AuthProvider GetAuthProvider(string provider_id)
		{
			if (mInstance?.mAuthProviders != null)
			{
				foreach (AuthProvider provider in mInstance.mAuthProviders)
				{
					if (provider != null && provider.ProviderId == provider_id)
					{
						return provider;
					}
				}
			}

			return null;
		}

		public static bool SetCurrentProvider(AuthProvider provider)
		{
			Debug.Log($"[SnipeAuthCommunicator] SetCurrentProvider - {provider?.ProviderId}");

			InitInstance();

			if (provider == null)
			{
				if (mInstance.mCurrentProvider != null)
				{
					mInstance.mCurrentProvider.Dispose();
					mInstance.mCurrentProvider = null;
				}
				return false;
			}

			if (mInstance.mCurrentProvider == provider || mInstance.mCurrentProvider?.ProviderId == provider?.ProviderId)
				return true;

			if (mInstance.mAuthProviders != null)
			{
				if (mInstance.mAuthProviders.IndexOf(provider) >= 0)
				{
					if (mInstance.mCurrentProvider != null)
						mInstance.mCurrentProvider.Dispose();

					mInstance.mCurrentProvider = provider;
					return true;
				}
				else
				{
					var added_provider = GetAuthProvider(provider.ProviderId);
					if (added_provider != null)
					{
						if (mInstance.mCurrentProvider != null)
							mInstance.mCurrentProvider.Dispose();

						mInstance.mCurrentProvider = added_provider;
						return true;
					}
				}
			}

			return false;
		}

		public static void BindAllProviders(bool force_all = false, BindProvider.BindResultCallback single_bind_callback = null)
		{
			if (mInstance?.mAuthProviders != null)
			{
				foreach (BindProvider provider in mInstance.mAuthProviders)
				{
					if (provider != null && (force_all || !provider.AccountExists))
					{
						provider.RequestBind(single_bind_callback);
					}
				}
			}
		}

		public static void Authorize<ProviderType>(Action succeess_callback, Action fail_callback = null) where ProviderType : AuthProvider
		{
			InitInstance();
			mInstance.mCurrentProvider = GetAuthProvider<ProviderType>();

			if (mInstance.mCurrentProvider == null)
			{
				Debug.Log("[SnipeAuthCommunicator] Authorize<ProviderType> - provider not found");

				if (fail_callback != null)
					fail_callback.Invoke();

				return;
			}

			mInstance.AuthorizeWithCurrentProvider(succeess_callback, fail_callback);
		}

		public static void Authorize(Action succeess_callback, Action fail_callback = null)
		{
			InitInstance();

			if (mInstance.mCurrentProvider == null)
				mInstance.SwitchToNextAuthProvider();

			mInstance.AuthorizeWithCurrentProvider(succeess_callback, fail_callback);
		}

		public static void Authorize(bool reset, Action succeess_callback, Action fail_callback = null)
		{
			if (reset && mInstance != null) // forget previous provider and start again from the beginning
			{
				ClearLoginToken();

				AuthProvider prev_provider = mInstance.mCurrentProvider;

				mInstance.mCurrentProvider = null; 
				mInstance.SwitchToNextAuthProvider();

				if (prev_provider != mInstance.mCurrentProvider)
					prev_provider.Dispose();
			}

			Authorize(succeess_callback, fail_callback);
		}

		protected void AuthorizeWithCurrentProvider(Action succeess_callback, Action fail_callback = null)
		{
			JustRegistered = false;

			mAuthSucceededCallback = succeess_callback;
			mAuthFailedCallback = fail_callback;

			bool reset_auth = !(mCurrentProvider is DefaultAuthProvider) || string.IsNullOrEmpty(PlayerPrefs.GetString(SnipePrefs.AUTH_KEY));
			mCurrentProvider.RequestAuth(OnCurrentProviderAuthSuccess, OnCurrentProviderAuthFail, reset_auth);
		}

		private void SwitchToNextAuthProvider(bool create_default = true)
		{
			AuthProvider prev_provider = mCurrentProvider;
			mCurrentProvider = null;

			if (mAuthProviders != null && mAuthProviders.Count > 0)
			{
				int next_index = 0;
				if (prev_provider != null)
				{
					next_index = mAuthProviders.IndexOf(prev_provider) + 1;
					if (next_index < 0)
						next_index = 0;
				}

				if (mAuthProviders.Count > next_index)
				{
					mCurrentProvider = mAuthProviders[next_index];
				}
			}

			if (mCurrentProvider == null && create_default)
			{
				mCurrentProvider = new DefaultAuthProvider();
			}
		}

		private void SwitchToDefaultAuthProvider()
		{
			if (mCurrentProvider != null && !(mCurrentProvider is DefaultAuthProvider))
			{
				mCurrentProvider.Dispose();
				mCurrentProvider = null;
			}
			if (mCurrentProvider == null)
				mCurrentProvider = new DefaultAuthProvider();
		}

		private void OnCurrentProviderAuthSuccess(int user_id, string login_token)
		{
			UserID = user_id;
			LoginToken = login_token;

			InvokeAuthSuccessCallback();

			ResetCheckLoginTokenExpiryCoroutine();

			mCurrentProvider?.Dispose();
			mCurrentProvider = null;
		}

		private void OnCurrentProviderAuthFail(string error_code)
		{
			Debug.Log("[SnipeAuthCommunicator] OnCurrentProviderAuthFail (" + (mCurrentProvider != null ? mCurrentProvider.ProviderId : "null") + ") error_code: " + error_code);

			if (mCurrentProvider is DefaultAuthProvider)
			{
				if (error_code == AuthProvider.ERROR_NOT_INITIALIZED || error_code == AuthProvider.ERROR_NO_SUCH_USER)
				{
					RequestRegister();
				}
				else
				{
					InvokeAuthFailCallback();

					mCurrentProvider?.Dispose();
					mCurrentProvider = null;
				}
			}
			else  // try next provider
			{
				if (mCurrentProvider != null)
					mCurrentProvider.Dispose();

				SwitchToNextAuthProvider();
				bool reset_auth = !(mCurrentProvider is DefaultAuthProvider) || string.IsNullOrEmpty(PlayerPrefs.GetString(SnipePrefs.AUTH_KEY));
				mCurrentProvider.RequestAuth(OnCurrentProviderAuthSuccess, OnCurrentProviderAuthFail, reset_auth);
			}
		}

		private void InvokeAuthSuccessCallback()
		{
			if (mAuthSucceededCallback != null)
				mAuthSucceededCallback.Invoke();

			mAuthSucceededCallback = null;
			mAuthFailedCallback = null;
		}

		private void InvokeAuthFailCallback()
		{
			if (mAuthFailedCallback != null)
				mAuthFailedCallback.Invoke();

			mAuthSucceededCallback = null;
			mAuthFailedCallback = null;
		}

		private void RequestRegister()
		{
			ExpandoObject data = new ExpandoObject();
			data["messageType"] = REQUEST_USER_REGISTER;

			if (SystemInfo.unsupportedIdentifier != SystemInfo.deviceUniqueIdentifier)
				data["name"] = SystemInfo.deviceUniqueIdentifier; // optional

			SingleRequestClient.Request(SnipeConfig.Instance.auth, data, (response) =>
			{
				string error_code = response?.SafeGetString("errorCode");
				if (error_code == "ok")
				{
					JustRegistered = true;

					string auth_login = response.SafeGetString("uid");
					string auth_token = response.SafeGetString("password");

					PlayerPrefs.SetString(SnipePrefs.AUTH_UID, auth_login);
					PlayerPrefs.SetString(SnipePrefs.AUTH_KEY, auth_token);

					SwitchToDefaultAuthProvider();
					mCurrentProvider.RequestAuth(OnCurrentProviderAuthSuccess, OnCurrentProviderAuthFail);
				}
				else
				{
					InvokeAuthFailCallback();
				}
			});
		}

		private void ResetCheckLoginTokenExpiryCoroutine()
		{
			if (mCheckLoginTokenExpiryCoroutine != null)
				StopCoroutine(mCheckLoginTokenExpiryCoroutine);

			mCheckLoginTokenExpiryCoroutine = StartCoroutine(CheckLoginTokenExpiryCoroutine());
		}

		private IEnumerator CheckLoginTokenExpiryCoroutine()
		{
			mLoginTokenExpiry = Time.realtimeSinceStartup + LOGING_TOKEN_REFRESH_TIMEOUT;
			while (mLoginTokenExpiry > Time.realtimeSinceStartup)
				yield return null;

			mCheckLoginTokenExpiryCoroutine = null;
			RefreshLoginToken();
		}

		private void RefreshLoginToken()
		{
			if (mAuthSucceededCallback != null)
				return;

			SwitchToDefaultAuthProvider();
			mCurrentProvider.RequestAuth(OnCurrentProviderAuthSuccess, OnCurrentProviderAuthFail);
		}
	}
}