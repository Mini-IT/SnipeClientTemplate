
using System;
using System.Collections;
using System.Collections.Generic;
using MiniIT;
using MiniIT.Social;

public class UserData
{
	public bool Initialized { get; private set; } = false;

	public int Id { get; private set; }

	private int mMoneySoft = 0;
	public int MoneySoft
	{
		get { return mMoneySoft; }
		set
		{
			if (mMoneySoft != value)
			{
				int delta = value - mMoneySoft;
				mMoneySoft = value;
			}
		}
	}

	private int mMoneyHard = 0;
	public int MoneyHard
	{
		get { return mMoneyHard; }
		set
		{
			if (mMoneyHard != value)
			{
				int delta = value - mMoneyHard;
				mMoneyHard = value;
			}
		}
	}

	public int Experienece = 0;
	public int Level { get; private set; } = 1;
	public int Energy = 0;

	public int DaysInGame = 1;

	public float VipTimeLeft = 0.0f; // seconds

	public SocialUserProfile FacebookProfile { get; set; } = null;
	public string FacebookProfileData
	{
		get
		{
			if (this.FacebookProfile != null)
				return this.FacebookProfile.ToJSONString();

			return null;
		}
		set
		{
			if (string.IsNullOrEmpty(value))
				this.FacebookProfile = null;
			else
				this.FacebookProfile = SocialUserProfile.FromJSON(value);
		}
	}

	public string TutorialState;  // json string

	public bool DebugMode = false;

	public UserData(int user_id)//, string name = null)
	{
		this.Id = user_id;
		//if (!string.IsNullOrEmpty(name))
		//	this.Name = name;
	}
	
	public void UpdateValues(IList list, bool notify_statistics)
	{
		// Example list:
		// [{"key":"softMoney","val":1000},{"key":"hardMoney","val":100}]
		
		
		if (list == null)
			return;
		
		foreach(ExpandoObject item in list)
		{
			if (item == null)
				continue;

			UpdateValue(item, notify_statistics);
		}

		if (!Initialized && string.IsNullOrEmpty(TutorialState))
			TutorialState = "{}";

		Initialized = true;
	}

	public void UpdateValue(ExpandoObject item, bool notify_statistics)
	{
		if (item == null)
			return;

		string key = item.SafeGetValue<string>("key", "");
		if (string.IsNullOrEmpty(key))
			key = item.SafeGetValue<string>("name", key);
		if (string.IsNullOrEmpty(key))
			return;

		object val = item.SafeGetValue<object>("val", null);
		if (val == null)
			val = item.SafeGetValue<object>("value", null);

		if (key == "softMoney")
			this.MoneySoft = Convert.ToInt32(val);
		else if (key == "hardMoney")
			this.MoneyHard = Convert.ToInt32(val);
		else if (key == "energy")
			this.Energy = Convert.ToInt32(val);
		else if (key == "exp")
			this.Experienece = Convert.ToInt32(val);
		else if (key == "level")
			this.Level = Math.Max(1, Convert.ToInt32(val));
		
		else if (key == "daysIngame")
			this.DaysInGame = Convert.ToInt32(val);

		
		else if (key == "facebook")
			this.FacebookProfileData = Convert.ToString(val);

		else if (key == "tutorial")
			this.TutorialState = Convert.ToString(val);

		else if (key == "debugMode")
			this.DebugMode = (Convert.ToInt32(val) > 0);
	}

	public void AddValue(ExpandoObject item)
	{
		if (item == null)
			return;

		string key = item.SafeGetValue<string>("key", "");
		if (string.IsNullOrEmpty(key))
			key = item.SafeGetValue<string>("name", key);
		if (string.IsNullOrEmpty(key))
			return;

		object val = item.SafeGetValue<object>("val", null);
		if (val == null)
			val = item.SafeGetValue<object>("value", null);

		if (key == "softMoney")
		{
			this.MoneySoft += Convert.ToInt32(val);
		}
		else if (key == "hardMoney")
		{
			this.MoneyHard += Convert.ToInt32(val);
		}
		else if (key == "energy")
		{
			this.Energy += Convert.ToInt32(val);
		}
		else if (key == "exp")
		{
			this.Experienece += Convert.ToInt32(val);
		}
	}

	public void Award(ExpandoObject data)
	{
		if (data == null)
			return;

		foreach (string key in data.Keys)
		{
			if (string.IsNullOrEmpty(key))
				continue;

			if (key == "softMoney")
			{
				this.MoneySoft += data.SafeGetValue<int>(key);
			}
			else if (key == "hardMoney")
			{
				this.MoneyHard += data.SafeGetValue<int>(key);
			}
			else if (key == "energy")
			{
				this.Energy += data.SafeGetValue<int>(key);
			}
			else if (key == "exp")
			{
				this.Experienece += data.SafeGetValue<int>(key);
			}
		}
	}
}

