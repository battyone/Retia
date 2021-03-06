﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Retia.Neural;
using Retia.Training.Trainers;
using Retia.Training.Trainers.Sessions;

namespace Retia.Integration
{
    /// <summary>
    /// Runs a trainer on a console supporting cancellation and other options.
    /// </summary>
    public class ConsoleRunner<T, TOptions, TReport, TSession> 
            where T : struct, IEquatable<T>, IFormattable
            where TOptions : TrainerOptionsBase
            where TReport : TrainReportEventArgsBase<TSession>
            where TSession : TrainingSessionBase
    {
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly TrainerBase<T, TOptions, TReport, TSession> _trainer;
        private readonly NeuralNet<T> _network;

        public ConsoleRunner(TrainerBase<T, TOptions, TReport, TSession> trainer, NeuralNet<T> network)
        {
            _trainer = trainer;
            _network = network;
        }

        public void Run()
        {
            var task = _trainer.Train(_cts.Token);
            var running = true;
            while (running)
            {
                var c = Console.ReadKey().Key;
                switch (c)
                {
                    case ConsoleKey.Q:
                        Console.WriteLine("Stopping training manually");
                        running = false;
                        break;

                    case ConsoleKey.R:
                        Console.WriteLine("Reseting optimizer cache!");
                        _trainer.Pause();
                        _network.ResetOptimizer();
                        _trainer.Resume();
                        break;
                }
            }

            _cts.Cancel();
            Task.WaitAll(task);
        }

        public void Stop()
        {
            _cts.Cancel();
        }
    }

    public class ConsoleRunner
    {
        public static ConsoleRunner<T, TOptions, TReport, TSession> Create<T, TOptions, TReport, TSession>(TrainerBase<T, TOptions, TReport, TSession> trainer, NeuralNet<T> network) 
            where T : struct, IEquatable<T>, IFormattable
            where TOptions : TrainerOptionsBase
            where TReport : TrainReportEventArgsBase<TSession>
            where TSession : TrainingSessionBase
        {
            return new ConsoleRunner<T, TOptions, TReport, TSession>(trainer, network);
        }
    }
}