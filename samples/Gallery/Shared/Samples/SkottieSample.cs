﻿using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SkiaSharp;
using SkiaSharp.Skottie;

namespace SkiaSharpSample.Samples
{
	[Preserve(AllMembers = true)]
	public class SkottieSample : AnimatedSampleBase
	{
		private readonly Animation _animation;
		private Stopwatch _watch = new Stopwatch();

		[Preserve]
		public SkottieSample()
		{
			using var reader = new StreamReader(SampleMedia.Images.LottieLogo);

			_animation = SkiaSharp.Skottie.Animation.Make(reader.ReadToEnd());
			_animation.Seek(0, null);

			Console.WriteLine($"SkottieSample(): Version:{_animation.Version} Duration:{_animation.Duration} Fps:{_animation.Fps} InPoint:{_animation.InPoint} OutPoint:{_animation.OutPoint}");
		}

		public override string Title => "Skottie";

		public override SampleCategories Category => SampleCategories.General;

		protected override async Task OnInit()
		{
			try
			{
				await base.OnInit();
			
				_watch.Start();
			}
			catch (Exception e)
			{
				Console.WriteLine(e);
			}
		}

		protected override async Task OnUpdate(CancellationToken token)
		{
			try
			{
				await Task.Delay(25, token);

				_animation.SeekFrameTime((float)_watch.Elapsed.TotalSeconds, null);

				if(_watch.Elapsed.TotalSeconds > _animation.Duration)
				{
					_watch.Restart();
				}
			}
			catch(Exception e)
			{
				Console.WriteLine(e);
			}
		}

		protected override void OnDrawSample(SKCanvas canvas, int width, int height)
		{
			try
			{
				_animation.Render(canvas, new SKRect(0, 0, width, height));
			}
			catch (Exception e)
			{
				Console.WriteLine(e);
			}
		}
	}
}
