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
using System.Text;
using System.Collections.Generic;
using LibreLancer;
using LibreLancer.Media;
using ImGuiNET;
namespace LancerEdit
{
	public class MainWindow : Game
	{
		ImGuiHelper guiHelper;
		public AudioManager Audio;
		public ResourceManager Resources;
		public Billboards Billboards;
		public PolylineRender Polyline;
		public PhysicsDebugRenderer DebugRender;
		public ViewportManager Viewport;
		public CommandBuffer Commands; //This is a huge object - only have one
		public MaterialMap MaterialMap;
        TextBuffer logBuffer;
        StringBuilder logText = new StringBuilder();
        static readonly string[] defaultFilters = {
            "Linear", "Bilinear", "Trilinear"
        };
        string[] filters;
        int[] anisotropyLevels;
        int cFilter = 2;
        FileDialogFilters UtfFilters = new FileDialogFilters(
            new FileFilter("All Utf Files","utf","cmp","3db","vms","mat","txm","ale"),
            new FileFilter("Utf Files","utf"),
            new FileFilter("Cmp Files","cmp"),
            new FileFilter("3db Files","3db"),
            new FileFilter("Vms Files","vms"),
            new FileFilter("Mat Files","mat"),
            new FileFilter("Txm Files","txm"),
            new FileFilter("Ale Files","ale")
        );
        FileDialogFilters ColladaFilters = new FileDialogFilters(
            new FileFilter("Collada Files", "dae")
        );
        public MainWindow(bool useDX9) : base(800,600,false,useDX9)
		{
			MaterialMap = new MaterialMap();
			MaterialMap.AddRegex(new LibreLancer.Ini.StringKeyValue("^nomad.*$", "NomadMaterialNoBendy"));
			MaterialMap.AddRegex(new LibreLancer.Ini.StringKeyValue("^n-texture.*$", "NomadMaterialNoBendy"));
            FLLog.UIThread = this;
            FLLog.AppendLine = (x) =>
            {
                logText.AppendLine(x);
                if (logText.Length > 16384)
                {
                    logText.Remove(0, logText.Length - 16384);
                }
                logBuffer.SetText(logText.ToString());
            };
            logBuffer = new TextBuffer(32768);
		}

		protected override void Load()
		{
			Title = "LancerEdit";
			guiHelper = new ImGuiHelper(this);
			Audio = new AudioManager(this);
            FileDialog.RegisterParent(this);
			Viewport = new ViewportManager(RenderState);
            var texturefilters = new List<string>(defaultFilters);
            if (RenderState.MaxAnisotropy > 0) {
                anisotropyLevels = RenderState.GetAnisotropyLevels();
                foreach(var lvl in anisotropyLevels) {
                    texturefilters.Add(string.Format("Anisotropic {0}x", lvl));
                }
            }
            filters = texturefilters.ToArray();
			Resources = new ResourceManager(this);
			Commands = new CommandBuffer();
			Billboards = new Billboards();
			Polyline = new PolylineRender(Commands);
			DebugRender = new PhysicsDebugRenderer();
			Viewport.Push(0, 0, 800, 600);
		}

