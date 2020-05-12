using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Ionic.Zlib;
using MiniIT;

//
// Client to Snipe server
// http://snipeserver.com
// https://github.com/Mini-IT/SnipeWiki/wiki

namespace MiniIT.Snipe
{
	internal abstract class SnipeAbstractClient : IDisposable
	{
		protected static readonly int RECEIVE_BUFFER_SIZE = 66560; // buffer size = 65 Kb
		protected static readonly int MESSAGE_BUFFER_SIZE = 307200; // buffer size = 300 Kb
		protected static readonly byte[] MESSAGE_MARKER = new byte[]{0xAA, 0xBB, 0xCD, 0xEF}; // marker of message beginning
		
		internal Action OnConnectionSucceeded;
		internal Action OnConnectionFailed;
		internal Action OnConnectionLost;
		internal Action<ExpandoObject> OnMessageReceived;
		
		protected bool mConnected = false;
		
		protected MemoryStream mBufferSream;

		protected int mMessageLength = 0;
		protected string mMessageString = "";
		protected bool mCompressed; // compression flag: 0 - not compressed, 1 - compressed

		public virtual bool Connected
		{
			get
			{
				return mConnected;
			}
		}

		public SnipeAbstractClient ()
		{
		}

		public abstract void Disconnect();
		

		protected void AccidentallyClearBuffer()
		{
			if (mBufferSream != null)
				mBufferSream.SetLength(0);  // clearing buffer

			mMessageLength = 0;
			mMessageString = "";
		}
		
		protected void DisposeBuffer()
		{
			if (mBufferSream != null)
			{
				mBufferSream.Close();
				mBufferSream.Dispose();
				mBufferSream = null;
			}

			mMessageLength = 0;
			mMessageString = "";
		}
		
		#region IDisposable implementation
		
		public virtual void Dispose()
		{
			this.OnConnectionSucceeded = null;
			this.OnConnectionFailed = null;
			this.OnConnectionLost = null;
			this.OnMessageReceived = null;
			
			Disconnect();
		}
		
		#endregion
	}

}