using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using AlbLib.Imaging;

namespace albtool
{
	public abstract class Format
	{
		public static readonly Dictionary<string, Format> Formats = new Dictionary<string, Format>
		{
			{"bin", new BinFormat()},
			{"rimg", new RimgFormat()},
			{"himg", new HimgFormat(true)},
			{"himg2", new HimgFormat(false)},
		};
		
		public abstract string DefaultExtension{get;}
		
		public abstract void Save(Stream input, int length, string output, RenderInfo info);
		
		public abstract void Load(string input, Stream output, RenderInfo info);
	}
	
	public class BinFormat : Format
	{
		public override string DefaultExtension{
			get{
				return "bin";
			}
		}
		
		public override void Save(Stream input, int length, string output, RenderInfo info)
		{
			byte[] data = new byte[length];
			input.Read(data, 0, length);
			File.WriteAllBytes(output, data);
		}
		
		public override void Load(string input, Stream output, RenderInfo info)
		{
			using(FileStream file = new FileStream(input, FileMode.Open))
			{
				file.CopyTo(output);
			}
		}
	}
	
	public class RimgFormat : Format
	{
		public override string DefaultExtension{
			get{
				return "png";
			}
		}
		
		public override void Save(Stream input, int length, string output, RenderInfo info)
		{
			if(info.Palette == null)
			{
				throw new Exception("Palette must be specified.");
			}
			if(info.Width == null && info.Height == null)
			{
				throw new Exception("Width or height must be specified.");
			}else if(info.Width == null)
			{
				int h = info.Height.Value;
				info.Width = (length+h-1)/h;
			}
			if(info.Height == null)
			{
				int w = info.Width.Value;
				info.Height = (length+w-1)/w;
			}
			new RawImage(input, (int)info.Width, (int)info.Height).Render((int)info.Palette).Save(output);
		}
		
		public override void Load(string input, Stream output, RenderInfo info)
		{
			if(info.Palette == null)
			{
				throw new Exception("Palette must be specified.");
			}
			var bmp = new Bitmap(input);
			RawImage.FromBitmap(bmp, ImagePalette.GetFullPalette((int)info.Palette)).Save(output);
		}
	}
	
	public class HimgFormat : Format
	{
		public override string DefaultExtension{
			get{
				return "png";
			}
		}
		
		bool _constsize;
		
		public HimgFormat(bool constsize)
		{
			_constsize = constsize;
		}
		
		public override void Save(Stream input, int length, string output, RenderInfo info)
		{
			if(info.Palette == null)
			{
				throw new Exception("Palette must be specified.");
			}
			new HeaderedImage(input, _constsize).Render((int)info.Palette).Save(output);
		}
		
		public override void Load(string input, Stream output, RenderInfo info)
		{
			throw new NotImplementedException();
		}
	}
	
	public class IlbmFormat : Format
	{
		public override string DefaultExtension{
			get{
				return "png";
			}
		}
		
		public override void Save(Stream input, int length, string output, RenderInfo info)
		{
			ILBMImage.FromStream(input).Render().Save(output, ImageFormat.Png);
		}
		
		public override void Load(string input, Stream output, RenderInfo info)
		{
			throw new NotImplementedException();
		}
	}
}
