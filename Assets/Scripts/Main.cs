using System.Collections;
using System.Globalization;
using System.Threading;
using UnityEngine;
using MiniIT;
using MiniIT.Snipe;

public class Main : MonoBehaviour
{
	private void Awake()
	{
		Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
	}

	private void Start()
	{
		InitConfig();
		InitAuth();

		Server server = new GameObject("SnipeCommunicator").AddComponent<Server>();
		server.LoginSucceeded += OnLogin;
		server.ConnectionFailed += OnConnectionFailed;
		server.StartCommunicator();
	}

	private void InitConfig()
	{
		// In real project it is better to use an external file.
		// Load it and initialize Snipe using SnipeConfig.Init() or SnipeConfig.InitFromJSON()

		// Hardcoded config values
		var config = new ExpandoObject()
		{
			["snipe_client_key"] = "client-vs8mf4ps9e2j",

			["auth"] = new ExpandoObject()
			{
				["host"] = "dev.snipe.dev",
				["port"] = 10000,
				["websocket"] = "wss://dev.snipe.dev/wss_10000/",
			},

			["server"] = new ExpandoObject()
			{
				["host"] = "dev.snipe.dev",
				["port"] = 10100,
				["websocket"] = "wss://dev.snipe.dev/wss_10100/",
			},
		};

		SnipeConfig.Init(config);
	}

	private void InitAuth()
	{
		// Initialization of AuthProviders

		SnipeAuthCommunicator.AddAuthProvider<AdvertisingIdAuthProvider>();

#if UNITY_ANDROID
		SnipeAuthCommunicator.AddAuthProvider<GooglePlayAuthProvider>();
#elif UNITY_IOS
		SnipeAuthCommunicator.AddAuthProvider<AppleGameCenterAuthProvider>();
#endif
	}

	private void OnLogin()
	{
		if (SnipeAuthCommunicator.JustRegistered)
		{
			// Do something related to the first session
			// ...
		}

		// Do something
		// ...
	}

	private void OnConnectionFailed()
	{
		Debug.Log("Connection failed");

		// Do something
		// ...
	}

	private void OnDestroy()
	{
		if (Server.Instance != null)
		{
			Server.Instance.LoginSucceeded -= OnLogin;
			Server.Instance.ConnectionFailed -= OnConnectionFailed;
		}
	}

	private void OnGUI()
	{
		if (Server.Instance != null)
		{
			GUI.Label(new Rect(50, 50, 200, 50), Server.Instance.LoggedIn ? $"Hello {SnipeAuthCommunicator.UserID}" : "You are not logged in");
			GUI.Label(new Rect(50, 70, 200, 50), SnipeAuthCommunicator.JustRegistered ? "New account registered" : "Welcome back!");

			if (Server.Instance.LoggedIn && Server.Instance.Player.PlayerData != null)
			{
				GUI.Label(new Rect(50, 150, 200, 50), $"You have {Server.Instance.Player.PlayerData.MoneySoft} coins");
				GUI.Label(new Rect(50, 170, 200, 50), $"You have {Server.Instance.Player.PlayerData.MoneyHard} gold");
				GUI.Label(new Rect(50, 190, 200, 50), $"Energy: {Server.Instance.Player.PlayerData.Energy} / {Server.Instance.Static.MaxEnergy}");

				GUI.Label(new Rect(50, 220, 200, 50), $"VIP time left: {Server.Instance.Player.PlayerData.VipTimeLeft.ToString("F0")} seconds");

				if (Server.Instance.Player.PlayerData.VipTimeLeft <= 0.0f && GUI.Button(new Rect(50, 250, 200, 50), "Buy VIP"))
				{
					Server.Instance.Player.BuyVip();
				}

				if (!Server.Instance.Race.Active)
				{
					if (GUI.Button(new Rect(50, 400, 200, 50), "Race"))
					{
						Server.Instance.Race.MatchmakingAdd();
					}
				}
			}
		}
	}
}
