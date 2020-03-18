using System;
using System.Text.RegularExpressions;
using System.Collections;
using System.Collections.Generic;
using Facebook.Unity;
using MiniIT;
using UnityEngine;

namespace MiniIT.Social
{
	public class FacebookProvider : SocialProvider //, ISocNetUserWallPoster, ISocNetUserRequestSender, ISocNetInvitableFriendsRequester
	{
		private static FacebookProvider sInstance;
		public static FacebookProvider Instance
		{
			get
			{
				if (sInstance == null)
					new FacebookProvider();
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

		protected const string PROFILE_FIELDS = "id,first_name,last_name,picture"; //,link,gender";
		
		protected readonly string[] SUPPORTED_READ_PERMISSIONS = new string[] { "public_profile" };//, "email", "user_link", "user_friends", "user_photos" };

		// вспомогательные поля для обеспечения возможности запрашивать
		// данные нескольких пользователей (см. метод DoRequestProfiles)

		protected class RequestProfilesQueueItem
		{
			internal IList<string> userids;
			internal Action<IList<SocialUserProfile>> callback;
		}
		protected int mAwaitingProfilesCount = 0;
		protected Queue<RequestProfilesQueueItem> mRequestProfilesQueue = new Queue<RequestProfilesQueueItem>();
		protected List<SocialUserProfile> mProfiles;
		protected Action<IList<SocialUserProfile>> mRequestProfilesCallback;

		//protected int mAwailingFriendsProfilesCount = 0;
		//protected List<SocialUserProfile> mFriendsProfiles;

		private Uri mAppLink;

		private Action mInitializationCompleteCallback = null;
		private Action mInitializationFailedCallback = null;

		public bool IsLoggedIn
		{
			get { return FB.IsLoggedIn; }
		}

		public FacebookProvider() : base(SocialNetworkType.FACEBOOK)
		{
			sInstance = this;
		}

		public override void Init(Action callback = null, Action fail_callback = null)
		{
			mInitializationCompleteCallback = callback;
			mInitializationFailedCallback = fail_callback;

			// HACK: FB.IsInitialized may return false, so we can't rely on it.
			// Check if FB.FacebookImpl != null. But this getter may throw a NullReferenceException
			bool inited = FB.IsInitialized;
			if (!inited)
			{
				try
				{
					var fbimpl = FB.FacebookImpl;
					inited = (fbimpl != null);
				}
				catch(Exception)
				{
					inited = false;
				}
			}

			if (inited)
				OnInitComplete();
			else
				FB.Init(OnInitComplete);//, OnHideUnity);
		}

		public override void Logout()
		{
			FB.LogOut();

			PlayerProfile = null;
			Initialized = false;
			
			base.Logout();
		}
		
		public override string GetPlayerUserID()
		{
			if (AccessToken.CurrentAccessToken != null)
				return AccessToken.CurrentAccessToken.UserId;

			return base.GetPlayerUserID();
		}

		private void OnInitComplete()
		{
			Debug.Log("[FacebookProvider] FB.Init completed: Is user logged in? " + FB.IsLoggedIn);
			if (FB.IsLoggedIn)
			{
				GetPlayerProfile();
			}
			else
			{
				FB.LogInWithReadPermissions(SUPPORTED_READ_PERMISSIONS, OnLogin);
			}
		}

		private void CallInitializationCompleteCallback()
		{
			if (mInitializationCompleteCallback != null)
				mInitializationCompleteCallback.Invoke();

			mInitializationCompleteCallback = null;
			mInitializationFailedCallback = null;

			DispatchEventInitializationComplete();
		}

		//private void OnHideUnity(bool isGameShown)
		//{
		//	//Debug.Log("Is game showing? " + isGameShown);
		//}

		private void OnLogin(ILoginResult result)
		{
			if (!FB.IsLoggedIn || result.Error != null)
			{
				Debug.Log("[FacebookProvider] Login failed. Error Response:\n" + result.Error);

				if (mInitializationFailedCallback != null)
					mInitializationFailedCallback.Invoke();

				mInitializationCompleteCallback = null;
				mInitializationFailedCallback = null;
			}
			else
			{
				if (AccessToken.CurrentAccessToken != null)
				{
					//string player_id = AccessToken.CurrentAccessToken.UserId;

					var request_data = new Dictionary<string, string>();
					request_data["fields"] = PROFILE_FIELDS;
					FB.API("me", HttpMethod.GET,
						(IGraphResult response) =>
						{
							if (AssertResult(response))
							{
								PlayerProfile = PrepareProfile(response.RawResult);

								Initialized = true;

								CallInitializationCompleteCallback();
							}
						},
						request_data);
				}

				FB.GetAppLink(delegate (IAppLinkResult res)
				   {
						if(!String.IsNullOrEmpty(res.Url))
						{
							string app_link = res.Url;
							int index = app_link.IndexOf("?");
							if (index > 0)
								app_link = app_link.Substring(0, app_link.IndexOf('?')); 
							
							mAppLink = new Uri(app_link);
							
							/*
							var index = (new Uri(res.Url)).Query.IndexOf("request_ids");
							if(index != -1)
							{
								// ...have the user interact with the friend who sent the request,
								// perhaps by showing them the gift they were given, taking them
								// to their turn in the game with that friend, etc.
							}
							*/
						}
					}
				);
			}
		}

		private void GetPlayerProfile()
		{
			var request_data = new Dictionary<string, string>();
			request_data["fields"] = PROFILE_FIELDS;
			FB.API("me", HttpMethod.GET,
				(IGraphResult response) =>
				{
					if (AssertResult(response))
					{
						PlayerProfile = PrepareProfile(response.RawResult);

						if (!Initialized)
						{
							Initialized = true;
							CallInitializationCompleteCallback();
						}
					}
				},
				request_data);
		}

		protected override void DoRequestProfiles(IList<string> userids, Action<IList<SocialUserProfile>> callback)
		{
			if (userids != null && userids.Count > 0)
			{
				// не будем отправлять следующий запрос, до тех пор, пока не получим ответ на предыдущий
				if (mAwaitingProfilesCount > 0)
				{
					mRequestProfilesQueue.Enqueue(new RequestProfilesQueueItem()
					{
						userids = userids,
						callback = callback
					});
					return;
				}
				
				// Facebook GraphAPI не позволяет запрашивать информацию о нескольких пользователях в одном запросе,
				// поэтому выполним отдельные вызовы для каждого userid из массива
				mAwaitingProfilesCount = userids.Count;
				mProfiles = new List<SocialUserProfile>();
				mRequestProfilesCallback = callback;
				foreach (string id in userids)
				{
					var request_data = new Dictionary<string, string>();
					request_data["fields"] = PROFILE_FIELDS;

					// change the callback (OnProfile instead of OnProfiles)
					// because facebook supports only one userinfo request per call
					FB.API(id, HttpMethod.GET, OnProfile, request_data);
				}
			}
		}

		private void OnProfile(IGraphResult response)
		{
			mAwaitingProfilesCount--;

			if (AssertResult(response))
			{
				SocialUserProfile rec = PrepareProfile(response.RawResult);
				mProfiles.Add(rec);
				
				// если дождались ответов для всех запрошенных пользователей
				if (mAwaitingProfilesCount <= 0)
				{
					if (mRequestProfilesCallback != null)
					{
						mRequestProfilesCallback.Invoke(mProfiles);
						mRequestProfilesCallback = null;
					}
					else
					{
						OnProfiles(mProfiles);
					}
				}
			}

			// если в очереди ожидают еще запросы, выполним их
			if (mRequestProfilesQueue.Count > 0)
			{
				RequestProfilesQueueItem req = mRequestProfilesQueue.Dequeue();
				DoRequestProfiles(req.userids, req.callback);
			}
		}

		private SocialUserProfile PrepareProfile(string data_string)
		{
			return PrepareProfile(ExpandoObject.FromJSONString(data_string));
		}

		private SocialUserProfile PrepareProfile(ExpandoObject data)
		{
			string profile_id = data.ContainsKey("uid") ? data.SafeGetString("uid") : data.SafeGetString("id");
			SocialUserProfile profile = new SocialUserProfile(profile_id, this.NetworkType);
			profile.FirstName   = data.SafeGetString("first_name");
			profile.LastName    = data.SafeGetString("last_name");
			if (data.ContainsKey("picture") && data["picture"] is ExpandoObject picture_data)
			{ 
				if (picture_data["data"] is ExpandoObject picture_internal_data)
				{
					profile.PhotoSmallURL = picture_internal_data.SafeGetString("url");
				}
			}
			if (string.IsNullOrEmpty(profile.PhotoSmallURL))
				profile.PhotoSmallURL = "https://graph.facebook.com/" + profile.Id + "/picture/?width=50&height=50";
			profile.PhotoMediumURL = "https://graph.facebook.com/" + profile.Id + "/picture/?width=100&height=100";
			//profile.Link         = data.ContainsKey("link") ? Convert.ToString(data["link"]) : data.ContainsKey("profile_url") ? Convert.ToString(data["profile_url"]) : ("http://www.facebook.com/" + profile.Id);
			//profile.Gender       = (((string)(data.ContainsKey("sex") ? data["sex"] : data["gender"]) != "female")) ? 1 : 2;  // 1-мужской, 2-женский; приходит: "male"/"female"
			//profile.Online       = Convert.ToBoolean( data["online"] );

			// добавим в кэш
			AddProfileToCache(profile);
			Debug.Log("[FacebookProvider] PrepareProfile " + profile.ToJSONString());
			
			return profile;
		}

		private bool AssertResult(IResult response)
		{
			// Some platforms return the empty string instead of null.
			if (!String.IsNullOrEmpty(response.Error))
			{
				Debug.Log("[FacebookProvider] response error: " + response.Error);

				//var error_data = new ExpandoObject();
				//error_data["error"] = response.Error;
				//OnError(new ExpandoObject(error_data));

				return false;
			}

			return true;
		}
	}
}
