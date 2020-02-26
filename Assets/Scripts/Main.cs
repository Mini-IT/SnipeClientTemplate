using System.Collections;
using System.Globalization;
using System.Threading;
using UnityEngine;
using MiniIT;
using MiniIT.Snipe;

public class Main : MonoBehaviour
{
	private Server mServer;

	private void Awake()
	{
		Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
	}

	private void Start()
	{
		InitConfig();
		InitAuth();

		mServer = new GameObject("SnipeCommunicator").AddComponent<Server>();
		mServer.LoginSucceeded += OnLogin;
		mServer.ConnectionFailed += OnConnectionFailed;
		mServer.StartCommunicator();
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

		SnipeAuthCommunicator.Instance.AddAuthProvider<AdvertisingIdAuthProvider>();

#if UNITY_ANDROID
		SnipeAuthCommunicator.Instance.AddAuthProvider<GooglePlayAuthProvider>();
#elif UNITY_IOS
		SnipeAuthCommunicator.Instance.AddAuthProvider<AppleGameCenterAuthProvider>();
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
		if (mServer != null)
		{
			mServer.LoginSucceeded -= OnLogin;
			mServer.ConnectionFailed -= OnConnectionFailed;
		}
	}

	private void OnGUI()
	{
		if (mServer != null)
		{
			GUI.Label(new Rect(50, 50, 200, 50), mServer.LoggedIn ? $"Hello {mServer.UserID}" : "You are not logged in");
			GUI.Label(new Rect(50, 70, 200, 50), SnipeAuthCommunicator.JustRegistered ? "New account registered" : "Welcome back!");

			if (mServer.LoggedIn && mServer.Player.PlayerData != null)
			{
				GUI.Label(new Rect(50, 150, 200, 50), $"You have {mServer.Player.PlayerData.MoneySoft} coins");
				GUI.Label(new Rect(50, 170, 200, 50), $"You have {mServer.Player.PlayerData.MoneyHard} gold");
				GUI.Label(new Rect(50, 190, 200, 50), $"Energy: {mServer.Player.PlayerData.Energy} / {mServer.Static.MaxEnergy}");

				GUI.Label(new Rect(50, 220, 200, 50), $"VIP time left: {mServer.Player.PlayerData.VipTimeLeft.ToString("F0")} seconds");

				if (mServer.Player.PlayerData.VipTimeLeft <= 0.0f && GUI.Button(new Rect(50, 250, 200, 50), "Buy VIP"))
				{
					mServer.Player.BuyVip();
				}

				if (!mServer.Race.Active)
				{
					if (GUI.Button(new Rect(50, 400, 200, 50), "Race"))
					{
						mServer.Race.MatchmakingAdd();
					}
				}
			}
		}
	}
}
