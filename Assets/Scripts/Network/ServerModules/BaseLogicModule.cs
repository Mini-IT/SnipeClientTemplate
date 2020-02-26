using System.Collections;
using System.Collections.Generic;
using MiniIT;

public class BaseLogicModule : ServerModule
{
	public const string VAR_TAP = "tap";
	public const string VAR_ADS = "ads_show";

	public List<LogicNode> Nodes { get; private set; }

	private bool mUpdateRequested = false;

	public BaseLogicModule(Server server) : base(server)
	{
	}

	protected LogicNode FindNodeById(int id)
	{
		if (Nodes != null)
		{
			foreach (LogicNode node in Nodes)
			{
				if (node != null && node.Id == id)
				{
					return node;
				}
			}
		}

		return null;
	}

	public LogicNode FindNodeByName(string name)
	{
		if (Nodes != null)
		{
			foreach (LogicNode node in Nodes)
			{
				if (node != null && node.Name.Equals(name))
				{
					return node;
				}
			}
		}

		return null;
	}

	public LogicNode FindNodeByTag(string tag)
	{
		if (Nodes != null)
		{
			foreach (LogicNode node in Nodes)
			{
				if (node != null && node.Tags.Contains(tag))
				{
					return node;
				}
			}
		}

		return null;
	}

	public LogicNode FindNodeByProductSku(string sku)
	{
		if (Nodes != null)
		{
			foreach (LogicNode node in Nodes)
			{
				if (node != null && node.PurchaseProductSku == sku)
				{
					return node;
				}
			}
		}

		return null;
	}

	public void Update()
	{
		if (mUpdateRequested)
			return;

		mUpdateRequested = true;

		mServer.Request("kit/logic.get");
	}

	public void IncVar(string name, int tree_id = 0)
	{
		ExpandoObject data = new ExpandoObject();
		data["name"] = name;
		if (tree_id > 0)
			data["treeID"] = tree_id;

		mServer.Request("kit/logic.incVar", data);
	}

	#region Response handling

	internal override void OnResponse(ExpandoObject data, bool original = false)
	{
		if (original)
			mUpdateRequested = false;

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

			case "kit/logic.get":
				OnLogicGet(data);
				break;

			//case "kit/logic.incVar":
				// OnLogicIncVar(data);
				//Update();
				//break;

			case "logic.exitNode":
				OnLogicExitNode(data);
				break;
		}
	}

	protected virtual void OnLogin(ExpandoObject data)
	{
		Update();
	}

	protected virtual void OnLogicGet(ExpandoObject data)
	{
		string error_code = data.SafeGetString("errorCode");
		if (error_code == "ok")
		{
			// data contains:
			//   logic - List<{ id, stringID, node:{...} }>

			if (Nodes == null)
				Nodes = new List<LogicNode>();
			else
				Nodes.Clear();

			if (data["logic"] is List<object> logic_list)
			{
				// иногда из-за округления приходит сообщение с нулевым значением таймера оффера
				// в этом случае надо запросить заново
				bool timer_finished = false;
				foreach (ExpandoObject tree_data in logic_list)
				{
					LogicNode node = new LogicNode(this, tree_data["stringID"] as string, tree_data["node"] as ExpandoObject);
					Nodes.Add(node);

					InitializeLogicNode(node);

					if (node.TimeLeft == 0) // (-1) means that the node has not a timer
						timer_finished = true;
				}

				if (timer_finished)
					Update();
				else
					mServer.DispatchEvent(mServer.LogicUpdated, data);
			}
		}
	}

	protected void OnLogicExitNode(ExpandoObject data)
	{
		ProcessExitNodeResults(data);
		Update();
	}

	protected virtual void InitializeLogicNode(LogicNode node)
	{
		// override in subclass
	}

	internal virtual void ProcessExitNodeResults(ExpandoObject data)
	{
		// data examples
		// { type : logic.exitNode, results : [{ name : softMoney, type : attr, id : 4, group : results, value : 100 }]}
		// { type : logic.exitNode, results : [{ type : chest, id : 1 }] }}
		// { type : logic.exitNode, results : [{ type : chestContents, "hardMoney" : 0, "softMoney" : 2, id : 1, rewards : {{ type : materials, id : 45 }, { type : materials, id : 67 }, { type : materials, id : 3 }} }] }

	}

	#endregion

	internal override void OnSecondsTimerTick()
	{
		if (Nodes != null)
		{
			bool finished = false;

			for (int i = 0; i < Nodes.Count; i++)
			{
				LogicNode node = Nodes[i];
				if (node != null && node.TimeLeft > 0)
				{
					node.TimeLeft--;
					if (node.TimeLeft <= 0)
						finished = true;
				}
			}

			if (finished)
			{
				Update();
			}
		}
	}
}

