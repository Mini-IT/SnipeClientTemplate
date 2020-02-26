using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;

namespace MiniIT.Utils
{
	public class SimpleImageLoader : MonoBehaviour
	{
		private static SimpleImageLoader mInstance;
		private static GameObject mInstanceGameObject;

		public static void Load(string url, Action<Texture2D> callBack = null)
		{
			if (url == "")
				return;

			if (mInstance == null)
			{
				mInstanceGameObject = new GameObject("MiniIT.Utils.SimpleImageLoader");
				mInstance = mInstanceGameObject.AddComponent<SimpleImageLoader>();
			}

			mInstance.DoLoad(url, callBack);
		}
		
		private void DoLoad(string url, Action<Texture2D> callBack)
		{
			StartCoroutine(LoadCoroutine(url, callBack));
		}

		private IEnumerator LoadCoroutine(string url, Action<Texture2D> callBack)
		{
			using (UnityWebRequest loader = new UnityWebRequest(url))
			{
				loader.downloadHandler = new DownloadHandlerTexture();
				yield return loader.SendWebRequest();

				if (loader.isNetworkError || loader.isHttpError)
				{
					Debug.Log("[SimpleImageLoader] Error loading image: " + url);
				}
				else if (callBack != null)
				{
					callBack.Invoke(((DownloadHandlerTexture)loader.downloadHandler).texture);
				}
			}
			//Resources.UnloadUnusedAssets();
		}
	}
}