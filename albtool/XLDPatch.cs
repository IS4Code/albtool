using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using AlbLib.Texts;
using AlbLib.XLD;

namespace albtool
{
	public class XLDPatch
	{
		public List<XLDPatchSubfile> Subfiles{get; private set;}
		
		public XLDPatch()
		{
			Subfiles = new List<XLDPatchSubfile>();
		}
		
		public XLDPatch(string input) : this()
		{
			Load(input);
		}
		
		public XLDPatch(Stream input) : this()
		{
			Load(input);
		}
		
		private void Load(string input)
		{
			using(var stream = Program.OpenStream(input))
			{
				string ext = Path.GetExtension(input);
				const StringComparison cmp = StringComparison.CurrentCultureIgnoreCase;
				if(ext.Equals(".xlz", cmp) || ext.Equals(".gz", cmp))
				{
					using(var gz = new GZipStream(stream, CompressionMode.Decompress, true))
					{
						Load(gz);
					}
				}else{
					Load(stream);
				}
			}
		}
		
		private void Load(Stream input)
		{
			BinaryReader reader = new BinaryReader(input);
			if(new String(reader.ReadChars(6)) != XLDFile.Signature) throw new Exception("Invalid patch signature.");
			int num = reader.ReadUInt16();
			int[] sizes = new int[num];
			for(int i = 0; i < num; i++)
			{
				sizes[i] = reader.ReadInt32();
			}
			for(int i = 0; i < num; i++)
			{
				Subfiles.Add(new XLDPatchSubfile(reader, sizes[i]));
			}
		}
		
		public void Save(string output)
		{
			using(FileStream stream = new FileStream(output, FileMode.Create))
			{
				string ext = Path.GetExtension(output);
				const StringComparison cmp = StringComparison.CurrentCultureIgnoreCase;
				if(ext.Equals(".xlz", cmp) || ext.Equals(".gz", cmp))
				{
					using(var gz = new GZipStream(stream, CompressionMode.Compress, true))
					{
						Save(gz);
					}
				}else{
					Save(stream);
				}
			}
		}
		
		public void Save(Stream output)
		{
			BinaryWriter writer = new BinaryWriter(output, Encoding.ASCII);
			writer.Write(XLDFile.Signature.ToCharArray());
			writer.Write((ushort)Subfiles.Count);
			foreach(var subfile in Subfiles)
			{
				writer.Write(subfile.Data.Length+3);
			}
			foreach(var subfile in Subfiles)
			{
				writer.Write((byte)subfile.Type);
				writer.Write(subfile.Index);
				writer.Write(subfile.Data);
			}
			output.Flush();
		}
	}
	
	public class XLDPatchSubfile
	{
		public XLDPatchType Type{get; set;}
		public short Index{get; set;}
		public byte[] Data{get; set;}
		
		public XLDPatchSubfile(short index, byte[] data) : this(index, 0, data)
		{
			
		}
		
		public XLDPatchSubfile(short index, XLDPatchType type, byte[] data)
		{
			Type = type;
			Index = index;
			Data = data;
		}
		
		public XLDPatchSubfile(BinaryReader reader, int length)
		{
			Type = (XLDPatchType)reader.ReadByte();
			Index = reader.ReadInt16();
			Data = reader.ReadBytes(length-3);
		}
		
		public void ModifyBytes(ref byte[] data)
		{
			byte[] tmp;
			switch(Type)
			{
				case XLDPatchType.Replace:
					data = Data;
					break;
				case XLDPatchType.Append:
					tmp = new byte[Data.Length+data.Length];
					data.CopyTo(tmp, 0);
					Data.CopyTo(tmp, data.Length);
					data = tmp;
					break;
				case XLDPatchType.Prepend:
					tmp = new byte[Data.Length+data.Length];
					Data.CopyTo(tmp, 0);
					data.CopyTo(tmp, Data.Length);
					data = tmp;
					break;
				case XLDPatchType.ReplaceText:
					TextLibrary texts;
					if(data.Length == 0)
					{
						texts = new TextLibrary();
					}else{
						using(MemoryStream buffer = new MemoryStream(data))
						{
							texts = new TextLibrary(buffer);
						}
					}
					using(MemoryStream buffer = new MemoryStream(Data))
					{
						BinaryReader reader = new BinaryReader(buffer);
						ushort count = reader.ReadUInt16();
						ushort[] lengths = new ushort[count];
						for(int i = 0; i < count; i++)
						{
							lengths[i] = reader.ReadUInt16();
						}
						for(int i = 0; i < count; i++)
						{
							ushort repid = reader.ReadUInt16();
							if(repid >= texts.Count)
							{
								texts.AddRange(Enumerable.Repeat("", repid-texts.Count+1));
							}
							texts[repid] = new string(reader.ReadChars(lengths[i]-2)).TrimEnd('\0');
						}
					}
					using(MemoryStream buffer = new MemoryStream())
					{
						texts.Save(buffer);
						data = buffer.ToArray();
					}
					break;
				case XLDPatchType.ReplaceBytes:
					int offset = BitConverter.ToInt32(Data, 0);
					int len = Data.Length-4;
					if(data.Length < offset+len)
					{
						Array.Resize(ref data, offset+len);
					}
					Array.Copy(Data, 4, data, offset, len);
					break;
				case XLDPatchType.Comment:
					Console.WriteLine(Encoding.UTF8.GetString(Data));
					break;
				case XLDPatchType.Ignore:
					break;
				default:
					throw new NotImplementedException();
			}
		}
		
		public void ModifyXLD(XLDSubfile subfile)
		{
			byte[] data = subfile.Data;
			ModifyBytes(ref data);
			subfile.Data = data;
		}
	}
	
	public enum XLDPatchType : byte
	{
		Replace = 0,
		ReplaceText = 1,
		Append = 2,
		Prepend = 3,
		ReplaceBytes = 4,
		
		Comment = 254,
		Ignore = 255,
	}
}