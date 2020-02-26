
using System;
using System.Collections;
using System.Collections.Generic;
using MiniIT;
using MiniIT.Snipe;

public class ShopModule : ServerModule
{
	public ShopModule(Server server) : base(server)
	{
	}

	public void BuyVip()
	{
		if (mServer.Player.PayPrice(mServer.Static.VipPrice))
		{
			mServer.CreateKitActionSelfRequest("vip.buy",  new ExpandoObject() { ["amount"] = 1 }).Request(OnVipBuy);
		}
	}

	private void OnVipBuy(ExpandoObject data)
	{
		if (data.ContainsKey("vipTime"))
		{
			mServer.Player.PlayerData.VipTimeLeft = Convert.ToSingle(data["vipTime"]);
		}
	}

}

