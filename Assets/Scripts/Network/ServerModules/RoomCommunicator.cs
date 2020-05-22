using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using MiniIT;
using MiniIT.Snipe;

public partial class RoomCommunicator : SnipeRoomCommunicator
{
	public const string ROOM_TYPE = "race";
	
	private Server mServer;
	
	public override void StartCommunicator()
	{
		mServer = mGameCommunicator as Server;
		base.StartCommunicator();
	}

	protected override void ProcessSnipeMessage(ExpandoObject data, bool original = false)
	{
		base.ProcessSnipeMessage(data, original);

		string message_type = data.SafeGetString("type");
		string error_code = data.SafeGetString("errorCode");

		switch(message_type)
		{
			case "race.prestart":
				OnRacePrestart(data);
				break;

			case "race.go":
				OnRaceGo(data);
				break;
			
			case "race.finish":
				OnRoomRaceFinish(data);
				break;
		}
	}
	
	protected override void OnRoomJoin(ExpandoObject data)
	{
		string error_code = data.SafeGetString("errorCode");
		
		if (error_code == "ok")
		{
			mServer.Race.Joined = true;
			mServer.Race.Active = true;

			//mServer.DispatchEvent(mServer.RaceJoined);
		}

		base.OnRoomJoin(data);
	}

	protected override void OnRoomLeft(ExpandoObject data)
	{
		// opponent disconnected

		//mServer.DispatchEvent(mServer.RaceOpponentLeft);

		//OnRoomDead(data);

		base.OnRoomLeft(data);
	}

	protected override void OnRoomDead(ExpandoObject data)
	{
		// kit/room.dead идет при любой смерти комнаты, в том числе и когда гонка завершилась корректно

		//if (mServer.Race.Joined)
		//	mServer.DispatchEvent(mServer.RaceRoomDead);

		base.OnRoomDead(data);
	}

	private void OnRacePrestart(ExpandoObject data)
	{
		//RaceState.RaceReady = true;
		//mServer.DispatchEvent(mServer.RacePrestart);
	}

	private void OnRaceGo(ExpandoObject data)
	{
		//mServer.Pers.PlayerData.Energy--;

		//mServer.DispatchEvent(mServer.RaceStarted);
	}

	private void OnRoomRaceFinish(ExpandoObject data)
	{
		// race.finish из рума приходит раньше, чем race.finish из гейма
		mServer.Race.Joined = false; // чтобы  kit/room.dead не привел к отображению ошибки
	}

	protected override void OnRoomBroadcast(string error_code, ExpandoObject msg)
	{
		base.OnRoomBroadcast(error_code, msg);

		if (msg != null)
		{
			// processing broadcast message
		}
	}
}
