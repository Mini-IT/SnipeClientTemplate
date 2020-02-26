using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using MiniIT;
using MiniIT.Snipe;

public partial class RoomCommunicator : SnipeCommunicator
{
	public const string ROOM_TYPE = "race";
	
	internal Server mServer;
	internal string mHost;
	internal int mPort;
	internal string mWebSocketUrl;

	protected override void InitClient()
	{
		InitClient(mHost, mPort, mWebSocketUrl);
	}

	protected override void ProcessSnipeMessage(ExpandoObject data, bool original = false)
	{
		base.ProcessSnipeMessage(data, original);

		#if UNITY_EDITOR
		Debug.Log("[RoomCommunicator] OnRoomResponse: " + (data != null ? data.ToJSONString() : "null"));
#endif

		string message_type = data.SafeGetString("type");
		string error_code = data.SafeGetString("errorCode");

		switch(message_type)
		{
			case "user.login":
				if (error_code == "ok")
				{
					OnLogin(data);
				}
				//else if (data.errorCode == "userAlreadyLogin")
				break;
					
			case "kit/room.join":
				OnRoomJoin(data);
				break;
				
			case "kit/room.leave":
			case "kit/user.logout":
				OnRoomLeave(data);
				break;

			case "kit/room.left":
				OnRoomLeft(data);
				break;

			case "kit/room.dead":
				OnRoomDead(data);
				break;

			case "race.prestart":
				OnRacePrestart(data);
				break;

			case "race.go":
				OnRaceGo(data);
				break;
			
			case "race.finish":
				OnRoomRaceFinish(data);
				break;

			case "kit/room.broadcast":
				OnRoomBroadcast(data);
				break;
		}
	}
	
	protected virtual void OnLogin(ExpandoObject data)
	{
		// join room
		Request("kit/room.join", new ExpandoObject()
		{
			["typeID"] = ROOM_TYPE,
			["roomID"] = mServer.Race.mRoomID,
		});
	}
	
	private void OnRoomJoin(ExpandoObject data)
	{
		string error_code = data.SafeGetString("errorCode");
		
		if (error_code == "ok")
		{
			mServer.Race.Joined = true;
			mServer.Race.Active = true;

			//mServer.DispatchEvent(mServer.RaceJoined);
		}
	}
	
	private void OnRoomLeave(ExpandoObject data)
	{
		Dispose();
	}

	private void OnRoomLeft(ExpandoObject data)
	{
		// opponent disconnected
	}

	private void OnRoomDead(ExpandoObject data)
	{
		// kit/room.dead идет при любой смерти комнаты, в том числе и когда гонка завершилась корректно

		//if (mServer.Race.Joined)
		//	mServer.DispatchEvent(mServer.RaceRoomDead);

		Dispose();
	}

	private void OnRacePrestart(ExpandoObject data)
	{
		
	}

	private void OnRaceGo(ExpandoObject data)
	{
		
	}

	private void OnRoomRaceFinish(ExpandoObject data)
	{
		// race.finish из рума приходит раньше, чем race.finish из гейма
		mServer.Race.Joined = false; // чтобы  kit/room.dead не привел к отображению ошибки
	}

	private void OnRoomBroadcast(ExpandoObject data)
	{
		string error_code = data.SafeGetString("errorCode");
		
		if (error_code == "ok" && data.ContainsKey("msg"))
		{
			ExpandoObject message_data = ExpandoObject.FromJSONString(data["msg"] as string);

			if ((int)message_data["id"] != mServer.Player.PlayerData.Id)
			{
				ProcessRoomBroadcastMessage(message_data);
			}
		}
	}
	
	protected virtual void ProcessRoomBroadcastMessage(ExpandoObject message_data)
	{
	}
}
