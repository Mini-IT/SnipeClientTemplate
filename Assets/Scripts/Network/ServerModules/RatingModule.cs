using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MiniIT;
using System;

public class RatingModule : ServerModule
{
	public RatingModule(Server server) : base(server)
	{
		
	}

	public void RequestRating(string leaderboard_id, int start_place = 1, bool get_self = false, Action<ExpandoObject> callback = null)
	{
		mServer.CreateRequest("kit/leaderboard.get", new ExpandoObject()
		{
			["boardID"] = leaderboard_id,
			["minPlace"] = start_place,
			["limit"] = 50, // default = 100 (max = 100)
			["getSelf"] = get_self,
		}).Request(callback);
	}

	public void ClaimReward(string leaderboard_id)
	{
		mServer.CreateKitActionSelfRequest("lb.claimReward", new ExpandoObject()
		{
			["boardID"] = leaderboard_id
		}).Request((response) =>
			{
				//if (response["reward"] is ExpandoObject reward_data)
				//	mServer.DispatchEvent(mServer.ChestOpen, reward_data);
			});
	}

	//internal override void OnResponse(ExpandoObject data, bool original = false)
	//{
	//	string message_type = data.SafeGetString("type");

	//	switch (message_type)
	//	{
	//		case "leaderboard.reward":
	//			OnResponseLeaderboardReward(data);
	//			break;
	//	}
	//}

	private void OnResponseLeaderboardReward(ExpandoObject data)
	{
		// example: { place => 1, type => leaderboard.reward, boardID => rating_delta_season }
	}

}
