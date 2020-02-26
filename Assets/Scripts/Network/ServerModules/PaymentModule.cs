using System;
using MiniIT;

public class PaymentModule : ServerModule
{
	public PaymentModule(Server server) : base(server)
	{
	}
	
	public void NotifyGoogleOrderSucceded(string item_id, string token)
	{
		ExpandoObject data = new ExpandoObject();
		data["itemID"] = item_id;
		data["token"] = token;
			
		mServer.Request("kit/payment/google.notify", data);
	}

	public void NotifyAppleOrderSucceded(string receipt_base64)
	{
		ExpandoObject data = new ExpandoObject();
		data["receipt"] = receipt_base64;

		mServer.Request("kit/payment/apple.notify", data);
	}
	
	public void RequestPaymentInfo(Action<ExpandoObject> callback = null)
	{
		mServer.CreateKitActionSelfRequest("payment.info", new ExpandoObject() {
			{ "provider",
#if UNITY_IOS
			"appl"
#else
			"goog"
#endif
			}
		}).Request((response) =>
		{
			if (callback != null)
				callback.Invoke(response);

			mServer.DispatchEvent(mServer.PaymentInfo, response);
		});
	}

#region Response handling

	internal override void OnResponse(ExpandoObject data, bool original = false)
	{
		string message_type = data.SafeGetString("type");
		string error_code = data.SafeGetString("errorCode");

		switch(message_type)
		{
			case "kit/payment/google.notify":
			case "kit/payment/apple.notify":
				OnPaymentNotify(data);
				break;
		}
	}

	private void OnPaymentNotify(ExpandoObject data)
	{
		string error_code = data.SafeGetString("errorCode");
		// errorCode:
		//   accessTokenError - Access token is not retrieved yet.
		//   internalVarsUnset - One or more of the internal variables is not set.
		//   noSuchItem - No payment item with this string ID.
		//   transactionExists - Transaction with this ID was already handled.
		//   serviceError - Google API returned an error.
		//   purchaseCancelled - This purchase was cancelled.

		if (error_code == "ok")
		{
			// data contains:
			//   token - String. Received purchase token.
			//   itemID - String.Payment item string ID. Returned only on success.

			mServer.DispatchEvent(mServer.PaymentVerificationSucceeded, data);

			mServer.Logic.ProcessExitNodeResults(data);
			mServer.Player.Update();
		}
		else
		{
			mServer.DispatchEvent(mServer.PaymentVerificationFailed, data);
		}
	}

#endregion
}
