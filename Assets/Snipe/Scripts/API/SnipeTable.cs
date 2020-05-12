using MiniIT;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEngine;
using UnityEngine.Networking;

namespace MiniIT.Snipe
{
	public class SnipeTable
	{
		//public static string Path;

		private const int MAX_LOADERS_COUNT = 5;
		private static int mLoadersCount = 0;

		public delegate void LoadingFinishedHandler(bool success);
		public event LoadingFinishedHandler LoadingFinished;

		public bool Loaded { get; private set; } = false;
		public bool LoadingFailed { get; private set; } = false;

		internal IEnumerator LoadTableCoroutine(string table_name)
		{
			while (mLoadersCount >= MAX_LOADERS_COUNT)
				yield return 0;
			mLoadersCount++;

			string url = string.Format("{0}/{1}.json.gz", SnipeConfig.Instance.GetTablesPath(), table_name);
			UnityEngine.Debug.Log("[SnipeTable] Loading table " + url);

			this.LoadingFailed = false;

			int retry = 0;
			while (!this.Loaded && retry <= 2)
			{
				if (retry > 0)
				{
					yield return new WaitForSecondsRealtime(0.1f);
					UnityEngine.Debug.Log($"[SnipeTable] Retry #{retry} to load table - {table_name}");
				}

				retry++;

				using (UnityWebRequest loader = new UnityWebRequest(url))
				{
					loader.downloadHandler = new DownloadHandlerBuffer();
					yield return loader.SendWebRequest();
					if (loader.isNetworkError || loader.isHttpError)
					{
						UnityEngine.Debug.Log("[SnipeTable] Network error: Failed to load table - " + table_name);
					}
					else
					{
						UnityEngine.Debug.Log("[SnipeTable] table file loaded - " + table_name);
						try
						{
							if (loader.downloadHandler.data == null || loader.downloadHandler.data.Length < 1)
							{
								UnityEngine.Debug.Log("[SnipeTable] Error: loaded data is null or empty. Table: " + table_name);
							}
							else
							{
								using (GZipStream gzip = new GZipStream(new MemoryStream(loader.downloadHandler.data, false), CompressionMode.Decompress))
								{
									using (StreamReader reader = new StreamReader(gzip))
									{
										string json_string = reader.ReadToEnd();
										ExpandoObject data = ExpandoObject.FromJSONString(json_string);

										if (data["list"] is List<object> list)
										{
											foreach (ExpandoObject item_data in list)
											{
												AddTableItem(item_data);
											}
										}

										UnityEngine.Debug.Log("[SnipeTable] table ready - " + table_name);
										this.Loaded = true;
									}
								}
							}
						}
						catch (Exception)
						{
							UnityEngine.Debug.Log("[SnipeTable] failed to parse table - " + table_name);
						}
					}
				}
			}

			this.LoadingFailed = !this.Loaded;
			LoadingFinished?.Invoke(this.Loaded);

			mLoadersCount--;
		}

		protected virtual void AddTableItem(ExpandoObject item_data)
		{
			// override this method
		}
	}
}