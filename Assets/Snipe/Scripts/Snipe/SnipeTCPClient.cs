using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Ionic.Zlib;
using MiniIT;
using UnityEngine;

//
// Client to Snipe server
// http://snipeserver.com
// https://github.com/Mini-IT/SnipeWiki/wiki


// Docs on how to use TCP Client:
// http://sunildube.blogspot.ru/2011/12/asynchronous-tcp-client-easy-example.html

namespace MiniIT.Snipe
{
	internal class SnipeTCPClient : SnipeAbstractClient
	{
		private const int CONNECTION_TIMEOUT = 1500;
		private TcpClient mTcpClient = null;
		
		public SnipeTCPClient()
		{
		}

		public void Connect (string host, int port)
		{
			Disconnect();

			bool connected;

			try
			{
				if (mTcpClient == null)
				{
					mTcpClient = new TcpClient(AddressFamily.InterNetwork);
					mTcpClient.ReceiveBufferSize = RECEIVE_BUFFER_SIZE;
					mTcpClient.NoDelay = true;  // send data immediately upon calling NetworkStream.Write
				}

				IPAddress[] host_address = Dns.GetHostAddresses(host);
				//Start the async connect operation
				IAsyncResult connection_result = mTcpClient.BeginConnect(host_address, port, new AsyncCallback(ConnectCallback), mTcpClient);

				connected = connection_result.AsyncWaitHandle.WaitOne(CONNECTION_TIMEOUT);
			}
			catch (Exception e)
			{
				Debug.Log("[SnipeTCPClient] TCP Client initialization failed: " + e.Message);

				connected = false;
				
			}

			if (!connected)
			{
#if DEBUG
				Debug.Log("[SnipeTCPClient] TCP Client connection failed");
#endif

				if (mTcpClient == null)
				{
					mTcpClient.Client?.Close();
					mTcpClient?.Close();
					mTcpClient = null;
				}

				if (OnConnectionFailed != null)
					OnConnectionFailed.Invoke();
			}
		}

		public override void Disconnect()
		{
			mConnected = false;
			
			DisposeBuffer();

			if (mTcpClient != null)
			{
				// BUG?
				// https://stackoverflow.com/questions/9186665/closing-system-net-sockets-tcpclient-kills-the-connection-for-other-tcpclients-a
				mTcpClient.Client?.Close();
				mTcpClient?.Close();
				mTcpClient = null;
			}
		}

		private void ConnectCallback(IAsyncResult result)
		{
			try
			{
				//We are connected successfully
				NetworkStream network_stream = mTcpClient.GetStream();

				byte[] buffer = new byte[mTcpClient.ReceiveBufferSize];
				
				//Now we are connected start asyn read operation
				network_stream.BeginRead(buffer, 0, buffer.Length, ReadCallback, buffer);

				if (mBufferSream == null)
				{
					mBufferSream = new MemoryStream();
					mBufferSream.Capacity = MESSAGE_BUFFER_SIZE;
				}
				else
				{
					mBufferSream.SetLength(0);  // "clearing" buffer
				}

				mConnected = true;
				
				if (OnConnectionSucceeded != null)
					OnConnectionSucceeded.Invoke();
			}
			catch(Exception)
			{
				//Debug.Log("[SnipeTCPClient] ConnectCallback: " + e.Message);

				mConnected = false;

				// send event
				if (OnConnectionFailed != null)
					OnConnectionFailed.Invoke();
			}
		}

		// Callback for Read operation
		private void ReadCallback(IAsyncResult result)
		{
			// ReadCallback is called asynchronously. That is why sometimes it could be called after disconnect and mTcpClient disposal.
			// Just ignoring and waiting for the next connection
			if (mTcpClient == null)
				return;
			
			NetworkStream network_stream;
			
			try
			{
				network_stream = mTcpClient.GetStream();
			}
#pragma warning disable CS0168
			catch (Exception e)
#pragma warning restore CS0168
			{
#if DEBUG
				Debug.Log("[SnipeTCPClient] ReadCallback GetStream error: " + e.Message);
#endif
				Disconnect();
				if (OnConnectionLost != null)
					OnConnectionLost.Invoke();
				return;
			}

			byte[] buffer = result.AsyncState as byte[];

			int bytes_read = 0;

			try
			{
				bytes_read = network_stream.EndRead(result);
			}
#pragma warning disable CS0168
			catch (Exception e)
#pragma warning restore CS0168
			{
#if DEBUG
				Debug.Log("[SnipeTCPClient] ReadCallback stream.EndRead error: " + e.Message);
#endif
				Disconnect();
				if (OnConnectionLost != null)
					OnConnectionLost.Invoke();
				return;
			}

			if (bytes_read > 0)
			{
				using(MemoryStream buf_stream = new MemoryStream(buffer, 0, bytes_read))
				{
					try
					{
						ProcessData(buf_stream);
					}
					catch(Exception ex)
					{
//#if DEBUG
						Debug.Log("[SnipeTCPClient] ProcessData error: " + ex.Message);
//#endif
					}
				}
			}

			//Then start reading from the network again.
			network_stream.BeginRead(buffer, 0, buffer.Length, ReadCallback, buffer);
		}

