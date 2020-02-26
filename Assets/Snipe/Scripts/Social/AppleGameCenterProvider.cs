using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SocialPlatforms;
using MiniIT;

namespace MiniIT.Social
{
	public class AppleGameCenterProvider : SocialProvider
	{
		public const string DUMMY_AVATAR_URL = ""; //"https://cdn.tuner-life.com/tl2/dummyavatar50.png";

		private static AppleGameCenterProvider sInstance;
		public static AppleGameCenterProvider Instance
		{
			get
			{
				if (sInstance == null)
					new AppleGameCenterProvider();
				return sInstance;
			}
		}

		public static bool InstanceInitialized
		{
			get
			{
				return sInstance != null && sInstance.Initialized;
			}
		}

		private List<Action> mOnInitializationSucceededCallbacks = null;
		private List<Action> mOnInitializationFailedCallbacks = null;

		public AppleGameCenterProvider () : base(SocialNetworkType.APPLE_GAME_CENTER)
		{
			sInstance = this;
		}
		
		public override void Init(System.Action callback = null, System.Action fail_callback = null)
		{
			if (callback != null)
			{
				if (mOnInitializationSucceededCallbacks == null)
					mOnInitializationSucceededCallbacks = new List<Action>();

				mOnInitializationSucceededCallbacks.Add(callback);
			}

			if (fail_callback != null)
			{
				if (mOnInitializationFailedCallbacks == null)
					mOnInitializationFailedCallbacks = new List<Action>();

				mOnInitializationFailedCallbacks.Add(fail_callback);
			}

			if (UnityEngine.Social.localUser.authenticated)
				OnAuthenticated();
			else
				UnityEngine.Social.localUser.Authenticate(ProcessAuthentication);
		}

		// This function gets called when Authenticate completes
		// Note that if the operation is successful, Social.localUser will contain data from the server. 
		private void ProcessAuthentication(bool success)
		{
			if (success || UnityEngine.Social.localUser.authenticated)
			{
				OnAuthenticated();
			}
			else
			{
				if (mOnInitializationFailedCallbacks != null)
				{
					for (int i = 0; i < mOnInitializationFailedCallbacks.Count; i++)
					{
						Action callback = mOnInitializationFailedCallbacks[i];
						if (callback != null)
							callback.Invoke();
					}
					mOnInitializationFailedCallbacks.Clear();
					mOnInitializationFailedCallbacks = null;
				}

				if (mOnInitializationSucceededCallbacks != null)
				{
					mOnInitializationSucceededCallbacks.Clear();
					mOnInitializationSucceededCallbacks = null;
				}

				// Dispatch event
				DispatchEventInitializationFailed();
			}
		}

		private void OnAuthenticated()
		{
			OnLocalUserInitialized();
		}
		
		//public void GetServerAuthToken(Action<string> callback)
		//{

		//}

		private void OnLocalUserInitialized(IUserProfile profile = null)
		{
			PlayerProfile = PrepareProfile(UnityEngine.Social.localUser);
			
			Initialized = true;

			if (mOnInitializationSucceededCallbacks != null)
			{
				for (int i = 0; i < mOnInitializationSucceededCallbacks.Count; i++)
				{
					Action callback = mOnInitializationSucceededCallbacks[i];
					if (callback != null)
						callback.Invoke();
				}
				mOnInitializationSucceededCallbacks.Clear();
				mOnInitializationSucceededCallbacks = null;
			}

			if (mOnInitializationFailedCallbacks != null)
			{
				mOnInitializationFailedCallbacks.Clear();
				mOnInitializationFailedCallbacks = null;
			}

			// Dispatch event
			DispatchEventInitializationComplete();
		}

		private SocialUserProfile PrepareProfile(IUserProfile data)
		{
			SocialUserProfile profile = new SocialUserProfile(data.id, SocialNetworkType.APPLE_GAME_CENTER);
			profile.Name         = data.userName;
			profile.PhotoSmallURL  = DUMMY_AVATAR_URL;
			profile.PhotoMediumURL = DUMMY_AVATAR_URL;

			// добавим в кэш
			//SetProfileData(profile);

			return profile;
		}
		
	}
}

