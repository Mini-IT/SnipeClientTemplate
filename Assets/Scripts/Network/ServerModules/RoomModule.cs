using System;
using System.Collections;
using System.Collections.Generic;
using MiniIT;
using MiniIT.Snipe;
using UnityEngine;

public class RoomModule : ServerModule
{
	public Dictionary<int, ExpandoObject> RoomUsersData { get; protected set; }
	
	public bool Joined { get; internal set; }	
	public bool Active { get; set; }

	protected bool mMatchmaking = false;
	protected string mRoomType;
	internal int mRoomID;
	protected RoomCommunicator mRoomClient;

	public RoomModule(Server server) : base(server)
	{
	}
	
	public void MatchmakingAdd(ExpandoObject request_data, Action<ExpandoObject> success_callback, Action<ExpandoObject> fail_callback)
	{
		mMatchmaking = true;
		mServer.CreateRequest("kit/matchmaking.add", request_data).Request( (data) =>
		{
			string error_code = data.SafeGetString("errorCode");
			
			if (error_code == "ok") // added to the queue
			{
				Joined = false;
				Active = true;

				if (success_callback != null)
					success_callback.Invoke(data);
			}
			else
			{
				Joined = false;
				Active = false;

				if (fail_callback != null)
					fail_callback.Invoke(data);
			}
		});
	}

	public void MatchmakingRemove()
	{
		if (!mMatchmaking)
			return;
		
		mServer.Request("kit/matchmaking.remove");
		mMatchmaking = false;
	}

	#region Response handling

	internal override void OnResponse(ExpandoObject data, bool original = false)
	{
		string message_type = data.SafeGetString("type");
		//string error_code = data.SafeGetString("errorCode");

		switch (message_type)
		{
			case "kit/matchmaking.start":
				OnMatchmakingStart(data);
				break;

			case "kit/matchmaking.remove":
				OnMatchmakingRemove(data);
				break;
		}
	}
	
	protected virtual void OnMatchmakingStart(ExpandoObject data)
	{
		if (data.SafeGetString("errorCode") == "ok")  // matchmaking is finished
		{
			mMatchmaking = false;
			
			mRoomID = data.SafeGetValue<int>("roomID");

			if (RoomUsersData == null)
				RoomUsersData = new Dictionary<int, ExpandoObject>();
			else
				RoomUsersData.Clear();
			
			if (data["users"] is IList users_data)
			{
				foreach(ExpandoObject user_data in users_data)
				{
					int user_id = Convert.ToInt32(user_data["id"]);
					if (user_id > 0)
					{
						RoomUsersData[user_id] = user_data;
					}
				}
			}
			
			mRoomClient = new GameObject("SnipeRoom").AddComponent<RoomCommunicator>();
			mRoomClient.mServer = mServer;
			mRoomClient.mHost = data.SafeGetString("host");
			mRoomClient.mPort = data.SafeGetValue<int>("port");
			mRoomClient.mWebSocketUrl = data.SafeGetString("webSocket");
			mRoomClient.StartCommunicator();
		}
	}

	protected virtual void OnMatchmakingRemove(ExpandoObject data)
	{
		
	}

	protected virtual void DisposeRoomClient()
	{
		Joined = false;

		if (mRoomClient != null)
		{
			mRoomClient.Dispose();
			mRoomClient = null;
		}
	}

	#endregion

	#region

	public void RoomBroadcast(ExpandoObject data)
	{
		if (mRoomClient == null)
			return;
		
		data["id"] = mServer.Player.PlayerData.Id;
		
		ExpandoObject request_data = new ExpandoObject();
		request_data["msg"] = data.ToJSONString();
		mRoomClient.Request("kit/room.broadcast", request_data);
	}

	public virtual void Logout()
	{
		MatchmakingRemove();

		Joined = false;

		if (mRoomClient != null)
		{
			if (mRoomClient.Connected)
				mRoomClient.Request("kit/user.logout");
			else
				DisposeRoomClient();
		}
	}
	
	#endregion
}

