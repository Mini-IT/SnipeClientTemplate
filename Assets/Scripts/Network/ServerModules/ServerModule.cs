using System.Collections;
using MiniIT;

public class ServerModule
{
	protected Server mServer;

	public ServerModule(Server server)
	{
		mServer = server;
		//mServer.DataReceived += DataReceived;

		if (mServer.Modules != null)
			mServer.Modules.Add(this);
	}

	//private void DataReceived(ExpandoObject e)
	//{
	//	if (e != null && e.Data != null)
	//	{
	//		OnResponse(e.Data);
	//	}
	//}

	// Override this method in a child class
	internal virtual void OnResponse(ExpandoObject data, bool original = false)
	{
	}

	// Override this method in a child class
	internal virtual void OnSecondsTimerTick()
	{
	}
}

