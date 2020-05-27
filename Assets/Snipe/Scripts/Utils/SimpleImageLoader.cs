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
		private static Dictionary<string, SimpleImageLoader> mActiveLoaders;

		private const int MAX_LOADERS_COUNT = 3;
		private static int mLoadersCount = 0;

#if UNITY_EDITOR
		public string Url;
#else
		public string Url { get; private set; }
#endif
		private bool mUseCache = false;

		private Action<Texture2D> mCallback;
		private List<SimpleImageLoader> mParasiteLoaders;

		public static SimpleImageLoader Load(string url, Action<Texture2D> callback = null, bool cache = false)
		{
			if (string.IsNullOrWhiteSpace(url))
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
			if (mActiveLoaders != null && mActiveLoaders.TryGetValue(url, out var master_loader) && master_loader != null)
			{
				loader.Url = url;
				loader.mCallback = callback;

				if (master_loader.mParasiteLoaders == null)
					master_loader.mParasiteLoaders = new List<SimpleImageLoader>();
				master_loader.mParasiteLoaders.Add(loader);
			}
			else
			{
				loader.DoLoad(url, callback);
			}
			return loader;
		}

		public void Cancel()
		{
			mCallback = null;

			if (!mUseCache && (mParasiteLoaders == null || mParasiteLoaders.Count < 1))
			{
				StopAllCoroutines();
				Destroy(this);
			}
		}

		private void DoLoad(string url, Action<Texture2D> callBack)
		{
			if (mActiveLoaders == null)
				mActiveLoaders = new Dictionary<string, SimpleImageLoader>();
			mActiveLoaders[url] = this;

			StartCoroutine(LoadCoroutine(url, callBack));
		}

		private IEnumerator LoadCoroutine(string url, Action<Texture2D> callback)
		{
			Url = url;
			mCallback = callback;

			while (mLoadersCount >= MAX_LOADERS_COUNT)
				yield return 0;
			mLoadersCount++;

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

					if (mParasiteLoaders != null)
					{
						foreach (var parasite in mParasiteLoaders)
						{
							if (parasite != null)
							{
								parasite.mCallback?.Invoke(texture);
								Destroy(parasite);
							}
						}
						mParasiteLoaders = null;
					}
				}
			}

			mLoadersCount--;

			mActiveLoaders?.Remove(url);
			Destroy(this);
		}
	}
}