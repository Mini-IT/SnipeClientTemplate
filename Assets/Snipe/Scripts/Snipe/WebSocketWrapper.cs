using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using MiniIT;
#if !(UNITY_WEBGL && !UNITY_EDITOR)
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
#endif

namespace MiniIT.Snipe
{
	public partial class WebSocketWrapper : IDisposable
	{
		#pragma warning disable 0067

		public Action OnConnectionOpened;
		public Action OnConnectionClosed;
		public Action<byte[], int> ProcessMessage;

		#pragma warning restore 0067

#if !(UNITY_WEBGL && !UNITY_EDITOR)

		private const int RECEIVE_MESSAGE_BUFFER_SIZE = 40960; // buffer size = 40 Kb
		private const int RECEIVE_CHUNK_BUFFER_SIZE = 4096; // receive chunk buffer size = 4 Kb
		private const int TASK_DELAY = 10; // milliseconds

		private ClientWebSocket mWebSocketClient = null;
		
		private Queue<string> mWebSocketSendQueue;
#endif

		public WebSocketWrapper()
		{
		}

#if !(UNITY_WEBGL && !UNITY_EDITOR)
		public void Connect(string url)
		{
			Disconnect();

#pragma warning disable CS4014
			StartWebSocketTask(url);
#pragma warning restore CS4014
		}

		protected async Task StartWebSocketTask(string url)
		{
			try
			{
				mWebSocketClient = new ClientWebSocket();
				await mWebSocketClient.ConnectAsync(new Uri(url), CancellationToken.None);
				OnWebSocketConnected();
				
				await Task.WhenAll(WebSocketReceive(mWebSocketClient), WebSocketSend(mWebSocketClient));
				//debug_log += "\n" + ("[SnipeClient] WebSocket tasks finished");
			}
			catch (Exception)
			{
				//debug_log += "\n" + ("[SnipeClient] WebSocket Client initialization faled: " + e.Message);

				if (OnConnectionClosed != null)
					OnConnectionClosed();
			}
			finally
			{
				//debug_log += "\n" + ("[SnipeClient] WebSocket - disposing");
				if (mWebSocketClient != null)
					mWebSocketClient.Dispose();
			}
		}
		
		private async Task WebSocketReceive(ClientWebSocket websocket)
		{
			byte[] message_buffer = new byte[RECEIVE_MESSAGE_BUFFER_SIZE];
			byte[] chunk_buffer = new byte[RECEIVE_CHUNK_BUFFER_SIZE];
			int message_length = 0;
			while (websocket.State == WebSocketState.Open)
			{
				WebSocketReceiveResult result = await websocket.ReceiveAsync(new ArraySegment<byte>(chunk_buffer), CancellationToken.None);
				if (result.MessageType == WebSocketMessageType.Close)
				{
					await websocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
					OnWebSocketClose();
				}
				else
				{
					if (message_buffer.Length < message_length + result.Count)
					{
						Array.Resize(ref message_buffer, message_length + RECEIVE_CHUNK_BUFFER_SIZE);
					}
					
					System.Buffer.BlockCopy(chunk_buffer, 0, message_buffer, message_length, result.Count);
					message_length += result.Count;
					
					if (result.EndOfMessage)
					{
						if (ProcessMessage != null)
							ProcessMessage(message_buffer, message_length);
						message_length = 0;
					}
				}
				
				await Task.Delay(TASK_DELAY);
			}
		}
		
		private async Task WebSocketSend(ClientWebSocket websocket)
		{
			while (websocket.State == WebSocketState.Open)
			{
				if (mWebSocketSendQueue == null || mWebSocketSendQueue.Count == 0)
				{
					await Task.Delay(TASK_DELAY);
					continue;
				}
				
				byte[] buffer = UTF8Encoding.UTF8.GetBytes(mWebSocketSendQueue.Dequeue());
				await websocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
			}
		}

		protected void OnWebSocketConnected()
		{
			mWebSocketSendQueue = new Queue<string>();
			
			if (OnConnectionOpened != null)
				OnConnectionOpened();
		}
		
		protected void OnWebSocketClose()
		{
			if (OnConnectionClosed != null)
				OnConnectionClosed();
		}

		/*
		protected void OnWebSocketError (object sender, WebSocketSharp.ErrorEventArgs e)
		{
			debug_log += "\n" + ("[SnipeClient] OnWebSocketError: " + e.Message);
			//DispatchEvent(ErrorHappened);

			if (mWebSocket != null && !mWebSocket.IsAlive)
				Disconnect();
		}
		*/
		
		public void Disconnect()
		{
			if (mWebSocketClient != null && mWebSocketClient.State != WebSocketState.Closed)
			{
				try
				{
					mWebSocketClient.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
				}
				catch (Exception) { }
				finally
				{
					mWebSocketClient.Dispose();
					mWebSocketClient = null;
				}
			}
			
			if (mWebSocketSendQueue != null)
			{
				mWebSocketSendQueue = null;
			}
		}

		public void SendRequest(string message)
		{
			if (mWebSocketSendQueue != null)
			{
				mWebSocketSendQueue.Enqueue(message);
			}
		}

		public bool Connected
		{
			get
			{
				return (mWebSocketClient != null && (mWebSocketClient.State == WebSocketState.Open /*|| mWebSocketClient.State == WebSocketState.Connecting*/));
			}
		}
#endif

		#region IDisposable implementation
		
		public void Dispose()
		{
			Disconnect();
		}
		
		#endregion
	}

}