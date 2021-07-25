using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using AlbLib;
using AlbLib.Imaging;
using AlbLib.Texts;
using AlbLib.XLD;

namespace albtool
{
	class Program
	{
		static Program()
		{
			AppDomain.CurrentDomain.AssemblyResolve += AssemblyResolve;
		}
		
		public static void Main(string[] args)
		{
			//args = new[]{"-e", "-f", "xlp", "3DBCKGR0.XLD:2", "patch.xlp"};
			//args = new[]{"-i", "-f", "xlp", "patch.xlp", "test.XLz"};
			
			
			Paths.SetXLDLIBS("./");
			Console.WriteLine("albtool (2015) by IllidanS4, version "+Assembly.GetExecutingAssembly().GetName().Version);
			if(args.Length == 0)
			{
				Console.WriteLine("Use --help to show the list of commands.");
				return;
			}
			try{
				if(args.Length >= 3)
				{
					RenderInfo info = new RenderInfo();
					string action = null;
					string format = null, input = null, output = null;
					
					for(int i = 0; i < args.Length; i++)
					{
						string arg = args[i];
						switch(arg)
						{
							case "-c":case "-e":case "-i":case "-m":case "-v":
								if(action != null)
								{
									Console.WriteLine("Action already specified.");
									return;
								}
								action = arg;
								break;
							case "-f":
								if(format != null)
								{
									Console.WriteLine("Format already specified.");
									return;
								}
								format = args[++i];
								break;
							case "-p":
								if(info.Palette != null)
								{
									Console.WriteLine("Palette already specified.");
									return;
								}
								info.Palette = Int32.Parse(args[++i]);
								break;
							case "-d":
								Paths.SetXLDLIBS(args[++i]);
								break;
							case "-w":
								if(info.Width != null)
								{
									Console.WriteLine("Width already specified.");
									return;
								}
								info.Width = Int32.Parse(args[++i]);
								break;
							case "-h":
								if(info.Height != null)
								{
									Console.WriteLine("Height already specified.");
									return;
								}
								info.Height = Int32.Parse(args[++i]);
								break;
							default:
								if(input == null)
								{
									input = arg;
								}else if(output == null)
								{
									output = arg;
								}else{
									Console.WriteLine("Unknown argument: "+arg);
									return;
								}
								break;
						}
					}
					
					switch(action)
					{
						case "-v":case "/v":case "--version":
							Version ver = Version.Parse(input);
							DateTime date = DateTime.Parse(output);
							UpdateVersion("main.exe", ver, date);
							UpdateVersion("albion.exe", ver, date);
							UpdateVersion("sr-main.exe", ver, date);
							return;
					}
					
					short[] insubfiles = ExtractSubfiles(ref input);
					short[] outsubfiles = ExtractSubfiles(ref output);
					
					format = format ?? "bin";
					Format fmt = null;
					if(format != "xlp" && !Format.Formats.TryGetValue(format, out fmt))
					{
						Console.WriteLine("Invalid format specified (use -lf to list all formats)");
						return;
					}
					switch(action)
					{
						case "-m":case "/m":case "--merge":
							var p1 = new XLDPatch(input);
							if(File.Exists(output))
							{
								var p2 = new XLDPatch(output);
								p1.Subfiles.AddRange(p2.Subfiles);
							}
							p1.Save(output);
							return;
						case "-c":case "/c":case "--convert":
							if(format == "xlp")
							{
								byte[] data;
								if(File.Exists(output))
								{
									data = File.ReadAllBytes(output);
								}else{
									data = new byte[0];
								}
								/*using(var stream = new FileStream(input, FileMode.Open))
								{
									XLDPatchSubfile pat = new XLDPatchSubfile(new BinaryReader(stream), (int)stream.Length);
									pat.ModifyBytes(ref data);
								}
								File.WriteAllBytes(output, data);*/
								XLDPatch pat = new XLDPatch(input);
								foreach(var sub in pat.Subfiles)
								{
									sub.ModifyBytes(ref data);
								}
								File.WriteAllBytes(output, data);
							}else{
								using(var buffer = new MemoryStream())
								{
									fmt.Load(input, buffer, info);
									fmt.Save(buffer, (int)buffer.Length, output, info);
								}
							}
							return;
						case "-e":case "/e":case "--extract":
							using(var stream = OpenXLDGzip(input, FileMode.Open))
							{
								XLDNavigator nav = new XLDNavigator(stream);
								if(format == "xlp")
								{
									XLDPatch patch = new XLDPatch();
									foreach(var i in ListSubfiles(nav, insubfiles))
									{
										byte[] data = new byte[nav.SubfileLength];
										nav.Read(data, 0, data.Length);
										patch.Subfiles.Add(new XLDPatchSubfile(i, data));
									}
									patch.Save(output);
								}else{
									string outpath;
									if(output.Contains("*") || (insubfiles != null && insubfiles.Length == 1))
									{
										outpath = output.Replace("*", "{0}");
									}else{
										outpath = Path.Combine(output, "{0}."+fmt.DefaultExtension);
									}
									foreach(var i in ListSubfiles(nav, insubfiles))
									{
										try{
											string outfile = String.Format(outpath, i);
											fmt.Save(nav, nav.SubfileLength, outfile, info);
											Console.WriteLine("Exported "+Path.GetFileNameWithoutExtension(input)+":"+i+" as "+Path.GetFileName(outfile));
										}catch(Exception e)
										{
											Console.WriteLine("Error while exporting file:");
											Console.WriteLine(e.Message);
										}
									}
								}
							}
							return;
						case "-i":case "/i":case "--import":
							if(format == "xlp")
							{
								XLDPatch patch = new XLDPatch(input);
								XLDFile xld;
								List<XLDSubfile> subs;
								if(File.Exists(output))
								{
									using(var stream = OpenXLDGzip(output, FileMode.Open))
									{
										xld = XLDFile.Parse(stream);
									}
									subs = xld.Subfiles.ToList();
									xld = new XLDFile(subs);
								}else{
									subs = new List<XLDSubfile>();
									xld = new XLDFile(subs);
								}
								foreach(var mod in patch.Subfiles)
								{
									if(subs.Count <= mod.Index)
									{
										subs.AddRange(Enumerable.Range(subs.Count, mod.Index-subs.Count+1).Select(i => new XLDSubfile(new byte[0], unchecked((short)i))));
									}
									mod.ModifyXLD(subs[mod.Index]);
								}
								using(var stream = OpenXLDGzip(output, FileMode.Create))
								{
									xld.Save(stream);
								}
							}else if(IsXLPPath(output))
							{
								byte[] data;
								using(var buffer = new MemoryStream())
								{
									fmt.Load(input, buffer, info);
									data = buffer.ToArray();
								}
								XLDPatch pat;
								if(File.Exists(output))
								{
									pat = new XLDPatch(output);
								}else{
									pat = new XLDPatch();
								}
								pat.Subfiles.Add(new XLDPatchSubfile(outsubfiles.Single(), data));
								pat.Save(output);
							}else{
								Console.WriteLine("Not implemented yet.");
							}
							return;
					}
				}
				switch(args.Single())
				{
					case "--help":case "-?":case "/?":
						Console.WriteLine("Usage: albtool [-c/e/i [other options] <input> <output> | -lf | -v <version> <created>]");
						Console.WriteLine();
						Console.WriteLine("Options:");
						Console.WriteLine("  -c  Converts a file between Albion and common format.");
						Console.WriteLine("  -e  Extracts (and converts) all data in a XLD archive.");
						Console.WriteLine("  -i  Imports files to a XLD.");
						Console.WriteLine("  -v  Updates version information.");
						Console.WriteLine("  -m  Merges two patch files.");
						Console.WriteLine("  -d  Sets XLD data folder.");
						Console.WriteLine("  -p  Sets images' palette.");
						Console.WriteLine("  -w  Sets images' width.");
						Console.WriteLine("  -h  Sets images' height.");
						Console.WriteLine("  -lf Lists all known formats.");
						return;
					case "-lf":case "/lf":
						Console.WriteLine(" bin Processes file without conversion.");
						Console.WriteLine(" rimg Raw image.");
						Console.WriteLine(" himg Headered image.");
						Console.WriteLine(" himg2 Headered image with variable-sized frames.");
						/*Console.WriteLine(" pal RGB palette.");
						Console.WriteLine(" lbm ILBM image.");
						Console.WriteLine(" pcm Raw PCM sound.");
						Console.WriteLine(" txt Text (dialogs etc.).");*/
						Console.WriteLine(" xlp Export as XLD Patch.");
						return;
					default:
						Console.WriteLine("Invalid parameters. Use --help.");
						break;
				}
			}catch(InvalidOperationException)
			{
				Console.WriteLine("Invalid parameters. Use --help.");
				return;
			}
		}
		
