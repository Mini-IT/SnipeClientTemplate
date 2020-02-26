using System;
using UnityEngine;

namespace MiniIT.Snipe
{
	public class BindProvider : AuthProvider
	{
		protected Action<string> mBindResultCallback;
		protected Action<BindProvider, bool, bool> mCheckAuthExistsCallback;

		public string BindDonePrefsKey
		{
			get { return SnipePrefs.AUTH_BIND_DONE + ProviderId; }
		}

		public bool IsBindDone
		{
			get { return PlayerPrefs.GetInt(BindDonePrefsKey, 0) == 1; }
		}

		public virtual void RequestBind(Action<string> bind_callback = null)
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

		public virtual bool CheckAuthExists(Action<BindProvider, bool, bool> callback)
		{
			// Override this method.
			return false;
		}

		protected virtual void CheckAuthExists(string user_id, Action<BindProvider, bool, bool> callback)
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
			InvokeBindResultCallback(data?.SafeGetString("errorCode"));
		}

		protected virtual void OnCheckAuthExistsResponse(ExpandoObject data)
		{
			if (mCheckAuthExistsCallback != null)
				mCheckAuthExistsCallback.Invoke(this, data.SafeGetString("errorCode") == "ok", data.SafeGetValue("isSame", false)); //(error_code == "noSuchAuth")

			mCheckAuthExistsCallback = null;
		}

		protected virtual void InvokeBindResultCallback(string error_code)
		{
			if (mBindResultCallback != null)
				mBindResultCallback.Invoke(error_code);

			mBindResultCallback = null;
		}

		public override void Dispose()
		{
			mBindResultCallback = null;

			base.Dispose();
		}
	}
}