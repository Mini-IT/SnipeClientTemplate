using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using MiniIT;
using MiniIT.Snipe;

public partial class Server : SnipeCommunicator
{
	public static Server Instance { get; private set; }

	internal List<ServerModule> Modules { get; private set; }
	public PlayerModule Player { get; private set; }
	public StaticModule Static { get; private set; }
	public LogicModule Logic { get; private set; }
	public PaymentModule Payment { get; private set; }
	public RaceModule Race { get; private set; }
	public RatingModule Rating { get; private set; }

	private Timer mSecondsTimer;

	public override void StartCommunicator()
	{
		base.StartCommunicator();
		
		Instance = this;
		
		this.DebugEnabled = true;

		Modules = new List<ServerModule>();
		this.Player = new PlayerModule(this);
		this.Static = new StaticModule(this);
		this.Logic = new LogicModule(this);
		this.Payment = new PaymentModule(this);
		this.Race = new RaceModule(this);
		this.Rating = new RatingModule(this);

		StartSecondsTimer();
	}
	
	protected override void OnDestroy()
	{
		if (Instance == this)
			Instance = null;

		base.OnDestroy();
	}

	protected override void OnConnectionSucceeded(ExpandoObject data)
	{
		Debug.Log("[Server] OnConnectionSucceeded");
		StartSecondsTimer();

		base.OnConnectionSucceeded(data);
	}

	protected override void OnConnectionFailed(ExpandoObject data = null)
	{
		Debug.Log("[Server] OnConnectionFailed");
		StopSecondsTimer();

		base.OnConnectionFailed(data);
	}

	private void StartSecondsTimer()
	{
		if (mSecondsTimer == null)
			mSecondsTimer = new Timer(OnSecondsTimerTick, null, 0, 1000);
	}

	private void StopSecondsTimer()
	{
		if (mSecondsTimer == null)
			return;

		mSecondsTimer.Dispose();
		mSecondsTimer = null;
	}

	private void OnSecondsTimerTick(object state = null)
	{
		if (Modules != null)
		{
			for (int i = 0; i < Modules.Count; i++)
			{
				ServerModule module = Modules[i];
				if (module != null)
					module.OnSecondsTimerTick();
			}
		}
	}

	private void NotifyModulesOnResponse(ExpandoObject data, bool original = false)
	{
		if (Modules != null)
		{
			for (int i = 0; i < Modules.Count; i++)
			{
				ServerModule module = Modules[i];
				if (module != null)
					module.OnResponse(data, original);
			}
		}
	}

	protected override void ProcessSnipeMessage(ExpandoObject data, bool original = false)
	{
		base.ProcessSnipeMessage(data, original);

		NotifyModulesOnResponse(data, true);
		DispatchEvent(DataReceived, data);

		// Here you can place a reaction to received server messages.
		// For example "notEnoughMoney" error can be handled here (regardless of what was requested)

		string error_code = data.SafeGetString("errorCode");
		if (error_code == "notEnoughMoney")
		{
			DispatchEvent(NotEnoughMoney, data);
		}

		// Or you can handle any other server messages
		//
		//string message_type = data.SafeGetString("type");
		//switch(message_type)
		//{
		//	case "kit/attr.getAll":
		//		OnResponseUserInfo(data);
		//		break;
		//}
	}
}
