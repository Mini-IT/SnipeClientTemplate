using System;
using UnityEngine;

namespace MiniIT.Snipe
{
	public class BindProvider : AuthProvider
	{
		public bool AccountExists { get; protected set; } = false;
		//public bool NeedToBind { get; protected set; } = false;

		public delegate void BindResultCallback(BindProvider provider, string error_code);
		public delegate void CheckAuthExistsCallback(BindProvider provider, bool exists, bool is_me, string user_name = null);

		protected BindResultCallback mBindResultCallback;
		protected CheckAuthExistsCallback mCheckAuthExistsCallback;

		public string BindDonePrefsKey
		{
			get { return SnipePrefs.AUTH_BIND_DONE + ProviderId; }
		}

		public bool IsBindDone
		{
			get
			{
				return PlayerPrefs.GetInt(BindDonePrefsKey, 0) == 1;
			}
			protected set
			{
				bool current_value = PlayerPrefs.GetInt(BindDonePrefsKey, 0) == 1;
				if (value != current_value)
				{
					Debug.Log($"[BindProvider] ({ProviderId}) Set bind done flag to {value}");

					PlayerPrefs.SetInt(BindDonePrefsKey, value ? 1 : 0);

					if (value)
						OnBindDone();
				}
			}
		}

		public BindProvider() : base()
		{
			if (IsBindDone)
				OnBindDone();
		}

		public virtual void RequestBind(BindResultCallback bind_callback = null)
		{
			// Override this method.

			mBindResultCallback = bind_callback;

			InvokeBindResultCallback(ERROR_NOT_INITIALIZED);
		}

		public virtual string GetUserId()
		{
			// Override this method.
			return "";
		}

		protected override void OnAuthLoginResponse(ExpandoObject data)
		{
			base.OnAuthLoginResponse(data);

			AccountExists = (data?.SafeGetString("errorCode") == ERROR_OK);
		}

		public virtual bool CheckAuthExists(CheckAuthExistsCallback callback)
		{
			// Override this method.
			return false;
		}

		protected virtual void CheckAuthExists(string user_id, CheckAuthExistsCallback callback)
		{
			mCheckAuthExistsCallback = callback;

			Debug.Log($"[BindProvider] ({ProviderId}) CheckAuthExists {user_id}");

			ExpandoObject data = new ExpandoObject();
			data["messageType"] = REQUEST_USER_EXISTS;
			data["provider"] = ProviderId;
			data["login"] = user_id;

			string login_id = PlayerPrefs.GetString(SnipePrefs.LOGIN_USER_ID);
			if (!string.IsNullOrEmpty(login_id))
				data["id"] = Convert.ToInt32(login_id);

			SingleRequestClient.Request(SnipeConfig.Instance.auth, data, OnCheckAuthExistsResponse);
		}

		protected virtual void OnBindResponse(ExpandoObject data)
		{
			string error_code = data?.SafeGetString("errorCode");

			Debug.Log($"[BindProvider] ({ProviderId}) OnBindResponse - {error_code}");

			AccountExists = (error_code == "ok");

			InvokeBindResultCallback(error_code);
		}

		protected void OnCheckAuthExistsResponse(ExpandoObject data)
		{
			AccountExists = (data.SafeGetString("errorCode") == ERROR_OK);
			
			bool is_me = data.SafeGetValue("isSame", false);
			if (AccountExists && is_me)
				IsBindDone = true;

			if (mCheckAuthExistsCallback != null)
				mCheckAuthExistsCallback.Invoke(this, AccountExists, is_me, data.SafeGetString("name"));

			mCheckAuthExistsCallback = null;

			if (!AccountExists)
			{
				RequestBind();
			}
		}

		protected virtual void InvokeBindResultCallback(string error_code)
		{
			Debug.Log($"[BindProvider] ({ProviderId}) InvokeBindResultCallback - {error_code}");

			if (mBindResultCallback != null)
				mBindResultCallback.Invoke(this, error_code);

			mBindResultCallback = null;
		}

		protected virtual void OnBindDone()
		{
		}

		public override void Dispose()
		{
			mBindResultCallback = null;

			base.Dispose();
		}
	}
}