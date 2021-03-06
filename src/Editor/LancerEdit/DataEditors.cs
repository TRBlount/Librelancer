﻿/* The contents of this file are subject to the Mozilla Public License
 * Version 1.1 (the "License"); you may not use this file except in
 * compliance with the License. You may obtain a copy of the License at
 * http://www.mozilla.org/MPL/
 * 
 * Software distributed under the License is distributed on an "AS IS"
 * basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See the
 * License for the specific language governing rights and limitations
 * under the License.
 * 
 * 
 * The Initial Developer of the Original Code is Callum McGing (mailto:callum.mcging@gmail.com).
 * Portions created by the Initial Developer are Copyright (C) 2013-2018
 * the Initial Developer. All Rights Reserved.
 */
using System;
using LibreLancer;
using ImGuiNET;
namespace LancerEdit
{
	public unsafe class DataEditors
	{
		public static void IntEditor(string title, ref int[] ints, ref bool intHex, LUtfNode selectedNode)
		{
			if (ImGui.BeginPopupModal(title))
			{
				bool remove = false;
				bool add = false;
				ImGui.Text(string.Format("Count: {0} ({1} bytes)", ints.Length, ints.Length * 4));
				ImGui.SameLine();
				add = ImGui.Button("+");
				ImGui.SameLine();
				remove = ImGui.Button("-");
				ImGui.SameLine();
				ImGui.Checkbox("Hex", ref intHex);
				ImGui.Separator();
				//Magic number 94px seems to fix the scrollbar thingy
				var h = ImGui.GetWindowHeight();
				ImGui.BeginChild("##scroll", new Vector2(0, h - 94), false, 0);
				ImGui.Columns(4, "##columns", true);
				fixed (int* ptr = ints)
				{
					for (int i = 0; i < ints.Length; i++)
					{
						ImGuiNative.igInputInt("##" + i.ToString(), &ptr[i], 0, 0, intHex ? InputTextFlags.CharsHexadecimal : InputTextFlags.CharsDecimal);
						ImGui.NextColumn();
						if (i % 4 == 0 && i != 0) ImGui.Separator();
					}
				}
				ImGui.EndChild();
				if (ImGui.Button("Ok"))
				{
					var bytes = new byte[ints.Length * 4];
					fixed (byte* ptr = bytes)
					{
						var f = (int*)ptr;
						for (int i = 0; i < ints.Length; i++) f[i] = ints[i];
					}
					selectedNode.Data = bytes;
					ints = null;
					ImGui.CloseCurrentPopup();
				}
				ImGui.SameLine();
				if (ImGui.Button("Cancel")) { ints = null; ImGui.CloseCurrentPopup(); }
				ImGui.EndPopup();
				if (add) Array.Resize(ref ints, ints.Length + 1);
				if (remove && ints.Length > 1) Array.Resize(ref ints, ints.Length - 1);
			}
		}
		public static void FloatEditor(string title, ref float[] floats, LUtfNode selectedNode)
		{
			if (ImGui.BeginPopupModal(title))
			{
				bool remove = false;
				bool add = false;
				ImGui.Text(string.Format("Count: {0} ({1} bytes)", floats.Length, floats.Length * 4));
				ImGui.SameLine();
				add = ImGui.Button("+");
				ImGui.SameLine();
				remove = ImGui.Button("-");
				ImGui.Separator();
				//Magic number 94px seems to fix the scrollbar thingy
				var h = ImGui.GetWindowHeight();
				ImGui.BeginChild("##scroll", new Vector2(0, h - 94), false, 0);
				ImGui.Columns(4, "##columns", true);
				fixed (float* ptr = floats)
				{
					for (int i = 0; i < floats.Length; i++)
					{
						ImGuiNative.igInputFloat("##" + i.ToString(), &ptr[i], 0.0f, 0.0f, 4, InputTextFlags.CharsDecimal);
						ImGui.NextColumn();
						if (i % 4 == 0 && i != 0) ImGui.Separator();
					}
				}
				ImGui.EndChild();
				if (ImGui.Button("Ok"))
				{
					var bytes = new byte[floats.Length * 4];
					fixed (byte* ptr = bytes)
					{
						var f = (float*)ptr;
						for (int i = 0; i < floats.Length; i++) f[i] = floats[i];
					}
					selectedNode.Data = bytes;
					floats = null;
					ImGui.CloseCurrentPopup();
				}
				ImGui.SameLine();
				if (ImGui.Button("Cancel")) { floats = null; ImGui.CloseCurrentPopup(); }
				ImGui.EndPopup();
				if (add) Array.Resize(ref floats, floats.Length + 1);
				if (remove && floats.Length > 1) Array.Resize(ref floats, floats.Length - 1);
			}
		}
	}
}
