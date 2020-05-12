using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MiniIT;

public class SnipeConfig
{
	public static SnipeConfig Instance
	{
		get;
#if !UNITY_EDITOR
		private
#endif
		set;
	}

	public string snipe_client_key;
	public string snipe_app_info;
	public SnipeServerConfig server;
	public SnipeServerConfig auth;

	public string snipe_service_websocket;
	public List<string> tables_path = new List<string>();

	public static void InitFromJSON(string json_string)
	{
		Init(ExpandoObject.FromJSONString(json_string));
	}

	public static void Init(ExpandoObject data)
	{
		Instance = new SnipeConfig();
		Instance.snipe_client_key = data.SafeGetString("snipe_client_key");
		Instance.snipe_service_websocket = data.SafeGetString("snipe_service_websocket", Instance.snipe_service_websocket);
		Instance.server = new SnipeServerConfig(data.SafeGetValue<ExpandoObject>("server"));
		Instance.auth = new SnipeServerConfig(data.SafeGetValue<ExpandoObject>("auth"));

 		if (Instance.tables_path == null)
			Instance.tables_path = new List<string>();
		else
			Instance.tables_path.Clear();
		if (data["tables_path"] is IList list)
		{
			foreach(string path in list)
			{
				Instance.tables_path.Add(path);
			}
		}

		Instance.InitAppInfo();
	}
	
	private void InitAppInfo()
	{
		this.snipe_app_info = new ExpandoObject()
		{
			["identifier"] = Application.identifier,
			["version"] = Application.version,
			["platform"] = Application.platform.ToString(),
		}.ToJSONString();
	}

	public string GetTablesPath()
	{
		if (tables_path != null && tables_path.Count > 0)
			return tables_path[0];

		return null;
	}
}

[System.Serializable]
public class SnipeServerConfig
{
	public string host;
	public int port;
	public string websocket;

	public SnipeServerConfig() { }

	public SnipeServerConfig(ExpandoObject data)
	{
		if (data != null)
		{
			host = data.SafeGetString("host");
			port = Convert.ToInt32(data["port"]);
			websocket = data.SafeGetString("websocket");
		}
	}
}
