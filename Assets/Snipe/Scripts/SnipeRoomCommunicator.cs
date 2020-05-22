using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace MiniIT.Snipe
{
	public class SnipeRoomCommunicator : SnipeCommunicator
	{
		protected SnipeCommunicator mGameCommunicator;

		protected string mRoomType;
		protected int mRoomId;

		protected string mHost;
		protected int mPort;
		protected string mWebSocketUrl;

		public static T Create<T>(string room_type, int room_id, SnipeCommunicator game_communicator, string host, int port, string websocket_url) where T : SnipeRoomCommunicator
		{
			var communicator = new GameObject("SnipeRoomCommunicator").AddComponent<T>();
			//communicator.mServer = mServer;
			communicator.mRoomType = room_type;
			communicator.mRoomId = room_id;
			communicator.mHost = host;
			communicator.mPort = port;
			communicator.mWebSocketUrl = websocket_url;
			if (string.IsNullOrEmpty(communicator.mWebSocketUrl) && !string.IsNullOrEmpty(SnipeConfig.Instance?.server.websocket))
				communicator.mWebSocketUrl = Regex.Replace(SnipeConfig.Instance.server.websocket, @"wss_\d*", $"wss_{port}");
			communicator.SetGameCommunicator(game_communicator);
			return communicator;
		}

		public SnipeRoomCommunicator() : base()
		{
			RestoreConnectionAttempts = 0;
		}

		protected override void InitClient()
		{
			InitClient(mHost, mPort, mWebSocketUrl);
		}

		private void SetGameCommunicator(SnipeCommunicator communicator)
		{
			if (mGameCommunicator != communicator)
			{
				if (mGameCommunicator != null)
				{
					mGameCommunicator.LoginSucceeded -= OnGameLogin;
				}

				mGameCommunicator = communicator;
				mGameCommunicator.LoginSucceeded += OnGameLogin;
			}
		}

		private void OnGameLogin()
		{
			InitClient();
		}

		public override void Dispose()
		{
			if (mGameCommunicator != null)
			{
				mGameCommunicator.LoginSucceeded -= OnGameLogin;
				mGameCommunicator = null;
			}
			base.Dispose();
		}

		protected override void ProcessSnipeMessage(ExpandoObject data, bool original = false)
		{
			base.ProcessSnipeMessage(data, original);

#if UNITY_EDITOR
			Debug.Log("[SnipeRoomCommunicator] OnRoomResponse: " + (data != null ? data.ToJSONString() : "null"));
#endif

			string message_type = data.SafeGetString("type");
			string error_code = data.SafeGetString("errorCode");

			switch (message_type)
			{
				case "user.login":
					if (error_code == "ok")
					{
						// join room
						ExpandoObject request_data = new ExpandoObject();
						request_data["typeID"] = mRoomType;
						request_data["roomID"] = mRoomId;
						Request("kit/room.join", request_data);
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

				case "kit/room.broadcast":
					ExpandoObject broadcast_msg = null;
					if (error_code == "ok" && data.ContainsKey("msg"))
					{
						try
						{
							broadcast_msg = ExpandoObject.FromJSONString(data["msg"] as string);
						}
						catch (Exception)
						{
							//
						}
					}
					OnRoomBroadcast(error_code, broadcast_msg);
					break;
			}
		}

		protected virtual void OnRoomJoin(ExpandoObject data)
		{
			
		}

		protected virtual void OnRoomLeave(ExpandoObject data)
		{
			Dispose();
		}

		protected virtual void OnRoomLeft(ExpandoObject data)
		{
			
		}

		protected virtual void OnRoomDead(ExpandoObject data)
		{
			// NOTE: kit/room.dead is dispatched on any room death even when it is correctly finilized

			Dispose();
		}

		
		protected virtual void OnRoomBroadcast(string error_code, ExpandoObject msg)
		{

		}
	}
}