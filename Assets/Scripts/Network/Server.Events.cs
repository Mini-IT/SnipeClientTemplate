using System;
using System.Collections;
using System.Security.Cryptography;
using MiniIT;

public partial class Server
{
#pragma warning disable 0067

	public Action<ExpandoObject> DataReceived;

	public Action<ExpandoObject> PlayerInfoUpdated;
	public Action<ExpandoObject> PlayerLevelUp;
	public Action<ExpandoObject> LogicUpdated;
	public Action<ExpandoObject> PaymentInfo;
	public Action<ExpandoObject> NotEnoughMoney;
	public Action<ExpandoObject> NotEnoughEnergy;
	public Action<ExpandoObject> MoneyAdded;
	public Action<ExpandoObject> PaymentVerificationSucceeded;
	public Action<ExpandoObject> PaymentVerificationFailed;

#pragma warning restore 0067

	internal void DispatchEvent(Action<ExpandoObject> handler, ExpandoObject data = null)
	{
		if (handler != null)
			handler.Invoke(data);
	}
}