		private static void UpdateVersion(string file, Version ver, DateTime created)
		{
			if(!File.Exists(file)) return;
			const string find = "v%u.%02u";
			byte[] data = File.ReadAllBytes(file);
			byte[] bytes = Encoding.ASCII.GetBytes(find);
			for(var i = 0; i <= (data.Length - bytes.Length); i++)
			{
			    if (data[i] == bytes[0])
			    {
			    	int j;
			        for(j = 1; j < bytes.Length && data[i + j] == bytes[j]; j++);
			        if(j == bytes.Length)
			        {
			        	var cult = CultureInfo.InvariantCulture;
			            int offset = i-0xF6019; //string format
			            BitConverter.GetBytes((ushort)ver.Major).CopyTo(data, offset+0xFA4B8); //uint16 major
			            BitConverter.GetBytes((ushort)ver.Minor).CopyTo(data, offset+0xFA4BA); //uint16 minor
			            string date = created.ToString("MMM d yyyy", cult).PadRight(11, '\0');
			            string time = created.ToString("HH:mm:ss", cult).PadRight(8, '\0');
			            Encoding.ASCII.GetBytes(date).CopyTo(data, offset+0xF600D); // string[11] date
			            Encoding.ASCII.GetBytes(time).CopyTo(data, offset+0xF6004); // string[8] time
			            File.WriteAllBytes(file, data);
			            return;
			        }
			    }
			}
		}
		
