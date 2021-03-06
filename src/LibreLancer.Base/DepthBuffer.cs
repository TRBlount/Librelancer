﻿using System;
namespace LibreLancer
{
	public class DepthBuffer : IDisposable
	{
		internal uint ID;
		public DepthBuffer(int width, int height)
		{
			ID = GL.GenRenderbuffer();
			GL.BindRenderbuffer(GL.GL_RENDERBUFFER, ID);
			GL.RenderbufferStorage(GL.GL_RENDERBUFFER, GL.GL_DEPTH_COMPONENT24, width, height);
		}
		public void Dispose()
		{
			GL.DeleteRenderbuffer(ID);
		}
	}
}