		protected void ProcessData(MemoryStream buf_stream)
		{
//#if DEBUG
			//Debug.Log("[SnipeTCPClient] portion", buf_stream.Length.ToString());
//#endif
			if (mBufferSream == null)
				return;

			if (buf_stream != null)
			{
				long position = mBufferSream.Position;

				mBufferSream.Position = mBufferSream.Length;
				buf_stream.WriteTo(mBufferSream);

				mBufferSream.Position = position;
			}

			while (mBufferSream.Length - mBufferSream.Position > 0)
			{
				// if the length of the message is not known yet and the buffer contains data
				if (this.mMessageLength == 0 && (mBufferSream.Length - mBufferSream.Position) >= 7)
				{
					// in the beginning of a message the marker must be
					byte[] marker = new byte[4];
					mBufferSream.Read(marker, 0, 4);
					if ( !(marker[0] == MESSAGE_MARKER[0] &&
					       marker[1] == MESSAGE_MARKER[1] &&
					       marker[2] == MESSAGE_MARKER[2] &&
					       marker[3] == MESSAGE_MARKER[3]) )
					{
//#if DEBUG
						//Debug.Log("[SnipeTCPClient] Message marker not found");
//#endif

//						if (OnError != null)
//							OnError(new HapiEventArgs(HapiEventArgs.ERROR, "Message marker not found"));
						// DispatchEvent

						// TODO: handle the error !!!!
						// ...

						// if something wrong with the format then clear buffer of the socket and remove all temporary data,
						// i.e. just ignore all that we have at the moment and we'll wait new messages
						AccidentallyClearBuffer();
						return;
					}

					// first 2 bytes contain the length of the message
					mMessageLength = mBufferSream.ReadByte() * 256 + mBufferSream.ReadByte();
					
					// the 3rd byte contains compression flag (0/1)
					mCompressed = (mBufferSream.ReadByte() == 1);

					continue;
				}
				else if (mMessageLength > 0 && (mBufferSream.Length - mBufferSream.Position) >= mMessageLength)  // if the legth of the message is known and the whole message is already in the buffer
				{
					// if the message is compressed
					if (mCompressed)
					{
						byte[] compressed_buffer = new byte[mMessageLength];
						mBufferSream.Read(compressed_buffer, 0, compressed_buffer.Length);

						byte[] decompressed_buffer = ZlibStream.UncompressBuffer(compressed_buffer);
						mMessageString = UTF8Encoding.UTF8.GetString( decompressed_buffer );
					}
					else
					{
						byte[] str_buf = new byte[mMessageLength];
						mBufferSream.Read(str_buf, 0, mMessageLength);
						mMessageString = UTF8Encoding.UTF8.GetString(str_buf);
					}

					mMessageLength = 0;

					// the message is read
					ExpandoObject response = null;
					try
					{
						response = (ExpandoObject)HaxeUnserializer.Run(mMessageString);
					}
					catch (Exception error)
					{
//#if DEBUG
						Debug.Log("[SnipeTCPClient] Deserialization error: " + error.Message);
						Debug.Log("mMessageString = " + mMessageString);
//#endif
						
//						if (OnError != null)
//							OnError(new HapiEventArgs(HapiEventArgs.ERROR, "Deserialization error: " + error.Message));

						// TODO: handle the error !!!!
						// ...
						
						// if something wrong with the format then clear buffer of the socket and remove all temporary data,
						// i.e. just ignore all that we have at the moment and we'll wait new messages
						AccidentallyClearBuffer();
						return;
					}
					
					// try // ????
					if (response != null)
					{
						if (OnDataReceived != null)
							OnDataReceived.Invoke(response);
					}
				}
				else  // not all of the message's bytes are in the buffer yet
				{
					// wait for the next portion of data
					break;

					// WARNING:
					// this shouldn't happen!!!!!!!!!!!!
				}
			}
		}
		
		public void SendRequest(string message)
		{
			if (this.Connected)
			{
				byte[] buffer = UTF8Encoding.UTF8.GetBytes(message);
				mTcpClient.GetStream().Write(buffer, 0, buffer.Length);
				mTcpClient.GetStream().WriteByte(0);  // every message ends with zero
			}
		}
		
		public override bool Connected
		{
			get
			{
				return mConnected && (mTcpClient != null && mTcpClient.Connected);
			}
		}
	}

}