		private static short[] ExtractSubfiles(ref string path)
		{
			/*short[] subfiles;
			string full = Path.GetFullPath(path);
			string dir = Path.GetDirectoryName(path);
			string filename = Path.GetFileName(path);
			string[] split = filename.Split(':');
			if(split.Length == 2)
			{
				subfiles = split[1].Split(',').Select(s => unchecked((short)UInt16.Parse(s))).ToArray();
				filename = split[0];
				path = Path.Combine(dir, filename);
			}else{
				subfiles = null;
			}
			return subfiles;*/
			var m = Regex.Match(path, @"^(.*):([\d,]+)$");
			if(m.Success)
			{
				path = m.Groups[1].Value;
				return m.Groups[2].Value.Split(',').Select(s => Int16.Parse(s)).ToArray();
			}else{
				return null;
			}
		}
		
		private static IEnumerable<short> ListSubfiles(XLDNavigator nav, short[] subfiles)
		{
			if(subfiles == null)
			{
				for(short i = 0; i < nav.NumSubfiles; i++)
				{
					nav.GoToSubfile(i);
					yield return i;
				}
			}else{
				foreach(short i in subfiles)
				{
					nav.GoToSubfile(i);
					yield return i;
				}
			}
		}
		
		private static Stream OpenXLDGzip(string path, FileMode mode)
		{
			FileStream stream = new FileStream(path, mode);
			string ext = Path.GetExtension(path);
			const StringComparison cmp = StringComparison.CurrentCultureIgnoreCase;
			if(ext.Equals(".xlz", cmp) || ext.Equals(".gz", cmp))
			{
				return new GZipStream(stream, mode == FileMode.Open ? CompressionMode.Decompress : CompressionMode.Compress, false);
			}else{
				return stream;
			}
		}
		
		private static bool IsXLPPath(string path)
		{
			string ext = Path.GetExtension(path);
			const StringComparison cmp = StringComparison.CurrentCultureIgnoreCase;
			return ext.Equals(".xlp", cmp) || ext.Equals(".xlz", cmp) || ext.Equals(".gz", cmp);
		}
		
		private static Assembly AssemblyResolve(object sender, ResolveEventArgs e)
		{
			if(e.Name.StartsWith("AlbLib, Version=1.3."))
			{
				Stream res = Assembly.GetExecutingAssembly().GetManifestResourceStream("AlbLib.dll");
				byte[] bytes = new byte[res.Length];
				res.Read(bytes, 0, bytes.Length);
				return Assembly.Load(bytes);
			}
			return null;
		}
	}
	
	public class RenderInfo
	{
		public int? Palette{get; set;}
		public int? Width{get; set;}
		public int? Height{get; set;}
	}
}