		bool openAbout = false;
		public List<DockTab> tabs = new List<DockTab>();
		public List<MissingReference> MissingResources = new List<MissingReference>();
		public List<uint> ReferencedMaterials = new List<uint>();
		public List<string> ReferencedTextures = new List<string>();
		public bool ClipboardCopy = true;
		public LUtfNode Clipboard;
		List<DockTab> toAdd = new List<DockTab>();
		public UtfTab ActiveTab;
		double frequency = 0;
		int updateTime = 10;
		public void AddTab(DockTab tab)
		{
			toAdd.Add(tab);
		}
		protected override void Update(double elapsed)
		{
			foreach (var tab in tabs)
				tab.Update(elapsed);
		}
        DockTab selected;
        TextBuffer errorText;
        bool showLog = false;
        bool showOptions = false;
        float h1 = 200, h2 = 200;
		protected override void Draw(double elapsed)
		{
			EnableTextInput();
			Viewport.Replace(0, 0, Width, Height);
			RenderState.ClearColor = new Color4(0.2f, 0.2f, 0.2f, 1f);
			RenderState.ClearAll();
			guiHelper.NewFrame(elapsed);
			ImGui.PushFont(ImGuiHelper.Noto);
			ImGui.BeginMainMenuBar();
			if (ImGui.BeginMenu("File"))
			{
				if (Theme.IconMenuItem("New", "new", Color4.White, true))
				{
					var t = new UtfTab(this, new EditableUtf(), "Untitled");
					ActiveTab = t;
                    AddTab(t);
				}
				if (Theme.IconMenuItem("Open", "open", Color4.White, true))
				{
                    var f = FileDialog.Open(UtfFilters);
					if (f != null && DetectFileType.Detect(f) == FileType.Utf)
					{
						var t = new UtfTab(this, new EditableUtf(f), System.IO.Path.GetFileName(f));
						ActiveTab = t;
                        AddTab(t);
					}
				}
				if (ActiveTab == null)
				{
					Theme.IconMenuItem("Save", "save", Color4.LightGray, false);
				}
				else
				{
					if (Theme.IconMenuItem(string.Format("Save '{0}'", ActiveTab.DocumentName), "save", Color4.White, true))
					{
                        var f = FileDialog.Save(UtfFilters);
						if (f != null)
						{
							ActiveTab.DocumentName = System.IO.Path.GetFileName(f);
                            ActiveTab.UpdateTitle();
							ActiveTab.Utf.Save(f);
						}
					}
				}
				if (Theme.IconMenuItem("Quit", "quit", Color4.White, true))
				{
					Exit();
				}
				ImGui.EndMenu();
			}
            bool openerror = false;
			if (ImGui.BeginMenu("Tools"))
			{
                if(ImGui.MenuItem("Options"))
                {
                    showOptions = true;
                }
                if(ImGui.MenuItem("Log"))
                {
                    showLog = true;
                }
				if (ImGui.MenuItem("Resources"))
				{
					AddTab(new ResourcesTab(Resources, MissingResources, ReferencedMaterials, ReferencedTextures));
				}
                if(ImGui.MenuItem("Import Collada"))
                {
                    string input;
                    if((input = FileDialog.Open(ColladaFilters)) != null) {
                        List<ColladaObject> dae = null;
                        try
                        {
                            dae = ColladaSupport.Parse(input);
                            AddTab(new ColladaTab(dae,System.IO.Path.GetFileName(input),this));
                        }
                        catch (Exception ex)
                        {
                            if (errorText != null) errorText.Dispose();
                            var str = "Import Error:\n" + ex.Message + "\n" + ex.StackTrace;
                            errorText = new TextBuffer();
                            errorText.SetText(str);
                            openerror = true;
                        }
                    }
                }
				ImGui.EndMenu();
			}
			if (ImGui.BeginMenu("Help"))
			{
				if (Theme.IconMenuItem("About","about",Color4.White,true))
				{
					openAbout = true;
				}
				ImGui.EndMenu();
			}
			if (openAbout)
			{
				ImGui.OpenPopup("About");
				openAbout = false;
			}
            if (openerror) ImGui.OpenPopup("Error");
            if(ImGui.BeginPopupModal("Error", WindowFlags.AlwaysAutoResize))
            {
                ImGui.Text("Error:");
                ImGui.InputTextMultiline("##etext", errorText.Pointer, (uint)errorText.Size,
                                         new Vector2(430, 200), InputTextFlags.ReadOnly, errorText.Callback);
                if (ImGui.Button("OK")) ImGui.CloseCurrentPopup();
                ImGui.EndPopup();
            }
			if (ImGui.BeginPopupModal("About", WindowFlags.AlwaysAutoResize))
			{
				ImGui.Text("LancerEdit");
				ImGui.Text("Callum McGing 2018");
                ImGui.Separator();
                ImGui.Text("Icons from Icons8: https://icons8.com/");
                ImGui.Text("Icons from komorra: https://opengameart.org/content/kmr-editor-icon-set");
                ImGui.Separator();
				if (ImGui.Button("OK")) ImGui.CloseCurrentPopup();
				ImGui.EndPopup();
			}
			var menu_height = ImGui.GetWindowSize().Y;
			ImGui.EndMainMenuBar();
			var size = (Vector2)ImGui.GetIO().DisplaySize;
			size.Y -= menu_height;
			//Window
			MissingResources.Clear();
			ReferencedMaterials.Clear();
			ReferencedTextures.Clear();
			foreach (var tab in tabs)
			{
				tab.DetectResources(MissingResources, ReferencedMaterials, ReferencedTextures);
			}
            ImGui.SetNextWindowSize(new Vector2(size.X, size.Y - 25), Condition.Always);
            ImGui.SetNextWindowPos(new Vector2(0, menu_height), Condition.Always, Vector2.Zero);
            bool childopened = true;
            ImGui.BeginWindow("tabwindow", ref childopened,
                              WindowFlags.NoTitleBar |
                              WindowFlags.NoSavedSettings |
                              WindowFlags.NoBringToFrontOnFocus |
                              WindowFlags.NoMove |
                              WindowFlags.NoResize);
            TabHandler.TabLabels(tabs, ref selected);
            var totalH = ImGui.GetWindowHeight();
            if (showLog)
            {
                ImGuiExt.SplitterV(2f, ref h1, ref h2, 8, 8, -1);
                h1 = totalH - h2 - 24f;
                if (tabs.Count > 0) h1 -= 20f;
                ImGui.BeginChild("###tabcontent" + (selected != null ? selected.Title : ""),new Vector2(-1,h1),false,WindowFlags.Default);
            } else
                ImGui.BeginChild("###tabcontent" + (selected != null ? selected.Title : ""));
            if (selected != null)
            {
                selected.Draw();
                selected.SetActiveTab(this);
            }
            else
                ActiveTab = null;
            ImGui.EndChild();
            TabHandler.DrawTabDrag(tabs);
            if(showLog) {
                ImGui.BeginChild("###log", new Vector2(-1, h2), false, WindowFlags.Default);
                ImGui.Text("Log");
                ImGui.SameLine(ImGui.GetWindowWidth() - 20);
                if (Theme.IconButton("closelog", "x", Color4.White))
                    showLog = false;
                ImGui.InputTextMultiline("##logtext", logBuffer.Pointer, 32768, new Vector2(-1, h2 - 24),
                                         InputTextFlags.ReadOnly, logBuffer.Callback);
                ImGui.EndChild();
            }
            ImGui.EndWindow();
			//Status bar
			ImGui.SetNextWindowSize(new Vector2(size.X, 25f), Condition.Always);
			ImGui.SetNextWindowPos(new Vector2(0, size.Y - 6f), Condition.Always, Vector2.Zero);
			bool sbopened = true;
			ImGui.BeginWindow("statusbar", ref sbopened, 
			                  WindowFlags.NoTitleBar | 
			                  WindowFlags.NoSavedSettings | 
			                  WindowFlags.NoBringToFrontOnFocus | 
			                  WindowFlags.NoMove | 
			                  WindowFlags.NoResize);
			if (updateTime > 9)
			{
				updateTime = 0;
				frequency = RenderFrequency;
			}
			else { updateTime++; }
			string activename = ActiveTab == null ? "None" : ActiveTab.DocumentName;
			string utfpath = ActiveTab == null ? "None" : ActiveTab.GetUtfPath();
			ImGui.Text(string.Format("FPS: {0} | {1} Materials | {2} Textures | Active: {3} - {4}",
									 (int)Math.Round(frequency),
									 Resources.MaterialDictionary.Count,
									 Resources.TextureDictionary.Count,
									 activename,
									 utfpath));
			ImGui.EndWindow();
            if(showOptions) {
                ImGui.BeginWindow("Options", ref showOptions, WindowFlags.AlwaysAutoResize);
                var pastC = cFilter;
                ImGui.Combo("Texture Filter", ref cFilter, filters);
                if(cFilter != pastC) {
                    switch(cFilter) {
                        case 0:
                            RenderState.PreferredFilterLevel = TextureFiltering.Linear;
                            break;
                        case 1:
                            RenderState.PreferredFilterLevel = TextureFiltering.Bilinear;
                            break;
                        case 2:
                            RenderState.PreferredFilterLevel = TextureFiltering.Trilinear;
                            break;
                        default:
                            RenderState.AnisotropyLevel = anisotropyLevels[cFilter - 3];
                            RenderState.PreferredFilterLevel = TextureFiltering.Anisotropic;
                            break;
                    }
                }
                ImGui.EndWindow();
            }
			ImGui.PopFont();
			guiHelper.Render(RenderState);
            foreach (var tab in toAdd)
            {
                tabs.Add(tab);
                selected = tab;
            }
            toAdd.Clear();
		}

		protected override void Cleanup()
		{
			Audio.Dispose();
		}
	}
}
