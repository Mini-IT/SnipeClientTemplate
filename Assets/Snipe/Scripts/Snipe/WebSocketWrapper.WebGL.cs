using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using MiniIT;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

// WebSocket JS plugin (for WebGL)
// http://forum.unity3d.com/threads/unity5-beta-webgl-unity-websockets-plug-in.277567/

namespace MiniIT.Snipe
{
	public partial class WebSocketWrapper : IDisposable
	{
#if UNITY_WEBGL && !UNITY_EDITOR
		[DllImport("__Internal")]
		private static extern int SocketCreate (string url);
		[DllImport("__Internal")]
		private static extern int SocketState (int socketInstance);
		[DllImport("__Internal")]
		private static extern void SocketSend (int socketInstance, byte[] buf, int length);
		[DllImport("__Internal")]
		private static extern int SocketRecv (int socketInstance, byte[] buf, int length);
		[DllImport("__Internal")]
		private static extern int SocketRecvLength (int socketInstance);
		[DllImport("__Internal")]
		private static extern void SocketClose (int socketInstance);
		[DllImport("__Internal")]
		private static extern string SocketError (int socketInstance);
		
		private int mWebSocketNativeRef = -1;
		
		public void Connect(string url)
		{
			Disconnect();

			StartCoroutine(WebSocketConnect(url));
		}
		
		private IEnumerator WebSocketConnect(string url)
		{
			mWebSocketNativeRef = SocketCreate(url);

#if DEBUG
//			Debug.Log("[SnipeClient] JS WebSocket Connecting");
#endif
			
			while (SocketState(mWebSocketNativeRef) == 0)  // State == 0 means "connecting"
				yield return 0;

#if DEBUG
//			Debug.Log("[SnipeClient] JS WebSocket Connected");
#endif
			
			OnWebSocketConnected();

			while (true)
			{
				if (!WebSocketIsAlive())  // Diconnected?
				{
					OnWebSocketClose();
					break;
				}
				
				byte[] reply = WebSocketReceive();
				if (reply != null && reply.Length > 0)
				{
					if (ProcessMessage != null)
						ProcessMessage(reply);
				}

				string error = WebSocketError;
				if (!string.IsNullOrEmpty(error))
				{
					//Debug.LogError ("[SnipeClient] JS WebSocket Error: "+ error);
					break;
				}

				yield return 0;
			}
		}
		
		protected void OnWebSocketConnected()
		{
			if (OnConnectionOpened != null)
				OnConnectionOpened();
		}
		
		protected void OnWebSocketClose()
		{
			if (OnConnectionClosed != null)
				OnConnectionClosed();
		}
		
		public void WebSocketClose()
		{
			if (mWebSocketNativeRef >= 0)
			{
				SocketClose(mWebSocketNativeRef);
				mWebSocketNativeRef = -1;
			}
		}
		
		public string WebSocketError
		{
			get
			{
				if (mWebSocketNativeRef >= 0 && SocketState(mWebSocketNativeRef) > 0)
					return SocketError (mWebSocketNativeRef);
				else
					return null;
			}
		}
		
		public byte[] WebSocketReceive()
		{
			int length = SocketRecvLength (mWebSocketNativeRef);
			if (length == 0)
				return null;
			byte[] buffer = new byte[length];
			if (SocketRecv (mWebSocketNativeRef, buffer, length) != 0)
				return buffer;
			else
				return null;
		}
		/*
		public string WebSocketReceiveString()
		{
			byte[] retval = WebSocketReceive();
			if (retval == null)
				return null;
			return Encoding.UTF8.GetString (retval);
		}
		*/

		private bool WebSocketIsAlive()
		{
			if (mWebSocketNativeRef >= 0)
			{
				// Possible States are: (https://developer.mozilla.org/ru/docs/Web/API/WebSocket#Ready_state_constants)
				// 0 - CONNECTING
				// 1 - OPEN
				// 2 - CLOSING
				// 3 - CLOSED

				int state = SocketState(mWebSocketNativeRef);
				return state == 0 || state == 1;
			}
			return false;
		}
		
		public void SendRequest(string message)
		{
			SendRequest(UTF8Encoding.UTF8.GetBytes(message));
		}
		
		public void SendRequest(byte[] buffer)
		{
			SocketSend(mWebSocketNativeRef, buffer, buffer.Length);
		}

		public void Ping()
		{
			// TODO
		}
		
		public bool Connected
		{
			get
			{
				return WebSocketIsAlive();
			}
		}
		
		public void Disconnect()
		{
			WebSocketClose();
		}

#endif
	}

}