using System.Collections;
using System.Collections.Generic;
using MiniIT;

public class LogicModule : BaseLogicModule
{
	public const string TAG_FREE_SOFT = "free_soft";
	public const string TAG_FREE_HARD = "free_hard";
	public const string TAG_FREE_CHEST = "free_chest";
	public const string TAG_STARTER_PACK = "starter_pack";
	public const string TAG_FREE_ENERGY = "free_energy";

	public LogicNode FreeSoftNode { get; private set; }
	public LogicNode FreeHardNode { get; private set; }
	public LogicNode FreeChestNode { get; private set; }
	public LogicNode StarterPackNode { get; private set; }

	public bool ShopFreeStuffAvailable
	{
		get
		{
			return (FreeSoftNode != null && FreeSoftNode.TimeLeft <= 0) ||
				(FreeHardNode != null && FreeHardNode.TimeLeft <= 0) ||
				(FreeChestNode != null && FreeChestNode.TimeLeft <= 0);
		}
	}

	private bool mUpdateRequested = false;

	public LogicModule(Server server) : base(server)
	{
	}



	protected override void InitializeLogicNode(LogicNode node)
	{
		if (node.Tags.Contains(TAG_FREE_SOFT))
			this.FreeSoftNode = node;
		else if (node.Tags.Contains(TAG_FREE_HARD))
			this.FreeHardNode = node;
		else if (node.Tags.Contains(TAG_FREE_CHEST))
			this.FreeChestNode = node;
		else if (node.Tags.Contains(TAG_STARTER_PACK))
			this.StarterPackNode = node;
	}

	internal override void ProcessExitNodeResults(ExpandoObject data)
	{
		// { type : logic.exitNode, results : [{ name : softMoney, type : attr, id : 4, group : results, value : 100 }]}
		// { type : logic.exitNode, results : [{ type : chest, id : 1 }] }}
		// { type : logic.exitNode, results : [{ type : chestContents, "hardMoney" : 0, "softMoney" : 2, id : 1, rewards : {{ type : materials, id : 45 }, { type : materials, id : 67 }, { type : materials, id : 3 }} }] }

		if (data["results"] is List<object> results_list)
		{
			ExpandoObject chest_content = null;
			foreach (ExpandoObject result_data in results_list)
			{
				if (result_data.SafeGetString("type") == "chestContents")
				{
					chest_content = result_data;
					break;
				}
			}

			foreach (ExpandoObject result_data in results_list)
			{
				string type = result_data.SafeGetString("type");
				if (type == "attr")
				{
					string attr_name = result_data.SafeGetString("name");

					// если среди наград есть открытый сундук, вложим деньги в него
					if (chest_content != null && (attr_name == "softMoney" || attr_name == "hardMoney"))
					{
						chest_content[attr_name] = chest_content.SafeGetValue<int>(attr_name, 0) + result_data.SafeGetValue<int>("value", 0);
					}
					else
					{
						mServer.Player.PlayerData.AddValue(result_data);
						mServer.Player.DispatchInfoUpdated();

						if (attr_name == "softMoney" || attr_name == "hardMoney")
							mServer.DispatchEvent(mServer.MoneyAdded, result_data);
					}
				}
			}
		}
	}
	
}