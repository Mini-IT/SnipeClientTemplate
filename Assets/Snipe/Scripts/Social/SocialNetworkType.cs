using System;

namespace MiniIT.Social
{
	public class SocialNetworkType
	{
		public const string NONE               = "__";
		public const string VK                 = "VK";
		public const string MAILRU             = "MM";
		public const string ODNOKLASSNIKI      = "OD";
		public const string FACEBOOK           = "FB";
		public const string GOOGLE_PLAY        = "GP";
		public const string APPLE_GAME_CENTER  = "GC";
		
		public static string GetCorrectValue(string id = NONE) 
		{
			string value = id.ToUpper();
			if (value != VK &&
				value != MAILRU &&
				value != ODNOKLASSNIKI &&
				value != FACEBOOK &&
				value != GOOGLE_PLAY &&
				value != APPLE_GAME_CENTER)
			{
				value = NONE;
			}
			return value;
		}
	}

}