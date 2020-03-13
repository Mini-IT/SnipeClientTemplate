using MiniIT;

namespace MiniIT.Snipe
{
	public class SnipeTableItem
	{
		//public ExpandoObject raw { get; protected set; } // ???

		public int id { get; protected set; } = 0;

		public SnipeTableItem(ExpandoObject data)
		{
			//this.raw = data;
			this.id = data.SafeGetValue<int>("id");
		}
	}
}