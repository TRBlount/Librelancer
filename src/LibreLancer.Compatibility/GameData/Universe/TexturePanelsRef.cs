﻿using System;
using System.Collections.Generic;
using LibreLancer.Ini;
namespace LibreLancer.Compatibility.GameData.Universe
{
	public class TexturePanelsRef
	{
		public List<string> Files { get; private set; }

		public TexturePanelsRef (Section section, FreelancerData gameData)
		{
			Files = new List<string> ();
			foreach (Entry e in section)
			{
				switch (e.Name.ToLowerInvariant())
				{
				case "file":
					if (e.Count != 1)
						throw new Exception ("Invalid number of values in " + section.Name + " Entry " + e.Name + ": " + e.Count);
					Files.Add (gameData.Freelancer.DataPath + e [0].ToString ());
					break;
				default: throw new Exception("Invalid Entry in " + section.Name + ": " + e.Name);
				}
			}
		}
	}
}

