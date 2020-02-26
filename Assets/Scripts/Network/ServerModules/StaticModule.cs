using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using MiniIT;
using UnityEngine;
using UnityEngine.Networking;

public class StaticModule : ServerModule
{
	//private const string TABLE_NAME_CARS = "cars";

	//public Dictionary<int, CarItem> Cars { get; protected set; }

	public int BaseMaxEnergy { get; private set; }
	public int VipEnergyBonus { get; private set; }
	public Price VipPrice { get; private set; }

	public int MaxEnergy
	{
		get
		{
			return this.BaseMaxEnergy + ((mServer.Player.PlayerData.VipTimeLeft > 0.0f) ? VipEnergyBonus : 0);
		}
	}

	public StaticModule(Server server) : base(server)
	{
		//Cars = new Dictionary<int, CarItem>();
	}
	

	internal override void OnResponse(ExpandoObject data, bool original = false)
	{
		string message_type = data.SafeGetString("type");
		string error_code = data.SafeGetString("errorCode");
		
		switch(message_type)
		{
			case "user.login":
				if (error_code == "ok")
				{
					OnLogin(data);
				}
				break;

			case "kit/vars.getAll":
				OnGetVars(data);
				break;
		}
	}
	
	public void OnLogin(ExpandoObject data)
	{
		mServer.Request("kit/vars.getAll");

		//mServer.StartCoroutine(LoadTables());
	}

	private void OnGetVars(ExpandoObject data)
	{
		if (data != null && data["data"] is IList list)
		{
			foreach (ExpandoObject item in list)
			{
				string key = item.SafeGetString("key");
				if (!string.IsNullOrEmpty(key))
				{
					switch (key)
					{
						case "user.energy":
							BaseMaxEnergy = item.SafeGetValue<int>("val", 5);
							break;

						case "vip.energy":
							VipEnergyBonus = item.SafeGetValue<int>("val", 5);
							break;

						case "vip.price":
							VipPrice = new Price(item.SafeGetValue<int>("val", 0), true);
							break;
					}
				}
			}
		}
	}

	//private IEnumerator LoadTables()
	//{
	//	Coroutine task1 = mServer.StartCoroutine(LoadTableCoroutine<CarItem>(TABLE_NAME_CARS, Cars));
	//	Coroutine task2 = mServer.StartCoroutine(LoadTableCoroutine<CarPartItem>(TABLE_NAME_PARTS, Parts));
	//	yield return task1;
	//	yield return task2;
	//}

	//private IEnumerator LoadTableCoroutine<T>(string table_name, Dictionary<int, T> collection) where T : TableItem, new()
	//{
	//	string url = $"{AppConfig.Instance.tables_path}/{table_name}.json.gz";
	//	UnityEngine.Debug.Log("[StaticModule] Loading table " + url);

	//	using (UnityWebRequest loader = new UnityWebRequest(url))
	//	{
	//		loader.downloadHandler = new DownloadHandlerBuffer();
	//		yield return loader.SendWebRequest();
	//		if (loader.isNetworkError || loader.isHttpError)
	//		{
	//			UnityEngine.Debug.Log("[StaticModule] Network error: Failed to load table - " + table_name);
	//		}
	//		else
	//		{
	//			UnityEngine.Debug.Log("[StaticModule] table file loaded - " + table_name);
	//			try
	//			{
	//				using (GZipStream gzip = new GZipStream(new MemoryStream(loader.downloadHandler.data, false), CompressionMode.Decompress))
	//				{
	//					using (StreamReader reader = new StreamReader(gzip))
	//					{
	//						string json_string = reader.ReadToEnd();
	//						ExpandoObject data = ExpandoObject.FromJSONString(json_string);
	//						UpdateTableItems<T>(data, collection);

	//						UnityEngine.Debug.Log("[StaticModule] table ready - " + table_name);
	//					}
	//				}
	//			}
	//			catch(Exception)
	//			{
	//				UnityEngine.Debug.Log("[StaticModule] failed - " + table_name);
	//			}
	//		}
	//	}
	//}

	//private void UpdateTableItems<T>(ExpandoObject data, Dictionary<int, T> collection) where T : TableItem, new()
	//{
	//	List<object> list = data["list"] as List<object>;
	//	if (list == null)
	//		return;

	//	foreach (ExpandoObject item_data in list)
	//	{
	//		T item = new T();
	//		item.UpdateData(item_data);
	//		if (item.Id > 0)
	//		{
	//			collection[item.Id] = item;
	//		}
	//	}
	//}

	//public CarItem GetCarByID(int car_id)
	//{
	//	if (Cars != null)
	//	{
	//		CarItem item = null;
	//		return Cars.TryGetValue(car_id, out item) ? item : null;
	//	}

	//	return null;
	//}

}
