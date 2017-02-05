﻿using System;
using System.Collections.Generic;
using Retia.Integration;
using Retia.Training.Data;

namespace Retia.Training.Batching
{
	public class LinearBatcher
	{
		public List<Sample> BatchSamples(List<LinearSample> samples, BatchDimension dimension, IProgressWriter progressWriter = null)
		{
			int batchSize, batchCount;
			switch (dimension.Type)
			{
				case BatchDimensionType.BatchSize:
					batchCount = samples.Count / dimension.Dimension;
					batchSize = dimension.Dimension;
					break;
				case BatchDimensionType.BatchCount:
					batchCount = dimension.Dimension;
					batchSize = samples.Count / dimension.Dimension;
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}

			return BatchSamples(samples, batchCount, batchSize, progressWriter);
		}

		private List<Sample> BatchSamples(List<LinearSample> samples, int batchCount, int batchSize, IProgressWriter progressWriter)
		{
			var result = new List<Sample>();
			int inputSize = samples[0].Input.Length;
			int targetSize = samples[0].Target.Length;

			var tracker = new ProgressTracker(batchSize);

			for (int sampleIdx = 0; sampleIdx < batchCount; sampleIdx++)
			{

				if (tracker.ShouldReport(sampleIdx))
				{
                    progressWriter?.SetProgress(sampleIdx, batchCount, "Batching");
				}

				var sample = new Sample(inputSize, targetSize, batchSize);
				for (int col = 0; col < batchSize; col++)
				{
					var input = samples[sampleIdx + col * batchCount];

					for (int i = 0; i < input.Input.Length; i++)
					{
						sample.Input[i, col] = input.Input[i];
					}

					for (int i = 0; i < input.Target.Length; i++)
					{
						sample.Target[i, col] = input.Target[i];
					}
				}

				result.Add(sample);
			}

            progressWriter?.Complete();

			return result;
		}
	}
}