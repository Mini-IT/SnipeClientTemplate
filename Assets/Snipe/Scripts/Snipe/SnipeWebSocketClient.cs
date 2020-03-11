using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using UnityEngine;
using Ionic.Zlib;
using MiniIT;

//
// Client to Snipe server
// http://snipeserver.com
// https://github.com/Mini-IT/SnipeWiki/wiki


namespace MiniIT.Snipe
{
	internal class SnipeWebSocketClient : SnipeAbstractClient
	{
		private WebSocketWrapper mWebSocketClient = null;

		public SnipeWebSocketClient ()
		{
		}

		public void Connect(string url)
		{
			Disconnect();

//#if DEBUG
			Debug.Log("[SnipeWebSocketClient] WebSocket Connect to " + url);
//#endif

			mWebSocketClient = new WebSocketWrapper();
			mWebSocketClient.OnConnectionOpened = OnWebSocketConnected;
			mWebSocketClient.OnConnectionClosed = OnWebSocketClose;
			mWebSocketClient.ProcessMessage = ProcessData;
			mWebSocketClient.Connect(url);
		}

		protected void OnWebSocketConnected()
		{
//#if DEBUG
			Debug.Log("[SnipeWebSocketClient] OnWebSocketConnected");
//#endif
			
			mConnected = true;

			if (OnConnectionSucceeded != null)
				OnConnectionSucceeded.Invoke();
		}
		
		protected void OnWebSocketClose()
		{
			Debug.Log("[SnipeWebSocketClient] OnWebSocketClose");

			if (this.mConnected)
			{
				Disconnect();
				if (OnConnectionLost != null)
					OnConnectionLost.Invoke();
				else if (OnConnectionFailed != null)
					OnConnectionFailed.Invoke();
			}
			else
			{
				Disconnect();
				if (OnConnectionFailed != null)
					OnConnectionFailed.Invoke();
			}
		}

		/*
		protected void OnWebSocketError (object sender, WebSocketSharp.ErrorEventArgs e)
		{
			Debug.Log("[SnipeWebSocketClient] OnWebSocketError: " + e.Message);
			//DispatchEvent(ErrorHappened);
		}
		*/
	
		public override void Disconnect()
		{
			mConnected = false;
			
			DisposeBuffer();

			if (mWebSocketClient != null)
			{
				mWebSocketClient.Dispose();
				mWebSocketClient = null;
			}
		}

		// Process WebSocket Message
		protected void ProcessData(byte[] raw_data_buffer, int data_length = -1)
		{
			if (raw_data_buffer != null && raw_data_buffer.Length > 0)
			{
				using (MemoryStream buf_stream = new MemoryStream(raw_data_buffer))
				{
					buf_stream.Position = 0;
					
					try
					{
						if (data_length < 1)
						{
							data_length = Convert.ToInt32(buf_stream.Length);
						}
						
						// the 1st byte contains compression flag (0/1)
						mCompressed = (buf_stream.ReadByte() == 1);
						mMessageLength = data_length - 1;

						if (mCompressed)
						{
							byte[] compressed_buffer = new byte[mMessageLength];
							buf_stream.Read(compressed_buffer, 0, compressed_buffer.Length);

							byte[] decompressed_buffer = ZlibStream.UncompressBuffer(compressed_buffer);
							mMessageString = UTF8Encoding.UTF8.GetString( decompressed_buffer );

//#if DEBUG
//							Debug.Log("[SnipeWebSocketClient] decompressed mMessageString = " + mMessageString);
//#endif
						}
						else
						{
							byte[] str_buf = new byte[mMessageLength];
							buf_stream.Read(str_buf, 0, mMessageLength);
							mMessageString = UTF8Encoding.UTF8.GetString(str_buf);

//#if DEBUG
//							Debug.Log("[SnipeWebSocketClient] mMessageString = " + mMessageString);
//#endif
						}
					}
					catch(Exception)
					{
//#if DEBUG
						//Debug.Log("[SnipeWebSocketClient] OnWebSocketMessage ProcessData error: " + ex.Message);
//#endif
						//CheckConnectionLost();
					}

					mMessageLength = 0;

					// the message is read

					try
					{
						ExpandoObject response = (ExpandoObject)HaxeUnserializer.Run(mMessageString);

						if (response != null)
						{
							if (OnMessageReceived != null)
								OnMessageReceived.Invoke(response);
						}
					}
#if DEBUG
					catch (Exception error)
					{
						Debug.Log("[SnipeWebSocketClient] Deserialization error: " + error.Message);
#else
					catch (Exception)
					{
#endif
						// if (OnError != null)
						//		OnError(new HapiEventArgs(HapiEventArgs.ERROR, "Deserialization error: " + error.Message));

						// TODO: handle the error !!!!
						// ...

						// if something wrong with the format then clear buffer of the socket and remove all temporary data,
						// i.e. just ignore all that we have at the moment and we'll wait new messages
						AccidentallyClearBuffer();
						return;
					}
				}
			}
		}

		public void SendRequest(string message)
		{
			if (this.Connected)
			{
				mWebSocketClient.SendRequest(message);
			}
		}
		
		public override bool Connected
		{
			get
			{
				return mConnected && WebSocketConnected;
			}
		}
		
		protected bool WebSocketConnected
		{
			get
			{
				return (mWebSocketClient != null && mWebSocketClient.Connected);
			}
		}
	}

}