using System;
using MiniIT;

namespace MiniIT.Snipe
{
	public class SnipeKitActionSelfRequest : SnipeRequest
	{
		protected string mActionId;
		
		public SnipeKitActionSelfRequest(SnipeClient client, string action_id) : base (client, "kit/action.self")
		{
			mActionId = action_id;
		}
		
		public override void Request(Action<ExpandoObject> callback = null)
		{
			if (string.IsNullOrEmpty(mActionId))
				mActionId = (Data != null) ? Data.SafeGetString("actionID") : null;

			if (string.IsNullOrEmpty(mActionId))
			{
				if (callback != null)
					callback.Invoke(new ExpandoObject() { { "errorCode", ERROR_INVALIND_DATA } });
				return;
			}

			if (Data == null)
			{
				Data = new ExpandoObject();
			}
			Data["actionID"] = mActionId;

			base.Request(callback);
		}

		protected override bool CheckResponse(ExpandoObject response_data)
		{
			return base.CheckResponse(response_data) && (response_data.SafeGetString("actionID") == mActionId);
		}
	}
}