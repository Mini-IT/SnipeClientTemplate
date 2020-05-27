using System;
using MiniIT;
using UnityEngine;

namespace MiniIT.Snipe
{
	public class SnipeCommunicatorRequest : SnipeRequest
	{
		protected SnipeCommunicator mCommunicator;
		
		public SnipeCommunicatorRequest(SnipeCommunicator communicator, string message_type = null) : base(communicator.Client, message_type)
		{
			mCommunicator = communicator;
		}

		public override void Request(Action<ExpandoObject> callback = null)
		{
			if (!CheckMessageType())
			{
				if (callback != null)
					callback.Invoke(new ExpandoObject() { { "errorCode", ERROR_INVALIND_DATA } });
				return;
			}

			SetCallback(callback);
			SendRequest();
		}

		protected override void SendRequest()
		{
			if (mCommunicator == null)
			{
				mCallback?.Invoke(new ExpandoObject() { { "errorCode", ERROR_NO_CONNECTION } });
				return;
			}

			if (!CheckMessageType())
			{
				mCallback?.Invoke(new ExpandoObject() { { "errorCode", ERROR_INVALIND_DATA } });
				return;
			}

			if (mCommunicator.LoggedIn)
			{
				mRequestId = mCommunicator.Request(this);
			}
			else
			{
				if (mCommunicator is SnipeRoomCommunicator room_communicator)
				{
					room_communicator.RoomJoined -= OnCommunicatorLoginSucceeded;
					room_communicator.RoomJoined += OnCommunicatorLoginSucceeded;
				}
				else
				{
					mCommunicator.LoginSucceeded -= OnCommunicatorLoginSucceeded;
					mCommunicator.LoginSucceeded += OnCommunicatorLoginSucceeded;
				}

				mCommunicator.PreDestroy -= OnCommunicatorPreDestroy;
				mCommunicator.PreDestroy += OnCommunicatorPreDestroy;
			}
		}

		private void OnCommunicatorLoginSucceeded()
		{
			if (mCommunicator != null)
			{
				if (mCommunicator is SnipeRoomCommunicator room_communicator)
				{
					room_communicator.RoomJoined -= OnCommunicatorLoginSucceeded;
				}

				mCommunicator.LoginSucceeded -= OnCommunicatorLoginSucceeded;

				mRequestId = mCommunicator.Request(this);
			}
		}

		private void OnCommunicatorPreDestroy()
		{
			if (mCommunicator != null)
			{
				if (mCommunicator is SnipeRoomCommunicator room_communicator)
					room_communicator.RoomJoined -= OnCommunicatorLoginSucceeded;

				mCommunicator.LoginSucceeded -= OnCommunicatorLoginSucceeded;
				mCommunicator.PreDestroy -= OnCommunicatorPreDestroy;
			}
		}

		public override void Dispose()
		{
			OnCommunicatorPreDestroy();

			base.Dispose();
		}
	}
}