using System;
using MiniIT;

namespace MiniIT.Snipe
{
	public class SnipeApiBase
	{
		public SnipeCommunicator Communicator { get; private set; }

		internal SnipeApiBase(SnipeCommunicator communicator)
		{
			this.Communicator = communicator;
		}

		internal SnipeRequest CreateRequest(ExpandoObject data)
		{
			if (Communicator == null || !Communicator.LoggedIn)
				return null;

			SnipeRequest request = Communicator.CreateRequest();
			request.Data = data;
			return request;
		}

		internal SnipeServiceRequest CreateServiceRequest(ExpandoObject data)
		{
			if (Communicator == null || Communicator.ServiceCommunicator == null || !Communicator.ServiceCommunicator.Ready)
				return null;

			var request = Communicator.ServiceCommunicator.CreateRequest();
			request.Data = data;
			return request;
		}
	}
}