public class LogicNode
{
	private BaseLogicModule mLogicModule;

	public ExpandoObject RawData { get; private set; }

	public int Id { get; private set; }
	public int TreeId { get; private set; }
	public string TreeStringId { get; private set; }
	public string Name { get; private set; }
	public string StringId { get; private set; }
	public string Note { get; private set; }
	public List<string> Tags { get; private set; }
	public List<LogicNodeVariable> Variables { get; private set; }
	// public List<LogicNodeResult> Results { get; private set; }
	public bool Visible { get; private set; }

	public int TimeLeft = -1; // seconds left. (-1) means that the node has not a timer
	public string PurchaseProductSku { get; private set; } = null;

	public bool NeedsTap { get; private set; } = false;
	public bool NeedsAds { get; private set; } = false;

	internal LogicNode(BaseLogicModule module, string tree_string_id, ExpandoObject data)
	{
		mLogicModule = module;
		TreeStringId = tree_string_id;

		RawData = data;

		this.Id = data.SafeGetValue<int>("id", 0);
		this.TreeId = data.SafeGetValue<int>("treeID", 0);
		this.Name = data.SafeGetValue<string>("name", "");
		this.StringId = data.SafeGetValue<string>("stringID", "");
		this.Note = data.SafeGetValue<string>("note", "");
		this.Visible = data.SafeGetValue<bool>("isVisible", false);

		this.Tags = new List<string>();
		if (data["tags"] is List<object> tags)
		{
			foreach (string tag in tags)
			{
				this.Tags.Add(tag);
			}
		}

		this.Variables = new List<LogicNodeVariable>();
		if (data["vars"] is List<object> vars)
		{
			this.TimeLeft = -1;

			foreach (ExpandoObject var_data in vars)
			{
				string type = var_data.SafeGetValue<string>("type", "");
				if (type == "timeout" || type == "timer")
				{
					this.TimeLeft = var_data.SafeGetValue<int>("value", 0);
				}
				else if (type == "paymentItemStringID")
				{
					PurchaseProductSku = var_data.SafeGetValue<string>("name", "");
				}
				else
				{
					LogicNodeVariable variable = new LogicNodeVariable(var_data);
					this.Variables.Add(variable);

					if (variable.Name == BaseLogicModule.VAR_TAP)
						this.NeedsTap = true;
					else if (variable.Name == BaseLogicModule.VAR_ADS)
						this.NeedsAds = true;
				}
			}
		}
	}

	public void IncVar(string name)
	{
		mLogicModule.IncVar(name, this.TreeId);
	}
}

public class LogicNodeVariable
{
	public string Name;
	public int Value;
	public int MaxValue;

	public LogicNodeVariable(ExpandoObject data)
	{
		this.Name = data.SafeGetValue<string>("name", "");
		this.Value = data.SafeGetValue<int>("value", 0);
		this.MaxValue = data.SafeGetValue<int>("maxValue", 0);
	}
}

//public class LogicNodeResult
//{
//	public int Id;
//	public string Type;
//	public string StringId;
//	public string Name;
//	//public string Group; //???
//	public object Value;
//	public ExpandoObject Custom;
//}