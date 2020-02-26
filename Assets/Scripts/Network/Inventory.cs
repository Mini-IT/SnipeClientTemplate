
using System;
using System.Collections;
using System.Collections.Generic;
using MiniIT;

public class Inventory
{
	public List<object> Items {get; private set; }
	public int NitroAmount {get; private set; }
	public int FuelAmount {get; private set; }

	public Inventory()
	{
		Items = new List<object>();
		NitroAmount = 0;
		FuelAmount = 0;
	}

	public void Clear()
	{
		Items.Clear();
		NitroAmount = 0;
		FuelAmount = 0;
	}

	public void UpdateData(ExpandoObject data)
	{
		if (data.ContainsKey("inventory"))
		{
			foreach (ExpandoObject item_data in (List<object>)data["inventory"])
			{
				string string_id = (string)item_data["stringID"];
				int amount = (int)item_data["amount"];
				if (string_id == "nitro")
					NitroAmount += amount;
				else if (string_id == "fuel")
					FuelAmount += amount;
			}
		}

		this.Items = (List<object>)data["items"];
	}
}

