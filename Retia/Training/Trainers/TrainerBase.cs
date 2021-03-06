﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Retia.Training.Trainers.Actions;
using Retia.Training.Trainers.Sessions;

namespace Retia.Training.Trainers
{
    public abstract class TrainerBase<T, TOptions, TReport, TSession> : ITrainingReporter<TReport, TSession>, ITrainerEvents
        where T : struct, IEquatable<T>, IFormattable
        where TOptions : TrainerOptionsBase
        where TReport : TrainReportEventArgsBase<TSession>
        where TSession : TrainingSessionBase
    {
        private readonly ManualResetEventSlim _pauseHandle = new ManualResetEventSlim(true);

        private bool _stop = false;

        protected TrainerBase(TOptions options, TSession session)
        {
            Options = options;
            Session = session;

            ValidateOptions(options);
        }

        public bool IsPaused { get; private set; }
        public bool IsTraining { get; private set; }

        public TOptions Options { get; }

        public List<PeriodicActionBase> PeriodicActions { get; } = new List<PeriodicActionBase>();
        public TSession Session { get; private set; }

        protected abstract TReport GetAndFlushTrainingReport();

        protected abstract void ResetMemory();

        protected abstract void TrainIteration();
        public event Action<TrainingSessionBase> EpochReached;

        public void Pause()
        {
            IsPaused = true;
            _pauseHandle.Reset();
            OnTrainingStateChanged();
        }

        public void Resume()
        {
            IsPaused = false;
            _pauseHandle.Set();
            OnTrainingStateChanged();
        }

        public event Action<TrainingSessionBase> SequenceTrained;

        public void Stop()
        {
            _stop = true;
        }

        public async Task Train(CancellationToken token)
        {
            if (IsTraining)
            {
                throw new InvalidOperationException("Already training!");
            }

            IsTraining = true;
            IsPaused = false;
            _stop = false;

            OnTrainingStateChanged();

            try
            {
                await Task.Run(() => TrainInternal(token), token);
            }
            catch (Exception e)
            {
                Options.ProgressWriter?.Message(e.ToString());
            }
            finally
            {
                OnTrainingStateChanged();
                Session.Dispose();
            }
        }

        public event EventHandler TrainingStateChanged;
        public event EventHandler<TReport> TrainReport;

        protected virtual string GetIterationProgress(int otherLen)
        {
            return $"I:{Session.Iteration}";
        }

        protected virtual string GetTrainingReportMessage()
        {
            return null;
        }

        protected virtual void InitTraining()
        {
        }

        protected virtual void OnTrainingStateChanged()
        {
            TrainingStateChanged?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void SubscribeActions()
        {
            foreach (var action in PeriodicActions)
            {
                action.Subscribe(this);
            }
        }

        protected virtual void UnsubscribeActions()
        {
            foreach (var action in PeriodicActions)
            {
                action.Unsubscribe();
            }
        }

        protected virtual void ValidateOptions(TOptions options)
        {
        }

        protected void OnEpochReached()
        {
            EpochReached?.Invoke(Session);
        }

        protected void OnTrainReport(TReport args)
        {
            TrainReport?.Invoke(this, args);
        }

        private void OnSequenceTrained()
        {
            SequenceTrained?.Invoke(Session);
        }

        private void TrainInternal(CancellationToken token)
        {
            Session.Iteration = 0;

            InitTraining();
            SubscribeActions();

            var watch = new Stopwatch();
            while (IsTraining)
            {
                if (token.IsCancellationRequested || _stop)
                {
                    Options.ProgressWriter?.Message("Training stopped");
                    IsTraining = false;
                    break;
                }

                if (!_pauseHandle.IsSet)
                {
                    Options.ProgressWriter?.Message("Training paused");
                    try
                    {
                        _pauseHandle.Wait(token);
                    }
                    catch (TaskCanceledException)
                    {
                        break;
                    }
                    Options.ProgressWriter?.Message("Training resumed");
                }

                watch.Restart();
                TrainIteration();
                watch.Stop();

                Session.Iteration++;

                // Check for memory reset on iteration.
                if (Options.ResetMemory?.ShouldDoOnIteration(Session.Iteration) == true)
                {
                    ResetMemory();
                }

                OnSequenceTrained();

                if (Options.ReportProgress?.ShouldDoOnIteration(Session.Iteration) == true)
                {
                    OnTrainReport(GetAndFlushTrainingReport());

                    if (Options.ReportMesages && Options.ProgressWriter != null)
                    {
                        var preIter = new StringBuilder();
                        preIter.Append('#').Append(Session.Epoch).Append('[');

                        var postIter = new StringBuilder();
                        postIter.Append(' ').AppendFormat("{0:0.0000}", watch.Elapsed.TotalSeconds).Append("s] ").Append(GetTrainingReportMessage());

                        preIter.Append(GetIterationProgress(preIter.Length + postIter.Length)).Append(postIter);

                        Options.ProgressWriter.SetItemProgress(preIter.ToString());
                    }
                }

                if (Session.Epoch > Options.MaxEpoch)
                {
                    Options.ProgressWriter?.Message($"{Options.MaxEpoch} reached, stopped training.");
                    break;
                }
            }

            UnsubscribeActions();
        }
    }
}