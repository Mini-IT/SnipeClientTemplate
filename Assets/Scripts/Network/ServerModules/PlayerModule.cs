
using System;
using System.Collections;
using System.Collections.Generic;
using MiniIT;
using MiniIT.Snipe;

public class PlayerModule : ServerModule
{
	public UserData PlayerData { get; private set; }
	public Inventory Inventory  { get; private set; }

	internal bool mFirstUpdate = false;

	public PlayerModule(Server server) : base(server)
	{
		Inventory = new Inventory();
	}

	public void Update()
	{
		mServer.Request("kit/attr.getAll");
		mServer.RequestKitActionSelf("inventory.get");
	}

	public void BuyVip()
	{
		if (PayPrice(mServer.Static.VipPrice))
		{
			ExpandoObject request_data = new ExpandoObject()
			{
				["amount"] = 1,
			};

			mServer.RequestKitActionSelf("vip.buy", request_data);
		}
	}

	internal override void OnSecondsTimerTick()
	{
		if (PlayerData != null && PlayerData.VipTimeLeft > 0.0f)
		{
			if (--PlayerData.VipTimeLeft < 0.0f)
			{
				PlayerData.VipTimeLeft = 0.0f;
			}
		}
	}

	#region Response handling

	internal override void OnResponse(ExpandoObject data, bool original = false)
	{
		string message_type = data.SafeGetString("type");
		string error_code = data.SafeGetString("errorCode");
		
		switch(message_type)
		{
			case "user.login":
				if (error_code == "ok")
				{
					OnLogin(data);
				}
				break;
				
			case "kit/attr.getAll":
				OnResponseUserInfo(data);
				break;

			case "kit/action.self":
				string action_id = data["actionID"] as string;
				if (error_code == "ok")
				{
					if (action_id == "inventory.get")
						OnInventoryGet(data);
				}
				break;
			
			case "user.level":
				OnUserLevel(data);
				break;
		}
	}

	private void OnLogin(ExpandoObject data)
	{
		mFirstUpdate = true;
		this.PlayerData = new UserData(data.SafeGetValue<int>("id"));

		Update();
	}

	private void OnResponseUserInfo(ExpandoObject data)
	{
		UpdateInfo(data);

		//if (mFullInfoUpdate)
		//{
		//	mFullInfoUpdate = false;

		//		mServer.CreateKitActionSelfRequest("inventory.get").Request((response_inventory_get) =>
		//		{
		//			//mServer.CreateKitActionSelfRequest("chest.get").Request();
		//			UpdateChests();
		//		});
		//}

	}

	private void OnInventoryGet(ExpandoObject data)
	{
		Inventory.UpdateData(data);

		if (data.ContainsKey("vipTime"))
			PlayerData.VipTimeLeft = Convert.ToSingle(data["vipTime"]);

		if (data.ContainsKey("debugMode"))
			PlayerData.DebugMode = Convert.ToBoolean(data["debugMode"]);
	}

	private void OnUserLevel(ExpandoObject data)
	{
		Update();

		mServer.DispatchEvent(mServer.PlayerLevelUp, data);
	}

	#endregion

	public void UpdateInfo(ExpandoObject data, bool notify = true)
	{
		// Example data:
		// {"data":[{"key":"softMoney","val":1000},{"key":"hardMoney","val":100}],"type":"kit/attr.getAll","errorCode":"ok"}

		if (data != null && data.ContainsKey("data"))
		{
			this.PlayerData.UpdateValues((IList)data["data"], !mFirstUpdate);
			
			if (notify)
				DispatchInfoUpdated();
		}
	}

	public bool PayPrice(Price price, bool notify_on_not_enough_money = false)
	{
		if (PlayerData.MoneyHard >= price.HardValue && PlayerData.MoneySoft >= price.SoftValue)
		{
			PlayerData.MoneyHard -= price.HardValue;
			PlayerData.MoneySoft -= price.SoftValue;
			mServer.DispatchEvent(mServer.PlayerInfoUpdated);
			return true;
		}

		if (notify_on_not_enough_money)
			mServer.DispatchEvent(mServer.NotEnoughMoney);

		return false;
	}

	public void DispatchInfoUpdated()
	{
		mServer.DispatchEvent(mServer.PlayerInfoUpdated);
	}
}

