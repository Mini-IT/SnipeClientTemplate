using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Collections.Generic;

namespace MiniIT.Utils
{
	public class SimpleImageLoader : MonoBehaviour
	{
		private static GameObject mGameObject;
		private static Dictionary<string, Texture2D> mCache;

		public string Url; // { get; private set; }
		private bool mUseCache = false;

		private Action<Texture2D> mCallback;

		public static SimpleImageLoader Load(string url, Action<Texture2D> callback = null, bool cache = false)
		{
			if (url == "")
				return null;

			if (cache)
			{
				if (mCache != null)
				{
					Texture2D texture;
					if (mCache.TryGetValue(url, out texture))
					{
						callback?.Invoke(texture);
						return null;
					}
				}
			}

			if (mGameObject == null)
			{
				mGameObject = new GameObject("MiniIT.Utils.SimpleImageLoader");
				DontDestroyOnLoad(mGameObject);
			}

			var loader = mGameObject.AddComponent<SimpleImageLoader>();
			loader.mUseCache = cache;
			loader.DoLoad(url, callback);
			return loader;
		}

		//public void Dispose()
		//{
		//	StopAllCoroutines();
		//	Destroy(this);
		//}
		public void Cancel()
		{
			mCallback = null;

			if (!mUseCache)
			{
				StopAllCoroutines();
				Destroy(this);
			}
		}

		private void DoLoad(string url, Action<Texture2D> callBack)
		{
			StartCoroutine(LoadCoroutine(url, callBack));
		}

		private IEnumerator LoadCoroutine(string url, Action<Texture2D> callback)
		{
			Url = url;
			mCallback = callback;

			using (UnityWebRequest loader = new UnityWebRequest(url))
			{
				loader.downloadHandler = new DownloadHandlerTexture();
				yield return loader.SendWebRequest();

				if (loader.isNetworkError || loader.isHttpError)
				{
					Debug.Log("[SimpleImageLoader] Error loading image: " + url);
				}
				else
				{
					Texture2D texture = ((DownloadHandlerTexture)loader.downloadHandler).texture;

					if (mUseCache)
					{
						if (mCache == null)
							mCache = new Dictionary<string, Texture2D>();
						mCache[Url] = texture;
					}

					if (mCallback != null)
					{
						mCallback.Invoke(texture);
						mCallback = null;
					}
				}
			}

			Destroy(this);
		}
	}
}