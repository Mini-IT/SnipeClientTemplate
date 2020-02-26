using System;
using System.Collections;
using System.Collections.Generic;
using MiniIT;
using MiniIT.Snipe;
using UnityEngine;

public class RaceModule : RoomModule
{
	private const string ROOM_TYPE = "race";

	public RaceModule(Server server) : base(server)
	{
	}

	//protected override void OnRoomLeft(ExpandoObject data)
	//{
	//	// opponent disconnected
	//	//.... add some custon logic if needed
	//  
	//  base.OnRoomLeft(data);
	//}

	//protected override void OnRoomDead(ExpandoObject data)
	//{
	//	// NOTE:
	//	// "kit/room.dead" is sent on any room death, even when all procedures are finished correctly
	//
	//	if (Joined)
	//		mServer.DispatchEvent(mServer.RaceRoomDead);
	//
	//	base.OnRoomDead(data);
	//}

	//protected override void ProcessRoomBroadcastMessage(ExpandoObject message_data)
	//{
	//}

	public void MatchmakingAdd()
	{
		//if (!CheckEnergy())
		//{
		//	mServer.DispatchEvent(mServer.NotEnoughEnergy);
		//	return;
		//}

		ExpandoObject data = new ExpandoObject()
		{
			["typeID"] = ROOM_TYPE,
			["carID"] = 1,
			["trackID"] = 1
		};

		base.MatchmakingAdd(data,
			(response) => { Debug.Log("Mathchmaking succeeded"); },
			(response) => { Debug.Log("Mathchmaking failed"); });
	}

	public void SendRaceInitialized()
	{
		if (Joined && Active && mRoomClient != null && mRoomClient.Connected)
		{
			ExpandoObject request_data = new ExpandoObject();
			request_data["actionID"] = "race.initialized";
			mRoomClient.Request("kit/room.event", request_data);
		}
	}

}

