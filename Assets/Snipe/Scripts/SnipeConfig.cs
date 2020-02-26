using System;
using UnityEngine;
using MiniIT;

[System.Serializable]
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

	public static void InitFromJSON(string json_string)
	{
		Instance = JsonUtility.FromJson<SnipeConfig>(json_string);
		Instance.InitAppInfo();
	}

	public static void Init(ExpandoObject data)
	{
		Instance = new SnipeConfig();
		Instance.snipe_client_key = data.SafeGetString("snipe_client_key");
		Instance.server = new SnipeServerConfig(data.SafeGetValue<ExpandoObject>("server"));
		Instance.auth = new SnipeServerConfig(data.SafeGetValue<ExpandoObject>("auth"));
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
