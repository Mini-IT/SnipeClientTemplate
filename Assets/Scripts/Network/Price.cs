using UnityEngine;
using System.Collections;

public class Price
{
	public int SoftValue
	{
		get;
		set;
	}
	public int HardValue
	{
		get;
		set;
	}

	public bool IsCurrencyHard
	{
		get
		{
			return this.HardValue > 0 || this.SoftValue <= 0;
		}
	}

	public Price(int soft, int hard)
	{
		SoftValue = soft;
		HardValue = hard;
	}

	public Price(int value, bool currency_hard)
	{
		SetValue(value, currency_hard);
	}

	public void SetValue(int soft, int hard)
	{
		SoftValue = soft;
		HardValue = hard;
	}

	public void SetValue(int value, bool currency_hard)
	{
		if (currency_hard)
		{
			SoftValue = 0;
			HardValue = value;
		}
		else
		{
			SoftValue = value;
			HardValue = 0;
		}
	}
}
